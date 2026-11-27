namespace Sofa.Identity.Workspaces;

public sealed class Team
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required Guid WorkspaceId { get; init; }

    public required string Name { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
}
