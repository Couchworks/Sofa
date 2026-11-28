using Sofa.AgentGateway.Mcp;
using Sofa.Contracts.Flow;

namespace Sofa.UnitTests.AgentGateway;

public class ToolsOfTheSandboxTests
{
    [Theory]
    [InlineData("read_work_item")]
    [InlineData("ask")]
    [InlineData("finish")]
    public void A_read_only_Run_may_call_Sofas_tools(string tool)
    {
        Assert.True(ToolsOfTheSandbox.Allows(Sandbox.ReadOnly, tool));
    }

    [Theory]
    [InlineData("end_test_run")]
    [InlineData("search_knowledge")]
    [InlineData(null)]
    public void A_tool_no_Sandbox_allows_is_never_called(string? tool)
    {
        Assert.False(ToolsOfTheSandbox.Allows(Sandbox.OwnBranch, tool));
    }
}
