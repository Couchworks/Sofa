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
        var lines = await db.TranscriptLines.AsNoTracking()
            .Where(l => l.WorkspaceId == workspaceId && l.RunId == runId && l.Number > from)
            .OrderBy(l => l.Number)
            .Select(l => new TranscriptLine(l.Number, l.Text))
            .ToListAsync(cancellation);
        return new Transcript(runId, lines);
    }
}
