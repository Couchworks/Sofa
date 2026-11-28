using Microsoft.EntityFrameworkCore;
using Sofa.Contracts.Execution;
using Sofa.Contracts.Flow;
using Sofa.Flow.Data;
using Sofa.Flow.Projects;
using Wolverine;

namespace Sofa.Flow.Runs;

public static class TestRunEndpoints
{
    public const string DefaultTask = "Write a haiku about sofas.";

    public static IEndpointRouteBuilder MapTestRuns(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/diagnostics/test-runs", StartTestRun);
        return endpoints;
    }

    private static async Task<IResult> StartTestRun(TestRunToStart toStart, FlowDb db, IMessageBus bus, CancellationToken cancellation)
    {
        var key = ProjectRules.NormalizeKey(toStart.ProjectKey);
        var project = await db.Projects.AsNoTracking()
            .SingleOrDefaultAsync(p => p.WorkspaceId == toStart.WorkspaceId && p.Key == key, cancellation);
        if (project is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "The Workspace has no such Project.");
        }

        var phase = await db.Phases.AsNoTracking()
            .SingleOrDefaultAsync(p => p.ProjectId == project.Id && p.Name == toStart.Phase, cancellation);
        if (phase is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: $"Project {key}'s Flow has no {toStart.Phase} Phase.");
        }

        var workItem = toStart.WorkItemKey is { } workItemKey
            ? await db.WorkItems.AsNoTracking()
                .SingleOrDefaultAsync(w => w.ProjectId == project.Id && w.Key == workItemKey.Trim().ToUpperInvariant(), cancellation)
            : null;
        if (toStart.WorkItemKey is not null && workItem is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: $"Project {key} has no Work Item {toStart.WorkItemKey}.");
        }

        var agent = await db.Agents.AsNoTracking().SingleAsync(a => a.ProjectId == project.Id && a.Id == phase.CastAgentId, cancellation);
        var run = new StartRun(
            Guid.CreateVersion7(),
            project.WorkspaceId,
            project.Id,
            WorkItemId: workItem?.Id ?? Guid.CreateVersion7(),
            phase.Name,
            agent.Id,
            agent.Vendor,
            agent.Model,
            phase.Sandbox,
            toStart.DelegatingUserId,
            toStart.Priority ?? Priority.None,
            TestBrief(agent, project.Key, phase.Name, workItem?.Key, toStart.Task ?? DefaultTask),
            toStart.RetryLimit ?? RunLimits.DefaultRetryLimit);
        await bus.SendAsync(run);
        return Results.Accepted($"/runs/{run.RunId}",
            new TestRunStarted(run.RunId, agent.Id, agent.Model, phase.Sandbox, workItem?.Id, workItem?.Key));
    }

    public static string TestBrief(Agent agent, string projectKey, PhaseName phase, string? workItemKey, string task) =>
        $"""
        You are the {agent.Role} of Project {projectKey}, on a test Run of its {phase} Phase{(workItemKey is null ? ", on no Work Item" : $" on Work Item {workItemKey}")}.

        {task.Trim()}

        Read your Work Item with Sofa's read_work_item tool first. When you are done, end the Run with Sofa's finish tool, naming one of the Phase's Outcomes and giving what you did as your Contribution. If you can't go on without a User's answer, end it with Sofa's ask tool instead. Then stop.
        """;
}

public sealed record TestRunToStart(
    Guid WorkspaceId,
    string ProjectKey,
    PhaseName Phase,
    Guid DelegatingUserId,
    Priority? Priority,
    string? Task,
    int? RetryLimit,
    string? WorkItemKey);

public sealed record TestRunStarted(Guid RunId, Guid AgentId, string Model, Sandbox Sandbox, Guid? WorkItemId, string? WorkItemKey);
