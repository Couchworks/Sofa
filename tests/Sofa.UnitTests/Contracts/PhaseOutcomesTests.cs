using Sofa.Contracts.Flow;

namespace Sofa.UnitTests.Contracts;

public class PhaseOutcomesTests
{
    [Fact]
    public void Triage_ends_by_picking_a_Track()
    {
        Assert.Equal(["small", "full"], PhaseOutcomes.Of(PhaseName.Triage).Select(outcome => outcome.Name));
    }

    [Theory]
    [InlineData(PhaseName.Triage)]
    [InlineData(PhaseName.Spec)]
    [InlineData(PhaseName.Plan)]
    [InlineData(PhaseName.Implement)]
    public void Every_Phase_has_an_Outcome_with_its_meaning(PhaseName phase)
    {
        Assert.NotEmpty(PhaseOutcomes.Of(phase));
        Assert.All(PhaseOutcomes.Of(phase), outcome => Assert.False(string.IsNullOrWhiteSpace(outcome.Meaning)));
    }

    [Fact]
    public void An_Outcome_is_named_whatever_its_case_and_only_in_its_own_Phase()
    {
        Assert.Equal(PhaseOutcomes.Specified, PhaseOutcomes.Named(PhaseName.Spec, " Specified "));
        Assert.Null(PhaseOutcomes.Named(PhaseName.Triage, "specified"));
        Assert.Null(PhaseOutcomes.Named(PhaseName.Spec, null));
    }
}
