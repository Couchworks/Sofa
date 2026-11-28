using Microsoft.AspNetCore.Http.HttpResults;
using Sofa.AppGateway.Access;
using Sofa.AppGateway.Identity;
using Sofa.Contracts.Flow;
using Wolverine;

namespace Sofa.AppGateway.Flow;

public static class WorkItemEndpoints
{
    public static RouteGroupBuilder MapWorkItems(this RouteGroupBuilder workspace)
    {
        workspace.MapGet("/projects/{key}/work-items", WorkItemsOf)
            .WithName("GetWorkItems")
            .WithSummary("The Project's Work Items, newest first, each with its Request and its delegating User.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        workspace.MapGet("/projects/{key}/work-items/{workItemKey}", GetWorkItem)
            .WithName("GetWorkItem")
            .WithSummary("A Work Item: where it is in the Flow and what it waits for, its Track, Runs, Phase ends, Questions and pull request.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        workspace.MapPost("/projects/{key}/work-items/{workItemKey}/phases/{phase}/approval", ApprovePhase)
            .WithName("ApprovePhase")
            .WithSummary("Approves the Phase waiting at its Approve Gate, so the Work Item goes on, or marks a Phase left Unverified as looked at.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        workspace.MapPost("/projects/{key}/work-items/{workItemKey}/phases/{phase}/sending-back", SendPhaseBack)
            .WithName("SendPhaseBack")
            .WithSummary("Sends the Phase waiting at its Approve Gate back with a comment: a new Run of it reads the comment.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        workspace.MapPut("/projects/{key}/work-items/{workItemKey}/track", ChangeTrack)
            .WithName("ChangeTrack")
            .WithSummary("Puts the Work Item on another Track; it applies to the Phases still ahead of it, and Triage no longer picks one.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        workspace.MapPost("/projects/{key}/work-items/{workItemKey}/questions/{questionId:guid}/answer", AnswerQuestion)
            .WithName("AnswerQuestion")
            .WithSummary("Answers a Question a Run asked; a new Run of its Phase reads the answer.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return workspace;
    }

    private static async Task<Results<Ok<WorkItemDetails>, ProblemHttpResult>> GetWorkItem(
        string key, string workItemKey, SignedInWorkspaceUser user, FlowClient flow, CancellationToken cancellation) =>
        await flow.WorkItemAsync(user.WorkspaceId, key, workItemKey, cancellation) is { } workItem
            ? TypedResults.Ok(workItem)
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "The Workspace has no such Work Item.");

    private static Task<Results<Ok<WorkItemDetails>, ProblemHttpResult>> ApprovePhase(
        string key, string workItemKey, PhaseName phase, SignedInWorkspaceUser user, FlowClient flow, IMessageBus bus, CancellationToken cancellation) =>
        DecideAsync(new ApprovePhase(user.WorkspaceId, key, workItemKey, phase, user.UserId), user, key, workItemKey, flow, bus, cancellation);

    private static Task<Results<Ok<WorkItemDetails>, ProblemHttpResult>> SendPhaseBack(
        string key,
        string workItemKey,
        PhaseName phase,
        SendingBack sendingBack,
        SignedInWorkspaceUser user,
        FlowClient flow,
        IMessageBus bus,
        CancellationToken cancellation) =>
        DecideAsync(new SendPhaseBack(user.WorkspaceId, key, workItemKey, phase, user.UserId, sendingBack.Comment ?? ""),
            user, key, workItemKey, flow, bus, cancellation);

    private static Task<Results<Ok<WorkItemDetails>, ProblemHttpResult>> ChangeTrack(
        string key,
        string workItemKey,
        TrackChange change,
        SignedInWorkspaceUser user,
        FlowClient flow,
        IMessageBus bus,
        CancellationToken cancellation) =>
        DecideAsync(new ChangeTrack(user.WorkspaceId, key, workItemKey, change.Track, user.UserId), user, key, workItemKey, flow, bus, cancellation);

    private static Task<Results<Ok<WorkItemDetails>, ProblemHttpResult>> AnswerQuestion(
        string key,
        string workItemKey,
        Guid questionId,
        AnswerToQuestion answer,
        SignedInWorkspaceUser user,
        FlowClient flow,
        IMessageBus bus,
        CancellationToken cancellation) =>
        DecideAsync(new AnswerQuestion(user.WorkspaceId, key, workItemKey, questionId, user.UserId, answer.Answer ?? ""),
            user, key, workItemKey, flow, bus, cancellation);

    private static async Task<Results<Ok<WorkItemDetails>, ProblemHttpResult>> DecideAsync(
        object command, SignedInWorkspaceUser user, string key, string workItemKey, FlowClient flow, IMessageBus bus, CancellationToken cancellation)
    {
        var answer = await bus.InvokeAsync<FlowAnswer>(command, cancellation, ProjectEndpoints.FlowAnswersWithin);
        if (answer.Refusal is { } refusal)
        {
            return TypedResults.Problem(
                statusCode: refusal == FlowRefusal.NotFound ? StatusCodes.Status404NotFound : StatusCodes.Status400BadRequest,
                title: answer.Problem);
        }

        return TypedResults.Ok((await flow.WorkItemAsync(user.WorkspaceId, key, workItemKey, cancellation))!);
    }

    private static async Task<Results<Ok<List<WorkItemOfProject>>, ProblemHttpResult>> WorkItemsOf(
        string key, SignedInWorkspaceUser user, FlowClient flow, IdentityUsers identity, CancellationToken cancellation)
    {
        if (await flow.WorkItemsOfAsync(user.WorkspaceId, key, cancellation) is not { } workItems)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "The Workspace has no such Project.");
        }

        var users = workItems.Any(w => w.DelegatingUserId is not null)
            ? (await identity.UsersOfAsync(user.WorkspaceId, cancellation)).ToDictionary(u => u.UserId, u => u.Name)
            : [];
        return TypedResults.Ok(workItems
            .Select(w => new WorkItemOfProject(
                w.Key,
                w.Title,
                w.RequestId,
                w.Request,
                w.DelegatingUserId is { } delegating
                    ? new DelegatingUser(delegating, users.GetValueOrDefault(delegating, "a User no longer in the Workspace"))
                    : null,
                w.OpenedAt,
                w.State,
                w.Phase,
                w.Track))
            .ToList());
    }
}

public sealed record WorkItemOfProject(
    string Key,
    string Title,
    Guid RequestId,
    RequestRef Request,
    DelegatingUser? DelegatedBy,
    DateTimeOffset OpenedAt,
    WorkItemState State,
    PhaseName? Phase,
    Track? Track);

public sealed record SendingBack(string? Comment);

public sealed record TrackChange(Track Track);

public sealed record AnswerToQuestion(string? Answer);

public sealed record DelegatingUser(Guid UserId, string Name);
