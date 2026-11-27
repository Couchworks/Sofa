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

        var agent = await db.Agents.AsNoTracking().SingleAsync(a => a.ProjectId == project.Id && a.Id == phase.CastAgentId, cancellation);
        var run = new StartRun(
            Guid.CreateVersion7(),
            project.WorkspaceId,
            project.Id,
            WorkItemId: Guid.CreateVersion7(),
            phase.Name,
            agent.Id,
            agent.Vendor,
            agent.Model,
            phase.Sandbox,
            toStart.DelegatingUserId,
            toStart.Priority ?? Priority.None,
            TestBrief(agent, project.Key, phase.Name, toStart.Task ?? DefaultTask),
            toStart.RetryLimit ?? RunLimits.DefaultRetryLimit);
        await bus.SendAsync(run);
        return Results.Accepted($"/runs/{run.RunId}", new TestRunStarted(run.RunId, agent.Id, agent.Model, phase.Sandbox));
    }

    public static string TestBrief(Agent agent, string projectKey, PhaseName phase, string task) =>
        $"""
        You are the {agent.Role} of Project {projectKey}, on a test Run of its {phase} Phase: there is no Work Item to read yet.

        {task.Trim()}

        When you are done, end the Run with Sofa's end_test_run tool, giving a one-line summary, and stop.
        """;
}

public sealed record TestRunToStart(
    Guid WorkspaceId, string ProjectKey, PhaseName Phase, Guid DelegatingUserId, Priority? Priority, string? Task, int? RetryLimit);

public sealed record TestRunStarted(Guid RunId, Guid AgentId, string Model, Sandbox Sandbox);
