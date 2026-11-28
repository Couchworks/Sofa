using Sofa.Contracts.Records;

namespace Sofa.Flow.Reports;

public static class ReportRules
{
    public const int LongestSummary = 1_500;
    public const int LongestDetail = 60_000;

    public static string SummaryOf(string markdown)
    {
        var paragraphs = markdown.ReplaceLineEndings("\n").Split("\n\n", StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        var summary = paragraphs.FirstOrDefault(paragraph => !paragraph.StartsWith('#')) ?? paragraphs.FirstOrDefault() ?? "";
        return Cut(summary, LongestSummary);
    }

    public static string DetailOf(ContributionDetails contribution)
    {
        var attached = contribution.Attachments.Count == 0
            ? ""
            : $"\n\nAttached in Sofa: {string.Join(", ", contribution.Attachments.Select(a => a.Name))}";
        return Cut(contribution.Markdown.Trim() + attached, LongestDetail);
    }

    public static string MergedSummary(string workItemKey, string pullRequestUrl, string? implemented) =>
        $"{pullRequestUrl} is merged, so {workItemKey} is done."
        + (string.IsNullOrWhiteSpace(implemented) ? "" : $"\n\n{implemented}");

    private static string Cut(string text, int longest) => text.Length <= longest ? text : text[..(longest - 1)] + "…";
}
