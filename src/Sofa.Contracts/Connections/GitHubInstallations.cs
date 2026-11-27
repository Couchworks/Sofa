namespace Sofa.Contracts.Connections;

public sealed record AddGitHubInstallation(Guid WorkspaceId, long InstallationId);

public sealed record GitHubInstallationAdded(Guid? ConnectionId, string? Problem);
