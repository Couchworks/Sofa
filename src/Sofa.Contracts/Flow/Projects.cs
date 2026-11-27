using System.Text.Json.Serialization;

namespace Sofa.Contracts.Flow;

public sealed record RequestProviderBinding(Guid ConnectionId, string Repository, string Label);

public sealed record CodeProviderBinding(Guid ConnectionId, IReadOnlyList<string> Repositories);

public sealed record CreateProject(
    Guid ProjectId,
    Guid WorkspaceId,
    string Key,
    string Name,
    Guid AgentsOwnedByTeamId,
    RequestProviderBinding Requests,
    CodeProviderBinding Code);

public sealed record BindProject(Guid WorkspaceId, string Key, RequestProviderBinding Requests, CodeProviderBinding Code);

public sealed record ConfigureAgent(
    Guid WorkspaceId, string Key, Guid AgentId, AgentRole Role, string Instructions, Vendor Vendor, string Model);

public sealed record ConfigurePhase(
    Guid WorkspaceId, string Key, PhaseName Phase, Guid CastAgentId, Gate Gate, Sandbox Sandbox);

public sealed record FlowAnswer(FlowRefusal? Refusal, string? Problem)
{
    public static FlowAnswer Done { get; } = new(null, null);

    public static FlowAnswer Refused(FlowRefusal refusal, string problem) => new(refusal, problem);
}

[JsonConverter(typeof(JsonStringEnumConverter<FlowRefusal>))]
public enum FlowRefusal
{
    Invalid,
    NotFound,
    Taken,
}

public sealed record ProjectSummary(Guid Id, string Key, string Name, string RequestRepository, string RequestLabel);

public sealed record ProjectDetails(
    Guid Id,
    string Key,
    string Name,
    RequestProviderBinding Requests,
    CodeProviderBinding Code,
    IReadOnlyList<PhaseDetails> Phases,
    IReadOnlyList<AgentDetails> Agents);

public sealed record PhaseDetails(PhaseName Name, int Position, Pattern Pattern, Guid CastAgentId, Gate Gate, Sandbox Sandbox);

public sealed record AgentDetails(Guid Id, string Name, AgentRole Role, string Instructions, Vendor Vendor, string Model);

[JsonConverter(typeof(JsonStringEnumConverter<PhaseName>))]
public enum PhaseName
{
    Triage,
    Spec,
    Plan,
    Implement,
}

[JsonConverter(typeof(JsonStringEnumConverter<Pattern>))]
public enum Pattern
{
    Solo,
}

[JsonConverter(typeof(JsonStringEnumConverter<Gate>))]
public enum Gate
{
    Auto,
    Approve,
    ReviewAfterwards,
}

[JsonConverter(typeof(JsonStringEnumConverter<Sandbox>))]
public enum Sandbox
{
    ReadOnly,
    OwnBranch,
    IntegrationBranch,
}

[JsonConverter(typeof(JsonStringEnumConverter<AgentRole>))]
public enum AgentRole
{
    Triager,
    Lead,
    Builder,
}

[JsonConverter(typeof(JsonStringEnumConverter<Vendor>))]
public enum Vendor
{
    Anthropic,
}
