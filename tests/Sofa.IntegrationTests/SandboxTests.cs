using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Sofa.Contracts;

namespace Sofa.IntegrationTests;

public class SandboxTests(SofaApp sofa)
{
    private const string Before = "1111111111111111111111111111111111111111";
    private const string After = "2222222222222222222222222222222222222222";
    private const string SandboxRepository = "acme/web";

    private readonly TestRuns _runs = new(sofa);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_finished_Runs_Transcript_is_read_as_entries_with_a_planted_secret_and_the_Runs_own_secrets_scrubbed()
    {
        var (nia, niaId) = await _runs.UserOfTheirOwnAsync(FakeGitHub.Nia);
        var apiKey = $"sk-ant-api03-nia-{Guid.NewGuid():N}";
        await _runs.AddRunnerAsync(nia, "api-key", new { name = "Nia's key", apiKey, forTheTeam = false });
        var planted = "ghp_" + new string('P', 36);

        var runId = await _runs.StartAsync(niaId, "Triage", $"STUB: say {planted} STUB: tell its secrets");
        await _runs.UntilAsync(nia, runId, r => r.State == "Finished");
        var transcript = await _runs.WholeTranscriptAsync(nia, runId);
        var whole = await _runs.TranscriptAsync(nia, runId);
        var sinceTheEnd = await _runs.TranscriptAsync(nia, runId, after: whole.ReadUpToLine);
        var said = transcript.Single(e => e.Text?.StartsWith("The planted secret", StringComparison.Ordinal) == true);
        var call = transcript.Single(e => e.ToolName == "Bash");
        var told = transcript.Single(e => e.Text?.StartsWith("GH_TOKEN=", StringComparison.Ordinal) == true);

        Assert.DoesNotContain(transcript, e => e.Everything.Contains(planted, StringComparison.Ordinal));
        Assert.DoesNotContain(transcript, e => e.Everything.Contains(apiKey, StringComparison.Ordinal));
        Assert.DoesNotContain(transcript, e => e.Everything.Contains(FakeGitHub.InstallationToken, StringComparison.Ordinal));
        Assert.Equal("The planted secret is [scrubbed].", said.Text);
        Assert.Equal("echo [scrubbed]", call.ToolInput!.Value.GetProperty("command").GetString());
        Assert.Equal("[scrubbed]", call.ToolResult);
        Assert.True(call.ToolResultLine > call.Line);
        Assert.Equal("GH_TOKEN=[scrubbed] ANTHROPIC_API_KEY=[scrubbed]", told.Text);
        Assert.Equal(("System", "Assistant"), (transcript[0].Role, said.Role));
        Assert.Empty(sinceTheEnd.Entries);
    }

    [Fact]
    public async Task A_read_only_Runs_token_may_only_read_and_an_Implement_Runs_may_push_only_its_own_branch()
    {
        var (oz, ozId) = await _runs.UserOfTheirOwnAsync(FakeGitHub.Oz);
        await _runs.AddRunnerAsync(oz, "api-key", new { name = "Oz's key", apiKey = "sk-ant-api03-oz", forTheTeam = false });

        var reading = await _runs.StartAsync(ozId, "Triage");
        var pushing = await _runs.StartAsync(ozId, "Implement");
        await _runs.UntilAsync(oz, reading, r => r.State == "Finished");
        await _runs.UntilAsync(oz, pushing, r => r.State == "Finished");
        var readingLogged = await _runs.LoggedByAsync(SofaServices.Code, line => line.Contains($"Run {reading} may", StringComparison.Ordinal));
        var pushingLogged = await _runs.LoggedByAsync(SofaServices.Code, line => line.Contains($"Run {pushing} may", StringComparison.Ordinal));

        Assert.Contains("may Read in acme/demo,", readingLogged);
        Assert.Contains($"may PushItsOwnBranch in acme/demo, pushing sofa/test-run-{pushing:N},", pushingLogged);
        Assert.Contains(sofa.GitHub.TokenRequests, request => Asks(request, "demo", contents: "read", pullRequests: "read"));
        Assert.Contains(sofa.GitHub.TokenRequests, request => Asks(request, "demo", contents: "write", pullRequests: "write"));
        Assert.DoesNotContain(sofa.GitHub.TokenRequests, request => Asks(request, "demo", contents: "read", pullRequests: "write"));
    }

    [Fact]
    public async Task A_push_by_Sofas_App_to_another_branch_than_the_Runs_own_is_undone_and_ends_the_Run()
    {
        var (pia, piaId) = await _runs.UserOfTheirOwnAsync(FakeGitHub.Pia);
        await _runs.AddRunnerAsync(pia, "api-key", new { name = "Pia's key", apiKey = "sk-ant-api03-pia", forTheTeam = false });
        var project = await ProjectOnAsync("PUSH", SandboxRepository);

        var runId = await _runs.StartAsync(piaId, "Implement", "STUB: hang", projectKey: project.Key);
        await _runs.RunningContainerAsync(pia, runId);
        await GitHubWebhooks.PushedAsync(sofa, SandboxRepository, $"refs/heads/sofa/test-run-{runId:N}", Before, After, FakeGitHub.AppBot, created: true);
        await GitHubWebhooks.PushedAsync(sofa, SandboxRepository, "refs/heads/main", After, Before, FakeGitHub.Ada.Login);
        await GitHubWebhooks.PushedAsync(sofa, SandboxRepository, "refs/heads/main", Before, After, FakeGitHub.AppBot);
        var refused = await _runs.UntilAsync(pia, runId, r => r.State == "Refused");
        var putBack = sofa.GitHub.RefChanges.Where(change => change.Repository == SandboxRepository).ToList();

        Assert.Equal("LeftItsSandbox", refused.RefusedAs);
        Assert.Contains("refs/heads/main in acme/web", refused.Why);
        Assert.Contains("Code put the branch back.", refused.Why);
        var change = Assert.Single(putBack);
        Assert.Equal(("refs/heads/main", "PATCH"), (change.Ref, change.Method));
        Assert.Equal(Before, JsonDocument.Parse(change.Body).RootElement.GetProperty("sha").GetString());
        Assert.True(JsonDocument.Parse(change.Body).RootElement.GetProperty("force").GetBoolean());
    }

    [Fact]
    public async Task A_pull_request_naming_DEMO_1_merged_on_GitHub_becomes_Codes_merged_event_for_DEMO_1()
    {
        await sofa.TheDemoProject;
        using var ada = await sofa.AdaSignedInAsync(Cancellation);
        var pullRequests = $"/api/workspaces/{(await sofa.SetUp).FirstUser.WorkspaceId}/projects/DEMO/work-items/DEMO-1/pull-requests";
        await UntilCodeKnowsTheProjectAsync(ada, pullRequests);

        await GitHubWebhooks.PullRequestAsync(sofa, TestProjects.DemoRepository, 41, "opened", "DEMO-1: Add a dark mode toggle", "sofa/DEMO-1");
        var opened = await _runs.LoggedByAsync(SofaServices.Code, line => line.Contains("PullRequestOpened for DEMO-1", StringComparison.Ordinal));
        sofa.GitHub.PullRequests[(TestProjects.DemoRepository, 41)] = new("DEMO-1: Add a dark mode toggle", "sofa/DEMO-1", Merged: true, Closed: true);
        await GitHubWebhooks.PullRequestAsync(sofa, TestProjects.DemoRepository, 41, "closed", "DEMO-1: Add a dark mode toggle", "sofa/DEMO-1", merged: true);
        var merged = await _runs.LoggedByAsync(SofaServices.Code, line => line.Contains("PullRequestMerged for DEMO-1", StringComparison.Ordinal));
        await GitHubWebhooks.PullRequestAsync(sofa, TestProjects.DemoRepository, 42, "closed", "Try another toggle", "sofa/DEMO-2");
        var closed = await _runs.LoggedByAsync(SofaServices.Code, line => line.Contains("PullRequestClosedUnmerged for DEMO-2", StringComparison.Ordinal));
        var listed = await PullRequestsAsync(ada, pullRequests);

        Assert.Contains("#41", opened);
        Assert.Contains("#41", merged);
        Assert.Contains("#42", closed);
        var pull = Assert.Single(listed);
        Assert.Equal((41L, "Merged", "DEMO-1", "sofa/DEMO-1", "main"), (pull.Number, pull.State, pull.WorkItemKey, pull.HeadBranch, pull.BaseBranch));
        Assert.NotNull(pull.ClosedAt);
    }

    [Fact]
    public async Task A_Worker_container_reaches_the_Agent_Gateway_and_GitHub_but_no_other_port_of_the_box()
    {
        var (nia, niaId) = await _runs.UserOfTheirOwnAsync(FakeGitHub.Nia);
        await _runs.AddRunnerAsync(nia, "api-key", new { name = "Nia's other key", apiKey = $"sk-ant-api03-nia-other-{Guid.NewGuid():N}", forTheTeam = false });
        var boxPort = sofa.AddressOf(SofaServices.Execution).Port;
        var probes = new[]
        {
            "https://github.com",
            "https://example.com",
            $"tcp:host.docker.internal:{boxPort}",
            $"tcp:@github.com:{boxPort}",
            "tcp:@github.com:22",
        };

        var runId = await _runs.StartAsync(niaId, "Triage", $"STUB: probe {string.Join(' ', probes)}");
        var finished = await _runs.UntilAsync(nia, runId, r => r.State == "Finished");
        var seen = (await _runs.WholeTranscriptAsync(nia, runId))
            .Where(e => e.Text?.StartsWith("probe ", StringComparison.Ordinal) == true)
            .ToDictionary(e => e.Text!["probe ".Length..e.Text!.IndexOf(": ", StringComparison.Ordinal)], e => e.Text![(e.Text!.IndexOf(": ", StringComparison.Ordinal) + 2)..]);

        Assert.Equal("The stub played back a recorded Run.", finished.Summary);
        Assert.StartsWith("reached", seen["https://github.com"]);
        Assert.StartsWith("failed", seen["https://example.com"]);
        Assert.StartsWith("failed", seen[$"tcp:host.docker.internal:{boxPort}"]);
        Assert.StartsWith("failed", seen[$"tcp:@github.com:{boxPort}"]);
        Assert.StartsWith("failed", seen["tcp:@github.com:22"]);
    }

    private static bool Asks(JsonElement request, string repository, string contents, string pullRequests) =>
        request.TryGetProperty("repositories", out var repositories)
        && repositories.EnumerateArray().Select(r => r.GetString()).SequenceEqual([repository])
        && request.TryGetProperty("permissions", out var permissions)
        && permissions.TryGetProperty("contents", out var contentsLevel) && contentsLevel.GetString() == contents
        && permissions.TryGetProperty("pull_requests", out var pullsLevel) && pullsLevel.GetString() == pullRequests;

    private async Task<ProjectForRuns> ProjectOnAsync(string key, string repository)
    {
        var workspaceId = (await sofa.SetUp).FirstUser.WorkspaceId;
        var connectionId = (await sofa.TheConnection).ConnectionId;
        using var ada = await sofa.AdaSignedInAsync(Cancellation);
        using var created = await ada.PostAsJsonAsync($"/api/workspaces/{workspaceId}/projects", new
        {
            key,
            name = $"Pushes of {key}",
            binding = new { connectionId, requestRepository = repository, label = "sofa", codeRepositories = new[] { repository } },
        }, Cancellation);
        created.EnsureSuccessStatusCode();
        return await ProjectForRuns.RegisteredAsync(ada, workspaceId, key, Cancellation);
    }

    private static async Task UntilCodeKnowsTheProjectAsync(WebClientSignIn user, string pullRequests)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(TestRuns.RunsSettleWithin);
        while (true)
        {
            using var read = await user.GetWithTokenAsync(pullRequests, timeout.Token);
            if (read.StatusCode == HttpStatusCode.OK)
            {
                return;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), timeout.Token);
        }
    }

    private static async Task<List<PullRequest>> PullRequestsAsync(WebClientSignIn user, string pullRequests)
    {
        using var read = await user.GetWithTokenAsync(pullRequests, Cancellation);
        read.EnsureSuccessStatusCode();
        return (await read.Content.ReadFromJsonAsync<List<PullRequest>>(Cancellation))!;
    }

    private sealed record PullRequest(
        long Number, string Title, string State, string WorkItemKey, string HeadBranch, string BaseBranch, DateTimeOffset? ClosedAt);
}
