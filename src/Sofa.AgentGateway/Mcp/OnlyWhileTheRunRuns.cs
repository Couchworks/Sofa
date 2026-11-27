using Sofa.AgentGateway.Runs;
using Sofa.Contracts.AgentGateway;
using Sofa.Contracts.Execution;

namespace Sofa.AgentGateway.Mcp;

public static class OnlyWhileTheRunRuns
{
    public static IApplicationBuilder ServeRunsOnlyWhileTheyRun(this IApplicationBuilder app) =>
        app.UseWhen(
            http => http.Request.Path.StartsWithSegments(SofaTools.McpPath) && http.User.Identity?.IsAuthenticated == true,
            mcp => mcp.Use(async (http, next) =>
            {
                if (RunTokenEndpoints.TryHolderOf(http.User) is not { } holder)
                {
                    http.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return;
                }

                var run = await http.RequestServices.GetRequiredService<ExecutionRuns>()
                    .RunAsync(holder.WorkspaceId, holder.RunId, http.RequestAborted);
                if (WhyItIsNotServed(run, holder) is { } refused)
                {
                    await Results.Problem(statusCode: StatusCodes.Status403Forbidden, title: refused).ExecuteAsync(http);
                    return;
                }

                await next(http);
            }));

    public static string? WhyItIsNotServed(RunDetails? run, RunTokenHolder holder) =>
        run is null || run.AgentId != holder.AgentId ? "Sofa has no such Run."
        : run.State != RunState.Running ? $"The Run has ended: it is {run.State}, so its token is no longer served."
        : null;
}
