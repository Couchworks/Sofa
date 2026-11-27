using Sofa.Contracts.Flow;
using Sofa.Intake.Data;
using Sofa.Intake.GitHub;
using Sofa.Intake.Requests;

namespace Sofa.Intake.IssueState;

public static class IssueStateHandler
{
    public static async Task Handle(
        WorkItemEnteredPhase entered, IntakeDb db, IRequestState state, ILogger<WorkItemEnteredPhase> logger, CancellationToken cancellation)
    {
        if (await RequestKeepingItsIssueStateAsync(db, entered.RequestId, cancellation) is not { } request)
        {
            return;
        }

        var label = PhaseLabels.For(entered.Phase);
        if (request.PhaseLabel == label)
        {
            return;
        }

        await state.SetAsync(request.Issue, new WantedIssueState(label, request.PhaseLabel, Closed: false), cancellation);
        request.PhaseLabel = label;
        await db.SaveChangesAsync(cancellation);
        logger.LogInformation("Labelled {Repository}#{Number} {Label} for {Key}", request.Repository, request.Number, label, entered.Key);
    }

    public static async Task Handle(
        WorkItemReachedMainBranch reached,
        IntakeDb db,
        IRequestState state,
        TimeProvider time,
        ILogger<WorkItemReachedMainBranch> logger,
        CancellationToken cancellation)
    {
        if (await RequestKeepingItsIssueStateAsync(db, reached.RequestId, cancellation) is not { ClosedAt: null } request)
        {
            return;
        }

        await state.SetAsync(request.Issue, new WantedIssueState(null, request.PhaseLabel, Closed: true), cancellation);
        request.PhaseLabel = null;
        request.ClosedAt = time.GetUtcNow();
        await db.SaveChangesAsync(cancellation);
        logger.LogInformation("Closed {Repository}#{Number}: {Key}'s work reached the main branch", request.Repository, request.Number, reached.Key);
    }

    private static async Task<Request?> RequestKeepingItsIssueStateAsync(IntakeDb db, Guid requestId, CancellationToken cancellation)
    {
        if (await db.Requests.FindAsync([requestId], cancellation) is not { } request)
        {
            return null;
        }

        var binding = await db.RequestBindings.FindAsync([request.ProjectId], cancellation);
        return binding is { Removed: false, KeepsIssueState: true } ? request : null;
    }
}
