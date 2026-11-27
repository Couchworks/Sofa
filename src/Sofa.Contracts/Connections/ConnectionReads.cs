namespace Sofa.Contracts.Connections;

public sealed record GitHubAppStatus(
    bool Created,
    string? Name,
    string? HtmlUrl,
    string? InstallUrl,
    string PublicAddress,
    bool GitHubCanReachPublicAddress);

public sealed record ConnectionSummary(
    Guid Id,
    string System,
    string Account,
    string AccountType,
    DateTimeOffset AddedAt,
    bool Suspended);
