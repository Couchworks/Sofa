using Microsoft.EntityFrameworkCore;
using Sofa.Code.Bindings;
using Sofa.Code.Data;
using Sofa.Code.PullRequests;
using Sofa.Code.Pushes;
using Sofa.Contracts.Code;
using Sofa.Contracts.Connections;
using Wolverine;

namespace Sofa.Code.GitHub;

public static class GitHubEventHandler
{
    public static async Task<OutgoingMessages> Handle(
        GitHubEventReceived received,
        CodeDb db,
        ConnectionsTokens connections,
        GitHubRepositories gitHub,
        SofasGitHubApp app,
        TimeProvider time,
        ILogger<GitHubEventReceived> logger,
        CancellationToken cancellation)
    {
        if (received is not { ConnectionId: { } connectionId, WorkspaceId: { } workspaceId })
        {
            return [];
        }

        return received.Event switch
        {
            GitHubPayloads.PullRequest when GitHubPayloads.ReadPullRequest(received.Payload, time.GetUtcNow()) is { } changed =>
                await PullRequestChangedAsync(changed, workspaceId, connectionId, db, logger, cancellation),
            GitHubPayloads.Push when GitHubPayloads.ReadPush(received.Payload) is { } pushed =>
                await PushedAsync(pushed, workspaceId, connectionId, db, connections, gitHub, app, time, logger, cancellation),
            _ => [],
        };
    }

    private static async Task<OutgoingMessages> PullRequestChangedAsync(
        PullRequestChanged changed, Guid workspaceId, Guid connectionId, CodeDb db, ILogger logger, CancellationToken cancellation)
    {
        var outgoing = new OutgoingMessages();
        var seen = new SeenPullRequest(changed.Title, changed.Url, changed.HeadBranch, changed.BaseBranch,
            PullRequestRules.StateOf(changed.Open, changed.Merged), changed.At);
        foreach (var binding in await BindingsHoldingAsync(db, workspaceId, connectionId, changed.Repository, cancellation))
        {
            if (await PullRequestTracking.SeeAsync(db, binding, changed.Repository, changed.Number, seen, cancellation) is { } codesEvent)
            {
                outgoing.Add((object)codesEvent);
                logger.LogInformation("Code sent {Event} for {WorkItemKey}: pull request {Repository}#{Number}",
                    codesEvent.GetType().Name, codesEvent.PullRequest.WorkItemKey, changed.Repository, changed.Number);
            }
        }

        await db.SaveChangesAsync(cancellation);
        return outgoing;
    }

    private static async Task<OutgoingMessages> PushedAsync(
        Pushed pushed,
        Guid workspaceId,
        Guid connectionId,
        CodeDb db,
        ConnectionsTokens connections,
        GitHubRepositories gitHub,
        SofasGitHubApp app,
        TimeProvider time,
        ILogger logger,
        CancellationToken cancellation)
    {
        if (!PushRules.IsByTheApp(pushed, await app.BotAsync(cancellation)))
        {
            return [];
        }

        var undoneBefore = (await db.UndonePushes.Where(u => u.Ref == pushed.Ref).ToListAsync(cancellation))
            .FirstOrDefault(undone => PushRules.IsTheUndoingOf(undone, pushed));
        if (undoneBefore is not null)
        {
            db.UndonePushes.Remove(undoneBefore);
            await db.SaveChangesAsync(cancellation);
            return [];
        }

        var bindings = await BindingsHoldingAsync(db, workspaceId, connectionId, pushed.Repository, cancellation);
        if (bindings.Count == 0)
        {
            return [];
        }

        var now = time.GetUtcNow();
        var grants = await db.Grants.Where(g => g.WorkspaceId == workspaceId && g.RunEndedAt == null && g.ExpiresAt > now).ToListAsync(cancellation);
        var verdict = PushRules.Judge(pushed, grants, now);
        if (verdict.Allowed)
        {
            return [];
        }

        var undone = await UndoAsync(pushed, connectionId, db, connections, gitHub, now, logger, cancellation);
        logger.LogWarning("Code caught a push outside its Sandbox: {Why} {Undone}", verdict.Why,
            undone ? "Code put it back." : "Code couldn't put it back.");
        var project = grants.FirstOrDefault(g => verdict.RunsThatCouldHavePushed.Contains(g.RunId))?.ProjectId ?? bindings[0].ProjectId;
        return
        [
            new PushOutsideItsSandboxCaught(workspaceId, project, pushed.Repository, pushed.Ref, pushed.Before, pushed.After,
                undone, verdict.RunsThatCouldHavePushed, verdict.Why!),
        ];
    }

    private static async Task<bool> UndoAsync(
        Pushed pushed,
        Guid connectionId,
        CodeDb db,
        ConnectionsTokens connections,
        GitHubRepositories gitHub,
        DateTimeOffset now,
        ILogger logger,
        CancellationToken cancellation)
    {
        var undone = new UndonePush
        {
            Id = Guid.CreateVersion7(),
            Repository = pushed.Repository,
            Ref = pushed.Ref,
            PutBackTo = PushRules.PutBackTo(pushed),
            UndoneAt = now,
        };
        db.UndonePushes.Add(undone);
        await db.SaveChangesAsync(cancellation);
        try
        {
            var token = await connections.ToChangeRefsOfAsync(connectionId, pushed.Repository, cancellation);
            var undoing = PushRules.UndoingOf(pushed) switch
            {
                Undoing.DeleteTheRef => gitHub.DeleteRefAsync(token, pushed.Repository, pushed.Ref, cancellation),
                Undoing.CreateTheRefAgain => gitHub.CreateRefAsync(token, pushed.Repository, pushed.Ref, pushed.Before, cancellation),
                _ => gitHub.PutRefBackAsync(token, pushed.Repository, pushed.Ref, pushed.Before, cancellation),
            };
            await undoing;
            return true;
        }
        catch (Exception failed) when (failed is HttpRequestException or InvalidOperationException)
        {
            logger.LogError(failed, "Code couldn't undo the push to {Ref} in {Repository}", pushed.Ref, pushed.Repository);
            db.UndonePushes.Remove(undone);
            await db.SaveChangesAsync(cancellation);
            return false;
        }
    }

    private static async Task<List<CodeBinding>> BindingsHoldingAsync(
        CodeDb db, Guid workspaceId, Guid connectionId, string repository, CancellationToken cancellation) =>
        (await db.Bindings.Where(b => b.WorkspaceId == workspaceId && b.ConnectionId == connectionId && !b.Removed).ToListAsync(cancellation))
        .Where(b => BindingRules.Holds(b, workspaceId, connectionId, repository))
        .ToList();
}
