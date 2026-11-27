using Sofa.Contracts.Identity;
using Sofa.Flow.Data;

namespace Sofa.Flow.Agents;

public static class AgentRegistrationHandler
{
    public static async Task Handle(
        AgentRegistration registration, FlowDb db, ILogger<AgentRegistration> logger, CancellationToken cancellation)
    {
        var agent = await db.Agents.FindAsync([registration.AgentId], cancellation);
        if (agent is null)
        {
            return;
        }

        agent.HeardFromIdentity(registration);
        await db.SaveChangesAsync(cancellation);
        if (!registration.Registered)
        {
            logger.LogWarning("Identity refused to register Agent {AgentId} ({Name}) of Project {ProjectId}: {Reason}",
                agent.Id, agent.Name, agent.ProjectId, registration.RefusedBecause);
        }
    }
}
