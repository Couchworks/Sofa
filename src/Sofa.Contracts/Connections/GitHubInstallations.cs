namespace Sofa.Contracts.Connections;

public sealed record AddGitHubInstallation(long InstallationId, GitHubReturn StartedBy);

public sealed record GitHubInstallationAdded(Guid? ConnectionId, string? Problem);
