namespace Sofa.Contracts.Connections;

public sealed record StartGitHubAppCreation(Guid WorkspaceId, string? Organisation, string? Name);

public sealed record GitHubAppCreationStarted(string? FormAction, string? Manifest, string? Problem);

public sealed record FinishGitHubAppCreation(string Code, string State);

public sealed record GitHubAppCreationFinished(string? Problem)
{
    public bool Created => Problem is null;
}
