namespace Sofa.Intake.Bindings;

public sealed class RequestBinding
{
    public required Guid ProjectId { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required string ProjectKey { get; init; }

    public Guid ConnectionId { get; set; }

    public string Repository { get; set; } = "";

    public string Label { get; set; } = "";

    public bool KeepsIssueState { get; set; }

    public bool Removed { get; set; }

    public long Version { get; set; }
}
