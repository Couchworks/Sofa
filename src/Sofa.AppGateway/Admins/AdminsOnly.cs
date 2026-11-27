namespace Sofa.AppGateway.Admins;

public static class AdminsOnly
{
    public static RouteGroupBuilder MapAdminsOnly(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGroup("")
            .WithMetadata(new AdminsOnlyEndpoint())
            .ProducesProblem(StatusCodes.Status401Unauthorized);

    public static IApplicationBuilder UseAdminsOnly(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<AdminsOnlyEndpoint>() is null)
            {
                await next(context);
                return;
            }

            var admin = context.RequestServices.GetRequiredService<IFindSignedInAdmin>().Find(context);
            if (admin is null)
            {
                await Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Only a Workspace's Admin may do this.")
                    .ExecuteAsync(context);
                return;
            }

            context.Features.Set(admin);
            await next(context);
        });

    private sealed class AdminsOnlyEndpoint;
}
