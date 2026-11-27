using Sofa.Contracts.Code;
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

    private const string RepositoryTokenValue = "ghs_repository-token";

    private static readonly WorkerOptions Options = new() { Image = "sofa-worker", AgentGatewayAddress = "http://host.docker.internal:5159" };

    private static readonly WorkerPlacement Placement =
        EgressRelay.PlacementBehind("sofa-workers-5159", "172.30.0.2", ["api.anthropic.com", "github.com"]);

    private static readonly RepositoryToken ReadOnly =
        new(RepositoryTokenValue, DateTimeOffset.UnixEpoch, ["acme/demo"], RepositoryAccess.Read, null);

    private static readonly RepositoryToken PushingItsOwnBranch =
        new(RepositoryTokenValue, DateTimeOffset.UnixEpoch, ["acme/demo", "acme/api"], RepositoryAccess.PushItsOwnBranch, "sofa/DEMO-1");

    private static RunSecrets Secrets(string? apiKey, RepositoryToken? repositories = null) =>
        new(RunToken, apiKey, repositories ?? ReadOnly);

    private static WorkerContainerSpec SpecOf(Run run, Runner runner, RunSecrets secrets) =>
        WorkerCommand.For(run, runner, secrets, Placement, Options);

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
        var spec = SpecOf(RunIn(Sandbox.ReadOnly), RunnerOf(RunnerKind.Subscription), Secrets(null));

        Assert.Equal(new WorkerVolume("sofa-worker-mine", "/claude"), spec.Volume);
        Assert.Equal("yes", spec.Environment["SOFA_CHECK_SIGN_IN"]);
        Assert.DoesNotContain("ANTHROPIC_API_KEY", spec.Environment.Keys);
    }

    [Fact]
    public void An_API_key_Run_mounts_no_volume_and_has_its_key()
    {
        var spec = SpecOf(RunIn(Sandbox.ReadOnly), RunnerOf(RunnerKind.ApiKey), Secrets(ApiKey));

        Assert.Null(spec.Volume);
        Assert.Equal(ApiKey, spec.Environment["ANTHROPIC_API_KEY"]);
        Assert.DoesNotContain("SOFA_CHECK_SIGN_IN", spec.Environment.Keys);
    }

    [Fact]
    public void A_subscription_Run_with_an_API_key_is_never_started()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SpecOf(RunIn(Sandbox.ReadOnly), RunnerOf(RunnerKind.Subscription), Secrets(ApiKey)));
    }

    [Fact]
    public void Claude_Code_runs_headless_on_the_Agents_model_with_only_Sofas_MCP_server_and_never_bare()
    {
        var spec = SpecOf(RunIn(Sandbox.ReadOnly, "claude-opus-5-5"), RunnerOf(RunnerKind.Subscription), Secrets(null));

        Assert.Equal("claude-opus-5-5", spec.Environment["SOFA_MODEL"]);
        Assert.Contains("--model \"$SOFA_MODEL\"", WorkerCommand.Script);
        Assert.Contains("--strict-mcp-config", WorkerCommand.Script);
        Assert.Contains("--output-format stream-json", WorkerCommand.Script);
        Assert.DoesNotContain("--bare", WorkerCommand.Script);
        Assert.DoesNotContain("--bare", spec.Command);
        Assert.Equal("http://172.30.0.2:8080/mcp", spec.Environment["SOFA_MCP_URL"]);
        Assert.Equal("false", spec.Environment["ENABLE_TOOL_SEARCH"]);
    }

    [Fact]
    public void No_Run_may_read_or_change_the_sign_in_on_its_volume_or_read_the_environment_of_its_processes()
    {
        Assert.Contains("Read(//claude/**)", WorkerCommand.NeverReachable);
        Assert.Contains("Edit(//claude/**)", WorkerCommand.NeverReachable);
        Assert.Contains("Write(//claude/**)", WorkerCommand.NeverReachable);
        Assert.Contains("Read(//proc/**)", WorkerCommand.NeverReachable);
        Assert.Contains("--disallowedTools 'Read(//claude/**)' 'Edit(//claude/**)' 'Write(//claude/**)' 'Read(//proc/**)'", WorkerCommand.Script);
    }

    [Fact]
    public void No_Run_may_change_gits_or_Claude_Codes_configuration_which_would_run_commands_of_its_own()
    {
        Assert.Contains("Edit(**/.git/**)", WorkerCommand.NeverReachable);
        Assert.Contains("Write(**/.git/**)", WorkerCommand.NeverReachable);
        Assert.Contains("Edit(**/.claude/**)", WorkerCommand.NeverReachable);
        Assert.Contains("Bash(git diff --no-index:*)", WorkerCommand.NeverReachable);
    }

    [Fact]
    public void A_read_only_Run_may_read_clone_and_call_Sofa_but_not_change_files_or_commit()
    {
        var tools = AllowedTools(SpecOf(RunIn(Sandbox.ReadOnly), RunnerOf(RunnerKind.ApiKey), Secrets(ApiKey)));

        Assert.Equal(["mcp__sofa__end_test_run", "Read", "Glob", "Grep"], tools.Take(4));
        Assert.Contains("Bash(git clone:*)", tools);
        Assert.DoesNotContain("Edit", tools);
        Assert.DoesNotContain("Bash(git commit:*)", tools);
        Assert.DoesNotContain("Bash(git push:*)", tools);
        Assert.DoesNotContain(tools, tool => tool is "Bash" or "Bash(git:*)");
    }

    [Fact]
    public void A_Run_on_its_own_branch_may_also_edit_and_use_git()
    {
        var tools = AllowedTools(SpecOf(RunIn(Sandbox.OwnBranch), RunnerOf(RunnerKind.ApiKey), Secrets(ApiKey)));

        Assert.Contains("Edit", tools);
        Assert.Contains("Bash(git commit:*)", tools);
        Assert.Contains("Bash(git push:*)", tools);
        Assert.DoesNotContain(tools, tool => tool is "Bash" or "Bash(git:*)" or "Bash(git config:*)");
    }

    [Fact]
    public void The_Brief_names_Sofas_tools_so_the_Run_needs_no_turn_to_find_them()
    {
        var spec = SpecOf(RunIn(Sandbox.ReadOnly), RunnerOf(RunnerKind.ApiKey), Secrets(ApiKey));

        Assert.StartsWith("Build it.", spec.Environment["SOFA_BRIEF"]);
        Assert.Contains("- mcp__sofa__end_test_run: ends this Run", spec.Environment["SOFA_BRIEF"]);
    }

    [Fact]
    public void Docker_is_given_the_names_of_the_secrets_never_their_values()
    {
        var spec = SpecOf(RunIn(Sandbox.ReadOnly), RunnerOf(RunnerKind.ApiKey), Secrets(ApiKey));

        var arguments = DockerWorkerContainers.RunArguments(spec);

        Assert.DoesNotContain(arguments, argument => argument.Contains(RunToken, StringComparison.Ordinal));
        Assert.DoesNotContain(arguments, argument => argument.Contains(ApiKey, StringComparison.Ordinal));
        Assert.DoesNotContain(arguments, argument => argument.Contains(RepositoryTokenValue, StringComparison.Ordinal));
        Assert.Contains("GH_TOKEN", arguments);
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

    [Fact]
    public void A_Run_holds_its_Repository_token_for_git_and_its_Brief_says_what_it_may_push()
    {
        var readOnly = SpecOf(RunIn(Sandbox.ReadOnly), RunnerOf(RunnerKind.ApiKey), Secrets(ApiKey));
        var pushing = SpecOf(RunIn(Sandbox.OwnBranch), RunnerOf(RunnerKind.ApiKey), Secrets(ApiKey, PushingItsOwnBranch));

        Assert.Equal(RepositoryTokenValue, pushing.Environment["GH_TOKEN"]);
        Assert.Contains("credential.https://github.com.helper", WorkerCommand.Script);
        Assert.Contains("https://github.com/acme/demo.git, https://github.com/acme/api.git", pushing.Environment["SOFA_BRIEF"]);
        Assert.Contains("push only its own branch, sofa/DEMO-1", pushing.Environment["SOFA_BRIEF"]);
        Assert.Contains("read them, never push", readOnly.Environment["SOFA_BRIEF"]);
    }

    [Fact]
    public void A_Run_runs_on_the_internal_Worker_network_and_reaches_the_outside_only_through_the_relay()
    {
        var arguments = DockerWorkerContainers.RunArguments(SpecOf(RunIn(Sandbox.ReadOnly), RunnerOf(RunnerKind.ApiKey), Secrets(ApiKey)));

        Assert.Equal("sofa-workers-5159", arguments[arguments.ToList().IndexOf("--network") + 1]);
        Assert.Contains("--add-host=api.anthropic.com:172.30.0.2", arguments);
        Assert.Contains("--add-host=github.com:172.30.0.2", arguments);
        Assert.DoesNotContain(arguments, argument => argument.Contains("host-gateway", StringComparison.Ordinal));
    }
}
