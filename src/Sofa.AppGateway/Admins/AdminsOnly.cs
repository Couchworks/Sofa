using Microsoft.OpenApi;

namespace Sofa.AppGateway.Admins;

public static class AdminsOnly
{
    public const string WorkspaceRouteValue = "workspaceId";

    public static RouteGroupBuilder MapAdminsOnly(this IEndpointRouteBuilder endpoints) =>
        endpoints.MapGroup($"/api/workspaces/{{{WorkspaceRouteValue}:guid}}")
            .WithMetadata(new AdminsOnlyEndpoint())
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .AddOpenApiOperationTransformer((operation, _, _) =>
            {
                operation.Parameters ??= [];
                operation.Parameters.Insert(0, new OpenApiParameter
                {
                    Name = WorkspaceRouteValue,
                    In = ParameterLocation.Path,
                    Required = true,
                    Description = "The Workspace the signed-in User is an Admin of.",
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
                });
                return Task.CompletedTask;
            });

    public static IApplicationBuilder UseAdminsOnly(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<AdminsOnlyEndpoint>() is null)
            {
                await next(context);
                return;
            }

            if (context.User.Identity?.IsAuthenticated != true)
            {
                await Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Sign in first.")
                    .ExecuteAsync(context);
                return;
            }

            var admin = await context.RequestServices.GetRequiredService<IFindSignedInAdmin>().FindAsync(context);
            if (admin is null)
            {
                await Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only an Admin of this Workspace may do this.")
                    .ExecuteAsync(context);
                return;
            }

            context.Features.Set(admin);
            await next(context);
        });

    private sealed class AdminsOnlyEndpoint;
}
