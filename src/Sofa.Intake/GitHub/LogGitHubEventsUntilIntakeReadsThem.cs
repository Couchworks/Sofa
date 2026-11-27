using Sofa.Contracts.Connections;
using Wolverine.Attributes;

namespace Sofa.Intake.GitHub;

[WolverineHandler]
public static class LogGitHubEventsUntilIntakeReadsThem
{
    public static void Handle(GitHubEventReceived received, ILogger<GitHubEventReceived> logger) =>
        logger.LogInformation(
            "Intake received GitHub {GitHubEvent} delivery {DeliveryId} for Connection {ConnectionId} of Workspace {WorkspaceId}",
            received.Name, received.DeliveryId, received.ConnectionId, received.WorkspaceId);
}
