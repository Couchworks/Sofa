namespace Sofa.AppGateway.Live;

public static class LiveEndpoints
{
    private const string TokenInTheQuery = "access_token";

    public static RouteGroupBuilder MapLiveUpdates(this RouteGroupBuilder workspace)
    {
        workspace.MapHub<LiveHub>(LiveHub.Path).ExcludeFromDescription();
        return workspace;
    }

    public static IApplicationBuilder UseTheTokenALiveConnectionSendsInItsQuery(this IApplicationBuilder app) =>
        app.Use((context, next) =>
        {
            if (IsALiveConnection(context.Request.Path)
                && !context.Request.Headers.ContainsKey("Authorization")
                && context.Request.Query[TokenInTheQuery] is [{ Length: > 0 } token])
            {
                context.Request.Headers.Authorization = $"Bearer {token}";
            }

            return next(context);
        });

    public static bool IsALiveConnection(PathString path)
    {
        var segments = (path.Value ?? "").Split('/', StringSplitOptions.RemoveEmptyEntries);
        return segments is ["api", "workspaces", var workspaceId, "live", ..] && segments.Length <= 5 && Guid.TryParse(workspaceId, out _);
    }
}
