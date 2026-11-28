using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using Sofa.Contracts.AgentGateway;
using Sofa.Contracts.Flow;

namespace Sofa.AgentGateway.Mcp;

public static class ToolsOfTheSandbox
{
    public static bool Allows(Sandbox sandbox, string? toolName) =>
        SofaTools.AllowedIn(sandbox).Any(tool => tool.Name == toolName);

    public static IMcpServerBuilder OfferOnlyTheToolsOfEachRunsSandbox(this IMcpServerBuilder server) =>
        server.WithRequestFilters(filters =>
        {
            filters.AddListToolsFilter(next => async (context, cancellation) =>
            {
                var listed = await next(context, cancellation);
                var sandbox = SandboxOf(context.Services);
                listed.Tools = listed.Tools.Where(tool => Allows(sandbox, tool.Name)).ToList();
                return listed;
            });
            filters.AddCallToolFilter(next => async (context, cancellation) =>
            {
                var sandbox = SandboxOf(context.Services);
                return Allows(sandbox, context.Params?.Name)
                    ? await next(context, cancellation)
                    : new CallToolResult
                    {
                        IsError = true,
                        Content = [new TextContentBlock { Text = $"The {sandbox} Sandbox of this Run's Phase doesn't allow {context.Params?.Name}." }],
                    };
            });
        });

    private static Sandbox SandboxOf(IServiceProvider? services) =>
        OnlyWhileTheRunRuns.RunServedTo(services!.GetRequiredService<IHttpContextAccessor>().HttpContext!).Sandbox;
}
