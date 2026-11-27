using System.ComponentModel;
using ModelContextProtocol.Server;
using Sofa.AgentGateway.WorkItems;
using Sofa.Contracts.Execution;
using Wolverine;

namespace Sofa.AgentGateway.Mcp;

[McpServerToolType]
public sealed class RunTools(
    IHttpContextAccessor http, IMessageBus bus, FlowWorkItems flow, IntakeRequests intake, RecordsContributions records)
{
    public static readonly TimeSpan ExecutionEndsTheRunWithin = TimeSpan.FromSeconds(30);
    public static readonly TimeSpan ExecutionKeepsTheContributionWithin = TimeSpan.FromMinutes(3);

    private RunDetails Run => OnlyWhileTheRunRuns.RunServedTo(http.HttpContext!);

    [McpServerTool(Name = "read_work_item", ReadOnly = true, Idempotent = true)]
    [Description("Reads this Run's Work Item: its Request as it is now on its issue, its Questions with their answers, and the Contributions of earlier Runs. Call it first.")]
    public async Task<string> ReadWorkItem(CancellationToken cancellation)
    {
        var run = Run;
        if (await flow.WorkItemAsync(run.WorkspaceId, run.WorkItemId, cancellation) is not { } workItem)
        {
            return WorkItemText.NoWorkItem(run);
        }

        var request = intake.RequestNowAsync(run.WorkspaceId, workItem.RequestId, cancellation);
        var contributions = records.OnAsync(run.WorkspaceId, workItem.Id, cancellation);
        return WorkItemText.Of(new WorkItemRead(run, workItem, await request, await contributions));
    }

    [McpServerTool(Name = "ask", Destructive = false, Idempotent = false)]
    [Description("Ends this Run with a Question for a User, when you can't go on without their answer. It is posted on the Request's issue; a new Run reads the answer. Stop once it is asked.")]
    public async Task<string> Ask(
        [Description("The Question, in Markdown: one thing to decide, with what you'd choose and why.")] string question,
        CancellationToken cancellation)
    {
        var run = Run;
        var answer = await bus.InvokeAsync<RunAnswer>(
            new EndRunWithQuestion(run.Id, run.AgentId, Guid.CreateVersion7(), question), cancellation, ExecutionEndsTheRunWithin);
        return answer.Refusal is null
            ? "Your Question is asked and the Run has ended. Stop now."
            : $"The Question wasn't asked: {answer.Problem}";
    }

    [McpServerTool(Name = "finish", Destructive = false, Idempotent = false)]
    [Description("Ends this Run with one of its Phase's Outcomes, named in the Brief, and your Contribution in Markdown, with files from your working directory attached, such as screenshots. Call it once, last, then stop.")]
    public async Task<string> Finish(
        [Description("One of this Phase's Outcomes, as the Brief names them.")] string outcome,
        [Description("Your Contribution, in Markdown: what you made, and the reasoning later Runs need.")] string contribution,
        [Description("Paths of files in your working directory to attach, such as screenshots; none if there are none.")] string[]? attachments,
        CancellationToken cancellation)
    {
        var run = Run;
        var answer = await bus.InvokeAsync<RunAnswer>(
            new FinishRun(run.Id, run.AgentId, outcome, contribution, attachments ?? []), cancellation, ExecutionKeepsTheContributionWithin);
        return answer.Refusal is null
            ? "Your Contribution is kept and the Run has finished. Stop now."
            : $"The Run wasn't finished: {answer.Problem}";
    }
}
