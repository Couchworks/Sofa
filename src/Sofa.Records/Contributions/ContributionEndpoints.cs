using System.Text.Json;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.EntityFrameworkCore;
using Sofa.Contracts.Records;
using Sofa.Records.Data;
using Sofa.Records.Files;

namespace Sofa.Records.Contributions;

public static class ContributionEndpoints
{
    public static IEndpointRouteBuilder MapContributions(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPut("/workspaces/{workspaceId:guid}/runs/{runId:guid}/contribution", KeepAsync).DisableAntiforgery();
        endpoints.MapGet("/workspaces/{workspaceId:guid}/runs/{runId:guid}/contribution", ContributionOfAsync);
        endpoints.MapGet("/workspaces/{workspaceId:guid}/runs/{runId:guid}/contribution/attachments/{attachmentId:guid}", AttachmentAsync);
        endpoints.MapGet("/workspaces/{workspaceId:guid}/work-items/{workItemId:guid}/contributions", ContributionsOnAsync);
        return endpoints;
    }

    private static async Task<IResult> KeepAsync(
        Guid workspaceId,
        Guid runId,
        HttpRequest request,
        RecordsDb db,
        IFileStore files,
        TimeProvider time,
        ILogger<StoredContribution> logger,
        CancellationToken cancellation)
    {
        if (request.HttpContext.Features.Get<IHttpMaxRequestBodySizeFeature>() is { IsReadOnly: false } bodyLimit)
        {
            bodyLimit.MaxRequestBodySize = ContributionRules.LargestUpload;
        }

        if (!request.HasFormContentType)
        {
            return Invalid("A Contribution comes as a form, with its attachments.");
        }

        var form = await request.ReadFormAsync(cancellation);
        var toKeep = form[ContributionForms.Contribution].ToString() is { Length: > 0 } json
            ? JsonSerializer.Deserialize<ContributionToKeep>(json, JsonSerializerOptions.Web)
            : null;
        var uploaded = form.Files.GetFiles(ContributionForms.Attachment);
        if (toKeep is null)
        {
            return Invalid("A Contribution names its Work Item, Phase, Agent and Outcome, with its Markdown.");
        }

        if ((ContributionRules.ProblemWith(toKeep, uploaded.Count)
             ?? uploaded.Select(file => ContributionRules.ProblemWithAttachment(file.Length)).FirstOrDefault(problem => problem is not null)) is { } problem)
        {
            return Invalid(problem);
        }

        var replaced = await db.Contributions.Include(c => c.Attachments).SingleOrDefaultAsync(c => c.RunId == runId, cancellation);
        if (replaced is not null && replaced.WorkspaceId != workspaceId)
        {
            return Results.Problem(statusCode: StatusCodes.Status409Conflict, title: "Another Workspace's Run has that id.");
        }

        var contribution = new StoredContribution
        {
            RunId = runId,
            WorkspaceId = workspaceId,
            WorkItemId = toKeep.WorkItemId,
            Phase = toKeep.Phase,
            AgentId = toKeep.AgentId,
            Outcome = toKeep.Outcome.Trim(),
            Markdown = toKeep.Markdown,
            MadeAt = time.GetUtcNow(),
        };
        foreach (var (file, position) in uploaded.Select((file, position) => (file, position)))
        {
            var attachmentId = Guid.CreateVersion7();
            var name = ContributionRules.NameOf(file.FileName);
            var key = FileKeys.OfAttachment(workspaceId, runId, attachmentId);
            await using (var content = file.OpenReadStream())
            {
                await files.SaveAsync(key, content, cancellation);
            }

            contribution.Attachments.Add(new StoredAttachment
            {
                Id = attachmentId,
                RunId = runId,
                WorkspaceId = workspaceId,
                Position = position,
                Name = name,
                ContentType = ContributionRules.ContentTypeOf(name),
                Size = file.Length,
                FileKey = key,
            });
        }

        await using (var transaction = await db.Database.BeginTransactionAsync(cancellation))
        {
            if (replaced is not null)
            {
                db.Contributions.Remove(replaced);
                await db.SaveChangesAsync(cancellation);
            }

            db.Contributions.Add(contribution);
            await db.SaveChangesAsync(cancellation);
            await transaction.CommitAsync(cancellation);
        }

        foreach (var gone in replaced?.Attachments ?? [])
        {
            await files.DeleteAsync(gone.FileKey, CancellationToken.None);
        }

        logger.LogInformation("Kept the Contribution of Run {RunId} on Work Item {WorkItemId}, {Outcome}, with {Attachments} attachments",
            runId, contribution.WorkItemId, contribution.Outcome, contribution.Attachments.Count);
        return Results.Ok(contribution.Details);
    }

    private static async Task<IResult> ContributionOfAsync(Guid workspaceId, Guid runId, RecordsDb db, CancellationToken cancellation) =>
        await db.Contributions.AsNoTracking().Include(c => c.Attachments)
            .SingleOrDefaultAsync(c => c.WorkspaceId == workspaceId && c.RunId == runId, cancellation) is { } contribution
            ? Results.Ok(contribution.Details)
            : NoContribution;

    private static async Task<List<ContributionDetails>> ContributionsOnAsync(
        Guid workspaceId, Guid workItemId, RecordsDb db, CancellationToken cancellation) =>
        (await db.Contributions.AsNoTracking().Include(c => c.Attachments)
            .Where(c => c.WorkspaceId == workspaceId && c.WorkItemId == workItemId)
            .OrderBy(c => c.MadeAt)
            .ToListAsync(cancellation))
        .Select(c => c.Details)
        .ToList();

    private static async Task<IResult> AttachmentAsync(
        Guid workspaceId, Guid runId, Guid attachmentId, RecordsDb db, IFileStore files, CancellationToken cancellation)
    {
        var attachment = await db.Attachments.AsNoTracking()
            .SingleOrDefaultAsync(a => a.WorkspaceId == workspaceId && a.RunId == runId && a.Id == attachmentId, cancellation);
        if (attachment is null || await files.OpenAsync(attachment.FileKey, cancellation) is not { } content)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "The Contribution has no such attachment.");
        }

        return Results.Stream(content, attachment.ContentType,
            ContributionRules.IsShownInThePage(attachment.ContentType) ? null : attachment.Name);
    }

    private static IResult Invalid(string problem) => Results.Problem(statusCode: StatusCodes.Status400BadRequest, title: problem);

    private static IResult NoContribution =>
        Results.Problem(statusCode: StatusCodes.Status404NotFound, title: "The Run has made no Contribution.");
}
