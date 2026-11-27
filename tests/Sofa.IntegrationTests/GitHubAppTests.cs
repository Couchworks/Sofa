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
    private const string OneTimeCode = "code-github-hands-back-once";
    private static readonly TimeSpan EventTimeout = TimeSpan.FromSeconds(30);
    private static readonly SemaphoreSlim OneAtATime = new(1, 1);
    private static CreatedApp? _created;
    private static string? _installed;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_Admin_creates_the_GitHub_App_from_its_manifest()
    {
        var created = await TheAppCreatedOnce();

        Assert.StartsWith($"{FakeGitHub.WebAddress}organizations/{FakeGitHub.Organisation}/settings/apps/new?state=", created.Form.FormAction);
        var manifest = JsonDocument.Parse(created.Form.Manifest).RootElement;
        Assert.EndsWith("/github-app/created", manifest.GetProperty("redirect_url").GetString());
        Assert.EndsWith("/webhooks/github", manifest.GetProperty("hook_attributes").GetProperty("url").GetString());
        Assert.Equal("/?github-app=done", created.BackInTheWebClient);
        Assert.Contains(OneTimeCode, sofa.GitHub.ConvertedCodes);

        using var gateway = sofa.CreateHttpClient(SofaServices.AppGateway);
        var app = await gateway.GetFromJsonAsync<GitHubAppStatus>("/api/github-app", Cancellation);
        Assert.True(app!.Created);
        Assert.Equal($"{FakeGitHub.WebAddress}apps/{FakeGitHub.AppSlug}/installations/new", app.InstallUrl);
    }

    [Fact]
    public async Task A_second_App_is_refused_once_one_exists()
    {
        await TheAppCreatedOnce();
        using var gateway = sofa.CreateHttpClient(SofaServices.AppGateway);

        using var response = await gateway.PostAsJsonAsync("/api/github-app/creations", new { organisation = "acme" }, Cancellation);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_return_from_GitHub_with_a_state_Sofa_never_issued_creates_nothing()
    {
        using var gateway = sofa.CreateHttpClientThatStaysOnRedirects(SofaServices.AppGateway);

        using var response = await gateway.GetAsync("/github-app/created?code=forged-code&state=forged-state", Cancellation);

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.StartsWith("/?github-app=failed", response.Headers.Location!.OriginalString);
        Assert.DoesNotContain("forged-code", sofa.GitHub.ConvertedCodes);
    }

    [Fact]
    public async Task Keeps_the_Apps_credentials_sealed_at_rest()
    {
        await TheAppCreatedOnce();
        await using var database = new NpgsqlConnection(await sofa.App.GetConnectionStringAsync("sofa", Cancellation));
        await database.OpenAsync(Cancellation);
        await using var query = new NpgsqlCommand(
            "select sealed_private_key || sealed_webhook_secret || sealed_client_secret from connections.github_apps", database);

        var stored = (string)(await query.ExecuteScalarAsync(Cancellation))!;

        Assert.DoesNotContain("PRIVATE KEY", stored);
        Assert.DoesNotContain(sofa.GitHub.WebhookSecret, stored);
        Assert.DoesNotContain("fake-client-secret", stored);
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
    }

    [Fact]
    public async Task An_installation_GitHub_does_not_know_is_no_Connection()
    {
        await TheAppCreatedOnce();
        using var gateway = sofa.CreateHttpClientThatStaysOnRedirects(SofaServices.AppGateway);

        using var response = await gateway.GetAsync("/github-app/installed?installation_id=31337&setup_action=install", Cancellation);

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
        await TheAppCreatedOnce();
        var body = Encoding.UTF8.GetBytes("""{"action":"labeled"}""");

        using var response = await DeliverWebhook(Guid.NewGuid(), body, "sha256=" + new string('0', 64));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_delivery_GitHub_failed_to_make_is_redelivered()
    {
        await TheAppCreatedOnce();
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

    private async Task<CreatedApp> TheAppCreatedOnce()
    {
        await OneAtATime.WaitAsync(Cancellation);
        try
        {
            return _created ??= await CreateTheApp();
        }
        finally
        {
            OneAtATime.Release();
        }
    }

    private async Task<CreatedApp> CreateTheApp()
    {
        using var gateway = sofa.CreateHttpClientThatStaysOnRedirects(SofaServices.AppGateway);
        using var started = await gateway.PostAsJsonAsync(
            "/api/github-app/creations", new { organisation = FakeGitHub.Organisation }, Cancellation);
        started.EnsureSuccessStatusCode();
        var form = (await started.Content.ReadFromJsonAsync<GitHubAppCreationForm>(Cancellation))!;
        var state = new Uri(form.FormAction).Query.Split("state=")[1];

        using var returned = await gateway.GetAsync($"/github-app/created?code={OneTimeCode}&state={state}", Cancellation);
        Assert.Equal(HttpStatusCode.Redirect, returned.StatusCode);
        return new CreatedApp(form, returned.Headers.Location!.OriginalString);
    }

    private async Task<string> TheAppInstalledOnce()
    {
        await TheAppCreatedOnce();
        await OneAtATime.WaitAsync(Cancellation);
        try
        {
            if (_installed is null)
            {
                using var gateway = sofa.CreateHttpClientThatStaysOnRedirects(SofaServices.AppGateway);
                using var returned = await gateway.GetAsync(
                    $"/github-app/installed?installation_id={FakeGitHub.InstallationId}&setup_action=install", Cancellation);
                _installed = returned.Headers.Location!.OriginalString;
            }

            return _installed;
        }
        finally
        {
            OneAtATime.Release();
        }
    }

    private async Task<ConnectionSummary> TheConnection()
    {
        using var gateway = sofa.CreateHttpClient(SofaServices.AppGateway);
        var connections = await gateway.GetFromJsonAsync<List<ConnectionSummary>>("/api/connections", Cancellation);
        return Assert.Single(connections!, c => c.Account == FakeGitHub.Organisation);
    }

    private sealed record CreatedApp(GitHubAppCreationForm Form, string BackInTheWebClient);

    private sealed record GitHubAppCreationForm(string FormAction, string Manifest);

    private sealed record GitHubAppStatus(bool Created, string? InstallUrl);

    private sealed record ConnectionSummary(Guid Id, string System, string Account, string AccountType);

    private sealed record InstallationToken(string Token, DateTimeOffset ExpiresAt);
}
