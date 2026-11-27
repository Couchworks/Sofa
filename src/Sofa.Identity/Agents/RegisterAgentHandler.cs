using Microsoft.EntityFrameworkCore;
using Sofa.Contracts.Identity;
using Sofa.Identity.Data;

namespace Sofa.Identity.Agents;

public static class RegisterAgentHandler
{
    public static async Task<AgentRegistration> Handle(
        RegisterAgent command, IdentityDb db, TimeProvider time, CancellationToken cancellation)
    {
        if (string.IsNullOrWhiteSpace(command.Name))
        {
            return AgentRegistration.Refused(command.AgentId, "An Agent needs a name.");
        }

        var existing = await db.Agents.FindAsync([command.AgentId], cancellation);
        if (existing is not null)
        {
            return IsTheSameAgent(existing, command)
                ? AgentRegistration.Accepted(command.AgentId)
                : AgentRegistration.Refused(command.AgentId, "Another Agent already has that id.");
        }

        if (!await OwnerBelongsToWorkspaceAsync(command, db, cancellation))
        {
            return AgentRegistration.Refused(command.AgentId,
                $"The Workspace has no {command.Owner.Kind} {command.Owner.Id} to own the Agent.");
        }

        db.Agents.Add(new Agent
        {
            Id = command.AgentId,
            WorkspaceId = command.WorkspaceId,
            Name = command.Name.Trim(),
            OwnerUserId = command.Owner.Kind == AgentOwnerKind.User ? command.Owner.Id : null,
            OwnerTeamId = command.Owner.Kind == AgentOwnerKind.Team ? command.Owner.Id : null,
            RegisteredAt = time.GetUtcNow(),
        });
        await db.SaveChangesAsync(cancellation);
        return AgentRegistration.Accepted(command.AgentId);
    }

    public static bool IsTheSameAgent(Agent existing, RegisterAgent command) =>
        existing.WorkspaceId == command.WorkspaceId
        && existing.Owner == command.Owner
        && existing.Name == command.Name.Trim();

    private static Task<bool> OwnerBelongsToWorkspaceAsync(
        RegisterAgent command, IdentityDb db, CancellationToken cancellation) =>
        command.Owner.Kind switch
        {
            AgentOwnerKind.User => db.WorkspaceUsers.AnyAsync(
                w => w.WorkspaceId == command.WorkspaceId && w.UserId == command.Owner.Id, cancellation),
            AgentOwnerKind.Team => db.Teams.AnyAsync(
                t => t.WorkspaceId == command.WorkspaceId && t.Id == command.Owner.Id, cancellation),
            _ => Task.FromResult(false),
        };
}
