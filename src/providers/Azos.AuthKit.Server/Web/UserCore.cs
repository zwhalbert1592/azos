﻿/*<FILE_LICENSE>
 * Azos (A to Z Application Operating System) Framework
 * The A to Z Foundation (a.k.a. Azist) licenses this file to you under the MIT license.
 * See the LICENSE file in the project root for more information.
</FILE_LICENSE>*/

using System.Threading.Tasks;

using Azos.Apps.Injection;
using Azos.Data;
using Azos.Security.Authkit;
using Azos.Wave.Mvc;

namespace Azos.AuthKit.Server.Web
{
  [NoCache]
  [ApiControllerDoc(
    BaseUri = "/authkit/users",
    Connection = "default/keep alive",
    Title = "AuthKit User Core Controller",
    Authentication = "Token/Default",
    Description = "Provides REST API for IIdpUserCoreLogic contracts",
    ResponseHeaders = new[] { API_DOC_HDR_NO_CACHE },
    TypeSchemas = new[] { typeof(UserCorePermission), typeof(LoginCorePermission) }
  )]
  [UserCorePermission(UserCoreAccessLevel.View)]
  public sealed class UserCore : ApiProtocolController
  {
    public const string ACT_FILTER = "filter";
    public const string ACT_USER = "user";
    public const string ACT_USER_LOGINS = "userlogins";

    [Inject] IIdpUserCoreLogic m_Logic;

    [ApiEndpointDoc(Title = "Filter",
                    Uri = "filter",
                    Description = "Queries AuthKit users by applying a structured filter `{@UserListFilter}`",
                    Methods = new[] { "POST = post filter object for query execution" },
                    RequestHeaders = new[] { API_DOC_HDR_ACCEPT_JSON },
                    ResponseHeaders = new[] { API_DOC_HDR_NO_CACHE },
                    RequestBody = "JSON representation of `{@UserListFilter}`",
                    ResponseContent = "JSON filter result - enumerable of `{@UserInfo}`",
                    TypeSchemas = new[] { typeof(UserInfo), typeof(UserListFilter) })]
    [ActionOnPost(Name = ACT_FILTER), AcceptsJson]
    [UserCorePermission(UserCoreAccessLevel.View)]
    public async Task<object> PostUserFilter(UserListFilter filter)
      => await ApplyFilterAsync(filter).ConfigureAwait(false);

    [ApiEndpointDoc(Title = "POST - Creates User Entity",
                    Description = "Creates a new user account. " +
                    "Do not include `Gdid` as it is generated by the server for new entities",
                    RequestBody = "JSON representation of {node: UserEntity}",
                    ResponseContent = "JSON representation of {OK: bool, data: ChangeResult}",
                    Methods = new[] { "POST: Inserts a new user account entity" },
                    TypeSchemas = new[]{ typeof(UserEntity), typeof(ChangeResult) })]
    [ActionOnPost(Name = ACT_USER)]
    [UserCorePermission(UserCoreAccessLevel.Change)]
    public async Task<object> PostUserEntity(UserEntity user)
      => await SaveNewAsync(user).ConfigureAwait(false);

    [ApiEndpointDoc(Title = "POST - Updates User Entity",
                    Description = "Updates an user account. " +
                    "Must include `Gdid` for the user which is being updated",
                    RequestBody = "JSON representation of {node: UserEntity}",
                    ResponseContent = "JSON representation of {OK: bool, data: ChangeResult}",
                    Methods = new[] { "POST: Updates user account entity" },
                    TypeSchemas = new[] { typeof(UserEntity), typeof(ChangeResult) })]
    [ActionOnPut(Name = ACT_USER)]
    [UserCorePermission(UserCoreAccessLevel.Change)]
    public async Task<object> PutUserEntity(UserEntity user)
    => await SaveEditAsync(user).ConfigureAwait(false);


    [ApiEndpointDoc(
      Title = "GET - Retrieves a list of user logins",
      Description = "Returns a list of login info objects for the selected user account",
      RequestQueryParameters = new[]{
            "gUser=GDID of the user used to retrieve logins"},
      ResponseContent = "Http 200 / JSON representation of {OK: true, data: [LoginInfo]} or Http 404 {OK: false, data: null}",
      Methods = new[] { "GET: list of [Atom]" },
      TypeSchemas = new[]{
            typeof(Atom)
      })]
    [ActionOnGet(Name = ACT_USER_LOGINS)]
    [LoginCorePermission(LoginCoreAccessLevel.View)]
    public async Task<object> GetTreeListAsync(GDID gUser)
      => GetLogicResult(await m_Logic.GetLoginsAsync(gUser).ConfigureAwait(false));
  }
}
