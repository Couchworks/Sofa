using Sofa.Contracts.Execution;

namespace Sofa.Execution.Runs;

public static class RunRules
{
    public static string? ProblemWith(StartRun start) =>
        string.IsNullOrWhiteSpace(start.Brief) || start.Brief.Length > RunLimits.LongestBrief
            ? $"A Run's Brief has 1 to {RunLimits.LongestBrief} characters."
        : string.IsNullOrWhiteSpace(start.Model) ? "A Run names its Agent's model."
        : start.RetryLimit is < 0 or > RunLimits.MostRetries ? $"A Run is retried 0 to {RunLimits.MostRetries} times."
        : start.RunId == Guid.Empty || start.AgentId == Guid.Empty || start.DelegatingUserId == Guid.Empty
            ? "A Run names itself, its Agent and its delegating User."
        : null;

    public static string WhyItCannotEnd(Run run) => $"The Run has already ended: it is {run.State}.";
}
