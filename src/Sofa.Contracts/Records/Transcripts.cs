namespace Sofa.Contracts.Records;

public sealed record AppendTranscript(Guid RunId, Guid WorkspaceId, int FirstLine, IReadOnlyList<string> Lines);

public sealed record Transcript(Guid RunId, IReadOnlyList<TranscriptLine> Lines);

public sealed record TranscriptLine(int Number, string Text);
