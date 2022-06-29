﻿/*<FILE_LICENSE>
 * Azos (A to Z Application Operating System) Framework
 * The A to Z Foundation (a.k.a. Azist) licenses this file to you under the MIT license.
 * See the LICENSE file in the project root for more information.
</FILE_LICENSE>*/

using System;
using System.Collections.Generic;
using System.Linq;

using Azos.Apps;
using Azos.Collections;
using Azos.Conf;

namespace Azos.Data.Idgen
{
  /// <summary>
  /// Generates GDIDs based on a local pool of auto-incremented sequence variable within a second-aligned date/time stamp.
  /// This class does generate consecutive IDs while it runs, however it does not generate IDs without gaps after reallocation because
  /// of the time stamp used in the high 32 bits of GDID, consequently this is not a full stateful GDID generator and
  /// it should be used for application which need to generate relatively limited number of IDs (a few billions) in
  /// comparison with a full stateful GDID generator.
  /// The benefit of this class is its state-less design (no data written to disk as dates are used instead) and simplicity.
  /// Attention: The generated IDs are only unique while generated by the same instance of this class per authority, that is:
  /// if you allocate more than one instance of this class per the same authority, then IDs generated by different instances will collide
  /// in the same scope/sequence space. The era is set as an external parameter, so you should manually manage it.
  /// WARNING: The generator relies on proper App.TimeSource, otherwise duplicate IDs are theoretically possible
  /// due to clock drift/change although it is very unlikely.
  /// </summary>
  /// <remarks>
  ///
  /// <para>
  /// By design, this class can generate per authority up to 2^28 = ~268M ids per second, handling around 100 years since 2020, which equates to
  /// 2^60 combinations. Upon the exhaustion of 268M ids the timestamp gets assigned to the current one.
  /// Every time a named sequence object is allocated, the system waits 1.5 seconds ensuring the new upper 32 bit timestamp counter value,
  /// therefore upon process restart or class re-allocation the system will ensure that counter would start under a new second time slice.
  /// The problem may arise if/when the system clock abruptly gets adjusted by a 2 seconds or more, and process restarts, which may theoretically lead
  /// to ID duplication, therefore this class relies on an accurate App.TimeSource service (e.g. network synchronized).
  /// </para>
  ///
  /// <para>
  /// You can also rely on a periodic OS clock sync (see Windows Control Panel "Set time automatically" from time.windows.com),
  /// or use Linux NTP/ntpdate (see http://ntp.org)
  /// </para>
  ///
  /// Format:
  /// <code>
  ///  [ ERA 32 bits ] : [ AUTH 4bits ] : [ COUNTER 60 bits ]
  ///
  ///   COUNTER := 32 bits SECONDS since Jan 1, 2020    (2^32 = ~135 years until year 2155)
  ///              28 bits INT counter (auto-incremented) = ~268M ids
  ///
  /// </code>
  /// </remarks>
  public sealed class LocalGdidGenerator : ApplicationComponent, IConfigurable, IGdidProvider
  {
    //Maximum number of counter per 1 second slice
    private const int MAX_COUNTER_PER_TIME_STAMP = 268_000_000;// 2 ^ (60 - 32) = 268_435_456 (rounded to millions)
    public static readonly DateTime START = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
    public const int MAX_BATCH = 1024;


    internal class scope : INamed
    {
      public string Name { get; set; }
      public Registry<sequence> Sequences = new Registry<sequence>();
    }


    internal class sequence : INamed, ISequenceInfo
    {
      internal scope Scope { get; set; }
      public string Name { get; set; }
      public volatile int Counter;
      public ulong Timestamp;

      public uint Era => 0;
      public ulong ApproximateCurrentValue => (ulong)Counter;
      public int TotalPreallocation => 0;
      public int RemainingPreallocation => 0;
      public string IssuerName => nameof(LocalGdidGenerator);
      public DateTime IssueUTCDate => DateTime.UtcNow;
    }


    public LocalGdidGenerator(IApplication application) : base(application) => ctor();
    public LocalGdidGenerator(IApplicationComponent director) : base(director) => ctor();

    private void ctor()
    {
      m_Name = Guid.NewGuid().ToString();
    }

    private string m_Name;
    private int m_Authority;
    private Registry<scope> m_Scopes = new Registry<scope>();

    public string Name => m_Name;

    /// <summary>
    /// Sets the era of the generated GDIDs. This class does not automatically manage eras.
    /// The Era is the higher-order 32 bits of GDID
    /// </summary>
    [Config]
    public uint Era{ get; set; }

    /// <summary>
    /// Assigns a cluster-unique id of the authority node which this class is allocated on.
    /// GDIDs support 16 authorities identified by 0..15 integers.
    /// WARNING: You must ensure no authority ID duplication in cluster, otherwise the generated IDs will clash.
    /// Typically you would configure authority using machine-wide environment variable such as: "authority=$(~AUTHORITY_ID)"
    /// </summary>
    [Config]
    public int Authority
    {
      get => m_Authority;
      set
      {
        if (value<0 || value>0xf) throw new AzosException(StringConsts.ARGUMENT_ERROR+".Authority.set({0} is not [0..16])".Args(value));
        m_Authority = value;
      }
    }

    public override string ComponentLogTopic => CoreConsts.TOPIC_ID_GEN;

    public string TestingAuthorityNode { get => null; set {} }

    public IEnumerable<string> SequenceScopeNames => m_Scopes.Names;

    public void Configure(IConfigSectionNode node)
    {
      ConfigAttribute.Apply(this, node);
    }

    public IEnumerable<ISequenceInfo> GetSequenceInfos(string scopeName)
    {
      var scope = m_Scopes[scopeName.NonBlank(nameof(scopeName))];
      if (scope==null) Enumerable.Empty<ISequenceInfo>();
      return scope.Sequences.Values;
    }

    public GDID GenerateOneGdid(string scopeName, string sequenceName, int blockSize = 0, ulong? vicinity = 1152921504606846975, bool noLWM = false)
     => generate(scopeName.NonBlank(nameof(scopeName)), sequenceName.NonBlank(nameof(sequenceName)), 1).first;

    public ulong GenerateOneSequenceId(string scopeName, string sequenceName, int blockSize = 0, ulong? vicinity = ulong.MaxValue, bool noLWM = false)
     => generate(scopeName.NonBlank(nameof(scopeName)), sequenceName.NonBlank(nameof(sequenceName)), 1).first.ID;

    public GDID[] TryGenerateManyConsecutiveGdids(string scopeName, string sequenceName, int gdidCount, ulong? vicinity = 1152921504606846975, bool noLWM = false)
    {
      var got = generate(scopeName.NonBlank(nameof(scopeName)), sequenceName.NonBlank(nameof(sequenceName)), gdidCount);
      var result = new GDID[got.count];
      var first = got.first;
      for(var i=0; i < got.count; i++)
       result[i] = new GDID(first.Era, first.Authority, first.Counter + (ulong)i);

      return result;
    }

    public ConsecutiveUniqueSequenceIds TryGenerateManyConsecutiveSequenceIds(string scopeName, string sequenceName, int idCount, ulong? vicinity = ulong.MaxValue, bool noLWM = false)
    {
      var got = generate(scopeName.NonBlank(nameof(scopeName)), sequenceName.NonBlank(nameof(sequenceName)), idCount);
      var result = new ConsecutiveUniqueSequenceIds(got.first.ID, got.count);
      return result;
    }

    private bool initTimestamp(sequence seq)
    {
      var was = seq.Timestamp;
      var nowdt = App.TimeSource.UTCNow;
      var span = nowdt - START;
      Aver.IsTrue(nowdt > START && span.TotalSeconds < uint.MaxValue, "App.Timesource is not set to current time after `{0}` or it is now the year of 2150 or beyond".Args(START));
      var now = ((ulong)span.TotalSeconds) << (60 - 32);//60 bit counter - 32 upper bits used for timestamp
      seq.Timestamp = now;
      return  now > was;//true if at least a second has passed since the last call
    }

    private (GDID first, int count) generate(string scopeName, string seqName, int count)
    {
      count = count.KeepBetween(1, MAX_BATCH);

      var scope = m_Scopes.GetOrRegister(scopeName, n => new scope{ Name = n } );
      var seq = scope.Sequences.GetOrRegister(seqName, n =>
      {
        //this safeguard is needed to ensure a passage of at least 1 second
        //in case of a quick re-allocation of the class on process restart etc...
        System.Threading.Thread.Sleep(1500);
        return new sequence { Name = n };
      });

      lock(seq)
      {
        var counter = seq.Counter;
        var increment = Math.Min(MAX_COUNTER_PER_TIME_STAMP - counter, count);

        if (increment>0 && seq.Timestamp>0)
        {
          seq.Counter += increment;
        }
        else
        {
          const int MAX_WAIT_SPINS = 50;//of 100 ms slices = 5 seconds
          var spin = 0;
          while(!initTimestamp(seq)) //spinlock - safeguard of block exhaustion within a second (not practically possible)
          {
            //safeguard in case of large clock drift due to clock change back by more than 3 seconds
            if (spin++ > MAX_WAIT_SPINS)
              throw new AzosException("Local GDID generation failed because wait limit was exceeded due to a significant clock drift. Did system time change?");

            System.Threading.Thread.Sleep(100);
          }

          increment = count;
          counter = Authority == 0 ? 1 : 0; //GDID(0,0,0) represents Zero, authority 0 counter starts with 1
          seq.Counter = counter + increment;
        }

        var id = seq.Timestamp | (0x0F_FF_FFFFul & (ulong)counter);

        return (new GDID(Era, Authority, id), increment);
      }//lock
    }

  }
}
