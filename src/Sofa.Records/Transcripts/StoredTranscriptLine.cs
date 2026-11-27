namespace Sofa.Records.Transcripts;

public sealed class StoredTranscriptLine
{
    public required Guid RunId { get; init; }

    public required int Number { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required string Text { get; init; }

    public DateTimeOffset ReceivedAt { get; init; }
}
