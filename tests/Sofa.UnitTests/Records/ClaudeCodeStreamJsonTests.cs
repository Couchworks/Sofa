using Sofa.Contracts.Records;
using Sofa.Records.Transcripts;

namespace Sofa.UnitTests.Records;

public class ClaudeCodeStreamJsonTests
{
    [Fact]
    public void The_start_says_the_model_the_tools_allowed_the_MCP_servers_and_the_credentials()
    {
        var converted = ClaudeCodeStreamJson.Convert(
            """{"type":"system","subtype":"init","claude_code_version":"2.1.332","model":"haiku","tools":["Read","mcp__sofa__end_test_run"],"mcp_servers":[{"name":"sofa","status":"connected"}],"apiKeySource":"none"}""");

        var start = Assert.Single(converted.Entries);
        Assert.Equal(TranscriptRole.System, start.Role);
        Assert.Equal(
            "Claude Code 2.1.332 started on haiku. Tools allowed: Read, mcp__sofa__end_test_run. MCP servers: sofa (connected). Credentials: none.",
            start.Text);
    }

    [Fact]
    public void The_start_lists_the_tools_the_Sandbox_allows_rather_than_every_tool_Claude_Code_has()
    {
        var converted = ClaudeCodeStreamJson.Convert(
            """{"type":"system","subtype":"init","model":"opus","tools":["Read","WebFetch","Task"],"mcp_servers":[],"apiKeySource":"ANTHROPIC_API_KEY"}""",
            ["mcp__sofa__finish", "Read", "Bash(git log:*)"]);

        Assert.Equal(
            "Claude Code started on opus. Tools allowed: mcp__sofa__finish, Read, Bash(git log:*). MCP servers: none. Credentials: ANTHROPIC_API_KEY.",
            Assert.Single(converted.Entries).Text);
    }

    [Fact]
    public void A_thinking_tokens_line_is_skipped()
    {
        var converted = ClaudeCodeStreamJson.Convert("""{"type":"system","subtype":"thinking_tokens","tokens":812}""");

        Assert.Empty(converted.Entries);
    }

    [Fact]
    public void A_message_becomes_an_entry_for_each_text_and_tool_call_leaving_out_thinking()
    {
        var converted = ClaudeCodeStreamJson.Convert(
            """{"type":"assistant","message":{"content":[{"type":"thinking","thinking":"hm"},{"type":"text","text":"Let me look."},{"type":"tool_use","id":"toolu_1","name":"Grep","input":{"pattern":"sofa"}}]}}""");

        Assert.Equal(2, converted.Entries.Count);
        Assert.Equal((TranscriptRole.Assistant, "Let me look."), (converted.Entries[0].Role, converted.Entries[0].Text));
        Assert.Equal(("toolu_1", "Grep", """{"pattern":"sofa"}"""),
            (converted.Entries[1].ToolUseId, converted.Entries[1].ToolName, converted.Entries[1].ToolInput!.ToJsonString()));
    }

    [Fact]
    public void A_tool_result_is_given_for_its_call_with_images_named()
    {
        var converted = ClaudeCodeStreamJson.Convert(
            """{"type":"user","message":{"content":[{"type":"tool_result","tool_use_id":"toolu_1","content":[{"type":"text","text":"found 2"},{"type":"image","source":{}}]}]}}""");

        Assert.Empty(converted.Entries);
        Assert.Equal(new ToolResultArrived("toolu_1", "found 2\n[image]"), Assert.Single(converted.Results));
    }

    [Fact]
    public void The_end_says_how_it_ended_after_how_many_turns_and_what_it_cost()
    {
        var success = ClaudeCodeStreamJson.Convert("""{"type":"result","subtype":"success","is_error":false,"num_turns":2,"total_cost_usd":0.0123,"result":"Done."}""");
        var failed = ClaudeCodeStreamJson.Convert("""{"type":"result","subtype":"error_during_execution","is_error":true,"num_turns":1,"result":"It broke."}""");

        Assert.Equal("Ended: success, after 2 turns, costing $0.0123.", Assert.Single(success.Entries).Text);
        Assert.Equal("Ended: error_during_execution, after 1 turn. It broke.", Assert.Single(failed.Entries).Text);
    }

    [Fact]
    public void A_line_that_is_not_JSON_is_kept_as_a_system_entry_and_one_Sofa_has_no_use_for_is_left_out()
    {
        Assert.Equal("sh: claude: not found", Assert.Single(ClaudeCodeStreamJson.Convert("sh: claude: not found").Entries).Text);
        Assert.Empty(ClaudeCodeStreamJson.Convert("""{"type":"stream_event","event":{}}""").Entries);
        Assert.Empty(ClaudeCodeStreamJson.Convert("   ").Entries);
    }

    [Fact]
    public void A_line_with_a_timestamp_carries_its_time()
    {
        var converted = ClaudeCodeStreamJson.Convert("""{"type":"user","timestamp":"2026-11-27T10:00:00Z","message":{"content":"Go on."}}""");

        Assert.Equal(new DateTimeOffset(2026, 11, 27, 10, 0, 0, TimeSpan.Zero), converted.At);
        Assert.Equal((TranscriptRole.User, "Go on."), (converted.Entries[0].Role, converted.Entries[0].Text));
    }
}
