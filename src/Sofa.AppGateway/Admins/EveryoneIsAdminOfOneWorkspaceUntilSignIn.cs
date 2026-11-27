namespace Sofa.AppGateway.Admins;

public sealed class EveryoneIsAdminOfOneWorkspaceUntilSignIn : IFindSignedInAdmin
{
    public static readonly Guid TheOneWorkspace = new("00000000-0000-7000-8000-000000000001");

    public SignedInAdmin? Find(HttpContext context) => new(TheOneWorkspace);
}
