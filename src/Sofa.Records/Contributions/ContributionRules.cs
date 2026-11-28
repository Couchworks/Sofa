using Sofa.Contracts.Execution;
using Sofa.Contracts.Records;

namespace Sofa.Records.Contributions;

public static class ContributionRules
{
    public const int LongestOutcome = 50;
    public const int LongestAttachmentName = 200;
    public const string Unnamed = "attachment";

    private static readonly IReadOnlyDictionary<string, string> ContentTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".png"] = "image/png",
        [".jpg"] = "image/jpeg",
        [".jpeg"] = "image/jpeg",
        [".gif"] = "image/gif",
        [".webp"] = "image/webp",
        [".pdf"] = "application/pdf",
        [".txt"] = "text/plain",
        [".log"] = "text/plain",
        [".md"] = "text/markdown",
        [".json"] = "application/json",
        [".csv"] = "text/csv",
        [".diff"] = "text/plain",
        [".patch"] = "text/plain",
    };

    private static readonly HashSet<string> ShownInThePage = ["image/png", "image/jpeg", "image/gif", "image/webp"];

    public static long LargestUpload => RunLimits.MostAttachments * RunLimits.LargestAttachment + 4L * RunLimits.LongestContribution + 1024 * 1024;

    public static string? ProblemWith(ContributionToKeep contribution, int attachments) =>
        string.IsNullOrWhiteSpace(contribution.Markdown) || contribution.Markdown.Length > RunLimits.LongestContribution
            ? $"A Contribution has 1 to {RunLimits.LongestContribution} characters of Markdown."
        : string.IsNullOrWhiteSpace(contribution.Outcome) || contribution.Outcome.Length > LongestOutcome
            ? "A Contribution names its Run's Outcome."
        : contribution.WorkItemId == Guid.Empty || contribution.AgentId == Guid.Empty
            ? "A Contribution names its Work Item and its Agent."
        : attachments > RunLimits.MostAttachments ? $"A Contribution has at most {RunLimits.MostAttachments} attachments."
        : null;

    public static string? ProblemWithAttachment(long size) =>
        size > RunLimits.LargestAttachment ? $"An attachment is at most {RunLimits.LargestAttachment} bytes." : null;

    public static string NameOf(string? fileName)
    {
        var name = Path.GetFileName((fileName ?? "").Replace('\\', '/')).Trim();
        var kept = new string(name.Where(c => !char.IsControl(c) && c is not ('"' or '/' or '\\')).ToArray());
        if (kept.Length == 0 || kept is "." or "..")
        {
            return Unnamed;
        }

        return kept.Length <= LongestAttachmentName ? kept : kept[^LongestAttachmentName..];
    }

    public static string ContentTypeOf(string name) =>
        ContentTypes.TryGetValue(Path.GetExtension(name), out var contentType) ? contentType : "application/octet-stream";

    public static bool IsShownInThePage(string contentType) => ShownInThePage.Contains(contentType);
}
