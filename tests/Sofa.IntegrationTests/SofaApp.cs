using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Sofa.Contracts;

[assembly: AssemblyFixture(typeof(Sofa.IntegrationTests.SofaApp))]

namespace Sofa.IntegrationTests;

/// <summary>
/// The whole of Sofa started once for the test run by Aspire's test host: every service,
/// the web client, and real Postgres and RabbitMQ in containers.
/// </summary>
public sealed class SofaApp : IAsyncLifetime
{
    private static readonly TimeSpan StartTimeout = TimeSpan.FromMinutes(5);

    /// <summary>Set to 1 to see every resource's own output, as CI does.</summary>
    private const string ShowResourceLogs = "SOFA_TEST_RESOURCE_LOGS";

    public DistributedApplication App { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        using var cancellation = new CancellationTokenSource(StartTimeout);
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Sofa_AppHost>(cancellation.Token);

        builder.Services.AddLogging(logging =>
        {
            logging.AddFilter(level => level >= LogLevel.Warning);
            // The app host's checks on Postgres and RabbitMQ fail until both have started; a check that
            // never passes shows as a test timing out instead.
            logging.AddFilter("Microsoft.Extensions.Diagnostics.HealthChecks", LogLevel.None);
            // Resources' output arrives as errors whenever it goes to stderr, as Postgres's and npm's does.
            if (Environment.GetEnvironmentVariable(ShowResourceLogs) != "1")
            {
                logging.AddFilter("Sofa.AppHost.Resources", LogLevel.None);
            }
        });

        builder.CreateResourceBuilder<ExecutableResource>("web")
            .WithEnvironment("SOFA_VITE_CACHE_DIR", "node_modules/.vite-integration-tests");

        App = await builder.BuildAsync(cancellation.Token);
        await App.StartAsync(cancellation.Token);

        await Task.WhenAll(SofaServices.All.Select(service =>
            App.ResourceNotifications.WaitForResourceHealthyAsync(service, cancellation.Token)));
    }

    public HttpClient CreateHttpClient(string service) => App.CreateHttpClient(service, "http");

    public async ValueTask DisposeAsync()
    {
        if (App is not null)
        {
            await App.DisposeAsync();
        }
    }
}
