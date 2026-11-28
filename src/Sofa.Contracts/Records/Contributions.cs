using Sofa.Contracts.Flow;

namespace Sofa.Contracts.Records;

public sealed record ContributionToKeep(Guid WorkItemId, PhaseName Phase, Guid AgentId, string Outcome, string Markdown);

public sealed record ContributionDetails(
    Guid RunId,
    Guid WorkItemId,
    PhaseName Phase,
    Guid AgentId,
    string Outcome,
    string Markdown,
    IReadOnlyList<AttachmentDetails> Attachments,
    DateTimeOffset MadeAt);

public sealed record AttachmentDetails(Guid Id, string Name, string ContentType, long Size);

public static class ContributionForms
{
    public const string Contribution = "contribution";
    public const string Attachment = "attachment";
}
