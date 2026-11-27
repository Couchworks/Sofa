using System.Text.Json.Serialization;

namespace Sofa.Contracts.Identity;

public sealed record RegisterAgent(Guid AgentId, Guid WorkspaceId, string Name, AgentOwner Owner);

public sealed record AgentOwner(AgentOwnerKind Kind, Guid Id)
{
    public static AgentOwner User(Guid userId) => new(AgentOwnerKind.User, userId);

    public static AgentOwner Team(Guid teamId) => new(AgentOwnerKind.Team, teamId);
}

[JsonConverter(typeof(JsonStringEnumConverter<AgentOwnerKind>))]
public enum AgentOwnerKind
{
    User,
    Team,
}

public sealed record AgentRegistration(Guid AgentId, string? RefusedBecause)
{
    public bool Registered => RefusedBecause is null;

    public static AgentRegistration Accepted(Guid agentId) => new(agentId, RefusedBecause: null);

    public static AgentRegistration Refused(Guid agentId, string reason) => new(agentId, reason);
}
