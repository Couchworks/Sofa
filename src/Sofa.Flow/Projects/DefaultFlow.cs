using Sofa.Contracts.Flow;

namespace Sofa.Flow.Projects;

public static class DefaultFlow
{
    public const string TriagerInstructions =
        "Read the Request and look for duplicates among the Project's other Requests. Propose Blocks on Requests whose " +
        "work this one needs, set its Priority when it has none, and pick its Track: Small for a change one plan covers " +
        "without a spec, Full otherwise. Give your reasons.";

    public const string LeadInstructions =
        "In Spec, write the spec with its acceptance criteria. Ask a Question whenever the Request leaves open something " +
        "you can't settle from the code or the Project's Knowledge; a Request detailed enough needs none. In Plan, write " +
        "the plan: its steps, its risks, and every decision it makes, each marked as new or as following existing Knowledge.";

    public const string BuilderInstructions =
        "Build the planned change on your own branch, with tests, keeping to the plan and the Project's conventions. " +
        "Screenshot anything visual you changed.";

    public static (IReadOnlyList<Agent> Agents, IReadOnlyList<Phase> Phases) For(
        Guid projectId, Guid workspaceId, Guid agentsOwnedByTeamId, DateTimeOffset now)
    {
        Agent AgentFor(AgentRole role, string model, string instructions) => new()
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = workspaceId,
            ProjectId = projectId,
            Name = role.ToString(),
            OwnerTeamId = agentsOwnedByTeamId,
            Role = role,
            Instructions = instructions,
            Vendor = Vendor.Anthropic,
            Model = model,
            CreatedAt = now,
        };

        var triager = AgentFor(AgentRole.Triager, AgentModels.Haiku, TriagerInstructions);
        var lead = AgentFor(AgentRole.Lead, AgentModels.Opus, LeadInstructions);
        var builder = AgentFor(AgentRole.Builder, AgentModels.Opus, BuilderInstructions);

        Phase PhaseFor(PhaseName name, Agent cast, Gate gate, Sandbox sandbox) => new()
        {
            ProjectId = projectId,
            Name = name,
            Position = (int)name,
            Pattern = Pattern.Solo,
            CastAgentId = cast.Id,
            Gate = gate,
            Sandbox = sandbox,
        };

        return ([triager, lead, builder],
        [
            PhaseFor(PhaseName.Triage, triager, Gate.Auto, Sandbox.ReadOnly),
            PhaseFor(PhaseName.Spec, lead, Gate.Approve, Sandbox.ReadOnly),
            PhaseFor(PhaseName.Plan, lead, Gate.Approve, Sandbox.ReadOnly),
            PhaseFor(PhaseName.Implement, builder, Gate.Auto, Sandbox.OwnBranch),
        ]);
    }
}
