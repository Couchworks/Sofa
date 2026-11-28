using Sofa.Contracts.Flow;

namespace Sofa.Flow.Projects;

public static class DefaultFlow
{
    public const string TriagerInstructions =
        "You judge how much of the Flow a Request needs, quickly, and always give your reasons.";

    public const string LeadInstructions =
        "You lead the design of the Work Item: you write its spec and its plan, and ask a User only what the Request, " +
        "the code and the Project's conventions can't settle.";

    public const string BuilderInstructions =
        "You build what the plan says, with tests, in small commits, keeping to the Project's conventions, and you " +
        "screenshot anything visual you change.";

    public const string TriageInstructions =
        "Read the Request and pick its Track on your own: Small (no spec, and one plan that a User reviews afterwards " +
        "instead of approving first) for a small change whose intent is clear, or Full (a spec and a plan, each approved " +
        "by a User before the work goes on) for anything larger or less clear. Read the clones Sofa made if the code " +
        "helps you judge. Note in your Contribution any issue of the Repository this one may duplicate, any Request whose work " +
        "this one needs, and the Priority you would give it, for a User to act on. Ask no Question: finish with `small` " +
        "or `full`, giving your reasons.";

    public const string SpecInstructions =
        "Write the spec of what the Request asks for: what changes for its users, what stays out, and numbered acceptance " +
        "criteria a reviewer can check one by one. Read the code the change touches in the clones Sofa made. When the " +
        "Request leaves open something you can't settle from the code, ask one Question with Sofa's ask tool, saying what " +
        "you would choose and why; a new Run reads the answer. A Request detailed enough needs no Question. Start your " +
        "Contribution with a one-paragraph summary, which is posted on the Request's issue as the spec's Report, then give " +
        "the whole spec, and finish with `specified`. A User approves it, or sends it back with a comment a new Run reads.";

    public const string PlanInstructions =
        "Write the plan for building what the spec asks for, or what the Request asks for when the Work Item has no spec: " +
        "the steps in order, the parts of the code each touches, how the change is tested, its risks, and every decision " +
        "it makes, each marked as new or as following the Project's existing conventions. Ground it in the code of the " +
        "clones Sofa made. Start your Contribution with a one-paragraph summary, which is posted on the Request's issue as the " +
        "plan's Report, then give the whole plan, and finish with `planned`.";

    public const string ImplementInstructions =
        "Build the change the plan describes, in the clone Sofa checked out on your own branch: make the change with " +
        "tests, keeping to the plan and the Project's conventions, run what tests you can, commit, and push only your own " +
        "branch. Your container reaches only GitHub and Sofa, so build only what needs nothing downloaded. " +
        "If the change shows in the Project's software, start it with its own scripts (`npm run`, `npm start` or `node`) " +
        "or open its pages straight from your working copy, and screenshot what you changed with " +
        "`playwright screenshot <address> <file>.png` (`--color-scheme dark` and `--viewport-size` may help), saving it " +
        "in your working directory, outside the clone. Then open a pull request from your branch to the main branch with " +
        "`gh pr create --title … --body …` in the clone, its title " +
        "starting with the Work Item's key. Finish with `implemented`: a Contribution saying what you built and naming " +
        "the pull request's address, with your screenshots attached.";

    public static string InstructionsFor(PhaseName phase) => phase switch
    {
        PhaseName.Triage => TriageInstructions,
        PhaseName.Spec => SpecInstructions,
        PhaseName.Plan => PlanInstructions,
        PhaseName.Implement => ImplementInstructions,
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Sofa knows no such Phase."),
    };

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
            Instructions = InstructionsFor(name),
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
