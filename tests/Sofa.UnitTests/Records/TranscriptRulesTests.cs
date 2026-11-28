using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Sofa.Contracts.Records;
using Sofa.Records.Transcripts;

namespace Sofa.UnitTests.Records;

public class TranscriptRulesTests
{
    private static readonly Guid RunId = Guid.NewGuid();
    private static readonly Guid WorkspaceId = Guid.NewGuid();

    [Fact]
    public void Lines_are_numbered_from_where_they_arrive_and_lines_stored_before_are_not_stored_again()
    {
        var arrived = new AppendTranscript(RunId, WorkspaceId, FirstLine: 4, ["four", "five", "six"]);

        var toStore = TranscriptRules.LinesNotYetStored(arrived, new HashSet<int> { 5 }).ToList();

        Assert.Equal([(4, "four"), (6, "six")], toStore);
    }

    [Fact]
    public void A_line_becomes_entries_numbered_by_their_place_in_it_at_the_time_it_arrived()
    {
        var converted = TranscriptRules.ScrubbedAndConverted(
            """{"type":"assistant","message":{"content":[{"type":"text","text":"Reading."},{"type":"tool_use","id":"t1","name":"Read","input":{"file_path":"/work/a.txt"}}]}}""");

        var entries = TranscriptRules.EntriesOf(RunId, WorkspaceId, 7, converted, DateTimeOffset.UnixEpoch);

        Assert.Equal([(7, 1, "Reading.", null), (7, 2, null, "Read")], entries.Select(e => (e.Line, e.Part, e.Text, e.ToolName)));
        Assert.Equal("""{"file_path":"/work/a.txt"}""", entries[1].ToolInput);
        Assert.All(entries, e => Assert.Equal((RunId, WorkspaceId, DateTimeOffset.UnixEpoch), (e.RunId, e.WorkspaceId, e.At)));
    }

    [Fact]
    public void Known_secrets_are_scrubbed_before_a_line_is_converted()
    {
        var key = "sk-ant-api03-" + new string('k', 40);
        var converted = TranscriptRules.ScrubbedAndConverted(
            $$$"""{"type":"assistant","message":{"content":[{"type":"text","text":"The key is {{{key}}}"},{"type":"tool_use","id":"t1","name":"Bash","input":{"command":"echo {{{key}}}"}}]}}""");

        var entries = TranscriptRules.EntriesOf(RunId, WorkspaceId, 1, converted, DateTimeOffset.UnixEpoch);

        Assert.Equal("The key is [scrubbed]", entries[0].Text);
        Assert.Equal("""{"command":"echo [scrubbed]"}""", entries[1].ToolInput);
    }

    [Fact]
    public void A_tool_result_fills_its_call_and_says_on_which_line_it_came()
    {
        var call = TranscriptRules.EntriesOf(RunId, WorkspaceId, 2,
            TranscriptRules.ScrubbedAndConverted("""{"type":"assistant","message":{"content":[{"type":"tool_use","id":"t1","name":"Read","input":{}}]}}"""),
            DateTimeOffset.UnixEpoch).Single();

        TranscriptRules.FillResult(call, new ToolResultArrived("t1", "the file's text"), line: 3);

        Assert.Equal(("the file's text", false, 3), (call.ToolResult, call.ToolResultCut, call.ToolResultLine));
    }

    [Fact]
    public void A_long_tool_result_is_cut_at_20_KB_never_inside_a_character()
    {
        var result = new string('a', TranscriptRules.CutLimitBytes - 1) + "é and more";

        var (kept, cut) = TranscriptRules.CutToLimit(result);

        Assert.True(cut);
        Assert.Equal(new string('a', TranscriptRules.CutLimitBytes - 1), kept);
        Assert.True(Encoding.UTF8.GetByteCount(kept) <= TranscriptRules.CutLimitBytes);
        Assert.Equal((result[..10], false), TranscriptRules.CutToLimit(result[..10]));
    }

    [Fact]
    public void A_tool_input_over_20_KB_is_kept_as_a_string_of_the_start_of_its_JSON()
    {
        var input = new JsonObject { ["content"] = new string('x', 30 * 1024) };

        var (json, cut) = TranscriptRules.ToolInputAsStored(input);

        Assert.True(cut);
        var kept = JsonSerializer.Deserialize<string>(json!)!;
        Assert.StartsWith("""{"content":"xxx""", kept);
        Assert.Equal(TranscriptRules.CutLimitBytes, Encoding.UTF8.GetByteCount(kept));
        Assert.Equal(("""{"small":true}""", false), TranscriptRules.ToolInputAsStored(new JsonObject { ["small"] = true }));
    }

    [Fact]
    public void An_entry_is_read_back_with_its_tool_input_as_JSON()
    {
        var stored = new StoredTranscriptEntry
        {
            RunId = RunId, Line = 1, Part = 1, WorkspaceId = WorkspaceId, Role = TranscriptRole.Assistant,
            ToolName = "Read", ToolInput = """{"file_path":"a"}""", At = DateTimeOffset.UnixEpoch,
        };

        var read = TranscriptRules.AsRead(stored);

        Assert.Equal("a", read.ToolInput!.Value.GetProperty("file_path").GetString());
    }
}
