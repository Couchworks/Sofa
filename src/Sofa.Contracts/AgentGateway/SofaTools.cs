namespace Sofa.Contracts.AgentGateway;

public sealed record SofaTool(string Name, string Purpose)
{
    public string AsClaudeCodeNamesIt => $"mcp__{SofaTools.Server}__{Name}";
}

public static class SofaTools
{
    public const string Server = "sofa";
    public const string McpPath = "/mcp";

    public static readonly SofaTool EndTestRun = new(
        "end_test_run",
        "ends this Run with a one-line summary of what you did; call it once, last, and stop");

    public static IReadOnlyList<SofaTool> EveryRunsTools { get; } = [EndTestRun];
}
