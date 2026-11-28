using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Sofa.Contracts;

namespace Sofa.IntegrationTests;

public class FlowTests(SofaApp sofa)
{
    private const string Demo = TestProjects.DemoRepository;
    private const string TheQuestion = "Should the toggle follow the system setting by default?";
    private const string TheAnswer = "Yes, follow the system setting.";
    private static readonly TimeSpan SettlesWithin = TimeSpan.FromSeconds(120);
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    private readonly TestRuns _runs = new(sofa);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_labelled_issue_goes_through_Triage_Spec_with_a_Question_Plan_and_Implement_and_merging_its_pull_request_finishes_it()
    {
        const long issue = 90;
        var (rae, raeId) = await UserWithARunnerAsync(FakeGitHub.Rae, "Rae's key");
        var project = await ProjectAsync("FLOW", "sofa-flow", new()
        {
            ["Triage"] = "STUB: outcome full",
            ["Spec"] = $"STUB: ask once {TheQuestion}",
            ["Implement"] = "STUB: attach a screenshot",
        });
        sofa.GitHub.Write(Demo, issue, "Add a dark mode toggle", "A toggle in the header.");

        await GitHubWebhooks.LabeledAsync(sofa, Demo, issue, "Add a dark mode toggle", "sofa-flow", FakeGitHub.Rae);
        var asking = await WorkItemUntilAsync(rae, project, "FLOW-1", w => w.State == "AwaitsAnswer");
        var question = Assert.Single(asking.Questions);
        var questionInTheInbox = await InboxUntilAsync(rae, items => items.SingleOrDefault(i => i.Kind == "Question" && i.Title.StartsWith("FLOW-1", StringComparison.Ordinal)));
        var questionOnTheIssue = await EventuallyAsync(() =>
            Task.FromResult(sofa.GitHub.CommentsOn(Demo, issue).SingleOrDefault(c => c.Body.StartsWith("### Question from Spec · FLOW-1", StringComparison.Ordinal))));

        await GitHubWebhooks.CommentedAsync(sofa, Demo, issue, sofa.GitHub.NextCommentId(), TheAnswer, FakeGitHub.Rae);
        var specified = await WorkItemUntilAsync(rae, project, "FLOW-1", w => w is { State: "AwaitsApproval", Phase: "Spec" });
        var specReport = await ReportOnAsync(issue, "Spec");
        var inboxWhileTheSpecWaits = await InboxUntilAsync(rae, items =>
            items.Any(i => i.Kind == "Approval") && !items.Any(i => i.Kind == "Question" && i.Title.StartsWith("FLOW-1", StringComparison.Ordinal)) ? items : null);
        var specRun = specified.Runs.Last(r => r.Phase == "Spec");
        var specTranscript = await _runs.WholeTranscriptAsync(rae, specRun.RunId);

        var approvingTheSpec = await DecideAsync(rae, project, "FLOW-1", "phases/Spec/approval", new { });
        var planned = await WorkItemUntilAsync(rae, project, "FLOW-1", w => w is { State: "AwaitsApproval", Phase: "Plan" });
        var sendingThePlanBack = await DecideAsync(rae, project, "FLOW-1", "phases/Plan/sending-back", new { comment = "Keep the toggle in the header." });
        var replanned = await WorkItemUntilAsync(rae, project, "FLOW-1",
            w => w is { State: "AwaitsApproval", Phase: "Plan" } && w.Runs.Count(r => r.Phase == "Plan") == 2);
        var replanTranscript = await _runs.WholeTranscriptAsync(rae, replanned.Runs.Last(r => r.Phase == "Plan").RunId);
        var planReport = await EventuallyAsync(async () => await ReportOnAsync(issue, "Plan") is { Edits: 1 } edited ? edited : null);

        await DecideAsync(rae, project, "FLOW-1", "phases/Plan/approval", new { });
        var implemented = await WorkItemUntilAsync(rae, project, "FLOW-1", w => w.State == "AwaitsPullRequest");
        var implementRun = implemented.Runs.Single(r => r.Phase == "Implement");
        var implementDetails = await _runs.UntilAsync(rae, implementRun.RunId, r => r.State == "Finished");
        var brief = await BriefOfAsync(rae, implementRun.RunId);
        var screenshot = await ScreenshotOfAsync(rae, implementRun.RunId);
        var implementReport = await ReportOnAsync(issue, "Implement");

        await GitHubWebhooks.PullRequestAsync(sofa, Demo, 91, "opened", "FLOW-1: Add a dark mode toggle", "sofa/FLOW-1");
        await WorkItemUntilAsync(rae, project, "FLOW-1", w => w.PullRequest?.State == "Open");
        await GitHubWebhooks.PullRequestAsync(sofa, Demo, 91, "closed", "FLOW-1: Add a dark mode toggle", "sofa/FLOW-1", merged: true);
        var done = await WorkItemUntilAsync(rae, project, "FLOW-1", w => w.State == "Done");
        var lastReport = await EventuallyAsync(() => Task.FromResult(sofa.GitHub.CommentsOn(Demo, issue)
            .SingleOrDefault(c => c.Body.StartsWith("### Implement · FLOW-1", StringComparison.Ordinal) && c.Body.Contains("is merged", StringComparison.Ordinal))));
        await EventuallyAsync(() => Task.FromResult(sofa.GitHub.ChangesOf(Demo, issue).Contains("closed as completed") ? true : (bool?)null));
        var inboxOnceDone = await InboxUntilAsync(rae, items => items.Any(i => i.Title.StartsWith("FLOW-1", StringComparison.Ordinal)) ? null : items);

        Assert.Equal(("Spec", "Full", TheQuestion), (asking.Phase, asking.Track, question.Text));
        Assert.Contains(TheQuestion, questionInTheInbox.Detail);
        Assert.Contains(TheQuestion, questionOnTheIssue.Body);
        Assert.Contains(specTranscript, e => (e.ToolResult ?? "").Contains(TheAnswer, StringComparison.Ordinal));
        Assert.Equal(["Triage", "Spec", "Spec"], specified.Runs.Select(r => r.Phase));
        Assert.Equal("Finished: specified", specRun.EndedAs);
        Assert.Contains("It played back a recorded Run.", specReport.Body);
        Assert.DoesNotContain(sofa.GitHub.CommentsOn(Demo, issue), c => c.Body.StartsWith("### Triage", StringComparison.Ordinal));
        Assert.DoesNotContain(inboxWhileTheSpecWaits, i => i.Kind == "Question" && i.Title.StartsWith("FLOW-1", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.OK, approvingTheSpec.StatusCode);

        Assert.Equal("Approved", planned.PhaseEnds.Single(e => e.Phase == "Spec").Standing);
        Assert.Equal(HttpStatusCode.OK, sendingThePlanBack.StatusCode);
        Assert.Equal(("SentBack", "Keep the toggle in the header."),
            (replanned.PhaseEnds.First(e => e.Phase == "Plan").Standing, replanned.PhaseEnds.First(e => e.Phase == "Plan").Comment));
        Assert.Contains(replanTranscript, e => (e.ToolResult ?? "").Contains("> Keep the toggle in the header.", StringComparison.Ordinal));
        Assert.Equal(1, planReport.Edits);

        Assert.Equal("implemented", implementDetails.Outcome);
        Assert.Contains("on a Run of its Implement Phase on Work Item FLOW-1: Add a dark mode toggle.", brief);
        Assert.Contains("Its key, FLOW-1, starts the title of any pull request you open.", brief);
        Assert.Contains("STUB: attach a screenshot", brief);
        Assert.Equal(PngSignature, screenshot.Take(PngSignature.Length));
        Assert.Contains("Attached in Sofa: screenshot.png", implementReport.Body);

        Assert.Equal(("Done", "Merged", 91L), (done.State, done.PullRequest!.State, done.PullRequest.Number));
        Assert.StartsWith("https://github.com/acme/demo/pull/91 is merged, so FLOW-1 is done.", lastReport.Body["### Implement · FLOW-1\n\n".Length..]);
        Assert.Equal(["+sofa: triage", "-sofa: triage", "+sofa: spec", "-sofa: spec", "+sofa: plan", "-sofa: plan", "+sofa: implement", "-sofa: implement", "closed as completed"],
            sofa.GitHub.ChangesOf(Demo, issue));
        Assert.DoesNotContain(inboxOnceDone, i => i.Title.StartsWith("FLOW-1", StringComparison.Ordinal));
    }

    [Fact]
    public async Task On_the_Small_Track_Spec_is_skipped_the_plan_is_left_Unverified_and_a_pull_request_closed_unmerged_waits_in_the_Inbox()
    {
        const long issue = 95;
        var (sam, _) = await UserWithARunnerAsync(FakeGitHub.Sam, "Sam's key");
        var project = await ProjectAsync("SMALL", "sofa-small", new() { ["Triage"] = "STUB: outcome small" });
        sofa.GitHub.Write(Demo, issue, "Fix the typo in the header", "It says Sofaa.");

        await GitHubWebhooks.LabeledAsync(sofa, Demo, issue, "Fix the typo in the header", "sofa-small", FakeGitHub.Sam);
        var implemented = await WorkItemUntilAsync(sam, project, "SMALL-1", w => w.State == "AwaitsPullRequest");
        await GitHubWebhooks.PullRequestAsync(sofa, Demo, 96, "opened", "SMALL-1: Fix the typo", "sofa/SMALL-1");
        await WorkItemUntilAsync(sam, project, "SMALL-1", w => w.PullRequest?.State == "Open");
        await GitHubWebhooks.PullRequestAsync(sofa, Demo, 96, "closed", "SMALL-1: Fix the typo", "sofa/SMALL-1");
        var closed = await WorkItemUntilAsync(sam, project, "SMALL-1", w => w.State == "AwaitsUser");
        var waiting = await InboxUntilAsync(sam, items => items.SingleOrDefault(i => i.Kind == "PullRequestClosedUnmerged"));
        var lookedAt = await DecideAsync(sam, project, "SMALL-1", "phases/Plan/approval", new { });
        var verified = await WorkItemUntilAsync(sam, project, "SMALL-1", w => w.PhaseEnds.Any(e => e is { Phase: "Plan", Standing: "Verified" }));

        Assert.Equal("Small", implemented.Track);
        Assert.Equal(["Triage", "Plan", "Implement"], implemented.Runs.Select(r => r.Phase));
        Assert.Equal(("ReviewAfterwards", "Unverified"),
            (implemented.PhaseEnds.Single(e => e.Phase == "Plan").Gate, implemented.PhaseEnds.Single(e => e.Phase == "Plan").Standing));
        Assert.Contains(sofa.GitHub.CommentsOn(Demo, issue), c => c.Body.StartsWith("### Plan · SMALL-1", StringComparison.Ordinal));
        Assert.DoesNotContain(sofa.GitHub.CommentsOn(Demo, issue), c => c.Body.StartsWith("### Spec", StringComparison.Ordinal));
        Assert.Equal(("PullRequestClosedUnmerged", "ClosedUnmerged"), (closed.Waiting!.Kind, closed.PullRequest!.State));
        Assert.Equal(("https://github.com/acme/demo/pull/96", "SMALL-1: Fix the typo in the header"), (waiting.Url, waiting.Title));
        Assert.Equal(HttpStatusCode.OK, lookedAt.StatusCode);
        Assert.Equal("AwaitsUser", verified.State);
    }

    [Fact]
    public async Task A_User_changes_the_Track_and_answers_in_Sofa_which_settles_the_Question_on_the_issue_and_shows_live()
    {
        const long issue = 120;
        var (lee, _) = await UserWithARunnerAsync(FakeGitHub.Lee, "Lee's key");
        var project = await ProjectAsync("LIVE", "sofa-live", new()
        {
            ["Triage"] = "STUB: outcome full",
            ["Spec"] = $"STUB: ask once {TheQuestion}",
        });
        await using var live = await LiveUpdates.ConnectAsync(sofa, lee, (await sofa.SetUp).FirstUser.WorkspaceId, Cancellation);
        sofa.GitHub.Write(Demo, issue, "Add a dark mode toggle", "A toggle in the header.");

        await GitHubWebhooks.LabeledAsync(sofa, Demo, issue, "Add a dark mode toggle", "sofa-live", FakeGitHub.Lee);
        var asking = await WorkItemUntilAsync(lee, project, "LIVE-1", w => w.State == "AwaitsAnswer");
        var question = Assert.Single(asking.Questions);
        var inTheInbox = await InboxUntilAsync(lee, items => items.SingleOrDefault(i => i.Kind == "Question" && i.WorkItemKey == "LIVE-1"));
        await EventuallyAsync(() => Task.FromResult(sofa.GitHub.CommentsOn(Demo, issue).SingleOrDefault(c => c.Body.StartsWith("### Question from Spec · LIVE-1", StringComparison.Ordinal))));
        using var changingTheTrack = await lee.SendAsync(HttpMethod.Put,
            $"{await _runs.WorkspaceApiAsync()}/projects/LIVE/work-items/LIVE-1/track", new { track = "Small" }, Cancellation);
        var onTheSmallTrack = await changingTheTrack.Content.ReadFromJsonAsync<WorkItem>(Cancellation);

        using var answering = await DecideAsync(lee, project, "LIVE-1", $"questions/{question.Id}/answer", new { answer = TheAnswer });
        var specified = await WorkItemUntilAsync(lee, project, "LIVE-1", w => w is { State: "AwaitsApproval", Phase: "Spec" });
        var settledOnTheIssue = await EventuallyAsync(() => Task.FromResult(sofa.GitHub.CommentsOn(Demo, issue)
            .SingleOrDefault(c => c.Body.StartsWith("### Question from Spec · LIVE-1", StringComparison.Ordinal) && c.Body.Contains("Answered in Sofa", StringComparison.Ordinal))));
        await GitHubWebhooks.CommentedAsync(sofa, Demo, issue, sofa.GitHub.NextCommentId(), "No, keep it light.", FakeGitHub.Lee);
        await DecideAsync(lee, project, "LIVE-1", "phases/Spec/approval", new { });
        var implementing = await WorkItemUntilAsync(lee, project, "LIVE-1", w => w.State == "AwaitsPullRequest");
        var specTranscript = await _runs.WholeTranscriptAsync(lee, specified.Runs.Last(r => r.Phase == "Spec").RunId);
        await EventuallyAsync(() => Task.FromResult(live.HeardThatWorkItemChanged("LIVE-1") && live.HeardThatTheInboxChanged
            && live.HeardThatRunChanged(implementing.Runs.Last().RunId) ? (bool?)true : null));

        Assert.Equal(HttpStatusCode.OK, changingTheTrack.StatusCode);
        Assert.Equal(("Small", true), (onTheSmallTrack!.Track, onTheSmallTrack.TrackChosenByAUser));
        Assert.Equal("LIVE-1: Add a dark mode toggle", inTheInbox.Title);
        Assert.Equal(HttpStatusCode.OK, answering.StatusCode);
        Assert.Contains($"> {TheAnswer}", settledOnTheIssue.Body);
        Assert.Equal(TheAnswer, Assert.Single(implementing.Questions).Answer);
        Assert.Equal(2, implementing.Runs.Count(r => r.Phase == "Spec"));
        Assert.Equal(("ReviewAfterwards", "Unverified"),
            (implementing.PhaseEnds.Single(e => e.Phase == "Plan").Gate, implementing.PhaseEnds.Single(e => e.Phase == "Plan").Standing));
        var start = specTranscript.First();
        Assert.Contains("mcp__sofa__finish", start.Text);
        Assert.DoesNotContain("WebFetch", start.Text);
        Assert.DoesNotContain(specTranscript, e => e.Text?.Contains("thinking_tokens", StringComparison.Ordinal) == true);
    }

    [Fact]
    public async Task A_Users_Retry_of_a_Run_gone_wrong_shows_on_the_Work_Item_at_once()
    {
        const long issue = 130;
        var (noa, _) = await UserWithARunnerAsync(FakeGitHub.Noa, "Noa's key");
        var project = await ProjectAsync("RETRY", "sofa-retry", new() { ["Triage"] = "STUB: hang" });
        sofa.GitHub.Write(Demo, issue, "Something that hangs", "It hangs.");

        await GitHubWebhooks.LabeledAsync(sofa, Demo, issue, "Something that hangs", "sofa-retry", FakeGitHub.Noa);
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            var running = await WorkItemUntilAsync(noa, project, "RETRY-1", w => w.State == "Running" && w.Runs.Count == attempt);
            var runId = running.Runs.Last().RunId;
            await _runs.RunningContainerAsync(noa, runId);
            await StubWorker.KillAsync(runId, Cancellation);
        }

        var goneWrong = await WorkItemUntilAsync(noa, project, "RETRY-1", w => w is { State: "AwaitsUser", Waiting.Kind: "RunGoneWrong" });
        using var retrying = await noa.PostAsJsonAsync($"{await _runs.WorkspaceApiAsync()}/runs/{goneWrong.Waiting!.WaitId}/retry", null, Cancellation);
        var retry = (await retrying.Content.ReadFromJsonAsync<TestRuns.Run>(Cancellation))!;
        var following = await WorkItemUntilAsync(noa, project, "RETRY-1", w => w.State == "Running" && w.Runs.Any(r => r.RunId == retry.Id));
        var retryWhileFollowed = await _runs.UntilAsync(noa, retry.Id, _ => true);
        using var stopping = await noa.PostAsJsonAsync($"{await _runs.WorkspaceApiAsync()}/runs/{retry.Id}/stop", null, Cancellation);

        Assert.Equal(HttpStatusCode.OK, retrying.StatusCode);
        Assert.Null(following.Waiting);
        Assert.Equal(goneWrong.Waiting.WaitId, following.Runs.Single(r => r.RunId == retry.Id).RetryOf);
        Assert.Contains(retryWhileFollowed.State, new[] { "Waiting", "Running" });
    }

    [Fact]
    public async Task A_Run_past_its_tokens_refresh_time_gets_a_fresh_Repository_token_in_its_container()
    {
        var (sam, samId) = await UserWithARunnerAsync(FakeGitHub.Sam, "Sam's key");

        var runId = await _runs.StartAsync(samId, "Implement", "STUB: take 20s STUB: tell its secrets");
        var refreshed = await _runs.LoggedByAsync(SofaServices.Execution, line => line.Contains($"Refreshed Run {runId}'s Repository token", StringComparison.Ordinal));
        await _runs.UntilAsync(sam, runId, r => r.State == "Finished");
        var transcript = await _runs.WholeTranscriptAsync(sam, runId);

        Assert.Contains("until", refreshed);
        Assert.Contains(transcript, e => e.Text == "GH_TOKEN=[scrubbed] ANTHROPIC_API_KEY=[scrubbed]");
    }

    private async Task<(WebClientSignIn User, Guid UserId)> UserWithARunnerAsync(GitHubAccountOnFake account, string runnerName)
    {
        var (user, userId) = await _runs.UserOfTheirOwnAsync(account);
        await RunnerOnceAsync(user, account, runnerName);
        return (user, userId);
    }

    private static readonly SemaphoreSlim OneRunnerAtATime = new(1, 1);
    private static readonly HashSet<long> HaveARunner = [];

    private async Task RunnerOnceAsync(WebClientSignIn user, GitHubAccountOnFake account, string runnerName)
    {
        await OneRunnerAtATime.WaitAsync(Cancellation);
        try
        {
            if (HaveARunner.Add(account.Id))
            {
                await _runs.AddRunnerAsync(user, "api-key", new { name = runnerName, apiKey = $"sk-ant-api03-{account.Login}-{Guid.NewGuid():N}", runsAtOnce = 3, forTheTeam = false });
            }
        }
        finally
        {
            OneRunnerAtATime.Release();
        }
    }

    private async Task<JsonElement> ProjectAsync(string key, string label, Dictionary<string, string> instructions)
    {
        var workspaceId = (await sofa.SetUp).FirstUser.WorkspaceId;
        var project = await TestProjects.CreateAsync(sofa, key, label, keepsIssueState: true, holdsAfterTriage: false);
        using var ada = await sofa.AdaSignedInAsync(Cancellation);
        foreach (var (phase, said) in instructions)
        {
            project = await TestProjects.ChangePhaseAsync(ada, workspaceId, project, phase, instructions: said);
        }

        await ProjectForRuns.RegisteredAsync(ada, workspaceId, key, Cancellation);
        var projectId = project.GetProperty("id").GetGuid();
        await EventuallyAsync(async () => await IntakeKnowsAsync(projectId) ? true : (bool?)null);
        return project;
    }

    private async Task<WorkItem> WorkItemUntilAsync(WebClientSignIn user, JsonElement project, string key, Func<WorkItem, bool> until)
    {
        WorkItem? last = null;
        try
        {
            return await EventuallyAsync(async () =>
            {
                using var read = await user.GetWithTokenAsync(
                    $"{await _runs.WorkspaceApiAsync()}/projects/{project.GetProperty("key").GetString()}/work-items/{key}", Cancellation);
                last = read.StatusCode == HttpStatusCode.OK ? await read.Content.ReadFromJsonAsync<WorkItem>(Cancellation) : last;
                return last is not null && until(last) ? last : null;
            });
        }
        catch (OperationCanceledException) when (!Cancellation.IsCancellationRequested)
        {
            throw new TimeoutException($"{key} never got there; it was last {last?.State} in {last?.Phase}: {last?.Waiting?.Why}");
        }
    }

    private async Task<HttpResponseMessage> DecideAsync(WebClientSignIn user, JsonElement project, string key, string decision, object body) =>
        await user.PostAsJsonAsync(
            $"{await _runs.WorkspaceApiAsync()}/projects/{project.GetProperty("key").GetString()}/work-items/{key}/{decision}", body, Cancellation);

    private async Task<List<InboxItem>> InboxAsync(WebClientSignIn user)
    {
        using var read = await user.GetWithTokenAsync($"{await _runs.WorkspaceApiAsync()}/inbox", Cancellation);
        read.EnsureSuccessStatusCode();
        return (await read.Content.ReadFromJsonAsync<List<InboxItem>>(Cancellation))!;
    }

    private Task<T> InboxUntilAsync<T>(WebClientSignIn user, Func<List<InboxItem>, T?> until) where T : class =>
        EventuallyAsync(async () => until(await InboxAsync(user)));

    private Task<CommentOnFake> ReportOnAsync(long issue, string phase) =>
        EventuallyAsync(() => Task.FromResult(sofa.GitHub.CommentsOn(Demo, issue)
            .SingleOrDefault(c => c.Body.StartsWith($"### {phase} · ", StringComparison.Ordinal))));

    private async Task<string> BriefOfAsync(WebClientSignIn user, Guid runId)
    {
        using var read = await user.GetWithTokenAsync($"{await _runs.WorkspaceApiAsync()}/runs/{runId}", Cancellation);
        return (await read.Content.ReadFromJsonAsync<RunWithItsBrief>(Cancellation))!.Brief;
    }

    private async Task<byte[]> ScreenshotOfAsync(WebClientSignIn user, Guid runId)
    {
        using var contributionRead = await user.GetWithTokenAsync($"{await _runs.WorkspaceApiAsync()}/runs/{runId}/contribution", Cancellation);
        var contribution = (await contributionRead.Content.ReadFromJsonAsync<Contribution>(Cancellation))!;
        var attachment = Assert.Single(contribution.Attachments);
        using var read = await user.GetWithTokenAsync(
            $"{await _runs.WorkspaceApiAsync()}/runs/{runId}/contribution/attachments/{attachment.Id}", Cancellation);
        return await read.Content.ReadAsByteArrayAsync(Cancellation);
    }

    private async Task<bool> IntakeKnowsAsync(Guid projectId)
    {
        await using var database = new NpgsqlConnection(await sofa.App.GetConnectionStringAsync("sofa", Cancellation));
        await database.OpenAsync(Cancellation);
        await using var query = new NpgsqlCommand("select count(*) from intake.request_bindings where project_id = @id", database);
        query.Parameters.AddWithValue("id", projectId);
        return (long)(await query.ExecuteScalarAsync(Cancellation))! == 1;
    }

    private static async Task<T> EventuallyAsync<T>(Func<Task<T?>> find)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(SettlesWithin);
        while (true)
        {
            if (await find() is { } found)
            {
                return found;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), timeout.Token);
        }
    }

    private sealed record WorkItem(
        Guid Id,
        string Key,
        string State,
        string? Phase,
        string? Track,
        bool TrackChosenByAUser,
        Wait? Waiting,
        List<Question> Questions,
        List<WorkItemRun> Runs,
        List<PhaseEnd> PhaseEnds,
        PullRequest? PullRequest);

    private sealed record Wait(Guid WaitId, string Kind, string Why);

    private sealed record Question(Guid Id, string Text, string? Answer);

    private sealed record WorkItemRun(Guid RunId, string Phase, string? EndedAs, Guid? RetryOf);

    private sealed record PhaseEnd(Guid RunId, string Phase, string Gate, string Standing, string? Comment);

    private sealed record PullRequest(long Number, string Url, string State);

    private sealed record InboxItem(Guid Id, string Kind, string? WorkItemKey, string Title, string Detail, string Url);

    private sealed record RunWithItsBrief(Guid Id, string Brief);

    private sealed record Contribution(List<Attachment> Attachments);

    private sealed record Attachment(Guid Id, string Name);
}
