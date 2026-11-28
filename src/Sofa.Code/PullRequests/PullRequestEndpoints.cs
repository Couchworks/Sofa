using Microsoft.EntityFrameworkCore;
using Sofa.Code.Data;
using Sofa.Code.GitHub;
using Sofa.Contracts.Code;
using Wolverine;

namespace Sofa.Code.PullRequests;

public static class PullRequestEndpoints
{
    public static IEndpointRouteBuilder MapPullRequests(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/workspaces/{workspaceId:guid}/projects/{key}/work-items/{workItemKey}/pull-requests", OfWorkItem);
        return endpoints;
    }

    private static async Task<IResult> OfWorkItem(
        Guid workspaceId,
        string key,
        string workItemKey,
        CodeDb db,
        ConnectionsTokens connections,
        GitHubRepositories gitHub,
        IMessageBus bus,
        TimeProvider time,
        ILogger<PullRequestOfWorkItem> logger,
        CancellationToken cancellation)
    {
        var projectKey = key.Trim().ToUpperInvariant();
        var binding = await db.Bindings.SingleOrDefaultAsync(b => b.WorkspaceId == workspaceId && b.ProjectKey == projectKey, cancellation);
        if (binding is null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "The Workspace has no such Project.");
        }

        var wanted = workItemKey.Trim().ToUpperInvariant();
        var kept = await db.PullRequests
            .Where(p => p.ProjectId == binding.ProjectId && p.WorkItemKey == wanted)
            .OrderBy(p => p.OpenedAt)
            .ToListAsync(cancellation);
        var changes = new List<IPullRequestEvent>();
        foreach (var pull in kept)
        {
            try
            {
                var token = await connections.ToReadPullRequestsOfAsync(pull.ConnectionId, pull.Repository, cancellation);
                if (await gitHub.PullRequestAsync(token, pull.Repository, pull.Number, cancellation) is { } read
                    && await PullRequestTracking.SeeAsync(db, binding, pull.Repository, pull.Number, Seen(read, time.GetUtcNow()), cancellation) is { } change)
                {
                    changes.Add(change);
                }
            }
            catch (Exception unread) when (unread is HttpRequestException or InvalidOperationException)
            {
                logger.LogWarning(unread, "Code couldn't read pull request {Repository}#{Number} on GitHub, so it shows what it kept",
                    pull.Repository, pull.Number);
            }
        }

        await db.SaveChangesAsync(cancellation);
        foreach (var change in changes)
        {
            await bus.PublishAsync((object)change);
        }

        return Results.Ok(kept.Select(p => p.AsRead).ToList());
    }

    private static SeenPullRequest Seen(PullRequestOnGitHub read, DateTimeOffset now) => new(
        read.Title ?? "",
        read.HtmlUrl ?? "",
        read.Head?.Ref ?? "",
        read.Base?.Ref ?? "",
        PullRequestRules.StateOf(read.State == "open", read.Merged),
        read.ClosedAt ?? read.CreatedAt ?? now);
}
