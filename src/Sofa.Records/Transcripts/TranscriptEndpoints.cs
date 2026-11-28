using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Sofa.Contracts.Records;
using Sofa.Records.Data;

namespace Sofa.Records.Transcripts;

public static class TranscriptEndpoints
{
    public static IEndpointRouteBuilder MapTranscripts(this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapGet("/workspaces/{workspaceId:guid}/runs/{runId:guid}/transcript", TranscriptOf);
        return endpoints;
    }

    private static async Task<Transcript> TranscriptOf(
        Guid workspaceId, Guid runId, [FromQuery] int? after, RecordsDb db, CancellationToken cancellation)
    {
        var from = after ?? 0;
        var entries = await db.TranscriptEntries.AsNoTracking()
            .Where(e => e.WorkspaceId == workspaceId && e.RunId == runId && (e.Line > from || e.ToolResultLine > from))
            .OrderBy(e => e.Line).ThenBy(e => e.Part)
            .ToListAsync(cancellation);
        var readUpTo = entries.Select(e => Math.Max(e.Line, e.ToolResultLine ?? 0)).DefaultIfEmpty(from).Max();
        return new Transcript(runId, entries.Select(TranscriptRules.AsRead).ToList(), readUpTo);
    }
}
