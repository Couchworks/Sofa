namespace Sofa.Identity.Workspaces;

public sealed class Workspace
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string Name { get; set; }

    public DateTimeOffset CreatedAt { get; init; }
}
