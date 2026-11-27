using Sofa.Contracts.AgentGateway;
using Sofa.Contracts.Flow;

namespace Sofa.Execution.Workers;

public static class SandboxTools
{
    private static readonly string[] Reading =
    [
        "Read", "Glob", "Grep",
        "Bash(git clone:*)", "Bash(git fetch:*)", "Bash(git pull:*)", "Bash(git status:*)", "Bash(git log:*)",
        "Bash(git show:*)", "Bash(git diff:*)", "Bash(git branch:*)", "Bash(git checkout:*)", "Bash(git switch:*)",
    ];

    private static readonly string[] ChangingItsOwnBranch =
    [
        "Edit", "Write",
        "Bash(git add:*)", "Bash(git commit:*)", "Bash(git push:*)", "Bash(git restore:*)", "Bash(git rm:*)",
        "Bash(git mv:*)", "Bash(git stash:*)", "Bash(git merge:*)", "Bash(git rebase:*)", "Bash(git reset:*)",
    ];

    public static IReadOnlyList<string> AllowedIn(Sandbox sandbox) =>
    [
        .. SofaTools.EveryRunsTools.Select(tool => tool.AsClaudeCodeNamesIt),
        .. Reading,
        .. sandbox switch
        {
            Sandbox.ReadOnly => [],
            Sandbox.OwnBranch or Sandbox.IntegrationBranch => ChangingItsOwnBranch,
            _ => throw new ArgumentOutOfRangeException(nameof(sandbox), sandbox, "Sofa knows no such Sandbox."),
        },
    ];
}
