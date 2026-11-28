namespace Sofa.Contracts.Flow;

public sealed record Outcome(string Name, string Meaning);

public static class PhaseOutcomes
{
    public static readonly Outcome Small = new("small", "the Request takes the Small Track: no Spec, and a plan by the Lead alone");
    public static readonly Outcome Full = new("full", "the Request takes the Full Track, through every Phase");
    public static readonly Outcome Specified = new("specified", "the spec is written, with its acceptance criteria");
    public static readonly Outcome Planned = new("planned", "the plan is written, with the decisions it makes");
    public static readonly Outcome Implemented = new("implemented", "the change is built and its pull request opened");

    public static IReadOnlyList<Outcome> Of(PhaseName phase) => phase switch
    {
        PhaseName.Triage => [Small, Full],
        PhaseName.Spec => [Specified],
        PhaseName.Plan => [Planned],
        PhaseName.Implement => [Implemented],
        _ => throw new ArgumentOutOfRangeException(nameof(phase), phase, "Sofa knows no such Phase."),
    };

    public static Outcome? Named(PhaseName phase, string? name) =>
        Of(phase).FirstOrDefault(outcome => string.Equals(outcome.Name, name?.Trim(), StringComparison.OrdinalIgnoreCase));
}
