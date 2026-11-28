using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts.Flow;
using Sofa.Contracts.Identity;
using Sofa.Flow.Data;
using Wolverine;

namespace Sofa.Flow.Projects;

public static class ProjectHandler
{
    public static async Task<(FlowAnswer, OutgoingMessages)> Handle(
        CreateProject command,
        FlowDb db,
        ConnectionsRepositories connections,
        TimeProvider time,
        ILogger<CreateProject> logger,
        CancellationToken cancellation)
    {
        var registrations = new OutgoingMessages();
        if (await db.Projects.FindAsync([command.ProjectId], cancellation) is { } existing)
        {
            if (existing.WorkspaceId != command.WorkspaceId)
            {
                return (FlowAnswer.Refused(FlowRefusal.Invalid, "Another Project has that id."), registrations);
            }

            registrations.AddRange(RegistrationsOf(await db.Agents.Where(a => a.ProjectId == existing.Id).ToListAsync(cancellation)));
            registrations.Add(existing.Bound);
            return (FlowAnswer.Done, registrations);
        }

        var key = ProjectRules.NormalizeKey(command.Key);
        if ((ProjectRules.KeyProblem(key)
             ?? ProjectRules.NameProblem(command.Name)
             ?? ProjectRules.BindingShapeProblem(command.Requests, command.Code)) is { } problem)
        {
            return (FlowAnswer.Refused(FlowRefusal.Invalid, problem), registrations);
        }

        if (await KeyIsTakenAsync(db, command.WorkspaceId, key, cancellation))
        {
            return (KeyTaken(key), registrations);
        }

        var (requests, code, refusal) = await AsTheConnectionsNameThemAsync(
            command.WorkspaceId, command.Requests, command.Code, connections, cancellation);
        if (refusal is not null)
        {
            return (refusal, registrations);
        }

        var now = time.GetUtcNow();
        var project = new Project
        {
            Id = command.ProjectId,
            WorkspaceId = command.WorkspaceId,
            Key = key,
            Name = command.Name.Trim(),
            RequestConnectionId = requests!.ConnectionId,
            RequestRepository = requests.Repository,
            RequestLabel = requests.Label,
            KeepsIssueState = requests.KeepsIssueState,
            CodeConnectionId = code!.ConnectionId,
            CodeRepositories = [.. code.Repositories],
            CreatedAt = now,
        };
        var (agents, phases) = DefaultFlow.For(project.Id, project.WorkspaceId, command.AgentsOwnedByTeamId, now);
        db.Projects.Add(project);
        db.Agents.AddRange(agents);
        db.Phases.AddRange(phases);

        try
        {
            await db.SaveChangesAsync(cancellation);
        }
        catch (DbUpdateException taken) when (taken.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return (KeyTaken(key), registrations);
        }

        logger.LogInformation("Created Project {Key} ({ProjectId}) in Workspace {WorkspaceId} with milestone 1's Flow",
            key, project.Id, project.WorkspaceId);
        registrations.AddRange(RegistrationsOf(agents));
        registrations.Add(project.Bound);
        return (FlowAnswer.Done, registrations);
    }

    public static async Task<(FlowAnswer, OutgoingMessages)> Handle(
        BindProject command, FlowDb db, ConnectionsRepositories connections, CancellationToken cancellation)
    {
        var nothingToTell = new OutgoingMessages();
        if (await FindAsync(db, command.WorkspaceId, command.Key, cancellation) is not { } project)
        {
            return (NoSuchProject, nothingToTell);
        }

        if (ProjectRules.BindingShapeProblem(command.Requests, command.Code) is { } problem)
        {
            return (FlowAnswer.Refused(FlowRefusal.Invalid, problem), nothingToTell);
        }

        var (requests, code, refusal) = await AsTheConnectionsNameThemAsync(
            command.WorkspaceId, command.Requests, command.Code, connections, cancellation);
        if (refusal is not null)
        {
            return (refusal, nothingToTell);
        }

        project.BindTo(requests!, code!);
        await db.SaveChangesAsync(cancellation);
        return (FlowAnswer.Done, [project.Bound]);
    }

    public static async Task<FlowAnswer> Handle(ConfigureAgent command, FlowDb db, CancellationToken cancellation)
    {
        if (await FindAsync(db, command.WorkspaceId, command.Key, cancellation) is not { } project)
        {
            return NoSuchProject;
        }

        var agent = await db.Agents.SingleOrDefaultAsync(a => a.ProjectId == project.Id && a.Id == command.AgentId, cancellation);
        if (agent is null)
        {
            return FlowAnswer.Refused(FlowRefusal.NotFound, $"Project {project.Key} has no such Agent.");
        }

        if (ProjectRules.AgentProblem(command.Instructions, command.Model) is { } problem)
        {
            return FlowAnswer.Refused(FlowRefusal.Invalid, problem);
        }

        agent.Role = command.Role;
        agent.Instructions = command.Instructions.Trim();
        agent.Vendor = command.Vendor;
        agent.Model = command.Model;
        await db.SaveChangesAsync(cancellation);
        return FlowAnswer.Done;
    }

    public static async Task<FlowAnswer> Handle(ConfigurePhase command, FlowDb db, CancellationToken cancellation)
    {
        if (await FindAsync(db, command.WorkspaceId, command.Key, cancellation) is not { } project)
        {
            return NoSuchProject;
        }

        var phase = await db.Phases.SingleOrDefaultAsync(p => p.ProjectId == project.Id && p.Name == command.Phase, cancellation);
        if (phase is null)
        {
            return FlowAnswer.Refused(FlowRefusal.NotFound, $"Project {project.Key}'s Flow has no {command.Phase} Phase.");
        }

        if (!await db.Agents.AnyAsync(a => a.ProjectId == project.Id && a.Id == command.CastAgentId, cancellation))
        {
            return FlowAnswer.Refused(FlowRefusal.Invalid, $"Only Project {project.Key}'s own Agents are cast in its Phases.");
        }

        if (command.Instructions is { } instructions && ProjectRules.InstructionsProblem(instructions) is { } problem)
        {
            return FlowAnswer.Refused(FlowRefusal.Invalid, problem);
        }

        phase.CastAgentId = command.CastAgentId;
        phase.Gate = command.Gate;
        phase.Sandbox = command.Sandbox;
        phase.Instructions = command.Instructions?.Trim() ?? phase.Instructions;
        await db.SaveChangesAsync(cancellation);
        return FlowAnswer.Done;
    }

    public static async Task<(FlowAnswer, OutgoingMessages)> Handle(
        RegisterAgentAgain command, FlowDb db, CancellationToken cancellation)
    {
        var registration = new OutgoingMessages();
        if (await FindAsync(db, command.WorkspaceId, command.Key, cancellation) is not { } project)
        {
            return (NoSuchProject, registration);
        }

        var agent = await db.Agents.SingleOrDefaultAsync(a => a.ProjectId == project.Id && a.Id == command.AgentId, cancellation);
        if (agent is null)
        {
            return (FlowAnswer.Refused(FlowRefusal.NotFound, $"Project {project.Key} has no such Agent."), registration);
        }

        agent.AskIdentityAgain();
        await db.SaveChangesAsync(cancellation);
        registration.AddRange(RegistrationsOf([agent]));
        return (FlowAnswer.Done, registration);
    }

    public static IEnumerable<RegisterAgent> RegistrationsOf(IEnumerable<Agent> agents) =>
        agents.Select(agent => new RegisterAgent(agent.Id, agent.WorkspaceId, agent.Name, AgentOwner.Team(agent.OwnerTeamId)));

    private static FlowAnswer NoSuchProject => FlowAnswer.Refused(FlowRefusal.NotFound, "The Workspace has no such Project.");

    private static FlowAnswer KeyTaken(string key) =>
        FlowAnswer.Refused(FlowRefusal.Taken, $"The Workspace already has a Project {key}; a key is never used twice.");

    private static Task<bool> KeyIsTakenAsync(FlowDb db, Guid workspaceId, string key, CancellationToken cancellation) =>
        db.Projects.AnyAsync(p => p.WorkspaceId == workspaceId && p.Key == key, cancellation);

    private static Task<Project?> FindAsync(FlowDb db, Guid workspaceId, string key, CancellationToken cancellation)
    {
        var normalized = ProjectRules.NormalizeKey(key);
        return db.Projects.SingleOrDefaultAsync(p => p.WorkspaceId == workspaceId && p.Key == normalized, cancellation);
    }

    private static async Task<(RequestProviderBinding? Requests, CodeProviderBinding? Code, FlowAnswer? Refusal)> AsTheConnectionsNameThemAsync(
        Guid workspaceId,
        RequestProviderBinding requests,
        CodeProviderBinding code,
        ConnectionsRepositories connections,
        CancellationToken cancellation)
    {
        var reached = await connections.ReachedByAsync(workspaceId, [requests.ConnectionId, code.ConnectionId], cancellation);
        if (reached.Problem is { } unreachable)
        {
            return (null, null, FlowAnswer.Refused(FlowRefusal.Invalid, unreachable));
        }

        var (named, namedCode, problem) = ProjectRules.AsTheConnectionsNameThem(requests, code, reached.ByConnection);
        return problem is null
            ? (named, namedCode, null)
            : (null, null, FlowAnswer.Refused(FlowRefusal.Invalid, problem));
    }
}
