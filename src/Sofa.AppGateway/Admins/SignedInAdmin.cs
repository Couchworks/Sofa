namespace Sofa.AppGateway.Admins;

public sealed record SignedInAdmin(Guid UserId, Guid WorkspaceId)
{
    public static ValueTask<SignedInAdmin?> BindAsync(HttpContext context) =>
        ValueTask.FromResult(context.Features.Get<SignedInAdmin>());
}

public interface IFindSignedInAdmin
{
    Task<SignedInAdmin?> FindAsync(HttpContext context);
}
