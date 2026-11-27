namespace Sofa.AppGateway;

/// <summary>Whether the App Gateway is up: the one endpoint the web client calls in slice 1.</summary>
public static class StatusEndpoints
{
    public static RouteGroupBuilder MapStatus(this RouteGroupBuilder api)
    {
        api.MapGet("/status", (TimeProvider time, IHostEnvironment environment) =>
                new Status("Sofa", environment.EnvironmentName, time.GetUtcNow()))
            .WithName("GetStatus")
            .WithSummary("Whether the App Gateway is up.");

        return api;
    }
}

public sealed record Status(string Name, string Environment, DateTimeOffset Time);
