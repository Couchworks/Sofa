using Microsoft.AspNetCore.Http.HttpResults;
using Sofa.AppGateway.Access;
using Sofa.AppGateway.Identity;
using Sofa.Contracts.Flow;
using Wolverine;

namespace Sofa.AppGateway.Flow;

public static class ProjectEndpoints
{
    public static readonly TimeSpan FlowAnswersWithin = TimeSpan.FromSeconds(30);

    public static RouteGroupBuilder MapProjectsForAdmins(this RouteGroupBuilder workspace)
    {
        workspace.MapPost("/projects", CreateProject)
            .WithName("CreateProject")
            .WithSummary("Creates a Project bound to its Providers through a Connection, with milestone 1's Flow and default Agents.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status409Conflict);

        workspace.MapPut("/projects/{key}/binding", BindProject)
            .WithName("BindProject")
            .WithSummary("Binds the Project to its Request Provider and Code Provider through a Connection.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return workspace;
    }

    public static RouteGroupBuilder MapProjects(this RouteGroupBuilder workspace)
    {
        workspace.MapGet("/projects", (SignedInWorkspaceUser user, FlowClient flow, CancellationToken cancellation) =>
                flow.ProjectsOf(user.WorkspaceId, cancellation))
            .WithName("GetProjects")
            .WithSummary("The Workspace's Projects.");

        workspace.MapGet("/projects/{key}", GetProject)
            .WithName("GetProject")
            .WithSummary("A Project with its bindings, its Flow and its Agents.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        workspace.MapPut("/projects/{key}/agents/{agentId:guid}", ConfigureAgent)
            .WithName("ConfigureAgent")
            .WithSummary("Changes an Agent's role, instructions, vendor and model.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        workspace.MapPost("/projects/{key}/agents/{agentId:guid}/registration", RegisterAgentAgain)
            .WithName("RegisterAgentAgain")
            .WithSummary("Asks Identity again to register an Agent it refused or hasn't answered for.")
            .ProducesProblem(StatusCodes.Status404NotFound);

        workspace.MapPut("/projects/{key}/phases/{phase}", ConfigurePhase)
            .WithName("ConfigurePhase")
            .WithSummary("Changes a Phase's cast, Gate and Sandbox, and its instructions when given.")
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status404NotFound);

        return workspace;
    }

    private static async Task<Results<Ok<ProjectDetails>, ProblemHttpResult>> GetProject(
        string key, SignedInWorkspaceUser user, FlowClient flow, CancellationToken cancellation) =>
        await flow.ProjectAsync(user.WorkspaceId, key, cancellation) is { } project
            ? TypedResults.Ok(project)
            : TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: "The Workspace has no such Project.");

    private static async Task<Results<Created<ProjectDetails>, ProblemHttpResult>> CreateProject(
        NewProject project,
        SignedInAdmin admin,
        IdentityUsers identity,
        FlowClient flow,
        IMessageBus bus,
        CancellationToken cancellation)
    {
        if (await identity.TheWorkspacesTeamAsync(admin.WorkspaceId, cancellation) is not { } team)
        {
            return TypedResults.Problem(statusCode: StatusCodes.Status409Conflict,
                title: "The Workspace has no Team to own the Project's Agents.");
        }

        var command = new CreateProject(Guid.CreateVersion7(), admin.WorkspaceId, project.Key ?? "", project.Name ?? "", team.Id,
            ProjectBinding.RequestsOf(project.Binding), ProjectBinding.CodeOf(project.Binding));
        var answer = await bus.InvokeAsync<FlowAnswer>(command, cancellation, FlowAnswersWithin);
        if (Refused(answer) is { } refused)
        {
            return refused;
        }

        var created = await flow.ProjectAsync(admin.WorkspaceId, ProjectKey(project.Key), cancellation);
        return TypedResults.Created($"/api/workspaces/{admin.WorkspaceId}/projects/{created!.Key}", created);
    }

    private static async Task<Results<Ok<ProjectDetails>, ProblemHttpResult>> BindProject(
        string key, ProjectBinding binding, SignedInAdmin admin, FlowClient flow, IMessageBus bus, CancellationToken cancellation) =>
        await ChangeAsync(new BindProject(admin.WorkspaceId, key, ProjectBinding.RequestsOf(binding), ProjectBinding.CodeOf(binding)),
            admin.WorkspaceId, key, flow, bus, cancellation);

    private static async Task<Results<Ok<ProjectDetails>, ProblemHttpResult>> ConfigureAgent(
        string key,
        Guid agentId,
        AgentConfiguration agent,
        SignedInWorkspaceUser user,
        FlowClient flow,
        IMessageBus bus,
        CancellationToken cancellation) =>
        await ChangeAsync(
            new ConfigureAgent(user.WorkspaceId, key, agentId, agent.Role, agent.Instructions ?? "", agent.Vendor, agent.Model ?? ""),
            user.WorkspaceId, key, flow, bus, cancellation);

    private static async Task<Results<Ok<ProjectDetails>, ProblemHttpResult>> RegisterAgentAgain(
        string key, Guid agentId, SignedInWorkspaceUser user, FlowClient flow, IMessageBus bus, CancellationToken cancellation) =>
        await ChangeAsync(new RegisterAgentAgain(user.WorkspaceId, key, agentId), user.WorkspaceId, key, flow, bus, cancellation);

    private static async Task<Results<Ok<ProjectDetails>, ProblemHttpResult>> ConfigurePhase(
        string key,
        PhaseName phase,
        PhaseConfiguration configuration,
        SignedInWorkspaceUser user,
        FlowClient flow,
        IMessageBus bus,
        CancellationToken cancellation) =>
        await ChangeAsync(
            new ConfigurePhase(user.WorkspaceId, key, phase, configuration.CastAgentId, configuration.Gate, configuration.Sandbox,
                configuration.Instructions),
            user.WorkspaceId, key, flow, bus, cancellation);

    private static async Task<Results<Ok<ProjectDetails>, ProblemHttpResult>> ChangeAsync(
        object command, Guid workspaceId, string key, FlowClient flow, IMessageBus bus, CancellationToken cancellation)
    {
        var answer = await bus.InvokeAsync<FlowAnswer>(command, cancellation, FlowAnswersWithin);
        if (Refused(answer) is { } refused)
        {
            return refused;
        }

        return TypedResults.Ok((await flow.ProjectAsync(workspaceId, key, cancellation))!);
    }

    private static ProblemHttpResult? Refused(FlowAnswer answer) =>
        answer.Refusal switch
        {
            null => null,
            FlowRefusal.NotFound => TypedResults.Problem(statusCode: StatusCodes.Status404NotFound, title: answer.Problem),
            FlowRefusal.Taken => TypedResults.Problem(statusCode: StatusCodes.Status409Conflict, title: answer.Problem),
            _ => TypedResults.Problem(statusCode: StatusCodes.Status400BadRequest, title: answer.Problem),
        };

    private static string ProjectKey(string? key) => (key ?? "").Trim().ToUpperInvariant();
}

public sealed record NewProject(string? Key, string? Name, ProjectBinding? Binding);

public sealed record ProjectBinding(
    Guid ConnectionId, string? RequestRepository, string? Label, IReadOnlyList<string>? CodeRepositories, bool? KeepsIssueState = null)
{
    public static RequestProviderBinding RequestsOf(ProjectBinding? binding) =>
        new(binding?.ConnectionId ?? Guid.Empty, binding?.RequestRepository ?? "", binding?.Label ?? "", binding?.KeepsIssueState ?? false);

    public static CodeProviderBinding CodeOf(ProjectBinding? binding) =>
        new(binding?.ConnectionId ?? Guid.Empty, binding?.CodeRepositories ?? []);
}

public sealed record AgentConfiguration(AgentRole Role, string? Instructions, Vendor Vendor, string? Model);

public sealed record PhaseConfiguration(Guid CastAgentId, Gate Gate, Sandbox Sandbox, string? Instructions = null);
