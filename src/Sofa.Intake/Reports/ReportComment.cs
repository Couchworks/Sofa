using Sofa.Contracts.Flow;

namespace Sofa.Intake.Reports;

public sealed class ReportComment
{
    public required Guid RequestId { get; init; }

    public required PhaseName Phase { get; init; }

    public required long CommentId { get; set; }

    public required DateTimeOffset PostedAt { get; init; }

    public DateTimeOffset? UpdatedAt { get; set; }
}
