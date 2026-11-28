using Sofa.Contracts.Flow;

namespace Sofa.Flow.WorkItems;

public sealed record PhaseInFlow(PhaseName Name, int Position, Gate Gate);

public sealed record FlowOfAProject(IReadOnlyList<PhaseInFlow> Phases)
{
    public PhaseName? First => InOrder.FirstOrDefault()?.Name;

    public PhaseName? After(PhaseName phase, Track? track) =>
        InOrder.SkipWhile(p => p.Name != phase).Skip(1).FirstOrDefault(p => !SkippedOn(track, p.Name))?.Name;

    public Gate GateOf(PhaseName phase, Track? track)
    {
        var configured = Phases.Single(p => p.Name == phase).Gate;
        return track == Track.Small && phase == PhaseName.Plan && configured == Gate.Approve ? Gate.ReviewAfterwards : configured;
    }

    public static bool SkippedOn(Track? track, PhaseName phase) => track == Track.Small && phase == PhaseName.Spec;

    public static bool Reports(PhaseName phase) => phase is PhaseName.Spec or PhaseName.Plan or PhaseName.Implement;

    public static Track? TrackOf(string outcome) =>
        outcome == PhaseOutcomes.Small.Name ? Track.Small
        : outcome == PhaseOutcomes.Full.Name ? Track.Full
        : null;

    private IEnumerable<PhaseInFlow> InOrder => Phases.OrderBy(p => p.Position);
}
