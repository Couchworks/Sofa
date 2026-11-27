using Sofa.Contracts.Connections;
using Sofa.Intake.Data;
using Sofa.Intake.Questions;
using Sofa.Intake.Requests;
using Wolverine;

namespace Sofa.Intake.GitHub;

public static class GitHubEventHandler
{
    public static async Task<OutgoingMessages> Handle(
        GitHubEventReceived received,
        IntakeDb db,
        IdentityGitHubAccounts identity,
        TimeProvider time,
        ILogger<GitHubEventReceived> logger,
        CancellationToken cancellation)
    {
        logger.LogInformation(
            "Intake read GitHub {GitHubEvent} delivery {DeliveryId} for Connection {ConnectionId} of Workspace {WorkspaceId}",
            received.Name, received.DeliveryId, received.ConnectionId, received.WorkspaceId);

        if (received is not { ConnectionId: { } connectionId, WorkspaceId: { } workspaceId })
        {
            return [];
        }

        return received.Name switch
        {
            GitHubPayloads.Labeled when GitHubPayloads.ReadLabeled(received.Payload) is { } labeled =>
                await RequestIntake.TakeInAsync(labeled, workspaceId, connectionId, db, identity, time, logger, cancellation),
            GitHubPayloads.Commented when GitHubPayloads.ReadCommented(received.Payload) is { } commented =>
                await AnswersOnTheIssue.ReadAsync(commented, workspaceId, connectionId, db, identity, time, logger, cancellation),
            _ => [],
        };
    }
}
