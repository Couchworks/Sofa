using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sofa.Contracts.Records;

namespace Sofa.Records.Transcripts;

public sealed record ConvertedEntry(
    TranscriptRole Role,
    string? Text = null,
    string? ToolUseId = null,
    string? ToolName = null,
    JsonNode? ToolInput = null,
    string? ToolResult = null);

public sealed record ToolResultArrived(string ToolUseId, string Result);

public sealed record ConvertedLine(IReadOnlyList<ConvertedEntry> Entries, IReadOnlyList<ToolResultArrived> Results, DateTimeOffset? At)
{
    public static ConvertedLine Nothing { get; } = new([], [], null);
}

public static class ClaudeCodeStreamJson
{
    private const string ThinkingTokensSubtype = "thinking_tokens";

    public static ConvertedLine Convert(string line, IReadOnlyList<string>? toolsTheSandboxAllows = null)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            return ConvertedLine.Nothing;
        }

        JsonObject? parsed;
        try
        {
            parsed = JsonNode.Parse(line) as JsonObject;
        }
        catch (JsonException)
        {
            parsed = null;
        }

        if (parsed is null)
        {
            return new ConvertedLine([new ConvertedEntry(TranscriptRole.System, Text: line.Trim())], [], null);
        }

        var at = TimeOf(parsed["timestamp"]);
        return StringOf(parsed["type"]) switch
        {
            "system" => new ConvertedLine(SystemEntries(parsed, toolsTheSandboxAllows), [], at),
            "result" => new ConvertedLine([new ConvertedEntry(TranscriptRole.System, Text: HowItEnded(parsed))], [], at),
            "assistant" => MessageOf(TranscriptRole.Assistant, parsed, at),
            "user" => MessageOf(TranscriptRole.User, parsed, at),
            _ => ConvertedLine.Nothing,
        };
    }

    private static IReadOnlyList<ConvertedEntry> SystemEntries(JsonObject line, IReadOnlyList<string>? toolsTheSandboxAllows)
    {
        var subtype = StringOf(line["subtype"]);
        if (subtype == "init")
        {
            return [new ConvertedEntry(TranscriptRole.System, Text: HowItStarted(line, toolsTheSandboxAllows))];
        }

        if (subtype == ThinkingTokensSubtype)
        {
            return [];
        }

        var said = StringOf(line["content"]) ?? StringOf(line["message"]);
        return said is null && subtype is null
            ? []
            : [new ConvertedEntry(TranscriptRole.System, Text: string.Join(": ", new[] { subtype, said }.OfType<string>()))];
    }

    private static string HowItStarted(JsonObject init, IReadOnlyList<string>? toolsTheSandboxAllows)
    {
        var tools = toolsTheSandboxAllows ?? (init["tools"] as JsonArray)?.Select(StringOf).OfType<string>().ToList() ?? [];
        var servers = (init["mcp_servers"] as JsonArray)?.OfType<JsonObject>()
            .Select(server => $"{StringOf(server["name"])} ({StringOf(server["status"])})").ToList() ?? [];
        return string.Join(' ', new[]
        {
            $"Claude Code {StringOf(init["claude_code_version"]) ?? ""}".TrimEnd() + $" started on {StringOf(init["model"]) ?? "its default model"}.",
            $"Tools allowed: {(tools.Count == 0 ? "none" : string.Join(", ", tools))}.",
            $"MCP servers: {(servers.Count == 0 ? "none" : string.Join(", ", servers))}.",
            $"Credentials: {StringOf(init["apiKeySource"]) ?? "not said"}.",
        });
    }

    private static string HowItEnded(JsonObject result)
    {
        var parts = new List<string> { $"Ended: {StringOf(result["subtype"]) ?? "with no result"}" };
        if (result["num_turns"] is JsonValue turns && turns.TryGetValue<int>(out var count))
        {
            parts.Add($"after {count} turn{(count == 1 ? "" : "s")}");
        }

        if (result["total_cost_usd"] is JsonValue cost && cost.TryGetValue<decimal>(out var dollars))
        {
            parts.Add(string.Create(CultureInfo.InvariantCulture, $"costing ${dollars:0.####}"));
        }

        var said = result["is_error"] is JsonValue error && error.TryGetValue<bool>(out var isError) && isError
            ? StringOf(result["result"])
            : null;
        return string.Join(", ", parts) + "." + (said is null ? "" : $" {said}");
    }

    private static ConvertedLine MessageOf(TranscriptRole role, JsonObject line, DateTimeOffset? at)
    {
        var content = (line["message"] as JsonObject)?["content"];
        if (StringOf(content) is { } text)
        {
            return string.IsNullOrWhiteSpace(text) ? ConvertedLine.Nothing : new ConvertedLine([new ConvertedEntry(role, Text: text)], [], at);
        }

        if (content is not JsonArray blocks)
        {
            return ConvertedLine.Nothing;
        }

        var entries = new List<ConvertedEntry>();
        var results = new List<ToolResultArrived>();
        foreach (var block in blocks.OfType<JsonObject>())
        {
            switch (StringOf(block["type"]))
            {
                case "text" when StringOf(block["text"]) is { } said && !string.IsNullOrWhiteSpace(said):
                    entries.Add(new ConvertedEntry(role, Text: said));
                    break;
                case "tool_use" when StringOf(block["name"]) is { } name:
                    entries.Add(new ConvertedEntry(role, ToolUseId: StringOf(block["id"]), ToolName: name, ToolInput: block["input"]?.DeepClone()));
                    break;
                case "tool_result":
                    var result = ResultText(block["content"]);
                    if (StringOf(block["tool_use_id"]) is { } callId)
                    {
                        results.Add(new ToolResultArrived(callId, result));
                    }
                    else
                    {
                        entries.Add(new ConvertedEntry(role, ToolResult: result));
                    }

                    break;
            }
        }

        return new ConvertedLine(entries, results, at);
    }

    public static string ResultText(JsonNode? content) => content switch
    {
        null => "",
        JsonValue value when value.GetValueKind() == JsonValueKind.String => value.GetValue<string>(),
        JsonArray blocks => string.Join('\n', blocks.Select(block => block switch
        {
            JsonObject { } part when StringOf(part["type"]) == "text" => StringOf(part["text"]) ?? "",
            JsonObject { } part when StringOf(part["type"]) is { } kind => $"[{kind}]",
            _ => "",
        })),
        _ => content.ToJsonString(),
    };

    private static string? StringOf(JsonNode? node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : null;

    private static DateTimeOffset? TimeOf(JsonNode? node) =>
        StringOf(node) is { } text && DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var at)
            ? at.ToUniversalTime()
            : null;
}
