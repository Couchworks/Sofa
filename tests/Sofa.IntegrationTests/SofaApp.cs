using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Dotnet;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Sofa.Contracts;

[assembly: AssemblyFixture(typeof(Sofa.IntegrationTests.SofaApp))]

namespace Sofa.IntegrationTests;

public sealed class SofaApp : IAsyncLifetime
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(5);

    private const string ShowResourceLogs = "SOFA_TEST_RESOURCE_LOGS";
    private const string HealthChecksFailingUntilContainersStart = "Microsoft.Extensions.Diagnostics.HealthChecks";
    private const string ResourceOutputWithStderrAsErrors = "Sofa.AppHost.Resources";

    private static readonly TimeSpan QuickDeliverySweeps = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan QuickQueueLooks = TimeSpan.FromSeconds(1);
    private const string EachTestRunStartsFresh = "--Sofa:FreshDatabaseEachStart=true";

    public DistributedApplication App { get; private set; } = null!;

    public FakeGitHub GitHub { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        using var cancellation = new CancellationTokenSource(StartTimeout);
        GitHub = await FakeGitHub.Start(cancellation.Token);
        await StubWorker.BuildAsync(cancellation.Token);
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Sofa_AppHost>(
            [EachTestRunStartsFresh], cancellation.Token);

        builder.Services.AddLogging(logging =>
        {
            logging.AddFilter(level => level >= LogLevel.Warning);
            logging.AddFilter(HealthChecksFailingUntilContainersStart, LogLevel.None);
            if (Environment.GetEnvironmentVariable(ShowResourceLogs) != "1")
            {
                logging.AddFilter(ResourceOutputWithStderrAsErrors, LogLevel.None);
            }
        });

        builder.CreateResourceBuilder<ExecutableResource>("web")
            .WithEnvironment("SOFA_VITE_CACHE_DIR", "node_modules/.vite-integration-tests");

        builder.CreateResourceBuilder<DotnetProjectResource>(SofaServices.Connections)
            .WithEnvironment("GitHub__ApiAddress", GitHub.ApiAddress.ToString())
            .WithEnvironment("GitHub__WebAddress", GitHub.WebAddress)
            .WithEnvironment("GitHub__DeliverySweepInterval", QuickDeliverySweeps.ToString());

        builder.CreateResourceBuilder<DotnetProjectResource>(SofaServices.Intake)
            .WithEnvironment("GitHub__ApiAddress", GitHub.ApiAddress.ToString());

        builder.CreateResourceBuilder<DotnetProjectResource>(SofaServices.Execution)
            .WithEnvironment("Workers__Image", StubWorker.Image)
            .WithEnvironment("Workers__QueueLooksAgainEvery", QuickQueueLooks.ToString());

        App = await builder.BuildAsync(cancellation.Token);
        await App.StartAsync(cancellation.Token);

        await Task.WhenAll(SofaServices.All.Select(service =>
            App.ResourceNotifications.WaitForResourceHealthyAsync(service, cancellation.Token)));
    }

    public HttpClient CreateHttpClient(string service) => App.CreateHttpClient(service, "http");

    public HttpClient CreateHttpClientThatStaysOnRedirects(string service) =>
        new(new HttpClientHandler { AllowAutoRedirect = false }) { BaseAddress = AddressOf(service) };

    public Uri AddressOf(string service) => App.GetEndpoint(service, "http");

    public Task<FreshInstallation> SetUp => _setUp.Value;

    public Task<CreatedGitHubApp> TheGitHubApp => _gitHubApp.Value;

    public Task<Guid> BeaAddedByHerGitHubLogin => _beaAdded.Value;

    public Task<InstalledConnection> TheConnection => _connection.Value;

    public Task<ProjectForRuns> TheProjectForRuns => _projectForRuns.Value;
    public Task<System.Text.Json.JsonElement> TheDemoProject => _demo.Value;

    private readonly Lazy<Task<FreshInstallation>> _setUp;
    private readonly Lazy<Task<CreatedGitHubApp>> _gitHubApp;
    private readonly Lazy<Task<Guid>> _beaAdded;
    private readonly Lazy<Task<InstalledConnection>> _connection;
    private readonly Lazy<Task<ProjectForRuns>> _projectForRuns;
    private readonly Lazy<Task<System.Text.Json.JsonElement>> _demo;

    public SofaApp()
    {
        _setUp = new(() => FreshInstallation.SetUpAsync(this), LazyThreadSafetyMode.ExecutionAndPublication);
        _gitHubApp = new(() => CreatedGitHubApp.CreateAsync(this), LazyThreadSafetyMode.ExecutionAndPublication);
        _beaAdded = new(AddBeaAsync, LazyThreadSafetyMode.ExecutionAndPublication);
        _connection = new(() => InstalledConnection.InstallAsync(this), LazyThreadSafetyMode.ExecutionAndPublication);
        _projectForRuns = new(() => ProjectForRuns.CreateAsync(this, "RUNS", CancellationToken.None), LazyThreadSafetyMode.ExecutionAndPublication);
        _demo = new(() => TestProjects.CreateAsync(this, "DEMO"), LazyThreadSafetyMode.ExecutionAndPublication);
    }

    public async Task<WebClientSignIn> AdaSignedInAsync(CancellationToken cancellation)
    {
        var ada = (await SetUp).FirstUser;
        var browser = new WebClientSignIn(AddressOf(SofaServices.AppGateway));
        await browser.SignInAsync(ada.Email, ada.Password, cancellation);
        return browser;
    }

    private async Task<Guid> AddBeaAsync()
    {
        var workspaceId = (await SetUp).FirstUser.WorkspaceId;
        using var ada = await AdaSignedInAsync(CancellationToken.None);
        using var added = await ada.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/users", new { gitHubLogin = FakeGitHub.Bea.Login }, CancellationToken.None);
        added.EnsureSuccessStatusCode();
        return (await added.Content.ReadFromJsonAsync<AddedUser>())!.UserId;
    }

    public async Task<Guid> AddedByGitHubLoginAsync(GitHubAccountOnFake account)
    {
        var workspaceId = (await SetUp).FirstUser.WorkspaceId;
        using var ada = await AdaSignedInAsync(CancellationToken.None);
        using var added = await ada.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/users", new { gitHubLogin = account.Login }, CancellationToken.None);
        added.EnsureSuccessStatusCode();
        return (await added.Content.ReadFromJsonAsync<AddedUser>())!.UserId;
    }

    public Task<Guid> UserOnceAsync(GitHubAccountOnFake account) =>
        _usersAdded.GetOrAdd(account.Id, _ => new Lazy<Task<Guid>>(() => AddedByGitHubLoginAsync(account))).Value;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<long, Lazy<Task<Guid>>> _usersAdded = new();

    public async Task<WebClientSignIn> SignedInWithGitHubAsync(GitHubAccountOnFake account, CancellationToken cancellation)
    {
        await TheGitHubApp;
        var browser = new WebClientSignIn(AddressOf(SofaServices.AppGateway));
        var signedIn = await browser.SignInWithGitHubAsync(FakeGitHub.CodeFor(account), cancellation);
        if (signedIn.Tokens is null)
        {
            browser.Dispose();
            throw new InvalidOperationException($"{account.Login} couldn't sign in with GitHub: {signedIn.LandedOn}");
        }

        return browser;
    }

    public async Task<WebClientSignIn> BeaSignedInAsync(CancellationToken cancellation)
    {
        await BeaAddedByHerGitHubLogin;
        return await SignedInWithGitHubAsync(FakeGitHub.Bea, cancellation);
    }

    private sealed record AddedUser(Guid UserId);

    public async ValueTask DisposeAsync()
    {
        if (App is not null)
        {
            await App.DisposeAsync();
        }

        if (GitHub is not null)
        {
            await GitHub.DisposeAsync();
        }

        await StubWorker.RemoveTheVolumesMadeAsync();
    }
}
