namespace Sofa.Contracts.Notices;

public sealed record InboxChanged(Guid WorkspaceId, Guid? UserId)
{
    public bool ForTheWorkspacesAdmins => UserId is null;
}
