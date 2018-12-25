/*<FILE_LICENSE>
 * Azos (A to Z Application Operating System) Framework
 * The A to Z Foundation (a.k.a. Azist) licenses this file to you under the MIT license.
 * See the LICENSE file in the project root for more information.
</FILE_LICENSE>*/

using System;
using System.Linq;

using Azos.Conf;
using Azos.Collections;
using Azos.Glue.Protocol;

namespace Azos.Glue
{
    /// <summary>
    /// Denotes an entity that can inspect messages
    /// </summary>
    public interface IMsgInspector : INamed, IOrdered, IConfigurable
    {

    }

    /// <summary>
    /// Inspects messages on the client side. ClientInspectors may be registered on ClientEndPoint, Binding or Glue levels
    /// </summary>
    public interface IClientMsgInspector : IMsgInspector
    {
       /// <summary>
       /// Intercepts client call during dispatch and optionally allows to change the RequestMsg
       /// </summary>
       RequestMsg ClientDispatchCall(ClientEndPoint endpoint, RequestMsg request);

       /// <summary>
       /// Intercepts server response message before it arrives into CallSlot and optionally allows to change it
       /// </summary>
       ResponseMsg ClientDeliverResponse(CallSlot callSlot, ResponseMsg response);
    }

    /// <summary>
    /// Inspects messages on the server side. ServerInspectors may be registered on ServerEndPoint, Binding or Glue levels
    /// </summary>
    public interface IServerMsgInspector : IMsgInspector
    {
       /// <summary>
       /// Intercepts RequestMsg that arrived from particular ServerEndPoint and optionally allows to change it
       /// </summary>
       RequestMsg ServerDispatchRequest(ServerEndPoint endpoint, RequestMsg request);

       /// <summary>
       /// Intercepts ResponseMsg generated by server before it is sent to client and optionally allows to change it
       /// </summary>
       ResponseMsg ServerReturnResponse(ServerEndPoint endpoint, RequestMsg request, ResponseMsg response);
    }


    /// <summary>
    /// Provides general configuration reading logic for message inspectors
    /// </summary>
    public static class MsgInspectorConfigurator
    {
       #region CONSTS

            public const string CONFIG_SERVER_INSPECTORS_SECTION = "server-inspectors";
            public const string CONFIG_CLIENT_INSPECTORS_SECTION = "client-inspectors";

            public const string CONFIG_INSPECTOR_SECTION = "inspector";

            public const string CONFIG_NAME_ATTR = "name";
            public const string CONFIG_POSITION_ATTR = "position";

       #endregion

       public static void ConfigureServerInspectors(IApplication app, Registry<IServerMsgInspector> registry, IConfigSectionNode node)
       {
         node = node[CONFIG_SERVER_INSPECTORS_SECTION];
         if (!node.Exists) return;

         foreach(var inode in node.Children.Where(c => c.IsSameName(CONFIG_INSPECTOR_SECTION)))
         {
           var si = FactoryUtils.MakeAndConfigure<IServerMsgInspector>(inode);
           app.DependencyInjector.InjectInto(si);
           registry.Register(si);
         }

       }


       public static void ConfigureClientInspectors(IApplication app, Registry<IClientMsgInspector> registry, IConfigSectionNode node)
       {
         node = node[CONFIG_CLIENT_INSPECTORS_SECTION];
         if (!node.Exists) return;

         foreach(var inode in node.Children.Where(c => c.IsSameName(CONFIG_INSPECTOR_SECTION)))
         {
           var ci = FactoryUtils.MakeAndConfigure<IClientMsgInspector>(inode);
           app.DependencyInjector.InjectInto(ci);
           registry.Register(ci);
         }

       }

    }


}
