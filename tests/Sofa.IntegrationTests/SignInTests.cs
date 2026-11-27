using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Sofa.Contracts;
using Sofa.Contracts.Identity;

namespace Sofa.IntegrationTests;

public class SignInTests(SofaApp sofa)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task The_first_User_signs_in_with_email_and_password_and_the_App_Gateway_shows_who_they_are()
    {
        var user = (await sofa.SetUp).FirstUser;
        using var browser = new WebClientSignIn(sofa.AddressOf(SofaServices.AppGateway));

        var tokens = await browser.SignInAsync(user.Email, user.Password, Cancellation);
        var signedIn = await SignedInUserAsync(tokens.AccessToken);

        Assert.Equal(user.Id, signedIn!.Id);
        Assert.Equal(user.Name, signedIn.Name);
        Assert.Equal(user.Email, signedIn.Email);
        var workspace = Assert.Single(signedIn.Workspaces);
        Assert.Equal((user.WorkspaceId, user.WorkspaceName, true), (workspace.Id, workspace.Name, workspace.IsAdmin));
        var team = Assert.Single(workspace.Teams);
        Assert.Equal((user.TeamId, user.TeamName), (team.Id, team.Name));
    }

    [Fact]
    public async Task Authorizing_without_a_signed_in_User_sends_the_browser_to_the_web_clients_sign_in_page()
    {
        await sofa.SetUp;
        using var browser = new WebClientSignIn(sofa.AddressOf(SofaServices.AppGateway));

        using var response = await browser.GetAsync(browser.AuthorizationRequest("challenge-of-43-characters-at-the-least-xyz", "state"), Cancellation);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/sign-in?returnUrl=%2Fconnect%2Fauthorize%3F", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task A_wrong_password_is_refused()
    {
        var user = (await sofa.SetUp).FirstUser;
        using var browser = new WebClientSignIn(sofa.AddressOf(SofaServices.AppGateway));

        using var response = await browser.PostSignInAsync(user.Email, "not the password", Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task An_unknown_email_is_refused_the_same_way()
    {
        await sofa.SetUp;
        using var browser = new WebClientSignIn(sofa.AddressOf(SofaServices.AppGateway));

        using var response = await browser.PostSignInAsync("nobody@example.com", "a long enough password", Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_refresh_token_gives_a_new_access_token()
    {
        var user = (await sofa.SetUp).FirstUser;
        using var browser = new WebClientSignIn(sofa.AddressOf(SofaServices.AppGateway));
        var tokens = await browser.SignInAsync(user.Email, user.Password, Cancellation);

        var refreshed = await browser.RefreshAsync(tokens.RefreshToken!, Cancellation);

        Assert.Equal(user.Id, (await SignedInUserAsync(refreshed.AccessToken))!.Id);
    }

    [Fact]
    public async Task Signing_out_makes_the_next_authorization_ask_for_the_password_again()
    {
        var user = (await sofa.SetUp).FirstUser;
        using var browser = new WebClientSignIn(sofa.AddressOf(SofaServices.AppGateway));
        await browser.SignInAsync(user.Email, user.Password, Cancellation);

        using var signedOut = await browser.GetAsync("/connect/endsession", Cancellation);
        using var authorizeAgain = await browser.GetAsync(browser.AuthorizationRequest("challenge-of-43-characters-at-the-least-xyz", "state"), Cancellation);

        Assert.Equal(HttpStatusCode.Redirect, signedOut.StatusCode);
        Assert.StartsWith("/sign-in?", authorizeAgain.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Without_a_token_the_App_Gateway_shows_no_one()
    {
        using var gateway = sofa.CreateHttpClient(SofaServices.AppGateway);

        using var response = await gateway.GetAsync("/api/me", Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task<UserProfile?> SignedInUserAsync(string accessToken)
    {
        using var gateway = sofa.CreateHttpClient(SofaServices.AppGateway);
        gateway.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return await gateway.GetFromJsonAsync<UserProfile>("/api/me", Cancellation);
    }
}
