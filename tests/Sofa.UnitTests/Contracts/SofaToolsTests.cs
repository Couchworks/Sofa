using Sofa.Contracts.AgentGateway;
using Sofa.Contracts.Flow;

namespace Sofa.UnitTests.Contracts;

public class SofaToolsTests
{
    [Theory]
    [InlineData(Sandbox.ReadOnly)]
    [InlineData(Sandbox.OwnBranch)]
    [InlineData(Sandbox.IntegrationBranch)]
    public void Every_Sandbox_of_milestone_1_allows_reading_the_Work_Item_asking_and_finishing(Sandbox sandbox)
    {
        Assert.Equal(["read_work_item", "ask", "finish"], SofaTools.AllowedIn(sandbox).Select(tool => tool.Name));
    }

    [Fact]
    public void Claude_Code_names_Sofas_tools_after_its_MCP_server()
    {
        Assert.Equal("mcp__sofa__finish", SofaTools.Finish.AsClaudeCodeNamesIt);
    }

    [Fact]
    public void The_test_tool_of_slice_6_is_gone()
    {
        Assert.DoesNotContain(SofaTools.All, tool => tool.Name == "end_test_run");
    }
}
