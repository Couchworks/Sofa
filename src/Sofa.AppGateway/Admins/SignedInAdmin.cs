namespace Sofa.AppGateway.Admins;

public sealed record SignedInAdmin(Guid WorkspaceId)
{
    public static ValueTask<SignedInAdmin?> BindAsync(HttpContext context) =>
        ValueTask.FromResult(context.Features.Get<SignedInAdmin>());
}

public interface IFindSignedInAdmin
{
    SignedInAdmin? Find(HttpContext context);
}
