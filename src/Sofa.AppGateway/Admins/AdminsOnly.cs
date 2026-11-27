namespace Sofa.AppGateway.Admins;

public static class AdminsOnly
{
    public static RouteGroupBuilder MapAdminsOnly(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGroup("")
            .AddEndpointFilter(LetInOnlyASignedInAdmin)
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    private static async ValueTask<object?> LetInOnlyASignedInAdmin(
        EndpointFilterInvocationContext invocation, EndpointFilterDelegate next)
    {
        var context = invocation.HttpContext;
        var admin = context.RequestServices.GetRequiredService<IFindSignedInAdmin>().Find(context);
        if (admin is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Only a Workspace's Admin may do this.");
        }

        context.Features.Set(admin);
        return await next(invocation);
    }
}
