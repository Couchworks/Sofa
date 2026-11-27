namespace Sofa.Contracts.Connections;

public sealed record InstallationTokenRequest(
    IReadOnlyList<string>? Repositories = null,
    IReadOnlyDictionary<string, string>? Permissions = null);

public sealed record InstallationToken(string Token, DateTimeOffset ExpiresAt);
