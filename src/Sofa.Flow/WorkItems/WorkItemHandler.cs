using Microsoft.EntityFrameworkCore;
using Npgsql;
using Sofa.Contracts.Flow;
using Sofa.Flow.Data;

namespace Sofa.Flow.WorkItems;

public static class WorkItemHandler
{
    public static async Task Handle(
        OpenWorkItem command, FlowDb db, TimeProvider time, ILogger<OpenWorkItem> logger, CancellationToken cancellation)
    {
        if (await db.WorkItems.AnyAsync(w => w.RequestId == command.RequestId, cancellation))
        {
            return;
        }

        var project = await db.Projects.AsNoTracking()
            .SingleOrDefaultAsync(p => p.Id == command.ProjectId && p.WorkspaceId == command.WorkspaceId, cancellation);
        if (project is null)
        {
            logger.LogWarning("Opened no Work Item for Request {RequestId}: Workspace {WorkspaceId} has no Project {ProjectId}",
                command.RequestId, command.WorkspaceId, command.ProjectId);
            return;
        }

        await using var transaction = await db.Database.BeginTransactionAsync(cancellation);
        var number = await NextNumberAsync(db, project.Id, cancellation);
        var workItem = new WorkItem
        {
            Id = Guid.CreateVersion7(),
            WorkspaceId = project.WorkspaceId,
            ProjectId = project.Id,
            Number = number,
            Key = WorkItem.KeyOf(project.Key, number),
            Title = WorkItemRules.TitleOf(command.Title),
            RequestId = command.RequestId,
            RequestRepository = command.Request.Repository,
            RequestNumber = command.Request.Number,
            RequestUrl = command.Request.Url,
            DelegatingUserId = command.DelegatingUserId,
            OpenedAt = time.GetUtcNow(),
        };
        db.WorkItems.Add(workItem);

        try
        {
            await db.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);
        }
        catch (DbUpdateException opened) when (opened.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation })
        {
            return;
        }

        logger.LogInformation("Opened Work Item {Key} for Request {Repository}#{Number}, delegated by {DelegatingUserId}",
            workItem.Key, workItem.RequestRepository, workItem.RequestNumber, workItem.DelegatingUserId?.ToString() ?? "no one yet");
    }

    private static async Task<int> NextNumberAsync(FlowDb db, Guid projectId, CancellationToken cancellation) =>
        (await db.Database.SqlQuery<int>($"""
            update flow.projects set last_work_item_number = last_work_item_number + 1
            where id = {projectId}
            returning last_work_item_number as "Value"
            """).ToListAsync(cancellation)).Single();
}
