namespace Sofa.Connections.Persistence;

public sealed class GitHubApp
{
    public required long Id { get; init; }

    public required string Slug { get; init; }

    public required string Name { get; init; }

    public required string OwnerLogin { get; init; }

    public required string HtmlUrl { get; init; }

    public required string ClientId { get; init; }

    public required string SealedPrivateKey { get; init; }

    public required string SealedWebhookSecret { get; init; }

    public required string SealedClientSecret { get; init; }

    public required DateTimeOffset CreatedAt { get; init; }

    public DateTimeOffset? DeliveriesCheckedUntil { get; set; }
}

public sealed class UsedGitHubReturn
{
    public required Guid Nonce { get; init; }

    public required DateTimeOffset ExpiresAt { get; init; }
}

public sealed class Connection
{
    public const string GitHub = "GitHub";

    public required Guid Id { get; init; }

    public required Guid WorkspaceId { get; init; }

    public string System { get; init; } = GitHub;

    public required long GitHubInstallationId { get; init; }

    public required string Account { get; set; }

    public required string AccountType { get; set; }

    public required DateTimeOffset AddedAt { get; init; }

    public DateTimeOffset? SuspendedAt { get; set; }
}

public sealed class ReceivedDelivery
{
    public required Guid DeliveryId { get; init; }

    public required DateTimeOffset ReceivedAt { get; init; }
}

public sealed class ApiKey
{
    public required Guid Id { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required string Vendor { get; init; }

    public required string SealedKey { get; init; }

    public required DateTimeOffset SavedAt { get; init; }
}
