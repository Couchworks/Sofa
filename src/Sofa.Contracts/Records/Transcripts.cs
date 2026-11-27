using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Sofa.Contracts.Records;

public sealed record AppendTranscript(Guid RunId, Guid WorkspaceId, int FirstLine, IReadOnlyList<string> Lines);

public sealed record Transcript(
    Guid RunId,
    IReadOnlyList<TranscriptEntry> Entries,
    [property: Description("The last line of the Run's output read into these entries: pass it as `after` to read only what came since.")]
    int ReadUpToLine);

public sealed record TranscriptEntry(
    [property: Description("The line of the Run's output it came from, from 1.")]
    int Line,
    [property: Description("Its place among the entries of that line, from 1.")]
    int Part,
    TranscriptRole Role,
    [property: Description("A message's text; null for a tool call.")]
    string? Text,
    [property: Description("The tool called, for a tool call.")]
    string? ToolName,
    [property: Description("What the tool was called with. One whose JSON was over 20 KB is cut: a string of its JSON's first 20 KB, with `toolInputCut` set.")]
    JsonElement? ToolInput,
    bool ToolInputCut,
    [property: Description("What the tool answered, once it has; cut to its first 20 KB, with `toolResultCut` set.")]
    string? ToolResult,
    bool ToolResultCut,
    [property: Description("The line its tool result came on, which a reader reading with `after` sees again.")]
    int? ToolResultLine,
    DateTimeOffset At);

[JsonConverter(typeof(JsonStringEnumConverter<TranscriptRole>))]
public enum TranscriptRole
{
    User,
    Assistant,
    System,
}
