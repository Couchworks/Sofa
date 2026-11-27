using Sofa.Contracts.Identity;

namespace Sofa.Identity.Agents;

public sealed class Agent
{
    public required Guid Id { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required string Name { get; set; }

    public Guid? OwnerUserId { get; init; }

    public Guid? OwnerTeamId { get; init; }

    public DateTimeOffset RegisteredAt { get; init; }

    public AgentOwner Owner => OwnerUserId is { } userId ? AgentOwner.User(userId) : AgentOwner.Team(OwnerTeamId!.Value);
}
