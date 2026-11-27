using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Sofa.Contracts;

namespace Sofa.IntegrationTests;

public class GitHubAppTests(SofaApp sofa)
{
    private const string ReturnCookie = "sofa_github_return";
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(30);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_Admin_creates_the_GitHub_App_from_its_manifest()
    {
        var created = await sofa.TheGitHubApp;

        Assert.StartsWith($"{sofa.GitHub.WebAddress}organizations/{FakeGitHub.Organisation}/settings/apps/new?state=", created.Form.FormAction);
        var manifest = JsonDocument.Parse(created.Form.Manifest).RootElement;
        Assert.EndsWith("/github-app/created", manifest.GetProperty("redirect_url").GetString());
        Assert.EndsWith("/webhooks/github", manifest.GetProperty("hook_attributes").GetProperty("url").GetString());
        Assert.EndsWith("/account/github/callback", manifest.GetProperty("callback_urls")[0].GetString());
        Assert.Equal("/?github-app=done", created.BackInTheWebClient);
        Assert.Contains(CreatedGitHubApp.OneTimeCode, sofa.GitHub.ConvertedCodes);

        using var ada = await sofa.AdaSignedInAsync(Cancellation);
        using var answer = await ada.GetWithTokenAsync($"/api/workspaces/{await AdasWorkspace()}/github-app", Cancellation);
        var app = await answer.Content.ReadFromJsonAsync<GitHubAppStatus>(Cancellation);
        Assert.True(app!.Created);
    }

    [Fact]
    public async Task A_second_App_is_refused_once_one_exists()
    {
        await sofa.TheGitHubApp;
        using var ada = await sofa.AdaSignedInAsync(Cancellation);

        using var response = await ada.PostAsJsonAsync(
            $"/api/workspaces/{await AdasWorkspace()}/github-app/creations", new { organisation = "acme" }, Cancellation);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_return_from_GitHub_with_a_state_Sofa_never_signed_creates_nothing()
    {
        using var gateway = sofa.CreateHttpClientThatStaysOnRedirects(SofaServices.AppGateway);

        using var response = await gateway.GetAsync("/github-app/created?code=forged-code&state=forged-state", Cancellation);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/?github-app=failed", response.Headers.Location!.OriginalString);
        Assert.DoesNotContain("forged-code", sofa.GitHub.ConvertedCodes);
    }

    [Fact]
    public async Task A_return_without_any_state_is_refused()
    {
        await sofa.TheGitHubApp;
        using var gateway = sofa.CreateHttpClientThatStaysOnRedirects(SofaServices.AppGateway);

        using var response = await gateway.GetAsync(
            $"/github-app/installed?installation_id={FakeGitHub.InstallationId}&setup_action=install", Cancellation);

        Assert.StartsWith("/?connection=failed", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task GitHubs_own_return_after_an_installation_changes_lands_without_acting()
    {
        await TheAppInstalledOnce();
        using var gateway = sofa.CreateHttpClientThatStaysOnRedirects(SofaServices.AppGateway);

        using var response = await gateway.GetAsync(
            $"/github-app/installed?installation_id={FakeGitHub.InstallationId}&setup_action=update", Cancellation);

        Assert.Equal("/?connection=updated", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task A_signed_state_is_refused_in_a_browser_that_did_not_start_it()
    {
        await sofa.TheGitHubApp;
        using var ada = await sofa.AdaSignedInAsync(Cancellation);
        var state = await StartInstallingAsync(ada);
        using var anotherBrowser = sofa.CreateHttpClientThatStaysOnRedirects(SofaServices.AppGateway);

        using var response = await anotherBrowser.GetAsync(InstalledReturn(state), Cancellation);

        Assert.StartsWith("/?connection=failed", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task A_state_already_used_is_refused_even_with_its_cookie()
    {
        await TheAppInstalledOnce();
        using var ada = await sofa.AdaSignedInAsync(Cancellation);
        var state = await StartInstallingAsync(ada);
        var cookie = ada.Cookies.GetCookies(new Uri(sofa.AddressOf(SofaServices.AppGateway), "/github-app/installed"))[ReturnCookie]!.Value;

        using var first = await ReturnWithCookieAsync(state, cookie);
        using var again = await ReturnWithCookieAsync(state, cookie);

        Assert.Equal("/?connection=done", first.Headers.Location!.OriginalString);
        Assert.StartsWith("/?connection=failed", again.Headers.Location!.OriginalString);
        Assert.Contains("already used", Uri.UnescapeDataString(again.Headers.Location!.OriginalString));
    }

    [Fact]
    public async Task Keeps_the_Apps_credentials_sealed_at_rest()
    {
        await sofa.TheGitHubApp;
        await using var database = new NpgsqlConnection(await sofa.App.GetConnectionStringAsync("sofa", Cancellation));
        await database.OpenAsync(Cancellation);
        await using var query = new NpgsqlCommand(
            "select sealed_private_key || sealed_webhook_secret || sealed_client_secret from connections.github_apps", database);

        var stored = (string)(await query.ExecuteScalarAsync(Cancellation))!;

        Assert.DoesNotContain("PRIVATE KEY", stored);
        Assert.DoesNotContain(sofa.GitHub.WebhookSecret, stored);
        Assert.DoesNotContain(FakeGitHub.ClientSecret, stored);
    }

    [Fact]
    public async Task An_installation_becomes_a_Connection_shown_to_the_Workspace()
    {
        var backInTheWebClient = await TheAppInstalledOnce();

        Assert.Equal("/?connection=done", backInTheWebClient);
        var connection = await TheConnection();
        Assert.Equal(FakeGitHub.Organisation, connection.Account);
        Assert.Equal("Organization", connection.AccountType);
        Assert.Equal("GitHub", connection.System);
        Assert.Equal(await AdasWorkspace(), await WorkspaceOfConnection(connection.Id));
    }

    [Fact]
    public async Task An_installation_GitHub_does_not_know_is_no_Connection()
    {
        await sofa.TheGitHubApp;
        using var ada = await sofa.AdaSignedInAsync(Cancellation);
        var state = await StartInstallingAsync(ada);

        using var response = await ada.GetAsync(InstalledReturn(state, installationId: 31337), Cancellation);

        Assert.StartsWith("/?connection=failed", response.Headers.Location!.OriginalString);
    }

    [Fact]
    public async Task Connections_hands_out_an_installation_token_for_a_Connection()
    {
        await TheAppInstalledOnce();
        var connection = await TheConnection();
        using var connections = sofa.CreateHttpClient(SofaServices.Connections);

        using var response = await connections.PostAsJsonAsync(
            $"/connections/{connection.Id}/installation-tokens", new { repositories = new[] { "web" } }, Cancellation);

        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<InstallationToken>(Cancellation);
        Assert.Equal($"ghs_fake_{FakeGitHub.InstallationId}", token!.Token);
        Assert.True(token.ExpiresAt > DateTimeOffset.UtcNow);
        Assert.Contains(sofa.GitHub.TokenRequests, request =>
            request.TryGetProperty("repositories", out var repositories) && repositories[0].GetString() == "web");
    }

    [Fact]
    public async Task A_webhook_signed_with_the_Apps_secret_becomes_an_event_Intake_receives()
    {
        await TheAppInstalledOnce();
        var deliveryId = Guid.NewGuid();
        var body = Encoding.UTF8.GetBytes(
            $$$"""{"action":"labeled","label":{"name":"sofa"},"issue":{"number":12},"installation":{"id":{{{FakeGitHub.InstallationId}}}}}""");
        var intakeHearsIt = IntakeLogs(deliveryId.ToString());

        using var response = await DeliverWebhook(deliveryId, body, sofa.GitHub.Sign(body));

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.True(await intakeHearsIt, "Intake never logged the issues.labeled event.");
    }

    [Fact]
    public async Task A_webhook_failing_the_check_is_refused()
    {
        await sofa.TheGitHubApp;
        var body = Encoding.UTF8.GetBytes("""{"action":"labeled"}""");

        using var response = await DeliverWebhook(Guid.NewGuid(), body, "sha256=" + new string('0', 64));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_delivery_GitHub_failed_to_make_is_redelivered()
    {
        await sofa.TheGitHubApp;
        const long failedDelivery = 9001;
        sofa.GitHub.FailedToDeliver(failedDelivery, Guid.NewGuid());

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(EventTimeout);
        while (!sofa.GitHub.Redelivered.Contains(failedDelivery))
        {
            await Task.Delay(TimeSpan.FromMilliseconds(250), timeout.Token);
        }
    }

    private async Task<HttpResponseMessage> DeliverWebhook(Guid deliveryId, byte[] body, string signature)
    {
        using var gateway = sofa.CreateHttpClient(SofaServices.AppGateway);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/webhooks/github")
        {
            Content = new ByteArrayContent(body) { Headers = { { "Content-Type", "application/json" } } },
            Headers =
            {
                { "X-GitHub-Event", "issues" },
                { "X-GitHub-Delivery", deliveryId.ToString() },
                { "X-Hub-Signature-256", signature },
            },
        };
        return await gateway.SendAsync(request, Cancellation);
    }

    private async Task<bool> IntakeLogs(string text)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(EventTimeout);
        var logs = sofa.App.Services.GetRequiredService<ResourceLoggerService>();
        var intake = sofa.App.Services.GetRequiredService<DistributedApplicationModel>().Resources
            .Single(resource => resource.Name == SofaServices.Intake);
        try
        {
            await foreach (var batch in logs.WatchAsync(intake).WithCancellation(timeout.Token))
            {
                if (batch.Any(line => line.Content.Contains(text, StringComparison.Ordinal)))
                {
                    return true;
                }
            }
        }
        catch (OperationCanceledException) when (!Cancellation.IsCancellationRequested)
        {
        }

        return false;
    }

    private async Task<string> TheAppInstalledOnce() => (await sofa.TheConnection).BackInTheWebClient;

    private async Task<string> StartInstallingAsync(WebClientSignIn admin)
    {
        using var started = await admin.PostAsJsonAsync($"/api/workspaces/{await AdasWorkspace()}/github-app/installations", null, Cancellation);
        started.EnsureSuccessStatusCode();
        var installation = await started.Content.ReadFromJsonAsync<GitHubAppInstallation>(Cancellation);
        Assert.StartsWith($"{sofa.GitHub.WebAddress}apps/{FakeGitHub.AppSlug}/installations/new?state=", installation!.InstallUrl);
        return WebClientSignIn.StateIn(new Uri(installation.InstallUrl));
    }

    private static string InstalledReturn(string state, long installationId = FakeGitHub.InstallationId) =>
        $"/github-app/installed?installation_id={installationId}&setup_action=install&state={Uri.EscapeDataString(state)}";

    private async Task<HttpResponseMessage> ReturnWithCookieAsync(string state, string cookie)
    {
        using var browser = sofa.CreateHttpClientThatStaysOnRedirects(SofaServices.AppGateway);
        using var request = new HttpRequestMessage(HttpMethod.Get, InstalledReturn(state))
        {
            Headers = { { "Cookie", $"{ReturnCookie}={cookie}" } },
        };
        return await browser.SendAsync(request, Cancellation);
    }

    private async Task<Guid> AdasWorkspace() => (await sofa.SetUp).FirstUser.WorkspaceId;

    private async Task<Guid> WorkspaceOfConnection(Guid connectionId)
    {
        await using var database = new NpgsqlConnection(await sofa.App.GetConnectionStringAsync("sofa", Cancellation));
        await database.OpenAsync(Cancellation);
        await using var query = new NpgsqlCommand("select workspace_id from connections.connections where id = @id", database);
        query.Parameters.AddWithValue("id", connectionId);
        return (Guid)(await query.ExecuteScalarAsync(Cancellation))!;
    }

    private async Task<ConnectionSummary> TheConnection()
    {
        using var ada = await sofa.AdaSignedInAsync(Cancellation);
        using var answer = await ada.GetWithTokenAsync($"/api/workspaces/{await AdasWorkspace()}/connections", Cancellation);
        var connections = await answer.Content.ReadFromJsonAsync<List<ConnectionSummary>>(Cancellation);
        return Assert.Single(connections!, c => c.Account == FakeGitHub.Organisation);
    }

    private sealed record GitHubAppStatus(bool Created, string? InstallUrl);

    private sealed record GitHubAppInstallation(string InstallUrl);

    private sealed record ConnectionSummary(Guid Id, string System, string Account, string AccountType);

    private sealed record InstallationToken(string Token, DateTimeOffset ExpiresAt);
}
