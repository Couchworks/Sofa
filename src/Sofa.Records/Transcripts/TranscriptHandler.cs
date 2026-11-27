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
        var alreadyStored = (await db.TranscriptLines
                .Where(l => l.RunId == arrived.RunId && l.Number >= arrived.FirstLine && l.Number <= lastLine)
                .Select(l => l.Number)
                .ToListAsync(cancellation))
            .ToHashSet();

        db.TranscriptLines.AddRange(TranscriptRules.LinesNotYetStored(arrived, alreadyStored, time.GetUtcNow()));
        await db.SaveChangesAsync(cancellation);
    }
}
