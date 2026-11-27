using Sofa.Contracts.AgentGateway;
using Sofa.Contracts.Flow;

namespace Sofa.Execution.Workers;

public static class SandboxTools
{
    private static readonly string[] Reading = ["Read", "Glob", "Grep"];

    private static readonly string[] ChangingItsOwnBranch = ["Edit", "Write", "Bash(git:*)"];

    public static IReadOnlyList<string> AllowedIn(Sandbox sandbox) =>
    [
        .. SofaTools.AllowedIn(sandbox).Select(tool => tool.AsClaudeCodeNamesIt),
        .. Reading,
        .. sandbox switch
        {
            Sandbox.ReadOnly => [],
            Sandbox.OwnBranch or Sandbox.IntegrationBranch => ChangingItsOwnBranch,
            _ => throw new ArgumentOutOfRangeException(nameof(sandbox), sandbox, "Sofa knows no such Sandbox."),
        },
    ];
}
