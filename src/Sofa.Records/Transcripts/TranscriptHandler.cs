using Microsoft.EntityFrameworkCore;
using Sofa.Contracts.Records;
using Sofa.Records.Data;

namespace Sofa.Records.Transcripts;

public static class TranscriptHandler
{
    public static async Task Handle(AppendTranscript arrived, RecordsDb db, TimeProvider time, CancellationToken cancellation)
    {
        if (arrived.Lines.Count == 0)
        {
            return;
        }

        var lastLine = arrived.FirstLine + arrived.Lines.Count - 1;
        var alreadyStored = (await db.TranscriptEntries
                .Where(e => e.RunId == arrived.RunId
                    && (e.Line >= arrived.FirstLine && e.Line <= lastLine
                        || e.ToolResultLine >= arrived.FirstLine && e.ToolResultLine <= lastLine))
                .Select(e => new { e.Line, e.ToolResultLine })
                .ToListAsync(cancellation))
            .SelectMany(e => e.ToolResultLine is { } resultLine ? [e.Line, resultLine] : new[] { e.Line })
            .ToHashSet();

        var now = time.GetUtcNow();
        var callsInThisBatch = new Dictionary<string, StoredTranscriptEntry>();
        foreach (var (number, text) in TranscriptRules.LinesNotYetStored(arrived, alreadyStored))
        {
            var converted = TranscriptRules.ScrubbedAndConverted(text);
            var entries = TranscriptRules.EntriesOf(arrived.RunId, arrived.WorkspaceId, number, converted, now);
            db.TranscriptEntries.AddRange(entries);
            foreach (var call in entries.Where(e => e.ToolUseId is not null))
            {
                callsInThisBatch[call.ToolUseId!] = call;
            }

            foreach (var result in converted.Results)
            {
                var call = callsInThisBatch.GetValueOrDefault(result.ToolUseId)
                    ?? await db.TranscriptEntries.FirstOrDefaultAsync(
                        e => e.RunId == arrived.RunId && e.ToolUseId == result.ToolUseId && e.ToolName != null, cancellation);
                if (call is not null)
                {
                    TranscriptRules.FillResult(call, result, number);
                    continue;
                }

                entries.Add(TranscriptRules.ResultWithoutItsCall(
                    arrived.RunId, arrived.WorkspaceId, number, entries.Count + 1, result, converted.At ?? now));
                db.TranscriptEntries.Add(entries[^1]);
            }
        }

        await db.SaveChangesAsync(cancellation);
    }
}
