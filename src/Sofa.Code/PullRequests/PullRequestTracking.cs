using Microsoft.EntityFrameworkCore;
using Sofa.Code.Bindings;
using Sofa.Code.Data;
using Sofa.Contracts.Code;

namespace Sofa.Code.PullRequests;

public static class PullRequestTracking
{
    public static async Task<IPullRequestEvent?> SeeAsync(
        CodeDb db, CodeBinding binding, string repository, long number, SeenPullRequest seen, CancellationToken cancellation)
    {
        var kept = await db.PullRequests.SingleOrDefaultAsync(
            p => p.ProjectId == binding.ProjectId && p.Repository == repository && p.Number == number, cancellation);
        var key = PullRequestRules.WorkItemKeyIn(binding.ProjectKey, seen.Title, seen.HeadBranch) ?? kept?.WorkItemKey;
        if (key is null)
        {
            return null;
        }

        var before = kept?.State;
        if (kept is null)
        {
            kept = new KeptPullRequest
            {
                Id = Guid.CreateVersion7(),
                WorkspaceId = binding.WorkspaceId,
                ProjectId = binding.ProjectId,
                ProjectKey = binding.ProjectKey,
                ConnectionId = binding.ConnectionId,
                Repository = repository,
                Number = number,
                WorkItemKey = key,
                Title = seen.Title,
                Url = seen.Url,
                HeadBranch = seen.HeadBranch,
                BaseBranch = seen.BaseBranch,
                OpenedAt = seen.At,
            };
            db.PullRequests.Add(kept);
        }

        PullRequestRules.Apply(kept, seen, key);
        return PullRequestRules.EventForChange(before, kept);
    }
}
