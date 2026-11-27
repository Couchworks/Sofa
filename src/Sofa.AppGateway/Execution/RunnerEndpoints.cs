using Microsoft.AspNetCore.Http.HttpResults;
using Sofa.AppGateway.Access;
using Sofa.AppGateway.Connections;
using Sofa.AppGateway.Identity;
using Sofa.Contracts.Connections;
using Sofa.Contracts.Execution;
using Sofa.Contracts.Flow;
using Wolverine;

namespace Sofa.AppGateway.Execution;

public static class RunnerEndpoints
{
    public static readonly TimeSpan ExecutionAnswersWithin = TimeSpan.FromSeconds(30);

    public static RouteGroupBuilder MapRunners(this RouteGroupBuilder workspace)
    {
        workspace.MapGet("/runners", (SignedInWorkspaceUser user, ExecutionClient execution, CancellationToken cancellation) =>
                execution.RunnersSeenBy(user, cancellation))
            .WithName("GetRunners")
            .WithSummary("The signed-in User's personal Runners and their Teams' Runners; an Admin sees every team Runner.");

        workspace.MapPost("/runners/subscription", AddSubscriptionRunner)
            .WithName("AddSubscriptionRunner")
            .WithSummary("Adds a personal Runner on the signed-in User's own subscription, signed in later on the box with `sofa runner login`.")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        workspace.MapPost("/runners/api-key", AddApiKeyRunner)
            .WithName("AddApiKeyRunner")
            .WithSummary("Adds a Runner on an API key, personal or, for an Admin, the Team's. The key is never returned.")
            .ProducesProblem(StatusCodes.Status400BadRequest);

        workspace.MapPut("/runners/{runnerId:guid}/runs-at-once", LimitRunsAtOnce)
            .WithName("LimitRunsAtOnce")
            .WithSummary("Changes how many Runs the Runner runs at once.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return workspace;
    }

    private static async Task<Results<Created<RunnerDetails>, ProblemHttpResult>> AddSubscriptionRunner(
        NewSubscriptionRunner runner, SignedInWorkspaceUser user, ExecutionClient execution, IMessageBus bus, CancellationToken cancellation) =>
        await CreateAsync(
            new CreateRunner(Guid.CreateVersion7(), user.WorkspaceId, runner.Name ?? "", RunnerKind.Subscription, Vendor.Anthropic,
                RunnerOwner.User(user.UserId), runner.RunsAtOnce ?? RunnerLimits.DefaultRunsAtOnce, ApiKeyId: null),
            user, execution, bus, cancellation);

    private static async Task<Results<Created<RunnerDetails>, ProblemHttpResult>> AddApiKeyRunner(
        NewApiKeyRunner runner,
        SignedInWorkspaceUser user,
        IdentityUsers identity,
        ConnectionsClient connections,
        ExecutionClient execution,
        IMessageBus bus,
        CancellationToken cancellation)
    {
        RunnerOwner owner;
        if (runner.ForTheTeam)
        {
            if (!user.IsAdmin)
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: "Only an Admin adds a team Runner.");
            }

            if (await identity.TheWorkspacesTeamAsync(user.WorkspaceId, cancellation) is not { } team)
            {
                return TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: "The Workspace has no Team.");
            }

            owner = RunnerOwner.Team(team.Id);
        }
        else
        {
            owner = RunnerOwner.User(user.UserId);
        }

        var saved = await connections.SaveApiKey(user.WorkspaceId, new ApiKeyToSave(Vendor.Anthropic, runner.ApiKey ?? ""), cancellation);
        if (saved.Value is not { } apiKey)
        {
            return TypedResults.Problem(statusCode: saved.Status, title: saved.Problem);
        }

        return await CreateAsync(
            new CreateRunner(Guid.CreateVersion7(), user.WorkspaceId, runner.Name ?? "", RunnerKind.ApiKey, Vendor.Anthropic,
                owner, runner.RunsAtOnce ?? RunnerLimits.DefaultRunsAtOnce, apiKey.ApiKeyId),
            user, execution, bus, cancellation);
    }

    private static async Task<Results<Ok<RunnerDetails>, ProblemHttpResult>> LimitRunsAtOnce(
        Guid runnerId, RunsAtOnceChange change, SignedInWorkspaceUser user, ExecutionClient execution, IMessageBus bus,
        CancellationToken cancellation)
    {
        var answer = await bus.InvokeAsync<RunnerAnswer>(
            new LimitRunsAtOnce(user.WorkspaceId, runnerId, change.RunsAtOnce, user.UserId, user.IsAdmin), cancellation, ExecutionAnswersWithin);
        if (Refused(answer) is { } refused)
        {
            return refused;
        }

        return TypedResults.Ok((await execution.RunnersSeenBy(user, cancellation)).Single(runner => runner.Id == runnerId));
    }

    private static async Task<Results<Created<RunnerDetails>, ProblemHttpResult>> CreateAsync(
        CreateRunner command, SignedInWorkspaceUser user, ExecutionClient execution, IMessageBus bus, CancellationToken cancellation)
    {
        var answer = await bus.InvokeAsync<RunnerAnswer>(command, cancellation, ExecutionAnswersWithin);
        if (Refused(answer) is { } refused)
        {
            return refused;
        }

        var created = (await execution.RunnersSeenBy(user, cancellation)).Single(runner => runner.Id == command.RunnerId);
        return TypedResults.Created((string?)null, created);
    }

    private static ProblemHttpResult? Refused(RunnerAnswer answer) =>
        answer.Refusal switch
        {
            null => null,
            RunnerRefusal.NotFound => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: answer.Problem),
            RunnerRefusal.NotTheirs => TypedResults.Problem(statusCode: StatusCodes.Status403Forbidden, title: answer.Problem),
            _ => TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: answer.Problem),
        };
}

public sealed record NewSubscriptionRunner(string? Name, int? RunsAtOnce);

public sealed record NewApiKeyRunner(string? Name, string? ApiKey, int? RunsAtOnce, bool ForTheTeam);

public sealed record RunsAtOnceChange(int RunsAtOnce);
