using Sofa.Connections.Persistence;
using Sofa.Contracts.Connections;

namespace Sofa.Connections.GitHub;

public static class InstallationLifecycle
{
    public const string InstallationEvent = "installation";

    public static void Follow(
        GitHubEventReceived received,
        Connection? connection,
        ConnectionsDb db,
        TimeProvider time,
        ILogger logger)
    {
        if (received.Event != InstallationEvent || connection is null)
        {
            return;
        }

        switch (received.Action)
        {
            case "deleted":
                db.Connections.Remove(connection);
                logger.LogInformation(
                    "Installation {InstallationId} was removed from GitHub, so Connection {ConnectionId} is gone",
                    connection.GitHubInstallationId, connection.Id);
                break;
            case "suspend":
                connection.SuspendedAt = time.GetUtcNow();
                break;
            case "unsuspend":
                connection.SuspendedAt = null;
                break;
        }
    }
}
