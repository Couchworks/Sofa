using Sofa.Contracts.AgentGateway;
using Sofa.Contracts.Code;
using Sofa.Contracts.Execution;
using Sofa.Execution.Runners;
using Sofa.Execution.Runs;

namespace Sofa.Execution.Workers;

public sealed record RunSecrets(string RunToken, string? ApiKey, RepositoryToken Repositories);

public static class WorkerCommand
{
    public const string WhereClaudeCodeKeepsItsSignIn = "/claude";
    public const string GoAhead = "start";
    public const string DontStart = "stop";
    public const int ExitWithoutStarting = 75;
    public const string RunLabel = "sofa.run";

    public const string ContainerNamePrefix = "sofa-run-";

    public const string WhereTheRunWorks = "/work";

    public static readonly IReadOnlyList<string> NeverReachable =
    [
        $"Read(/{WhereClaudeCodeKeepsItsSignIn}/**)",
        $"Edit(/{WhereClaudeCodeKeepsItsSignIn}/**)",
        $"Write(/{WhereClaudeCodeKeepsItsSignIn}/**)",
        "Read(//proc/**)",
        "Edit(**/.git/**)",
        "Write(**/.git/**)",
        "Edit(**/.claude/**)",
        "Write(**/.claude/**)",
        "Bash(git diff --no-index:*)",
    ];

    public static string ContainerNameOf(Guid runId) => $"{ContainerNamePrefix}{runId}";

    public static readonly string Script = $$"""
        set -u
        umask 077
        mkdir -p /tmp/sofa
        printf '{"mcpServers":{"{{SofaTools.Server}}":{"type":"http","url":"%s","headers":{"Authorization":"Bearer %s"} } } }' "$SOFA_MCP_URL" "$SOFA_RUN_TOKEN" > /tmp/sofa/mcp.json
        unset SOFA_RUN_TOKEN
        if command -v git >/dev/null 2>&1; then
          git config --global credential.https://github.com.helper '!f() { test "$1" = get && echo username=x-access-token && echo "password=$GH_TOKEN"; }; f'
          git config --global user.name "Sofa Run $SOFA_RUN_ID"
          git config --global user.email "run-$SOFA_RUN_ID@runs.sofa.invalid"
        fi
        if [ "${SOFA_CHECK_SIGN_IN:-}" = yes ]; then
          claude auth status --json 2>/dev/null | tr -d '\n'
          echo
          read -r go || exit {{ExitWithoutStarting}}
          [ "$go" = {{GoAhead}} ] || exit {{ExitWithoutStarting}}
        fi
        exec claude -p "$SOFA_BRIEF" --model "$SOFA_MODEL" --mcp-config /tmp/sofa/mcp.json --strict-mcp-config --output-format stream-json --verbose --no-session-persistence --disallowedTools {{string.Join(' ', NeverReachable.Select(rule => $"'{rule}'"))}} --allowedTools "$@" < /dev/null
        """;

    public static WorkerContainerSpec For(Run run, Runner runner, RunSecrets secrets, WorkerPlacement placement, WorkerOptions options)
    {
        var onASubscription = runner.Kind == RunnerKind.Subscription;
        if (onASubscription == secrets.ApiKey is not null)
        {
            throw new InvalidOperationException("A subscription Run spends its volume's sign-in, and an API key Run its key: never both, never neither.");
        }

        var environment = new Dictionary<string, string>
        {
            ["SOFA_MCP_URL"] = new Uri(placement.AgentGateway, SofaTools.McpPath).ToString(),
            ["SOFA_RUN_TOKEN"] = secrets.RunToken,
            ["SOFA_RUN_ID"] = run.Id.ToString(),
            ["SOFA_BRIEF"] = BriefFor(run.Brief, SofaTools.EveryRunsTools, secrets.Repositories),
            ["SOFA_MODEL"] = run.Model,
            ["GH_TOKEN"] = secrets.Repositories.Token,
            ["ENABLE_TOOL_SEARCH"] = "false",
            ["CLAUDE_CODE_DISABLE_NONESSENTIAL_TRAFFIC"] = "1",
        };
        if (onASubscription)
        {
            environment["SOFA_CHECK_SIGN_IN"] = "yes";
        }
        else
        {
            environment["ANTHROPIC_API_KEY"] = secrets.ApiKey!;
        }

        return new WorkerContainerSpec(
            ContainerNameOf(run.Id),
            options.Image,
            onASubscription ? new WorkerVolume(runner.WorkerVolume!, WhereClaudeCodeKeepsItsSignIn) : null,
            environment,
            ["sh", "-c", Script, "sofa-run", .. SandboxTools.AllowedIn(run.Sandbox)],
            new Dictionary<string, string> { [RunLabel] = run.Id.ToString() },
            placement.Network);
    }

    public static string BriefFor(string brief, IReadOnlyList<SofaTool> tools, RepositoryToken repositories) =>
        $"""
        {brief.TrimEnd()}

        Sofa's tools for this Run are ready to call, with no need to search for them:
        {string.Join('\n', tools.Select(tool => $"- {tool.AsClaudeCodeNamesIt}: {tool.Purpose}"))}

        {WhatTheRunMayDoWith(repositories)}
        """;

    public static string WhatTheRunMayDoWith(RepositoryToken repositories) =>
        $"This Run's Repositories, which git reaches over HTTPS with the token Sofa gave it: "
        + string.Join(", ", repositories.Repositories.Select(repository => $"https://github.com/{repository}.git"))
        + (repositories is { Access: RepositoryAccess.PushItsOwnBranch, PushesTo: { } branch }
            ? $". It may push only its own branch, {branch}: Sofa undoes a push to any other branch and ends the Run."
            : ". It may read them, never push.");
}
