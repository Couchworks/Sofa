using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts.Connections;
using Sofa.Contracts.Flow;
using Sofa.Contracts.Intake;
using Sofa.Intake.Bindings;
using Sofa.Intake.Data;
using Sofa.Intake.GitHub;
using Wolverine;

namespace Sofa.Intake.Requests;

public static class RequestIntake
{
    public static async Task<OutgoingMessages> TakeInAsync(
        IssueLabeled labeled,
        Guid workspaceId,
        Guid connectionId,
        IntakeDb db,
        IdentityGitHubAccounts identity,
        TimeProvider time,
        ILogger<GitHubEventReceived> logger,
        CancellationToken cancellation)
    {
        var bindings = (await db.RequestBindings.AsNoTracking().Where(b => b.ConnectionId == connectionId).ToListAsync(cancellation))
            .Where(b => BindingRules.Marks(b, workspaceId, connectionId, labeled.Repository, labeled.Label))
            .ToList();

        var messages = new OutgoingMessages();
        foreach (var binding in bindings)
        {
            var request = await KeptOrNewAsync(binding, labeled, db, identity, time, cancellation);
            messages.Add(new OpenWorkItem(request.Id, request.WorkspaceId, request.ProjectId, request.Title, request.Ref, request.DelegatingUserId));
            if (request.DelegatingUserId is null)
            {
                logger.LogInformation(
                    "{Repository}#{Number} was labelled for Project {Key} by {Login}, linked to no User of the Workspace: it waits for its Admins",
                    request.Repository, request.Number, request.ProjectKey, request.MarkedByGitHubLogin);
                messages.Add(new RequestAwaitingDelegation(request.Id, request.WorkspaceId, request.ProjectId, request.ProjectKey,
                    request.Title, request.Ref, request.MarkedByGitHubLogin, request.MarkedAt));
            }
        }

        return messages;
    }

    private static async Task<Request> KeptOrNewAsync(
        RequestBinding binding,
        IssueLabeled labeled,
        IntakeDb db,
        IdentityGitHubAccounts identity,
        TimeProvider time,
        CancellationToken cancellation)
    {
        if (await FindAsync(db, binding.ProjectId, labeled, cancellation) is { } kept)
        {
            return kept;
        }

        var delegating = labeled.By.IsBot
            ? null
            : await identity.HolderInWorkspaceAsync(binding.WorkspaceId, labeled.By.Id, cancellation);
        var request = new Request
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = binding.WorkspaceId,
            ProjectId = binding.ProjectId,
            ProjectKey = binding.ProjectKey,
            ConnectionId = binding.ConnectionId,
            Repository = binding.Repository,
            Number = labeled.Number,
            Title = RequestRules.TitleOf(labeled.Title),
            Url = labeled.Url,
            MarkedByGitHubAccountId = labeled.By.Id,
            MarkedByGitHubLogin = labeled.By.Login,
            DelegatingUserId = delegating?.UserId,
            MarkedAt = time.GetUtcNow(),
        };
        db.Requests.Add(request);
        try
        {
            await db.SaveChangesAsync(cancellation);
            return request;
        }
        catch (DbUpdateException taken) when (taken.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            db.ChangeTracker.Clear();
            return (await FindAsync(db, binding.ProjectId, labeled, cancellation))!;
        }
    }

    private static Task<Request?> FindAsync(IntakeDb db, Guid projectId, IssueLabeled labeled, CancellationToken cancellation) =>
        db.Requests.AsNoTracking().SingleOrDefaultAsync(
            r => r.ProjectId == projectId && r.Repository.ToLower() == labeled.Repository.ToLower() && r.Number == labeled.Number,
            cancellation);
}
