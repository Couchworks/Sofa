using Sofa.Contracts.Records;

namespace Sofa.Records.Transcripts;

public sealed class StoredTranscriptEntry
{
    public required Guid RunId { get; init; }

    public required int Line { get; init; }

    public required int Part { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required TranscriptRole Role { get; init; }

    public string? Text { get; init; }

    public string? ToolUseId { get; init; }

    public string? ToolName { get; init; }

    public string? ToolInput { get; init; }

    public bool ToolInputCut { get; init; }

    public string? ToolResult { get; set; }

    public bool ToolResultCut { get; set; }

    public int? ToolResultLine { get; set; }

    public required DateTimeOffset At { get; init; }
}
