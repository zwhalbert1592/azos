﻿/*<FILE_LICENSE>
 * Azos (A to Z Application Operating System) Framework
 * The A to Z Foundation (a.k.a. Azist) licenses this file to you under the MIT license.
 * See the LICENSE file in the project root for more information.
</FILE_LICENSE>*/

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

using Azos.Apps;
using Azos.Apps.Injection;
using Azos.Data;
using Azos.Data.AST;
using Azos.Data.Business;
using Azos.Serialization.Bix;
using Azos.Time;

namespace Azos.Sky.Fabric
{
  [Schema(Description = "Query fiber list")]
  [Bix("a217f751-28ea-452e-b900-7758a6ab1103")]
  public sealed class StoreQueryArgs : TransientModel
  {
    public StoreQueryArgs() { }
    public StoreQueryArgs(FiberFilter proto)
    {
      proto.CopyFields(this);
      if (proto.Id.HasValue) this.Gdid = proto.Id.Value.Gdid;
    }

    [Field(Description = "Gdid of the specific fiber or zero")]
    public GDID?  Gdid { get; set; }

    [Field(typeof(FiberFilter))]  public Guid?         InstanceGuid { get; set; }
    [Field(typeof(FiberFilter))]  public Atom?         Origin       { get; set; }
    [Field(typeof(FiberFilter))]  public string        Group        { get; set; }
    [Field(typeof(FiberFilter))]  public FiberStatus?  Status       { get; set; }
    [Field(typeof(FiberFilter))]  public Guid?         ImageTypeId  { get; set; }
    [Field(typeof(FiberFilter))]  public DateRange?    CreateUtc    { get; set; }
    [Field(typeof(FiberFilter))]  public EntityId?     Initiator    { get; set; }
    [Field(typeof(FiberFilter))]  public EntityId?     Owner        { get; set; }
    [Field(typeof(FiberFilter))]  public int?          MinAvgLatencySec      { get; set; }
    [Field(typeof(FiberFilter))]  public int?          MaxAvgLatencySec      { get; set; }
    [Field(typeof(FiberFilter))]  public int?          MinAvgSliceDurationMs { get; set; }
    [Field(typeof(FiberFilter))]  public int?          MaxAvgSliceDurationMs { get; set; }
    [Field(typeof(FiberFilter))]  public DateRange?    NextSliceUtc   { get; set; }
    [Field(typeof(FiberFilter))]  public Expression    TagFilter      { get; set; }
    [Field(typeof(FiberFilter))]  public Expression    StateTagFilter { get; set; }
  }
}
