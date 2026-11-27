namespace Sofa.Contracts.Connections;

public sealed record GitHubReturn(Guid Nonce, Guid WorkspaceId, Guid AdminUserId, DateTimeOffset ExpiresAt);
