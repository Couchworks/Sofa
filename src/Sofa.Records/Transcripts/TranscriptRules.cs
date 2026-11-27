using Sofa.Contracts.Records;

namespace Sofa.Records.Transcripts;

public static class TranscriptRules
{
    public static IEnumerable<StoredTranscriptLine> LinesNotYetStored(
        AppendTranscript arrived, IReadOnlySet<int> alreadyStored, DateTimeOffset now) =>
        arrived.Lines
            .Select((text, index) => (Number: arrived.FirstLine + index, Text: text))
            .Where(line => !alreadyStored.Contains(line.Number))
            .Select(line => new StoredTranscriptLine
            {
                RunId = arrived.RunId,
                Number = line.Number,
                WorkspaceId = arrived.WorkspaceId,
                Text = line.Text,
                ReceivedAt = now,
            });
}
