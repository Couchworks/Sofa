namespace Sofa.Identity.Workspaces;

public sealed class WorkspaceUser
{
    public required Guid WorkspaceId { get; init; }

    public required Guid UserId { get; init; }

    public bool IsAdmin { get; set; }

    public DateTimeOffset JoinedAt { get; init; }
}
