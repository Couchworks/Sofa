namespace Sofa.Identity.Workspaces;

public sealed class TeamUser
{
    public required Guid WorkspaceId { get; init; }

    public required Guid TeamId { get; init; }

    public required Guid UserId { get; init; }

    public DateTimeOffset JoinedAt { get; init; }
}
