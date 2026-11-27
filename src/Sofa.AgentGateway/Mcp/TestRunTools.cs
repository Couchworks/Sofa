using System.ComponentModel;
using ModelContextProtocol.Server;
using Sofa.AgentGateway.Runs;
using Sofa.Contracts.Execution;
using Wolverine;

namespace Sofa.AgentGateway.Mcp;

[McpServerToolType]
public sealed class TestRunTools(IHttpContextAccessor http, IMessageBus bus)
{
    public static readonly TimeSpan ExecutionAnswersWithin = TimeSpan.FromSeconds(30);
    public const int LongestSummary = 1000;

    [McpServerTool(Name = "end_test_run", Destructive = false, Idempotent = false)]
    [Description("Ends this Run with a one-line summary of what you did. Call it once, last, then stop.")]
    public async Task<string> EndTestRun(
        [Description("One line: what you did.")] string summary, CancellationToken cancellation)
    {
        var holder = RunTokenEndpoints.HolderOf(http.HttpContext!.User);
        var trimmed = summary.Trim();
        var answer = await bus.InvokeAsync<RunAnswer>(
            new EndTestRun(holder.RunId, trimmed.Length > LongestSummary ? trimmed[..LongestSummary] : trimmed),
            cancellation, ExecutionAnswersWithin);
        return answer.Refusal is null ? "The Run has ended. Stop now." : $"The Run wasn't ended: {answer.Problem}";
    }
}
