using Sofa.Contracts.AgentGateway;
using Sofa.Contracts.Execution;
using Sofa.Execution.Runners;
using Sofa.Execution.Runs;

namespace Sofa.Execution.Workers;

public sealed record RunSecrets(string RunToken, string? ApiKey);

public static class WorkerCommand
{
    public const string WhereClaudeCodeKeepsItsSignIn = "/claude";
    public const string GoAhead = "start";
    public const string DontStart = "stop";
    public const int ExitWithoutStarting = 75;
    public const string RunLabel = "sofa.run";

    public const string ContainerNamePrefix = "sofa-run-";

    public static readonly IReadOnlyList<string> NeverReachable =
        [$"Read(/{WhereClaudeCodeKeepsItsSignIn}/**)", $"Edit(/{WhereClaudeCodeKeepsItsSignIn}/**)", "Read(//proc/**)"];

    public static string ContainerNameOf(Guid runId) => $"{ContainerNamePrefix}{runId}";

    public static readonly string Script = $$"""
        set -u
        umask 077
        mkdir -p /tmp/sofa
        printf '{"mcpServers":{"{{SofaTools.Server}}":{"type":"http","url":"%s","headers":{"Authorization":"Bearer %s"} } } }' "$SOFA_MCP_URL" "$SOFA_RUN_TOKEN" > /tmp/sofa/mcp.json
        unset SOFA_RUN_TOKEN
        if [ "${SOFA_CHECK_SIGN_IN:-}" = yes ]; then
          claude auth status --json 2>/dev/null | tr -d '\n'
          echo
          read -r go || exit {{ExitWithoutStarting}}
          [ "$go" = {{GoAhead}} ] || exit {{ExitWithoutStarting}}
        fi
        exec claude -p "$SOFA_BRIEF" --model "$SOFA_MODEL" --mcp-config /tmp/sofa/mcp.json --strict-mcp-config --output-format stream-json --verbose --no-session-persistence --disallowedTools {{string.Join(' ', NeverReachable.Select(rule => $"'{rule}'"))}} --allowedTools "$@" < /dev/null
        """;

    public static WorkerContainerSpec For(Run run, Runner runner, RunSecrets secrets, WorkerOptions options)
    {
        var agentGateway = options.AgentGatewayAddress
            ?? throw new InvalidOperationException($"Execution needs {WorkerOptions.Section}:AgentGatewayAddress, where its Workers reach the Agent Gateway.");
        var onASubscription = runner.Kind == RunnerKind.Subscription;
        if (onASubscription == secrets.ApiKey is not null)
        {
            throw new InvalidOperationException("A subscription Run spends its volume's sign-in, and an API key Run its key: never both, never neither.");
        }

        var environment = new Dictionary<string, string>
        {
            ["SOFA_MCP_URL"] = new Uri(new Uri(agentGateway), SofaTools.McpPath).ToString(),
            ["SOFA_RUN_TOKEN"] = secrets.RunToken,
            ["SOFA_BRIEF"] = BriefNamingItsTools(run.Brief, SofaTools.EveryRunsTools),
            ["SOFA_MODEL"] = run.Model,
            ["ENABLE_TOOL_SEARCH"] = "false",
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
            new Dictionary<string, string> { [RunLabel] = run.Id.ToString() });
    }

    public static string BriefNamingItsTools(string brief, IReadOnlyList<SofaTool> tools) =>
        $"""
        {brief.TrimEnd()}

        Sofa's tools for this Run are ready to call, with no need to search for them:
        {string.Join('\n', tools.Select(tool => $"- {tool.AsClaudeCodeNamesIt}: {tool.Purpose}"))}
        """;
}
