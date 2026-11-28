using Sofa.Contracts.Flow;

namespace Sofa.Flow.Reports;

public sealed record ReportPhaseOnRequest(Guid WorkspaceId, Guid WorkItemId, PhaseName Phase, Guid RunId, WorkItemPullRequest? Merged);
