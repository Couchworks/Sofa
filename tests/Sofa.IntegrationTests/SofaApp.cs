using Aspire.Hosting.ApplicationModel;
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

    public DistributedApplication App { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        using var cancellation = new CancellationTokenSource(StartTimeout);
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.Sofa_AppHost>(cancellation.Token);

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
