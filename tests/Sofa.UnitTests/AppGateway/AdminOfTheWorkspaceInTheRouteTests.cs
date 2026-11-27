using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Sofa.AppGateway.Admins;
using Sofa.AppGateway.Identity;
using Sofa.Contracts.Identity;

namespace Sofa.UnitTests.AppGateway;

public class AdminOfTheWorkspaceInTheRouteTests
{
    private static readonly Guid AdasWorkspace = Guid.NewGuid();
    private static readonly Guid BeasWorkspace = Guid.NewGuid();
    private static readonly UserProfile Ada = new(Guid.NewGuid(), "Ada", "ada@example.com", null,
    [
        new UserWorkspace(AdasWorkspace, "Ada's Workspace", IsAdmin: true, []),
        new UserWorkspace(BeasWorkspace, "Bea's Workspace", IsAdmin: false, []),
    ]);

    [Fact]
    public async Task Finds_the_signed_in_User_as_Admin_of_a_Workspace_they_administer()
    {
        var admin = await FindAsync(Ada.Id, AdasWorkspace);

        Assert.Equal(new SignedInAdmin(Ada.Id, AdasWorkspace), admin);
    }

    [Fact]
    public async Task Finds_no_Admin_in_a_Workspace_the_User_only_belongs_to()
    {
        Assert.Null(await FindAsync(Ada.Id, BeasWorkspace));
    }

    [Fact]
    public async Task Finds_no_Admin_for_a_User_Identity_does_not_know()
    {
        Assert.Null(await FindAsync(Guid.NewGuid(), AdasWorkspace));
    }

    private static Task<SignedInAdmin?> FindAsync(Guid userId, Guid workspaceId)
    {
        var identity = new IdentityUsers(new HttpClient(new IdentityKnowing(Ada)) { BaseAddress = IdentityUsers.Address });
        var context = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", userId.ToString())], "Bearer")),
        };
        context.Request.RouteValues[AdminsOnly.WorkspaceRouteValue] = workspaceId.ToString();
        return new AdminOfTheWorkspaceInTheRoute(new WorkspaceAdmins(identity)).FindAsync(context);
    }

    private sealed class IdentityKnowing(UserProfile profile) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(request.RequestUri!.AbsolutePath == $"/users/{profile.Id}"
                ? new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(profile) }
                : new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
