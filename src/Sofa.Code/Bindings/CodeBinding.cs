namespace Sofa.Code.Bindings;

public sealed class CodeBinding
{
    public required Guid ProjectId { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required string ProjectKey { get; init; }

    public Guid ConnectionId { get; set; }

    public List<string> Repositories { get; set; } = [];

    public bool Removed { get; set; }

    public long Version { get; set; }
}
