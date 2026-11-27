using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Npgsql;
using Sofa.Contracts;

namespace Sofa.IntegrationTests;

public class RunToolTests(SofaApp sofa)
{
    private const string Demo = TestProjects.DemoRepository;
    private const long Issue = 80;
    private const string TheQuestion = "Should the toggle follow the system setting by default?";
    private const string TheAnswer = "Yes, follow the system setting.";
    private static readonly TimeSpan SettlesWithin = TimeSpan.FromSeconds(90);
    private static readonly byte[] PngSignature = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_Run_reads_the_issue_as_it_is_now_asks_on_it_and_a_new_Run_reads_the_answer_and_finishes_with_a_screenshot()
    {
        var (nia, niaId) = await NiaWithARunnerAsync();
        var workItem = await WorkItemLabelledByNiaAsync();
        sofa.GitHub.Write(Demo, Issue, "Add a dark mode toggle that remembers", "A toggle in the header, which remembers the choice.");

        var asking = await StartTestRunAsync(niaId, "Spec", $"STUB: ask {TheQuestion}", workItem.Key);
        var asked = await RunUntilAsync(nia, asking, run => run.State == "Finished");
        var askingTranscript = await WholeTranscriptAsync(nia, asking);
        var questionOnTheIssue = await EventuallyAsync(() =>
            Task.FromResult(sofa.GitHub.CommentsOn(Demo, Issue).SingleOrDefault(comment => comment.Body.Contains(TheQuestion, StringComparison.Ordinal))));

        await GitHubWebhooks.CommentedAsync(sofa, Demo, Issue, sofa.GitHub.NextCommentId(), $"> {TheQuestion}\n\n{TheAnswer}", FakeGitHub.Nia);
        await EventuallyAsync(async () => (await QuestionsInFlowAsync(workItem.Id)).SingleOrDefault(q => q.Answer is not null));

        var finishing = await StartTestRunAsync(niaId, "Spec", "STUB: attach a screenshot", workItem.Key);
        var finished = await RunUntilAsync(nia, finishing, run => run.State == "Finished");
        var finishingTranscript = await WholeTranscriptAsync(nia, finishing);
        using var contributionRead = await nia.GetWithTokenAsync($"{await WorkspaceApiAsync()}/runs/{finishing}/contribution", Cancellation);
        var contribution = (await contributionRead.Content.ReadFromJsonAsync<Contribution>(Cancellation))!;
        var screenshot = Assert.Single(contribution.Attachments);
        using var screenshotRead = await nia.GetWithTokenAsync(
            $"{await WorkspaceApiAsync()}/runs/{finishing}/contribution/attachments/{screenshot.Id}", Cancellation);
        var screenshotBytes = await screenshotRead.Content.ReadAsByteArrayAsync(Cancellation);

        Assert.Equal((null, TheQuestion), (asked.Outcome, asked.Question?.Text));
        Assert.Contains(askingTranscript, line => ToolResult(line).Contains("A toggle in the header, which remembers the choice.", StringComparison.Ordinal));
        Assert.Contains(askingTranscript, line => ToolResult(line).Contains("Add a dark mode toggle that remembers", StringComparison.Ordinal));
        Assert.StartsWith($"### Question from Spec · {workItem.Key}", questionOnTheIssue.Body);

        Assert.Contains(finishingTranscript, line => ToolResult(line).Contains(TheQuestion, StringComparison.Ordinal)
            && ToolResult(line).Contains(TheAnswer, StringComparison.Ordinal));
        Assert.Equal("specified", finished.Outcome);
        Assert.Equal(HttpStatusCode.OK, contributionRead.StatusCode);
        Assert.Equal(("specified", "Spec", workItem.Id), (contribution.Outcome, contribution.Phase, contribution.WorkItemId));
        Assert.Contains("It played back a recorded Run.", contribution.Markdown);
        Assert.Equal(("screenshot.png", "image/png"), (screenshot.Name, screenshot.ContentType));
        Assert.Equal(HttpStatusCode.OK, screenshotRead.StatusCode);
        Assert.Equal("image/png", screenshotRead.Content.Headers.ContentType?.MediaType);
        Assert.Equal(PngSignature, screenshotBytes.Take(PngSignature.Length));
        Assert.Equal(screenshot.Size, screenshotBytes.Length);
    }

    [Fact]
    public async Task A_Run_is_offered_only_its_Sandboxs_tools_and_its_token_is_refused_once_it_has_ended()
    {
        var (nia, niaId) = await NiaWithARunnerAsync();

        var runId = await StartTestRunAsync(niaId, "Triage", "Triage it.", workItemKey: null);
        var run = await RunUntilAsync(nia, runId, r => r.State == "Finished");
        var transcript = await WholeTranscriptAsync(nia, runId);
        var offered = transcript.Single(line => SubtypeOf(line) == "stub_tools_offered");
        var afterTheEnd = transcript.Single(line => SubtypeOf(line) == "stub_called_after_the_end");

        Assert.Equal("small", run.Outcome);
        Assert.Equal(["read_work_item", "ask", "finish"], offered.GetProperty("tools").EnumerateArray().Select(tool => tool.GetString()));
        Assert.Contains(transcript, line => ToolResult(line).Contains("Sofa has no Work Item for this Run", StringComparison.Ordinal));
        Assert.StartsWith("403", afterTheEnd.GetProperty("answer").GetString());
        Assert.Contains("The Run has ended", afterTheEnd.GetProperty("answer").GetString());
    }

    [Fact]
    public async Task A_finish_naming_an_Outcome_of_another_Phase_is_refused_and_keeps_no_Contribution()
    {
        var (nia, niaId) = await NiaWithARunnerAsync();

        var runId = await StartTestRunAsync(niaId, "Triage", "STUB: outcome implemented", workItemKey: null, retryLimit: 0);
        var failed = await RunUntilAsync(nia, runId, r => r.State == "Failed");
        var transcript = await WholeTranscriptAsync(nia, runId);
        using var contribution = await nia.GetWithTokenAsync($"{await WorkspaceApiAsync()}/runs/{runId}/contribution", Cancellation);

        Assert.Null(failed.Outcome);
        Assert.Contains(transcript, line => ToolResult(line).Contains("The Triage Phase's Outcomes are `small`, `full`", StringComparison.Ordinal));
        Assert.Equal(HttpStatusCode.NotFound, contribution.StatusCode);
    }

    private async Task<(WebClientSignIn User, Guid UserId)> NiaWithARunnerAsync()
    {
        var niaId = await sofa.UserOnceAsync(FakeGitHub.Nia);
        var nia = await sofa.SignedInWithGitHubAsync(FakeGitHub.Nia, Cancellation);
        await RunnerOnceAsync(nia);
        return (nia, niaId);
    }

    private static readonly SemaphoreSlim OneRunnerAdded = new(1, 1);
    private static bool _runnerAdded;

    private async Task RunnerOnceAsync(WebClientSignIn nia)
    {
        await OneRunnerAdded.WaitAsync(Cancellation);
        try
        {
            if (!_runnerAdded)
            {
                using var created = await nia.PostAsJsonAsync($"{await WorkspaceApiAsync()}/runners/api-key",
                    new { name = "Nia's key", apiKey = "sk-ant-api03-nia", runsAtOnce = 3, forTheTeam = false }, Cancellation);
                Assert.Equal(HttpStatusCode.Created, created.StatusCode);
                _runnerAdded = true;
            }
        }
        finally
        {
            OneRunnerAdded.Release();
        }
    }

    private async Task<WorkItem> WorkItemLabelledByNiaAsync()
    {
        var workspaceId = (await sofa.SetUp).FirstUser.WorkspaceId;
        var projectId = (await TestProjects.CreateAsync(sofa, "TOOLS", "sofa-tools")).GetProperty("id").GetGuid();
        using (var ada = await sofa.AdaSignedInAsync(Cancellation))
        {
            await ProjectForRuns.RegisteredAsync(ada, workspaceId, "TOOLS", Cancellation);
        }

        await EventuallyAsync(async () => await IntakeKnowsAsync(projectId) ? true : (bool?)null);
        sofa.GitHub.Write(Demo, Issue, "Add a dark mode toggle", "A toggle in the header.");
        await GitHubWebhooks.LabeledAsync(sofa, Demo, Issue, "Add a dark mode toggle", "sofa-tools", FakeGitHub.Nia);
        return await EventuallyAsync(async () => (await WorkItemsOfAsync("TOOLS")).SingleOrDefault(w => w.Request.Number == Issue));
    }

    private async Task<Guid> StartTestRunAsync(Guid delegatingUserId, string phase, string task, string? workItemKey, int? retryLimit = null)
    {
        using var flow = sofa.CreateHttpClient(SofaServices.Flow);
        using var started = await flow.PostAsJsonAsync("/diagnostics/test-runs", new
        {
            workspaceId = (await sofa.SetUp).FirstUser.WorkspaceId,
            projectKey = workItemKey is null ? (await sofa.TheProjectForRuns).Key : "TOOLS",
            phase,
            delegatingUserId,
            task,
            retryLimit,
            workItemKey,
        }, Cancellation);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        return (await started.Content.ReadFromJsonAsync<TestRunStarted>(Cancellation))!.RunId;
    }

    private async Task<Run> RunUntilAsync(WebClientSignIn user, Guid runId, Func<Run, bool> until)
    {
        Run? last = null;
        try
        {
            return await EventuallyAsync(async () =>
            {
                using var read = await user.GetWithTokenAsync($"{await WorkspaceApiAsync()}/runs/{runId}", Cancellation);
                last = read.StatusCode == HttpStatusCode.OK ? await read.Content.ReadFromJsonAsync<Run>(Cancellation) : last;
                return last is not null && until(last) ? last : null;
            });
        }
        catch (OperationCanceledException) when (!Cancellation.IsCancellationRequested)
        {
            throw new TimeoutException($"Run {runId} never got there; it was last {last?.State}: {last?.Why}");
        }
    }

    private async Task<List<JsonElement>> WholeTranscriptAsync(WebClientSignIn user, Guid runId) =>
        await EventuallyAsync(async () =>
        {
            using var read = await user.GetWithTokenAsync($"{await WorkspaceApiAsync()}/runs/{runId}/transcript", Cancellation);
            read.EnsureSuccessStatusCode();
            var lines = (await read.Content.ReadFromJsonAsync<Transcript>(Cancellation))!.Lines
                .Select(line => JsonDocument.Parse(line.Text).RootElement.Clone())
                .ToList();
            return lines.Count > 0 && lines.Last().GetProperty("type").GetString() == "result" ? lines : null;
        });

    private async Task<List<Question>> QuestionsInFlowAsync(Guid workItemId)
    {
        using var flow = sofa.CreateHttpClient(SofaServices.Flow);
        var workItem = await flow.GetFromJsonAsync<WorkItemInFlow>(
            $"/workspaces/{(await sofa.SetUp).FirstUser.WorkspaceId}/work-items/{workItemId}", Cancellation);
        return workItem!.Questions;
    }

    private async Task<List<WorkItem>> WorkItemsOfAsync(string key)
    {
        using var flow = sofa.CreateHttpClient(SofaServices.Flow);
        return (await flow.GetFromJsonAsync<List<WorkItem>>(
            $"/workspaces/{(await sofa.SetUp).FirstUser.WorkspaceId}/projects/{key}/work-items", Cancellation))!;
    }

    private async Task<bool> IntakeKnowsAsync(Guid projectId)
    {
        await using var database = new NpgsqlConnection(await sofa.App.GetConnectionStringAsync("sofa", Cancellation));
        await database.OpenAsync(Cancellation);
        await using var query = new NpgsqlCommand("select count(*) from intake.request_bindings where project_id = @id", database);
        query.Parameters.AddWithValue("id", projectId);
        return (long)(await query.ExecuteScalarAsync(Cancellation))! == 1;
    }

    private static string ToolResult(JsonElement line) =>
        line.GetProperty("type").GetString() == "user"
            ? line.GetProperty("message").GetProperty("content")[0].GetProperty("content").GetString() ?? ""
            : "";

    private static string? SubtypeOf(JsonElement line) =>
        line.TryGetProperty("subtype", out var subtype) ? subtype.GetString() : null;

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

    private async Task<string> WorkspaceApiAsync() => $"/api/workspaces/{(await sofa.SetUp).FirstUser.WorkspaceId}";

    private sealed record TestRunStarted(Guid RunId);

    private sealed record Run(Guid Id, string State, string? Why, string? Outcome, AskedQuestion? Question);

    private sealed record AskedQuestion(Guid QuestionId, string Text);

    private sealed record Transcript(Guid RunId, List<TranscriptLine> Lines);

    private sealed record TranscriptLine(int Number, string Text);

    private sealed record WorkItem(Guid Id, string Key, string Title, Guid RequestId, RequestRef Request);

    private sealed record RequestRef(string Repository, long Number, string Url);

    private sealed record WorkItemInFlow(Guid Id, string Key, List<Question> Questions);

    private sealed record Question(Guid Id, string Text, string? Answer);

    private sealed record Contribution(Guid RunId, Guid WorkItemId, string Phase, string Outcome, string Markdown, List<Attachment> Attachments);

    private sealed record Attachment(Guid Id, string Name, string ContentType, long Size);
}
