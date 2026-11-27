using Sofa.Contracts.Identity;
using Wolverine;

namespace Sofa.Flow.Agents;

public static class AgentDiagnosticsEndpoints
{
    private static readonly TimeSpan IdentityAnswersWithin = TimeSpan.FromSeconds(30);

    public static IEndpointRouteBuilder MapAgentDiagnostics(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/diagnostics/agents", async (AgentToRegister agent, IMessageBus bus, CancellationToken cancellation) =>
        {
            var command = new RegisterAgent(Guid.CreateVersion7(), agent.WorkspaceId, agent.Name, agent.Owner);
            var registration = await bus.InvokeAsync<AgentRegistration>(command, cancellation, IdentityAnswersWithin);
            return registration.Registered
                ? Results.Created($"/diagnostics/agents/{registration.AgentId}", registration)
                : Results.Problem(statusCode: StatusCodes.Status422UnprocessableEntity, title: registration.RefusedBecause);
        });
        return endpoints;
    }
}

public sealed record AgentToRegister(Guid WorkspaceId, string Name, AgentOwner Owner);
