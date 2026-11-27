namespace Sofa.Contracts.Code;

public sealed record PushOutsideItsSandboxCaught(
    Guid WorkspaceId,
    Guid ProjectId,
    string Repository,
    string Ref,
    string Before,
    string After,
    bool Undone,
    IReadOnlyList<Guid> RunsThatCouldHavePushed,
    string Why);
