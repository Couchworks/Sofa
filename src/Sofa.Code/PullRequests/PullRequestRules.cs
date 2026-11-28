using System.Text.RegularExpressions;
using Sofa.Contracts.Code;

namespace Sofa.Code.PullRequests;

public sealed record SeenPullRequest(string Title, string Url, string HeadBranch, string BaseBranch, PullRequestState State, DateTimeOffset At);

public static class PullRequestRules
{
    public static string? WorkItemKeyIn(string projectKey, string title, string headBranch)
    {
        var key = new Regex($@"(?<![A-Za-z0-9]){Regex.Escape(projectKey)}-(?<number>[1-9][0-9]*)(?![A-Za-z0-9])", RegexOptions.IgnoreCase);
        var match = key.Match(title) is { Success: true } inTitle ? inTitle : key.Match(headBranch);
        return match.Success ? $"{projectKey.ToUpperInvariant()}-{match.Groups["number"].Value}" : null;
    }

    public static PullRequestState StateOf(bool open, bool merged) =>
        merged ? PullRequestState.Merged : open ? PullRequestState.Open : PullRequestState.ClosedUnmerged;

    public static IPullRequestEvent? EventForChange(PullRequestState? before, KeptPullRequest now) =>
        before == now.State
            ? null
            : now.State switch
            {
                PullRequestState.Open => new PullRequestOpened(now.AsRead),
                PullRequestState.Merged => new PullRequestMerged(now.AsRead),
                PullRequestState.ClosedUnmerged => new PullRequestClosedUnmerged(now.AsRead),
                _ => null,
            };

    public static void Apply(KeptPullRequest kept, SeenPullRequest seen, string workItemKey)
    {
        kept.WorkItemKey = workItemKey;
        kept.Title = seen.Title;
        kept.Url = seen.Url;
        kept.HeadBranch = seen.HeadBranch;
        kept.BaseBranch = seen.BaseBranch;
        kept.State = seen.State;
        kept.ClosedAt = seen.State == PullRequestState.Open ? null : kept.ClosedAt ?? seen.At;
    }
}
