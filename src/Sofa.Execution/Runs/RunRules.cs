using Sofa.Contracts.Execution;
using Sofa.Contracts.Flow;
using Sofa.Execution.Workers;

namespace Sofa.Execution.Runs;

public static class RunRules
{
    public const int LongestAttachmentPath = 500;

    public static string? ProblemWith(StartRun start) =>
        string.IsNullOrWhiteSpace(start.Brief) || start.Brief.Length > RunLimits.LongestBrief
            ? $"A Run's Brief has 1 to {RunLimits.LongestBrief} characters."
        : string.IsNullOrWhiteSpace(start.Model) ? "A Run names its Agent's model."
        : start.RetryLimit is < 0 or > RunLimits.MostRetries ? $"A Run is retried 0 to {RunLimits.MostRetries} times."
        : start.RunId == Guid.Empty || start.AgentId == Guid.Empty || start.DelegatingUserId == Guid.Empty
            ? "A Run names itself, its Agent and its delegating User."
        : null;

    public static string? ProblemWith(FinishRun finish, PhaseName phase) =>
        PhaseOutcomes.Named(phase, finish.Outcome) is null
            ? $"The {phase} Phase's Outcomes are {string.Join(", ", PhaseOutcomes.Of(phase).Select(outcome => $"`{outcome.Name}`"))}; `{finish.Outcome}` is none of them."
        : string.IsNullOrWhiteSpace(finish.Contribution) || finish.Contribution.Length > RunLimits.LongestContribution
            ? $"A Contribution has 1 to {RunLimits.LongestContribution} characters of Markdown."
        : finish.AttachmentPaths.Count > RunLimits.MostAttachments
            ? $"A Contribution has at most {RunLimits.MostAttachments} attachments."
        : finish.AttachmentPaths.Select(ProblemWithAttachmentPath).FirstOrDefault(problem => problem is not null);

    public static string? ProblemWith(EndRunWithQuestion asked) =>
        string.IsNullOrWhiteSpace(asked.Question) || asked.Question.Trim().Length > RunLimits.LongestQuestion
            ? $"A Question has 1 to {RunLimits.LongestQuestion} characters."
        : asked.QuestionId == Guid.Empty ? "A Question has an id."
        : null;

    public static string? ProblemWithAttachmentPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path.Length > LongestAttachmentPath || path.Any(char.IsControl))
        {
            return $"An attachment is named by its path, of 1 to {LongestAttachmentPath} characters.";
        }

        return InTheWorkingDirectory(path) is null
            ? $"Sofa attaches only files in your working directory, {WorkerCommand.WorkingDirectory}; {path} is not in it."
            : null;
    }

    public static string? InTheWorkingDirectory(string path)
    {
        var absolute = path.StartsWith('/') ? path : $"{WorkerCommand.WorkingDirectory}/{path}";
        var segments = new List<string>();
        foreach (var segment in absolute.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            switch (segment)
            {
                case ".":
                    continue;
                case "..":
                    if (segments.Count == 0)
                    {
                        return null;
                    }

                    segments.RemoveAt(segments.Count - 1);
                    continue;
                default:
                    segments.Add(segment);
                    continue;
            }
        }

        var normalized = "/" + string.Join('/', segments);
        return normalized.StartsWith(WorkerCommand.WorkingDirectory + "/", StringComparison.Ordinal) ? normalized : null;
    }

    public static string WhyItCannotEnd(Run run) => $"The Run has already ended: it is {run.State}.";
}
