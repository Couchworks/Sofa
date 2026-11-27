using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Sofa.Contracts;
using Sofa.Contracts.Identity;

namespace Sofa.IntegrationTests;

public class GitHubSignInTests(SofaApp sofa)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_GitHub_account_no_User_has_is_refused()
    {
        await sofa.TheGitHubApp;
        using var mallory = new WebClientSignIn(sofa.AddressOf(SofaServices.AppGateway));

        var signIn = await mallory.SignInWithGitHubAsync(FakeGitHub.CodeFor(FakeGitHub.Mallory), Cancellation);
        using var authorizeAgain = await mallory.GetAsync(
            mallory.AuthorizationRequest("challenge-of-43-characters-at-the-least-xyz", "state"), Cancellation);

        Assert.Null(signIn.Tokens);
        Assert.StartsWith("/sign-in?problem=", signIn.LandedOn);
        Assert.Contains($"No User has the GitHub account {FakeGitHub.Mallory.Login}", Uri.UnescapeDataString(signIn.LandedOn));
        Assert.StartsWith("/sign-in?", authorizeAgain.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Someone_an_Admin_added_by_GitHub_login_signs_in_with_GitHub_and_joins_the_Workspace()
    {
        var ada = (await sofa.SetUp).FirstUser;
        await sofa.TheGitHubApp;
        var beaId = await sofa.BeaAddedByHerGitHubLogin;
        using var bea = new WebClientSignIn(sofa.AddressOf(SofaServices.AppGateway));

        var signIn = await bea.SignInWithGitHubAsync(FakeGitHub.CodeFor(FakeGitHub.Bea), Cancellation);
        var profile = await ProfileAsync(signIn.Tokens!);

        Assert.Equal(beaId, profile.Id);
        Assert.Equal(FakeGitHub.Bea.Login, profile.GitHubLogin);
        Assert.Equal(FakeGitHub.Bea.VerifiedEmail, profile.Email);
        var workspace = Assert.Single(profile.Workspaces);
        Assert.Equal((ada.WorkspaceId, false), (workspace.Id, workspace.IsAdmin));
        Assert.Contains(sofa.GitHub.RedirectUris, uri => uri.EndsWith("/account/github/callback", StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_signed_in_User_links_their_GitHub_account_and_then_signs_in_with_it()
    {
        await sofa.TheGitHubApp;
        using var ada = await sofa.AdaSignedInAsync(Cancellation);

        using var started = await ada.PostAsJsonAsync("/api/me/github-link", null, Cancellation);
        started.EnsureSuccessStatusCode();
        var link = await started.Content.ReadFromJsonAsync<GitHubLink>(Cancellation);
        using var linked = await ada.ReturnFromGitHubAsync(
            FakeGitHub.CodeFor(FakeGitHub.Ada), WebClientSignIn.StateIn(new Uri(link!.AuthorizeUrl)), Cancellation);
        using var anotherBrowser = new WebClientSignIn(sofa.AddressOf(SofaServices.AppGateway));
        var signIn = await anotherBrowser.SignInWithGitHubAsync(FakeGitHub.CodeFor(FakeGitHub.Ada), Cancellation);

        Assert.StartsWith($"{sofa.GitHub.WebAddress}login/oauth/authorize?client_id={Uri.EscapeDataString(FakeGitHub.ClientId)}", link.AuthorizeUrl);
        Assert.Equal("/?github-link=done", linked.Headers.Location!.OriginalString);
        Assert.Equal(FakeGitHub.Ada.Login, (await ProfileAsync(ada.Tokens!)).GitHubLogin);
        Assert.Equal((await sofa.SetUp).FirstUser.Id, (await ProfileAsync(signIn.Tokens!)).Id);
    }

    [Fact]
    public async Task A_return_from_GitHub_this_browser_did_not_start_is_refused()
    {
        await sofa.TheGitHubApp;
        using var browser = new WebClientSignIn(sofa.AddressOf(SofaServices.AppGateway));

        using var response = await browser.ReturnFromGitHubAsync(FakeGitHub.CodeFor(FakeGitHub.Bea), "made-up-state", Cancellation);

        Assert.StartsWith("/sign-in?problem=", response.Headers.Location!.OriginalString);
        Assert.Empty(browser.Cookies.GetCookies(new Uri(sofa.AddressOf(SofaServices.AppGateway), "/connect/authorize")));
    }

    [Fact]
    public async Task A_return_from_GitHub_is_used_once()
    {
        await sofa.TheGitHubApp;
        await sofa.BeaAddedByHerGitHubLogin;
        using var bea = new WebClientSignIn(sofa.AddressOf(SofaServices.AppGateway));
        var signIn = await bea.SignInWithGitHubAsync(FakeGitHub.CodeFor(FakeGitHub.Bea), Cancellation);
        using var replay = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
        {
            BaseAddress = sofa.AddressOf(SofaServices.AppGateway),
        };
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"/account/github/callback?code={FakeGitHub.CodeFor(FakeGitHub.Bea)}&state={Uri.EscapeDataString(signIn.GitHubState)}")
        {
            Headers = { { "Cookie", $"{GitHubSignInState.Cookie}={signIn.GitHubState}" } },
        };

        using var again = await replay.SendAsync(request, Cancellation);

        Assert.NotNull(signIn.Tokens);
        Assert.StartsWith("/sign-in?problem=", again.Headers.Location!.OriginalString);
        Assert.False(again.Headers.TryGetValues("Set-Cookie", out var cookies)
            && cookies.Any(cookie => cookie.StartsWith("sofa_sign_in=", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task A_login_that_is_no_person_on_GitHub_is_refused_when_added()
    {
        var workspaceId = (await sofa.SetUp).FirstUser.WorkspaceId;
        using var ada = await sofa.AdaSignedInAsync(Cancellation);

        using var nobody = await ada.PostAsJsonAsync($"/api/workspaces/{workspaceId}/users", new { gitHubLogin = "nobody-on-github" }, Cancellation);
        using var organisation = await ada.PostAsJsonAsync($"/api/workspaces/{workspaceId}/users", new { gitHubLogin = FakeGitHub.Organisation }, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, nobody.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, organisation.StatusCode);
    }

    [Fact]
    public async Task A_login_renamed_and_taken_by_someone_else_before_the_first_sign_in_reaches_only_its_first_holder()
    {
        var workspaceId = (await sofa.SetUp).FirstUser.WorkspaceId;
        await sofa.TheGitHubApp;
        using var ada = await sofa.AdaSignedInAsync(Cancellation);
        using var added = await ada.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/users", new { gitHubLogin = FakeGitHub.Carol.Login }, Cancellation);
        var carolId = (await added.Content.ReadFromJsonAsync<UserAdded>(Cancellation))!.UserId;

        sofa.GitHub.Rename(FakeGitHub.Carol, "carol-renamed");
        sofa.GitHub.Rename(FakeGitHub.Dave, FakeGitHub.Carol.Login);
        using var dave = new WebClientSignIn(sofa.AddressOf(SofaServices.AppGateway));
        var daveSignsIn = await dave.SignInWithGitHubAsync(FakeGitHub.CodeFor(FakeGitHub.Carol), Cancellation);
        using var carol = new WebClientSignIn(sofa.AddressOf(SofaServices.AppGateway));
        var carolSignsIn = await carol.SignInWithGitHubAsync($"carol-renamed{FakeGitHub.CodeNamesItsAccount}{Guid.NewGuid():N}", Cancellation);

        Assert.Equal(HttpStatusCode.Created, added.StatusCode);
        Assert.Null(daveSignsIn.Tokens);
        var profile = await ProfileAsync(carolSignsIn.Tokens!);
        Assert.Equal((carolId, "carol-renamed"), (profile.Id, profile.GitHubLogin));
    }

    private async Task<UserProfile> ProfileAsync(Tokens tokens)
    {
        using var gateway = sofa.CreateHttpClient(SofaServices.AppGateway);
        gateway.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", tokens.AccessToken);
        return (await gateway.GetFromJsonAsync<UserProfile>("/api/me", Cancellation))!;
    }

    private sealed record GitHubLink(string AuthorizeUrl);
}
