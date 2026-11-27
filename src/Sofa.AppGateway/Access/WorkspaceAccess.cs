using Microsoft.OpenApi;

namespace Sofa.AppGateway.Access;

public static class WorkspaceAccess
{
    public const string WorkspaceRouteValue = "workspaceId";

    public const string OnlyAdmins = "Only an Admin of this Workspace may do this.";
    public const string OnlyUsersOfTheWorkspace = "Only a User of this Workspace may do this.";

    public static RouteGroupBuilder MapAdminsOnly(this IEndpointRouteBuilder endpoints) =>
        MapWorkspaceGroup(endpoints, new WorkspaceAccessNeeded(AdminsOnly: true), "The Workspace the signed-in User is an Admin of.");

    public static RouteGroupBuilder MapUsersOfTheWorkspaceOnly(this IEndpointRouteBuilder endpoints) =>
        MapWorkspaceGroup(endpoints, new WorkspaceAccessNeeded(AdminsOnly: false), "A Workspace the signed-in User belongs to.");

    public static IApplicationBuilder UseWorkspaceAccess(this IApplicationBuilder app) =>
        app.Use(async (context, next) =>
        {
            if (context.GetEndpoint()?.Metadata.GetMetadata<WorkspaceAccessNeeded>() is not { } needed)
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

            var user = await context.RequestServices.GetRequiredService<IFindSignedInWorkspaceUser>().FindAsync(context);
            var refusal = user is null ? OnlyUsersOfTheWorkspace
                : needed.AdminsOnly && !user.IsAdmin ? OnlyAdmins
                : null;
            if (refusal is not null)
            {
                await Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: refusal).ExecuteAsync(context);
                return;
            }

            context.Features.Set(user);
            if (user!.IsAdmin)
            {
                context.Features.Set(new SignedInAdmin(user.UserId, user.WorkspaceId));
            }

            await next(context);
        });

    private static RouteGroupBuilder MapWorkspaceGroup(IEndpointRouteBuilder endpoints, WorkspaceAccessNeeded needed, string described) =>
        endpoints.MapGroup($"/api/workspaces/{{{WorkspaceRouteValue}:guid}}")
            .WithMetadata(needed)
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
                    Description = described,
                    Schema = new OpenApiSchema { Type = JsonSchemaType.String, Format = "uuid" },
                });
                return Task.CompletedTask;
            });

    private sealed record WorkspaceAccessNeeded(bool AdminsOnly);
}
