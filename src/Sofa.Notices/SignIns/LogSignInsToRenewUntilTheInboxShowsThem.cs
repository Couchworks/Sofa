using Sofa.Contracts.Execution;
using Wolverine.Attributes;

namespace Sofa.Notices.SignIns;

[WolverineHandler]
public static class LogSignInsToRenewUntilTheInboxShowsThem
{
    public static void Handle(SubscriptionSignedOut signedOut, ILogger<SubscriptionSignedOut> logger) =>
        logger.LogWarning(
            "User {UserId} of Workspace {WorkspaceId} must sign subscription Runner {RunnerId} ({RunnerName}) in again: it refused Run {RunId}",
            signedOut.UserId, signedOut.WorkspaceId, signedOut.RunnerId, signedOut.RunnerName, signedOut.RunId);
}
