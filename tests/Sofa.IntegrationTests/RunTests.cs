using System.Net;
using System.Net.Http.Json;
using Npgsql;
using Sofa.Contracts;

namespace Sofa.IntegrationTests;

public class RunTests(SofaApp sofa)
{
    private static readonly TimeSpan RunsSettleWithin = TimeSpan.FromSeconds(90);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_Run_of_a_test_Brief_finishes_on_a_subscription_Runner_without_an_API_key()
    {
        var (fay, fayId) = await UserOfTheirOwnAsync(FakeGitHub.Fay);
        var runner = await AddRunnerAsync(fay, "subscription", new { name = "Fay's Max" });
        await StubWorker.SignInOnTheVolumeAsync(runner.WorkerVolume!, Cancellation);

        var runId = await StartTestRunAsync(fayId, "Triage");
        var run = await RunUntilAsync(fay, runId, r => r.State == "Finished");
        var transcript = await WholeTranscriptAsync(fay, runId);
        var start = StartOf(transcript);
        var signedIn = (await RunnersSeenByAsync(fay)).Single(r => r.Id == runner.Id).SignIn!;

        Assert.Equal(runner.Id, run.RunnerId);
        Assert.Equal("small", run.Outcome);
        Assert.Contains("started on haiku.", start);
        Assert.Contains("Credentials: none.", start);
        Assert.Contains("mcp__sofa__finish", start);
        Assert.DoesNotContain("mcp__sofa__end_test_run", start);
        Assert.DoesNotContain("Edit", start);
        Assert.StartsWith("Ended: success", transcript.Last().Text);
        Assert.DoesNotContain(transcript, entry => entry.Everything.Contains("stub@example.com", StringComparison.Ordinal));
        Assert.True(signedIn.SignedIn);
        Assert.Equal(new DateTimeOffset(2026, 12, 24, 10, 0, 0, TimeSpan.Zero), signedIn.EndsAt);
    }

    [Fact]
    public async Task A_Run_of_a_test_Brief_finishes_on_a_personal_API_key_Runner()
    {
        var (gus, gusId) = await UserOfTheirOwnAsync(FakeGitHub.Gus);
        var apiKey = $"sk-ant-api03-gus-{Guid.NewGuid():N}";
        var runner = await AddRunnerAsync(gus, "api-key", new { name = "Gus's key", apiKey, forTheTeam = false });

        var runId = await StartTestRunAsync(gusId, "Spec");
        var run = await RunUntilAsync(gus, runId, r => r.State == "Finished");
        var transcript = await WholeTranscriptAsync(gus, runId);

        Assert.Equal(runner.Id, run.RunnerId);
        Assert.Contains("started on opus.", StartOf(transcript));
        Assert.Contains("Credentials: ANTHROPIC_API_KEY.", StartOf(transcript));
        Assert.DoesNotContain(transcript, entry => entry.Everything.Contains(apiKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task A_Run_of_a_delegating_User_without_a_Runner_of_their_own_finishes_on_their_Teams_Runner()
    {
        var (hal, halId) = await UserOfTheirOwnAsync(FakeGitHub.Hal);
        using var ada = await sofa.AdaSignedInAsync(Cancellation);
        var teamRunner = await AddRunnerAsync(ada, "api-key", new { name = "Engines' key for Runs", apiKey = "sk-ant-api03-engines", forTheTeam = true });

        var runId = await StartTestRunAsync(halId, "Implement");
        var run = await RunUntilAsync(hal, runId, r => r.State == "Finished");
        var spent = (await RunnersSeenByAsync(ada)).Single(r => r.Id == run.RunnerId);
        var start = StartOf(await WholeTranscriptAsync(hal, runId));

        Assert.Equal("Team", spent.Owner.Kind);
        Assert.Equal(teamRunner.Owner.Id, spent.Owner.Id);
        Assert.Contains("Edit", start);
        Assert.Contains("Bash(git push:*)", start);
    }

    [Fact]
    public async Task Two_Runs_beyond_a_limit_of_one_run_one_after_the_other()
    {
        var (ida, idaId) = await UserOfTheirOwnAsync(FakeGitHub.Ida);
        await AddRunnerAsync(ida, "api-key", new { name = "Ida's one at a time", apiKey = "sk-ant-api03-ida", runsAtOnce = 1, forTheTeam = false });

        var first = await StartTestRunAsync(idaId, "Triage", "STUB: take 3s");
        await RunUntilAsync(ida, first, r => r.State == "Running");
        var second = await StartTestRunAsync(idaId, "Triage", "STUB: take 3s");
        var secondWaiting = await RunUntilAsync(ida, second, r => r.State == "Waiting" && r.Why is not null);
        var firstDone = await RunUntilAsync(ida, first, r => r.State == "Finished");
        var secondDone = await RunUntilAsync(ida, second, r => r.State == "Finished");

        Assert.Contains("at its limit", secondWaiting.Why);
        Assert.True(secondDone.StartedAt >= firstDone.EndedAt,
            $"The second Run started at {secondDone.StartedAt:O}, before the first ended at {firstDone.EndedAt:O}.");
    }

    [Fact]
    public async Task A_killed_container_ends_as_failed_and_is_retried_until_its_limit_then_waits_for_a_Users_Retry()
    {
        var (jo, joId) = await UserOfTheirOwnAsync(FakeGitHub.Jo);
        await AddRunnerAsync(jo, "api-key", new { name = "Jo's key", apiKey = "sk-ant-api03-jo", forTheTeam = false });

        var first = await StartTestRunAsync(joId, "Triage", "STUB: hang", retryLimit: 1);
        await RunUntilAsync(jo, first, r => r.State == "Running" && r.StartedAt is not null);
        await RunningContainerAsync(jo, first);
        await StubWorker.KillAsync(first, Cancellation);
        var failed = await RunUntilAsync(jo, first, r => r.State == "Failed" && r.RetriedBy is not null);

        var retry = failed.RetriedBy!.Value;
        await RunningContainerAsync(jo, retry);
        await StubWorker.KillAsync(retry, Cancellation);
        var failedAgain = await RunUntilAsync(jo, retry, r => r.State == "Failed");

        using var retriedByJo = await jo.PostAsJsonAsync($"{await WorkspaceApiAsync()}/runs/{retry}/retry", null, Cancellation);
        var third = (await retriedByJo.Content.ReadFromJsonAsync<TestRuns.Run>(Cancellation))!;
        using var stopped = await jo.PostAsJsonAsync($"{await WorkspaceApiAsync()}/runs/{third.Id}/stop", null, Cancellation);
        var thirdStopped = await RunUntilAsync(jo, third.Id, r => r.State == "Stopped");
        using var retryingTheStopped = await jo.PostAsJsonAsync($"{await WorkspaceApiAsync()}/runs/{third.Id}/retry", null, Cancellation);

        Assert.Contains("exit code 137", failed.Why);
        Assert.Equal((2, retry, false), (failed.Attempt + 1, failed.RetriedBy, failed.WaitsForRetry));
        Assert.Equal((2, first, true, (Guid?)null), (failedAgain.Attempt, failedAgain.RetryOf, failedAgain.WaitsForRetry, failedAgain.RetriedBy));
        Assert.Equal(HttpStatusCode.OK, retriedByJo.StatusCode);
        Assert.Equal((3, retry), (third.Attempt, third.RetryOf));
        Assert.Equal(HttpStatusCode.OK, stopped.StatusCode);
        Assert.NotNull(thirdStopped.EndedAt);
        Assert.Equal("Stopped by a User.", thirdStopped.Why);
        Assert.Equal(HttpStatusCode.Conflict, retryingTheStopped.StatusCode);
    }

    [Fact]
    public async Task A_Run_of_an_Agent_Identity_does_not_know_is_refused_naming_the_reason()
    {
        var (max, maxId) = await UserOfTheirOwnAsync(FakeGitHub.Max);
        await AddRunnerAsync(max, "api-key", new { name = "Max's key", apiKey = "sk-ant-api03-max", forTheTeam = false });
        var project = await ProjectForRuns.CreateAsync(sofa, "GONE", Cancellation);
        await ForgetInIdentityAsync(project.Cast("Triager").Id);

        var runId = await StartTestRunAsync(maxId, "Triage", projectKey: "GONE");
        var refused = await RunUntilAsync(max, runId, r => r.State == "Refused");
        var transcript = await TranscriptAsync(max, runId);

        Assert.Equal("AgentNotRegistered", refused.RefusedAs);
        Assert.Contains($"Identity knows no Agent {project.Cast("Triager").Id}", refused.Why);
        Assert.True(refused.WaitsForRetry);
        Assert.Empty(transcript);
    }

    [Fact]
    public async Task A_volume_no_longer_signed_in_refuses_its_Run_as_sign_in_again()
    {
        var (kim, kimId) = await UserOfTheirOwnAsync(FakeGitHub.Kim);
        var runner = await AddRunnerAsync(kim, "subscription", new { name = "Kim's Pro" });
        StubWorker.WillMake(runner.WorkerVolume!);

        var runId = await StartTestRunAsync(kimId, "Triage");
        var refused = await RunUntilAsync(kim, runId, r => r.State == "Refused");
        var seen = (await RunnersSeenByAsync(kim)).Single(r => r.Id == runner.Id).SignIn!;
        var noticed = await NoticesLoggedAsync(line => line.Contains(runner.Id.ToString(), StringComparison.Ordinal) && line.Contains("in again", StringComparison.Ordinal));

        Assert.Contains(runId.ToString(), noticed);
        Assert.Equal("SignInAgain", refused.RefusedAs);
        Assert.Contains("Sign in again", refused.Why);
        Assert.Contains($"sofa runner login {runner.Id}", refused.Why);
        Assert.False(seen.SignedIn);
        Assert.NotNull(seen.CheckedAt);
        Assert.Empty(await TranscriptAsync(kim, runId));
    }

    [Fact]
    public async Task A_Run_of_a_delegating_User_with_no_Runner_and_no_Team_waits_saying_why()
    {
        var (lu, luId) = await UserOfTheirOwnAsync(FakeGitHub.Lu);
        var team = (await sofa.SetUp).FirstUser.TeamId;
        using var ada = await sofa.AdaSignedInAsync(Cancellation);
        using var removed = await ada.SendAsync(HttpMethod.Delete, $"{await WorkspaceApiAsync()}/teams/{team}/users/{luId}", null, Cancellation);
        removed.EnsureSuccessStatusCode();

        var runId = await StartTestRunAsync(luId, "Triage");
        var waiting = await RunUntilAsync(lu, runId, r => r.Why is not null);

        Assert.Equal("Waiting", waiting.State);
        Assert.Contains("no Anthropic Runner of their own", waiting.Why);
    }

    [Fact]
    public async Task Connections_opens_an_API_key_only_for_Execution()
    {
        using var connections = sofa.CreateHttpClient(SofaServices.Connections);

        using var asked = await connections.PostAsJsonAsync(
            $"/workspaces/{(await sofa.SetUp).FirstUser.WorkspaceId}/api-keys/{Guid.NewGuid()}/opened-for-a-run",
            new { runId = Guid.NewGuid() }, Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, asked.StatusCode);
    }

    [Fact]
    public async Task Nobody_outside_the_Workspace_reads_a_Run_or_its_Transcript()
    {
        var (gus, _) = await UserOfTheirOwnAsync(FakeGitHub.Gus);
        using var gateway = sofa.CreateHttpClient(SofaServices.AppGateway);
        var someRun = Guid.NewGuid();

        using var elsewhere = await gus.GetWithTokenAsync($"/api/workspaces/{Guid.NewGuid()}/runs/{someRun}/transcript", Cancellation);
        using var anonymous = await gateway.GetAsync($"{await WorkspaceApiAsync()}/runs/{someRun}/transcript", Cancellation);
        using var noSuchRun = await gus.GetWithTokenAsync($"{await WorkspaceApiAsync()}/runs/{someRun}/transcript", Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, elsewhere.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, noSuchRun.StatusCode);
    }

    private readonly TestRuns _runs = new(sofa);

    private static string StartOf(List<TestRuns.TranscriptEntry> transcript) => transcript.First().Text!;

    private Task<(WebClientSignIn User, Guid UserId)> UserOfTheirOwnAsync(GitHubAccountOnFake account) => _runs.UserOfTheirOwnAsync(account);

    private Task<Guid> StartTestRunAsync(Guid delegatingUserId, string phase, string? task = null, int? retryLimit = null, string? projectKey = null) =>
        _runs.StartAsync(delegatingUserId, phase, task, retryLimit, projectKey);

    private Task<TestRuns.Run> RunUntilAsync(WebClientSignIn user, Guid runId, Func<TestRuns.Run, bool> until) => _runs.UntilAsync(user, runId, until);

    private Task RunningContainerAsync(WebClientSignIn user, Guid runId) => _runs.RunningContainerAsync(user, runId);

    private Task<List<TestRuns.TranscriptEntry>> WholeTranscriptAsync(WebClientSignIn user, Guid runId) => _runs.WholeTranscriptAsync(user, runId);

    private async Task<List<TestRuns.TranscriptEntry>> TranscriptAsync(WebClientSignIn user, Guid runId) =>
        (await _runs.TranscriptAsync(user, runId)).Entries;

    private Task<TestRuns.Runner> AddRunnerAsync(WebClientSignIn user, string kind, object runner) => _runs.AddRunnerAsync(user, kind, runner);

    private Task<List<TestRuns.Runner>> RunnersSeenByAsync(WebClientSignIn user) => _runs.RunnersSeenByAsync(user);

    private Task<string> NoticesLoggedAsync(Func<string, bool> matching) => _runs.LoggedByAsync(SofaServices.Notices, matching);

    private async Task ForgetInIdentityAsync(Guid agentId)
    {
        await using var database = new NpgsqlConnection(await sofa.App.GetConnectionStringAsync("sofa", Cancellation));
        await database.OpenAsync(Cancellation);
        await using var forget = new NpgsqlCommand("delete from identity.agents where id = @id", database);
        forget.Parameters.AddWithValue("id", agentId);
        Assert.Equal(1, await forget.ExecuteNonQueryAsync(Cancellation));
    }

    private Task<string> WorkspaceApiAsync() => _runs.WorkspaceApiAsync();
}
