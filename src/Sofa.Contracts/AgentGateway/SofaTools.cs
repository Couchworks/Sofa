using Sofa.Contracts.Flow;

namespace Sofa.Contracts.AgentGateway;

public sealed record SofaTool(string Name, string Purpose)
{
    public string AsClaudeCodeNamesIt => $"mcp__{SofaTools.Server}__{Name}";
}

public static class SofaTools
{
    public const string Server = "sofa";
    public const string McpPath = "/mcp";

    public static readonly SofaTool ReadWorkItem = new(
        "read_work_item",
        "reads this Run's Work Item: its Request as it is now on the issue, the Contributions of earlier Runs, and its Questions with their answers; call it first");

    public static readonly SofaTool Ask = new(
        "ask",
        "ends this Run with a Question for a User, when you can't go on without their answer; a new Run reads the answer, so stop once it's asked");

    public static readonly SofaTool Finish = new(
        "finish",
        "ends this Run with one of its Phase's Outcomes and your Contribution in Markdown, with files from your working directory attached, such as screenshots; call it once, last, and stop");

    public static IReadOnlyList<SofaTool> All { get; } = [ReadWorkItem, Ask, Finish];

    public static IReadOnlyList<SofaTool> AllowedIn(Sandbox sandbox) => sandbox switch
    {
        Sandbox.ReadOnly or Sandbox.OwnBranch or Sandbox.IntegrationBranch => [ReadWorkItem, Ask, Finish],
        _ => throw new ArgumentOutOfRangeException(nameof(sandbox), sandbox, "Sofa knows no such Sandbox."),
    };
}
