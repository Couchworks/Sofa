using System.Globalization;
using System.Text;
using Sofa.Contracts.Execution;
using Sofa.Contracts.Flow;
using Sofa.Contracts.Records;

namespace Sofa.AgentGateway.WorkItems;

public sealed record WorkItemRead(
    RunDetails Run, WorkItemDetails WorkItem, RequestRead Request, IReadOnlyList<ContributionDetails> Contributions);

public static class WorkItemText
{
    public static string NoWorkItem(RunDetails run) =>
        $"Sofa has no Work Item for this Run: it is a test Run of the {run.Phase} Phase. Do what its Brief asks, then end it.";

    public static string Of(WorkItemRead read)
    {
        var (run, workItem, request, contributions) = read;
        var text = new StringBuilder();
        text.AppendLine($"# {workItem.Key}: {workItem.Title}");
        text.AppendLine();
        text.AppendLine($"This Run is in the {run.Phase} Phase. The Request is {workItem.Request.Repository}#{workItem.Request.Number}, {workItem.Request.Url}."
            + (workItem.Track is { } track ? $" The Work Item is on the {track} Track." : ""));
        text.AppendLine();
        text.AppendLine("## The Request, as it is now on its issue");
        text.AppendLine();
        switch (request)
        {
            case RequestRead.Now { Request: var now }:
                text.AppendLine($"**{now.Title.Trim()}** ({now.State})");
                text.AppendLine();
                text.AppendLine(string.IsNullOrWhiteSpace(now.Body) ? "*The issue has no description.*" : now.Body.Trim());
                break;
            case RequestRead.Unread { Why: var why }:
                text.AppendLine($"*Sofa couldn't read the issue now: {why} Its title when it came in was \"{workItem.Title}\".*");
                break;
        }

        text.AppendLine();
        text.AppendLine("## Questions asked and their answers");
        text.AppendLine();
        if (workItem.Questions.Count == 0)
        {
            text.AppendLine("*No Run has asked a Question on this Work Item yet.*");
        }

        foreach (var question in workItem.Questions)
        {
            text.AppendLine($"### Asked in the {question.Phase} Phase, {When(question.AskedAt)}");
            text.AppendLine();
            text.AppendLine(Quoted(question.Text));
            text.AppendLine();
            text.AppendLine(question.Answer is { } answer
                ? $"**Answered {When(question.AnsweredAt!.Value)}:**\n\n{answer.Trim()}"
                : "*Not answered yet.*");
            text.AppendLine();
        }

        var decided = workItem.PhaseEnds.Where(end => end.DecidedAt is not null).ToList();
        if (decided.Count > 0)
        {
            text.AppendLine("## What Users decided at the Gates");
            text.AppendLine();
            foreach (var end in decided)
            {
                text.AppendLine(end.Standing switch
                {
                    PhaseEndStanding.SentBack => $"### {end.Phase} sent back, {When(end.DecidedAt!.Value)}\n\n{Quoted(end.Comment ?? "")}",
                    _ => $"### {end.Phase} {end.Standing.ToString().ToLowerInvariant()}, {When(end.DecidedAt!.Value)}",
                });
                text.AppendLine();
            }
        }

        text.AppendLine("## Contributions of earlier Runs");
        text.AppendLine();
        if (contributions.Count == 0)
        {
            text.AppendLine("*No Run has made a Contribution to this Work Item yet.*");
        }

        foreach (var contribution in contributions)
        {
            text.AppendLine($"### {contribution.Phase}, ended `{contribution.Outcome}`, {When(contribution.MadeAt)}");
            text.AppendLine();
            text.AppendLine(contribution.Markdown.Trim());
            if (contribution.Attachments.Count > 0)
            {
                text.AppendLine();
                text.AppendLine($"Attached: {string.Join(", ", contribution.Attachments.Select(a => $"{a.Name} ({a.ContentType}, {a.Size} bytes)"))}");
            }

            text.AppendLine();
        }

        return text.ToString().TrimEnd() + "\n";
    }

    private static string When(DateTimeOffset at) => at.UtcDateTime.ToString("yyyy-MM-dd HH:mm 'UTC'", CultureInfo.InvariantCulture);

    private static string Quoted(string text) =>
        string.Join('\n', text.Trim().ReplaceLineEndings("\n").Split('\n').Select(line => $"> {line}"));
}
