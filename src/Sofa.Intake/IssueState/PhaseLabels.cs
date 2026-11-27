using Sofa.Contracts.Flow;

namespace Sofa.Intake.IssueState;

public static class PhaseLabels
{
    public static string For(PhaseName phase) => $"sofa: {phase.ToString().ToLowerInvariant()}";
}
