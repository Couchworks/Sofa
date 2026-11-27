using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Dotnet;
using Microsoft.Extensions.DependencyInjection;
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
    private const string EachTestRunStartsFresh = "--Sofa:FreshDatabaseEachStart=true";

    public DistributedApplication App { get; private set; } = null!;

    public FakeGitHub GitHub { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        using var cancellation = new CancellationTokenSource(StartTimeout);
        GitHub = await FakeGitHub.Start(cancellation.Token);
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
            .WithEnvironment("GitHub__WebAddress", FakeGitHub.WebAddress)
            .WithEnvironment("GitHub__DeliverySweepInterval", QuickDeliverySweeps.ToString());

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

    private readonly Lazy<Task<FreshInstallation>> _setUp;

    public SofaApp()
    {
        _setUp = new(() => FreshInstallation.SetUpAsync(this), LazyThreadSafetyMode.ExecutionAndPublication);
    }

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
    }
}
