using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sofa.Contracts.Records;

namespace Sofa.Records.Transcripts;

public static class TranscriptRules
{
    public const int CutLimitBytes = 20 * 1024;

    public static IEnumerable<(int Number, string Text)> LinesNotYetStored(AppendTranscript arrived, IReadOnlySet<int> alreadyStored) =>
        arrived.Lines
            .Select((text, index) => (Number: arrived.FirstLine + index, Text: text))
            .Where(line => !alreadyStored.Contains(line.Number));

    public static ConvertedLine ScrubbedAndConverted(string line, IReadOnlyList<string>? toolsTheSandboxAllows = null) =>
        ClaudeCodeStreamJson.Convert(SecretScrubbing.ScrubLine(line), toolsTheSandboxAllows);

    public static List<StoredTranscriptEntry> EntriesOf(
        Guid runId, Guid workspaceId, int line, ConvertedLine converted, DateTimeOffset arrivedAt) =>
        converted.Entries.Select((entry, index) =>
        {
            var (toolInput, toolInputCut) = ToolInputAsStored(entry.ToolInput);
            var (toolResult, toolResultCut) = entry.ToolResult is null ? (null, false) : CutToLimit(entry.ToolResult);
            return new StoredTranscriptEntry
            {
                RunId = runId,
                Line = line,
                Part = index + 1,
                WorkspaceId = workspaceId,
                Role = entry.Role,
                Text = entry.Text,
                ToolUseId = entry.ToolUseId,
                ToolName = entry.ToolName,
                ToolInput = toolInput,
                ToolInputCut = toolInputCut,
                ToolResult = toolResult,
                ToolResultCut = toolResultCut,
                ToolResultLine = toolResult is null ? null : line,
                At = converted.At ?? arrivedAt,
            };
        }).ToList();

    public static void FillResult(StoredTranscriptEntry call, ToolResultArrived result, int line)
    {
        (call.ToolResult, call.ToolResultCut) = CutToLimit(result.Result);
        call.ToolResultLine = line;
    }

    public static StoredTranscriptEntry ResultWithoutItsCall(
        Guid runId, Guid workspaceId, int line, int part, ToolResultArrived result, DateTimeOffset at)
    {
        var (toolResult, cut) = CutToLimit(result.Result);
        return new StoredTranscriptEntry
        {
            RunId = runId,
            Line = line,
            Part = part,
            WorkspaceId = workspaceId,
            Role = TranscriptRole.User,
            ToolUseId = result.ToolUseId,
            ToolResult = toolResult,
            ToolResultCut = cut,
            ToolResultLine = line,
            At = at,
        };
    }

    public static (string? Json, bool Cut) ToolInputAsStored(JsonNode? input)
    {
        if (input is null)
        {
            return (null, false);
        }

        var json = input.ToJsonString();
        var (kept, cut) = CutToLimit(json);
        return cut ? (JsonSerializer.Serialize(kept), true) : (json, false);
    }

    public static (string Text, bool Cut) CutToLimit(string text)
    {
        if (Encoding.UTF8.GetByteCount(text) <= CutLimitBytes)
        {
            return (text, false);
        }

        var bytes = Encoding.UTF8.GetBytes(text);
        var end = CutLimitBytes;
        while (end > 0 && (bytes[end] & 0b1100_0000) == 0b1000_0000)
        {
            end--;
        }

        return (Encoding.UTF8.GetString(bytes, 0, end), true);
    }

    public static TranscriptEntry AsRead(StoredTranscriptEntry entry) => new(
        entry.Line,
        entry.Part,
        entry.Role,
        entry.Text,
        entry.ToolName,
        entry.ToolInput is null ? null : JsonDocument.Parse(entry.ToolInput).RootElement.Clone(),
        entry.ToolInputCut,
        entry.ToolResult,
        entry.ToolResultCut,
        entry.ToolResultLine,
        entry.At);
}
