namespace Sofa.Contracts.Connections;

public sealed record StartGitHubAppCreation(Guid WorkspaceId, string? Organisation, string? Name, string State);

public sealed record GitHubAppCreationStarted(string? FormAction, string? Manifest, string? Problem);

public sealed record FinishGitHubAppCreation(string Code, GitHubReturn StartedBy);

public sealed record GitHubAppCreationFinished(string? Problem)
{
    public bool Created => Problem is null;
}
