using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sofa.AppGateway.Access;
using Sofa.AppGateway.Identity;
using Sofa.Contracts.Identity;

namespace Sofa.UnitTests.AppGateway;

public class WorkspaceUserInTheRouteTests
{
    private static readonly Guid AdasWorkspace = Guid.NewGuid();
    private static readonly Guid BeasWorkspace = Guid.NewGuid();
    private static readonly Guid BeasTeam = Guid.NewGuid();
    private static readonly UserProfile Ada = new(Guid.NewGuid(), "Ada", "ada@example.com", null,
    [
        new UserWorkspace(AdasWorkspace, "Ada's Workspace", IsAdmin: true, []),
        new UserWorkspace(BeasWorkspace, "Bea's Workspace", IsAdmin: false, [new UserTeam(BeasTeam, "Looms")]),
    ]);

    [Fact]
    public async Task Finds_the_signed_in_User_as_Admin_of_a_Workspace_they_administer()
    {
        var user = await FindAsync(Ada.Id, AdasWorkspace);

        Assert.Equal(Ada.Id, user!.UserId);
        Assert.Equal(AdasWorkspace, user.WorkspaceId);
        Assert.True(user.IsAdmin);
    }

    [Fact]
    public async Task Finds_a_User_who_only_belongs_to_the_Workspace_with_their_Teams_and_no_Admin()
    {
        var user = await FindAsync(Ada.Id, BeasWorkspace);

        Assert.False(user!.IsAdmin);
        Assert.Equal([BeasTeam], user.TeamIds);
    }

    [Fact]
    public async Task Finds_no_one_in_a_Workspace_the_User_does_not_belong_to()
    {
        Assert.Null(await FindAsync(Ada.Id, Guid.NewGuid()));
    }

    [Fact]
    public async Task Finds_no_one_for_a_User_Identity_does_not_know()
    {
        Assert.Null(await FindAsync(Guid.NewGuid(), AdasWorkspace));
    }

    private static Task<SignedInWorkspaceUser?> FindAsync(Guid userId, Guid workspaceId)
    {
        var identity = new IdentityUsers(new HttpClient(new IdentityKnowing(Ada)) { BaseAddress = IdentityUsers.Address });
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId.ToString())], "Bearer")),
        };
        context.Request.RouteValues[WorkspaceAccess.WorkspaceRouteValue] = workspaceId.ToString();
        return new WorkspaceUserInTheRoute(identity).FindAsync(context);
    }

    private sealed class IdentityKnowing(UserProfile profile) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(request.RequestUri!.AbsolutePath == $"/users/{profile.Id}"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(profile) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
