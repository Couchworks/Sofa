using System.Net;
using Sofa.Contracts;

namespace Sofa.IntegrationTests;

public class AdminTests(SofaApp sofa)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Without_a_token_the_admin_pages_answer_no_one()
    {
        var workspaceId = (await sofa.SetUp).FirstUser.WorkspaceId;
        using var gateway = sofa.CreateHttpClient(SofaServices.AppGateway);

        using var response = await gateway.GetAsync($"/api/workspaces/{workspaceId}/connections", Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_User_of_the_Workspace_who_is_no_Admin_is_refused()
    {
        var workspaceId = (await sofa.SetUp).FirstUser.WorkspaceId;
        await sofa.TheGitHubApp;
        await sofa.BeaAddedByHerGitHubLogin;
        using var bea = new WebClientSignIn(sofa.AddressOf(SofaServices.AppGateway));
        var signedIn = await bea.SignInWithGitHubAsync(FakeGitHub.CodeFor(FakeGitHub.Bea), Cancellation);
        Assert.NotNull(signedIn.Tokens);

        using var connections = await bea.GetWithTokenAsync($"/api/workspaces/{workspaceId}/connections", Cancellation);
        using var creation = await bea.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/github-app/creations", new { organisation = "acme" }, Cancellation);
        using var installation = await bea.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/github-app/installations", null, Cancellation);
        using var adding = await bea.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/users", new { gitHubLogin = "someone" }, Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, connections.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, creation.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, installation.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, adding.StatusCode);
    }

    [Fact]
    public async Task An_Admin_is_refused_in_a_Workspace_they_do_not_administer()
    {
        using var ada = await sofa.AdaSignedInAsync(Cancellation);

        using var response = await ada.GetWithTokenAsync($"/api/workspaces/{Guid.NewGuid()}/connections", Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
