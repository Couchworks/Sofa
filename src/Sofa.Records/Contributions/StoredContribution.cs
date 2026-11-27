using Sofa.Contracts.Flow;
using Sofa.Contracts.Records;

namespace Sofa.Records.Contributions;

public sealed class StoredContribution
{
    public required Guid RunId { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required Guid WorkItemId { get; init; }

    public required PhaseName Phase { get; init; }

    public required Guid AgentId { get; init; }

    public required string Outcome { get; init; }

    public required string Markdown { get; init; }

    public required DateTimeOffset MadeAt { get; init; }

    public List<StoredAttachment> Attachments { get; init; } = [];

    public ContributionDetails Details => new(
        RunId, WorkItemId, Phase, AgentId, Outcome, Markdown,
        Attachments.OrderBy(a => a.Position).Select(a => a.Details).ToList(),
        MadeAt);
}

public sealed class StoredAttachment
{
    public required Guid Id { get; init; }

    public required Guid RunId { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required int Position { get; init; }

    public required string Name { get; init; }

    public required string ContentType { get; init; }

    public required long Size { get; init; }

    public required string FileKey { get; init; }

    public AttachmentDetails Details => new(Id, Name, ContentType, Size);
}
