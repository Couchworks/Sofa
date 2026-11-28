using Sofa.Contracts.Flow;
using Sofa.Flow.Projects;
using Sofa.Flow.Runs;
using Sofa.Flow.WorkItems;

namespace Sofa.UnitTests.Flow;

public class BriefsTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 28, 9, 0, 0, TimeSpan.Zero);
    private static readonly Guid ProjectId = Guid.NewGuid();
    private static readonly (IReadOnlyList<Agent> Agents, IReadOnlyList<Phase> Phases) Default =
        DefaultFlow.For(ProjectId, Guid.NewGuid(), Guid.NewGuid(), Now);

    private static readonly Project Demo = new()
    {
        Id = ProjectId,
        WorkspaceId = Guid.NewGuid(),
        Key = "DEMO",
        Name = "Demo",
        RequestConnectionId = Guid.NewGuid(),
        RequestRepository = "acme/demo",
        RequestLabel = "sofa",
        CodeConnectionId = Guid.NewGuid(),
        CodeRepositories = ["acme/demo"],
    };

    private static WorkItem DemoOne() => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = Demo.WorkspaceId,
        ProjectId = ProjectId,
        Number = 1,
        Key = "DEMO-1",
        Title = "Add a dark mode toggle",
        RequestId = Guid.NewGuid(),
        RequestRepository = "acme/demo",
        RequestNumber = 12,
        RequestUrl = "https://github.com/acme/demo/issues/12",
        DelegatingUserId = Guid.NewGuid(),
        OpenedAt = Now,
    };

    private static string BriefOf(PhaseName name)
    {
        var phase = Default.Phases.Single(p => p.Name == name);
        return Briefs.For(Demo, DemoOne(), phase, Default.Agents.Single(a => a.Id == phase.CastAgentId));
    }

    [Theory]
    [InlineData(PhaseName.Triage, "Triager")]
    [InlineData(PhaseName.Spec, "Lead")]
    [InlineData(PhaseName.Plan, "Lead")]
    [InlineData(PhaseName.Implement, "Builder")]
    public void A_Brief_is_Sofas_preamble_the_Agents_role_and_the_Phases_instructions_naming_the_Work_Items_key(PhaseName phase, string role)
    {
        var brief = BriefOf(phase);

        Assert.StartsWith($"You are the {role} of Project DEMO, on a Run of its {phase} Phase on Work Item DEMO-1: Add a dark mode toggle.", brief);
        Assert.Contains("Read your Work Item with Sofa's read_work_item tool first.", brief);
        Assert.Contains("Its key, DEMO-1, starts the title of any pull request you open.", brief);
        Assert.Contains($"## As the {role}\n\n{Default.Agents.Single(a => a.Role.ToString() == role).Instructions}", brief);
        Assert.Contains($"## In the {phase} Phase\n\n{DefaultFlow.InstructionsFor(phase)}", brief);
    }

    [Fact]
    public void Each_default_Phase_says_which_Outcome_finishes_it_and_Implement_opens_its_pull_request_with_a_screenshot()
    {
        Assert.Contains("`small`", DefaultFlow.TriageInstructions);
        Assert.Contains("`full`", DefaultFlow.TriageInstructions);
        Assert.Contains("`specified`", DefaultFlow.SpecInstructions);
        Assert.Contains("ask one Question", DefaultFlow.SpecInstructions);
        Assert.Contains("`planned`", DefaultFlow.PlanInstructions);
        Assert.Contains("`implemented`", DefaultFlow.ImplementInstructions);
        Assert.Contains("gh pr create", DefaultFlow.ImplementInstructions);
        Assert.Contains("playwright screenshot", DefaultFlow.ImplementInstructions);
        Assert.All(Default.Phases, phase => Assert.Equal(DefaultFlow.InstructionsFor(phase.Name), phase.Instructions));
    }
}
