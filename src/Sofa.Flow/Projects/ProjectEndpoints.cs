using Microsoft.EntityFrameworkCore;
using Sofa.Contracts.Flow;
using Sofa.Flow.Data;

namespace Sofa.Flow.Projects;

public static class ProjectEndpoints
{
    public static IEndpointRouteBuilder MapProjects(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/workspaces/{workspaceId:guid}/projects", ProjectsOf);
        endpoints.MapGet("/workspaces/{workspaceId:guid}/projects/{key}", async (
            Guid workspaceId, string key, FlowDb db, CancellationToken cancellation) =>
            await DetailsOfAsync(db, workspaceId, key, cancellation) is { } project ? Results.Ok(project) : Results.NotFound());
        endpoints.MapGet("/workspaces/{workspaceId:guid}/projects-by-id/{projectId:guid}/binding", async (
            Guid workspaceId, Guid projectId, FlowDb db, CancellationToken cancellation) =>
            await db.Projects.AsNoTracking().SingleOrDefaultAsync(p => p.Id == projectId && p.WorkspaceId == workspaceId, cancellation) is { } project
                ? Results.Ok(project.Bound)
                : Results.NotFound());
        return endpoints;
    }

    private static async Task<List<ProjectSummary>> ProjectsOf(Guid workspaceId, FlowDb db, CancellationToken cancellation) =>
        await db.Projects
            .Where(p => p.WorkspaceId == workspaceId)
            .OrderBy(p => p.Key)
            .Select(p => new ProjectSummary(p.Id, p.Key, p.Name, p.RequestRepository, p.RequestLabel))
            .ToListAsync(cancellation);

    public static async Task<ProjectDetails?> DetailsOfAsync(FlowDb db, Guid workspaceId, string key, CancellationToken cancellation)
    {
        var normalized = ProjectRules.NormalizeKey(key);
        var project = await db.Projects.AsNoTracking()
            .SingleOrDefaultAsync(p => p.WorkspaceId == workspaceId && p.Key == normalized, cancellation);
        if (project is null)
        {
            return null;
        }

        var phases = await db.Phases.AsNoTracking().Where(p => p.ProjectId == project.Id).OrderBy(p => p.Position)
            .Select(p => new PhaseDetails(p.Name, p.Position, p.Pattern, p.CastAgentId, p.Gate, p.Sandbox, p.Instructions))
            .ToListAsync(cancellation);
        var agents = (await db.Agents.AsNoTracking().Where(a => a.ProjectId == project.Id).ToListAsync(cancellation))
            .OrderBy(a => a.Role).ThenBy(a => a.Name)
            .Select(a => new AgentDetails(a.Id, a.Name, a.Role, a.Instructions, a.Vendor, a.Model, a.Registration, a.RegistrationRefusedBecause))
            .ToList();

        return new ProjectDetails(project.Id, project.Key, project.Name, project.Requests, project.Code, phases, agents);
    }
}
