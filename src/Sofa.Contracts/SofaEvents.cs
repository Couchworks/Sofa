namespace Sofa.Contracts;

public static class SofaEvents
{
    public const string GitHubEventReceived = "github-event-received";
    public const string RunEnded = "run-ended";
    public const string SubscriptionSignedOut = "subscription-signed-out";
    public const string ProjectBound = "project-bound";
    public const string RequestAwaitingDelegation = "request-awaiting-delegation";
    public const string QuestionAnsweredOnRequest = "question-answered-on-request";
    public const string WorkItemEnteredPhase = "work-item-entered-phase";
    public const string WorkItemReachedMainBranch = "work-item-reached-main-branch";
    public const string PullRequestOpened = "pull-request-opened";
    public const string PullRequestMerged = "pull-request-merged";
    public const string PullRequestClosedUnmerged = "pull-request-closed-unmerged";
    public const string PushOutsideItsSandboxCaught = "push-outside-its-sandbox-caught";
    public const string WorkItemAwaitsUser = "work-item-awaits-user";
    public const string WorkItemNoLongerAwaitsUser = "work-item-no-longer-awaits-user";
}
