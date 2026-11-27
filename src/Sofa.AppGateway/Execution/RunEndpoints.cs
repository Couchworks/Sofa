using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using Sofa.AppGateway.Access;
using Sofa.AppGateway.Records;
using Sofa.Contracts.Execution;
using Sofa.Contracts.Records;
using Wolverine;

namespace Sofa.AppGateway.Execution;

public static class RunEndpoints
{
    public static RouteGroupBuilder MapRuns(this RouteGroupBuilder workspace)
    {
        workspace.MapGet("/runs", (SignedInWorkspaceUser user, ExecutionClient execution, CancellationToken cancellation) =>
                execution.RunsOf(user.WorkspaceId, cancellation))
            .WithName("GetRuns")
            .WithSummary("The Workspace's latest Runs, newest first.");

        workspace.MapGet("/runs/{runId:guid}", GetRun)
            .WithName("GetRun")
            .WithSummary("A Run: its state and why, its Runner, and the Runs it retries or is retried by.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        workspace.MapGet("/runs/{runId:guid}/transcript", GetTranscript)
            .WithName("GetTranscript")
            .WithSummary("The Run's Transcript: its messages and tool calls in order, with known secrets scrubbed; `after` skips the lines already read.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        workspace.MapPost("/runs/{runId:guid}/retry", RetryRun)
            .WithName("RetryRun")
            .WithSummary("Retries a Run that failed, was lost or was refused, as a new Run.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        workspace.MapPost("/runs/{runId:guid}/stop", StopRun)
            .WithName("StopRun")
            .WithSummary("Stops a Run that is waiting or running.")
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict);

        return workspace;
    }

    private static async Task<Results<Ok<RunDetails>, ProblemHttpResult>> GetRun(
        Guid runId, SignedInWorkspaceUser user, ExecutionClient execution, CancellationToken cancellation) =>
        await execution.RunAsync(user.WorkspaceId, runId, cancellation) is { } run ? TypedResults.Ok(run) : NoSuchRun;

    private static async Task<Results<Ok<Transcript>, ProblemHttpResult>> GetTranscript(
        Guid runId,
        [FromQuery] int? after,
        SignedInWorkspaceUser user,
        ExecutionClient execution,
        RecordsClient records,
        CancellationToken cancellation) =>
        await execution.RunAsync(user.WorkspaceId, runId, cancellation) is null
            ? NoSuchRun
            : TypedResults.Ok(await records.TranscriptOfAsync(user.WorkspaceId, runId, after, cancellation));

    private static async Task<Results<Ok<RunDetails>, ProblemHttpResult>> RetryRun(
        Guid runId, SignedInWorkspaceUser user, ExecutionClient execution, IMessageBus bus, CancellationToken cancellation) =>
        await AnsweredAsync(new RetryRun(user.WorkspaceId, runId, user.UserId), user, execution, bus, cancellation);

    private static async Task<Results<Ok<RunDetails>, ProblemHttpResult>> StopRun(
        Guid runId, SignedInWorkspaceUser user, ExecutionClient execution, IMessageBus bus, CancellationToken cancellation) =>
        await AnsweredAsync(new StopRun(user.WorkspaceId, runId, user.UserId), user, execution, bus, cancellation);

    private static async Task<Results<Ok<RunDetails>, ProblemHttpResult>> AnsweredAsync(
        object command, SignedInWorkspaceUser user, ExecutionClient execution, IMessageBus bus, CancellationToken cancellation)
    {
        var answer = await bus.InvokeAsync<RunAnswer>(command, cancellation, RunnerEndpoints.ExecutionAnswersWithin);
        return answer switch
        {
            { Refusal: RunRefusal.NotFound } => NoSuchRun,
            { Refusal: not null } => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: answer.Problem),
            _ => TypedResults.Ok((await execution.RunAsync(user.WorkspaceId, answer.RunId!.Value, cancellation))!),
        };
    }

    private static ProblemHttpResult NoSuchRun =>
        TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "The Workspace has no such Run.");
}
