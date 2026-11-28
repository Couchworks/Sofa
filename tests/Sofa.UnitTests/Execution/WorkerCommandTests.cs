using Sofa.Contracts.Execution;
using Sofa.Contracts.Flow;
using Sofa.Execution.Runners;
using Sofa.Execution.Runs;
using Sofa.Execution.Workers;

namespace Sofa.UnitTests.Execution;

public class WorkerCommandTests
{
    private const string RunToken = "eyJ.run-token.sig";
    private const string ApiKey = "sk-ant-api03-secret";

    private static readonly WorkerOptions Options = new() { Image = "sofa-worker", AgentGatewayAddress = "http://host.docker.internal:5159" };

    private static Runner RunnerOf(RunnerKind kind) => new()
    {
        Id = Guid.NewGuid(),
        WorkspaceId = Guid.NewGuid(),
        Name = "Mine",
        Kind = kind,
        Vendor = Vendor.Anthropic,
        OwnerUserId = Guid.NewGuid(),
        RunsAtOnce = 1,
        ApiKeyId = kind == RunnerKind.ApiKey ? Guid.NewGuid() : null,
        WorkerVolume = kind == RunnerKind.Subscription ? "sofa-worker-mine" : null,
    };

    private static Run RunIn(Sandbox sandbox, string model = "haiku") =>
        Run.From(new StartRun(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), PhaseName.Implement, Guid.NewGuid(),
            Vendor.Anthropic, model, sandbox, Guid.NewGuid(), Priority.None, "Build it.", 2), DateTimeOffset.UnixEpoch);

    private static IReadOnlyList<string> AllowedTools(WorkerContainerSpec spec) => spec.Command.Skip(4).ToList();

    [Fact]
    public void A_subscription_Run_mounts_its_Users_volume_checks_the_sign_in_and_holds_no_API_key()
    {
        var spec = WorkerCommand.For(RunIn(Sandbox.ReadOnly), RunnerOf(RunnerKind.Subscription), new RunSecrets(RunToken, null), Options);

        Assert.Equal(new WorkerVolume("sofa-worker-mine", "/claude"), spec.Volume);
        Assert.Equal("yes", spec.Environment["SOFA_CHECK_SIGN_IN"]);
        Assert.DoesNotContain("ANTHROPIC_API_KEY", spec.Environment.Keys);
    }

    [Fact]
    public void An_API_key_Run_mounts_no_volume_and_has_its_key()
    {
        var spec = WorkerCommand.For(RunIn(Sandbox.ReadOnly), RunnerOf(RunnerKind.ApiKey), new RunSecrets(RunToken, ApiKey), Options);

        Assert.Null(spec.Volume);
        Assert.Equal(ApiKey, spec.Environment["ANTHROPIC_API_KEY"]);
        Assert.DoesNotContain("SOFA_CHECK_SIGN_IN", spec.Environment.Keys);
    }

    [Fact]
    public void A_subscription_Run_with_an_API_key_is_never_started()
    {
        Assert.Throws<InvalidOperationException>(() =>
            WorkerCommand.For(RunIn(Sandbox.ReadOnly), RunnerOf(RunnerKind.Subscription), new RunSecrets(RunToken, ApiKey), Options));
    }

    [Fact]
    public void Claude_Code_runs_headless_on_the_Agents_model_with_only_Sofas_MCP_server_and_never_bare()
    {
        var spec = WorkerCommand.For(RunIn(Sandbox.ReadOnly, "claude-opus-5-5"), RunnerOf(RunnerKind.Subscription), new RunSecrets(RunToken, null), Options);

        Assert.Equal("claude-opus-5-5", spec.Environment["SOFA_MODEL"]);
        Assert.Contains("--model \"$SOFA_MODEL\"", WorkerCommand.Script);
        Assert.Contains("--strict-mcp-config", WorkerCommand.Script);
        Assert.Contains("--output-format stream-json", WorkerCommand.Script);
        Assert.DoesNotContain("--bare", WorkerCommand.Script);
        Assert.DoesNotContain("--bare", spec.Command);
        Assert.Equal("http://host.docker.internal:5159/mcp", spec.Environment["SOFA_MCP_URL"]);
        Assert.Equal("false", spec.Environment["ENABLE_TOOL_SEARCH"]);
    }

    [Fact]
    public void No_Run_may_read_the_sign_in_on_its_volume_or_the_environment_of_its_processes()
    {
        Assert.Equal(["Read(//claude/**)", "Edit(//claude/**)", "Read(//proc/**)"], WorkerCommand.NeverReachable);
        Assert.Contains("--disallowedTools 'Read(//claude/**)' 'Edit(//claude/**)' 'Read(//proc/**)' --allowedTools", WorkerCommand.Script);
    }

    [Fact]
    public void A_read_only_Run_may_read_and_call_Sofa_but_not_change_files()
    {
        var tools = AllowedTools(WorkerCommand.For(RunIn(Sandbox.ReadOnly), RunnerOf(RunnerKind.ApiKey), new RunSecrets(RunToken, ApiKey), Options));

        Assert.Equal(["mcp__sofa__read_work_item", "mcp__sofa__ask", "mcp__sofa__finish", "Read", "Glob", "Grep"], tools);
    }

    [Fact]
    public void A_Run_on_its_own_branch_may_also_edit_and_use_git()
    {
        var tools = AllowedTools(WorkerCommand.For(RunIn(Sandbox.OwnBranch), RunnerOf(RunnerKind.ApiKey), new RunSecrets(RunToken, ApiKey), Options));

        Assert.Contains("Edit", tools);
        Assert.Contains("Bash(git:*)", tools);
        Assert.DoesNotContain("Bash", tools);
    }

    [Fact]
    public void The_Brief_names_Sofas_tools_so_the_Run_needs_no_turn_to_find_them()
    {
        var spec = WorkerCommand.For(RunIn(Sandbox.ReadOnly), RunnerOf(RunnerKind.ApiKey), new RunSecrets(RunToken, ApiKey), Options);

        Assert.StartsWith("Build it.", spec.Environment["SOFA_BRIEF"]);
        Assert.Contains("- mcp__sofa__read_work_item: reads this Run's Work Item", spec.Environment["SOFA_BRIEF"]);
        Assert.Contains("- mcp__sofa__ask: ends this Run with a Question", spec.Environment["SOFA_BRIEF"]);
        Assert.Contains("- mcp__sofa__finish: ends this Run with one of its Phase's Outcomes", spec.Environment["SOFA_BRIEF"]);
        Assert.DoesNotContain("end_test_run", spec.Environment["SOFA_BRIEF"]);
    }

    [Fact]
    public void The_Brief_names_its_Phases_Outcomes_and_where_attachments_come_from()
    {
        var brief = WorkerCommand.For(RunIn(Sandbox.OwnBranch), RunnerOf(RunnerKind.ApiKey), new RunSecrets(RunToken, ApiKey), Options)
            .Environment["SOFA_BRIEF"];

        Assert.Contains("- `implemented`: the change is built and its pull request opened", brief);
        Assert.DoesNotContain("`small`", brief);
        Assert.Contains("your working directory, /work", brief);
    }

    [Fact]
    public void An_attachment_is_copied_out_of_the_container_by_its_path_as_an_argument_never_in_the_script()
    {
        var command = WorkerCommand.CopyingOutAttachment("/work/a'; rm -rf / #.png", 1000);

        Assert.Equal(["sh", "-c"], command.Take(2));
        Assert.DoesNotContain("rm -rf", command[2]);
        Assert.Contains("realpath -e", command[2]);
        Assert.Contains("head -c 1001", command[2]);
        Assert.Equal("/work/a'; rm -rf / #.png", command[^1]);
    }

    [Fact]
    public void Docker_is_given_the_names_of_the_secrets_never_their_values()
    {
        var spec = WorkerCommand.For(RunIn(Sandbox.ReadOnly), RunnerOf(RunnerKind.ApiKey), new RunSecrets(RunToken, ApiKey), Options);

        var arguments = DockerWorkerContainers.RunArguments(spec);

        Assert.DoesNotContain(arguments, argument => argument.Contains(RunToken, StringComparison.Ordinal));
        Assert.DoesNotContain(arguments, argument => argument.Contains(ApiKey, StringComparison.Ordinal));
        Assert.Contains("SOFA_RUN_TOKEN", arguments);
        Assert.Contains($"sofa.run={spec.Labels["sofa.run"]}", arguments);
        Assert.Equal(["sofa-worker", "sh", "-c", WorkerCommand.Script], arguments.SkipWhile(a => a != "sofa-worker").Take(4));
    }

    [Fact]
    public void A_container_that_ended_before_its_Run_says_how()
    {
        Assert.Equal("Its container ended with exit code 137 before the Run was ended.", RunsInContainers.WhyItFailed(new WorkerExit(137, "")));
        Assert.EndsWith(": out of memory", RunsInContainers.WhyItFailed(new WorkerExit(1, "out of memory\n")));
    }
}
