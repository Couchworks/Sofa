using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Sofa.Contracts;

namespace Sofa.IntegrationTests;

public class IntakeTests(SofaApp sofa)
{
    private const string Demo = TestProjects.DemoRepository;
    private static readonly TimeSpan ArrivesWithin = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan GivenTheChanceToGoWrong = TimeSpan.FromSeconds(2);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static readonly SemaphoreSlim OneAtATime = new(1, 1);
    private static WorkItem? _issue12;

    [Fact]
    public async Task Labelling_an_issue_in_the_test_Repository_makes_DEMO_1_delegated_by_the_User_who_labelled_it()
    {
        var workItem = await BeaLabelledIssue12Async();

        Assert.Equal("DEMO-1", workItem.Key);
        Assert.Equal("Add a dark mode toggle", workItem.Title);
        Assert.Equal(new RequestRef(Demo, 12, $"https://github.com/{Demo}/issues/12"), workItem.Request);
        Assert.Equal(await sofa.BeaAddedByHerGitHubLogin, workItem.DelegatedBy?.UserId);
    }

    [Fact]
    public async Task The_same_issue_labelled_twice_makes_one_Request()
    {
        await BeaLabelledIssue12Async();

        await GitHubWebhooks.LabeledAsync(sofa, Demo, 12, "Add a dark mode toggle", "sofa", FakeGitHub.Bea);
        await GitHubWebhooks.LabeledAsync(sofa, Demo, 12, "Add a dark mode toggle", "Sofa", FakeGitHub.Ada);
        await Task.Delay(GivenTheChanceToGoWrong, Cancellation);

        Assert.Single(await WorkItemsOfAsync("DEMO"), w => w.Request.Number == 12);
        Assert.Equal(1, await CountAsync("select count(*) from intake.requests where repository = 'acme/demo' and number = 12 " +
            "and project_id = (select id from flow.projects where key = 'DEMO')"));
    }

    [Fact]
    public async Task A_label_other_than_the_Projects_brings_nothing_in()
    {
        await IntakeKnowsAsync(ProjectIdOf(await sofa.TheDemoProject), "sofa");

        await GitHubWebhooks.LabeledAsync(sofa, Demo, 13, "Just a bug", "bug", FakeGitHub.Bea);
        await Task.Delay(GivenTheChanceToGoWrong, Cancellation);

        Assert.DoesNotContain(await WorkItemsOfAsync("DEMO"), w => w.Request.Number == 13);
    }

    [Fact]
    public async Task A_label_from_an_account_linked_to_no_User_lands_with_the_Admins_instead()
    {
        await ProjectTakingRequestsAsync("LANDS", "sofa-lands");

        await GitHubWebhooks.LabeledAsync(sofa, Demo, 20, "Something from outside", "sofa-lands", FakeGitHub.Mallory);
        var workItem = await EventuallyAsync(async () => (await WorkItemsOfAsync("LANDS")).SingleOrDefault(w => w.Request.Number == 20));
        var inboxItem = await EventuallyAsync(async () =>
            (await AdminsInboxAsync()).SingleOrDefault(item => item.Url == $"https://github.com/{Demo}/issues/20"));

        Assert.Equal("LANDS-1", workItem.Key);
        Assert.Null(workItem.DelegatedBy);
        Assert.Equal(("RequestAwaitingDelegation", "TheWorkspacesAdmins", workItem.RequestId),
            (inboxItem.Kind, inboxItem.For, inboxItem.SubjectId));
        Assert.Contains(FakeGitHub.Mallory.Login, inboxItem.Detail);
    }

    [Fact]
    public async Task A_Report_appears_on_the_issue_and_posting_it_again_for_its_Phase_updates_it_in_place()
    {
        await ProjectTakingRequestsAsync("REPORT", "sofa-report");
        var workItem = await LabelledAsync("REPORT", 30, "sofa-report");

        await PostReportAsync(workItem, "Spec", "The toggle follows the system setting.", "Acceptance criteria: first draft.");
        var posted = Assert.Single(sofa.GitHub.CommentsOn(Demo, 30));
        await PostReportAsync(workItem, "Spec", "The toggle follows the system setting, and remembers a choice.", "Acceptance criteria: agreed.");
        await PostReportAsync(workItem, "Plan", "Three steps.", "");

        var comments = sofa.GitHub.CommentsOn(Demo, 30);
        Assert.Equal(2, comments.Count);
        var spec = comments.Single(c => c.Id == posted.Id);
        Assert.Equal(1, spec.Edits);
        Assert.StartsWith("### Spec · REPORT-1", spec.Body);
        Assert.Contains("remembers a choice", spec.Body);
        Assert.Contains("<details>\n<summary>Detail</summary>\n\nAcceptance criteria: agreed.\n\n</details>", spec.Body);
        Assert.Matches(@"\[See REPORT-1 in Sofa\]\(https?://[^)/]+/projects/REPORT/work-items/REPORT-1\)", spec.Body);
        Assert.StartsWith("### Plan · REPORT-1", comments.Single(c => c.Id != posted.Id).Body);
    }

    [Fact]
    public async Task A_reply_from_a_linked_User_answers_the_Question_and_a_strangers_reply_is_ignored()
    {
        await ProjectTakingRequestsAsync("ANSWER", "sofa-answer");
        var workItem = await LabelledAsync("ANSWER", 40, "sofa-answer");
        var questionId = Guid.NewGuid();
        await IntakeDoesAsync("/diagnostics/questions", new
        {
            questionId, requestId = workItem.RequestId, workItemKey = workItem.Key, phase = "Spec",
            question = "Should the toggle follow the system setting by default?",
        });
        var question = Assert.Single(sofa.GitHub.CommentsOn(Demo, 40));
        Assert.Contains("Should the toggle follow the system setting by default?", question.Body);

        var strangersReply = sofa.GitHub.NextCommentId();
        var strangerIgnored = LogsAsync(SofaServices.Intake, $"Ignored {FakeGitHub.Mallory.Login}'s reply on {Demo}#40");
        await GitHubWebhooks.CommentedAsync(sofa, Demo, 40, strangersReply, "No, never.", FakeGitHub.Mallory);
        Assert.True(await strangerIgnored, "Intake never said it ignored the stranger's reply.");

        var beasReply = sofa.GitHub.NextCommentId();
        var flowHearsTheAnswer = LogsAsync(SofaServices.Flow, $"Flow received the answer to Question {questionId}");
        await GitHubWebhooks.CommentedAsync(sofa, Demo, 40, beasReply,
            "> Should the toggle follow the system setting by default?\n\n@sofa-acme[bot] Yes, follow the system setting.", FakeGitHub.Bea);

        Assert.True(await flowHearsTheAnswer, "Flow never received the answer event.");
        var answered = await AnswerOfAsync(questionId);
        Assert.Equal((beasReply, await sofa.BeaAddedByHerGitHubLogin, "Yes, follow the system setting."), answered);
    }

    [Fact]
    public async Task With_the_issues_state_kept_its_label_follows_the_Phase_from_Triage_on_and_it_closes_once_the_work_reaches_main()
    {
        await ProjectTakingRequestsAsync("STATE", "sofa-state", keepsIssueState: true);
        var workItem = await LabelledAsync("STATE", 50, "sofa-state");
        await EventuallyAsync(() => Task.FromResult(sofa.GitHub.ChangesOf(Demo, 50).Contains("+sofa: triage") ? true : (bool?)null));

        await EnteredPhaseAsync(workItem, "Triage");
        await EnteredPhaseAsync(workItem, "Spec");
        await IntakeDoesAsync("/diagnostics/work-items/reached-main-branch",
            new { workItemId = Guid.NewGuid(), workspaceId = await WorkspaceAsync(), requestId = workItem.RequestId, key = workItem.Key });

        Assert.Equal(["+sofa: triage", "-sofa: triage", "+sofa: spec", "-sofa: spec", "closed as completed"],
            sofa.GitHub.ChangesOf(Demo, 50));
    }

    [Fact]
    public async Task Without_the_issues_state_kept_Intake_leaves_its_labels_alone()
    {
        await ProjectTakingRequestsAsync("LEAVE", "sofa-leave");
        var workItem = await LabelledAsync("LEAVE", 60, "sofa-leave");

        await EnteredPhaseAsync(workItem, "Triage");
        await IntakeDoesAsync("/diagnostics/work-items/reached-main-branch",
            new { workItemId = Guid.NewGuid(), workspaceId = await WorkspaceAsync(), requestId = workItem.RequestId, key = workItem.Key });

        Assert.Empty(sofa.GitHub.ChangesOf(Demo, 60));
    }

    [Fact]
    public async Task A_binding_changed_in_Flow_reaches_Intake()
    {
        var projectId = await ProjectTakingRequestsAsync("MOVE", "sofa-move");
        using var ada = await sofa.AdaSignedInAsync(Cancellation);

        using var rebound = await ada.SendAsync(HttpMethod.Put, $"/api/workspaces/{await WorkspaceAsync()}/projects/MOVE/binding", new
        {
            connectionId = (await sofa.TheConnection).ConnectionId,
            requestRepository = "acme/web",
            label = "sofa-moved",
            codeRepositories = new[] { Demo },
            keepsIssueState = true,
        }, Cancellation);

        Assert.Equal(HttpStatusCode.OK, rebound.StatusCode);
        await IntakeKnowsAsync(projectId, "sofa-moved");
        await GitHubWebhooks.LabeledAsync(sofa, "acme/web", 70, "Moved here", "sofa-moved", FakeGitHub.Bea);
        Assert.Equal("MOVE-1", (await EventuallyAsync(async () => (await WorkItemsOfAsync("MOVE")).SingleOrDefault())).Key);
    }

    private async Task<WorkItem> BeaLabelledIssue12Async()
    {
        await OneAtATime.WaitAsync(Cancellation);
        try
        {
            if (_issue12 is null)
            {
                await sofa.BeaAddedByHerGitHubLogin;
                await IntakeKnowsAsync(ProjectIdOf(await sofa.TheDemoProject), "sofa");
                await GitHubWebhooks.LabeledAsync(sofa, Demo, 12, "Add a dark mode toggle", "sofa", FakeGitHub.Bea);
                _issue12 = await EventuallyAsync(async () => (await WorkItemsOfAsync("DEMO")).SingleOrDefault(w => w.Request.Number == 12));
            }

            return _issue12;
        }
        finally
        {
            OneAtATime.Release();
        }
    }

    private async Task<Guid> ProjectTakingRequestsAsync(string key, string label, bool keepsIssueState = false)
    {
        var projectId = ProjectIdOf(await TestProjects.CreateAsync(sofa, key, label, keepsIssueState));
        await IntakeKnowsAsync(projectId, label);
        return projectId;
    }

    private async Task<WorkItem> LabelledAsync(string key, long number, string label)
    {
        await sofa.BeaAddedByHerGitHubLogin;
        await GitHubWebhooks.LabeledAsync(sofa, Demo, number, $"Issue {number}", label, FakeGitHub.Bea);
        return await EventuallyAsync(async () => (await WorkItemsOfAsync(key)).SingleOrDefault(w => w.Request.Number == number));
    }

    private async Task PostReportAsync(WorkItem workItem, string phase, string summary, string detail) =>
        await IntakeDoesAsync("/diagnostics/reports", new { requestId = workItem.RequestId, workItemKey = workItem.Key, phase, summary, detail });

    private async Task EnteredPhaseAsync(WorkItem workItem, string phase) =>
        await IntakeDoesAsync("/diagnostics/work-items/entered-phase", new
        {
            workItemId = Guid.NewGuid(), workspaceId = await WorkspaceAsync(), requestId = workItem.RequestId, key = workItem.Key, phase,
        });

    private async Task IntakeDoesAsync(string path, object message)
    {
        using var intake = sofa.CreateHttpClient(SofaServices.Intake);
        using var response = await intake.PostAsJsonAsync(path, message, Cancellation);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task<List<WorkItem>> WorkItemsOfAsync(string key)
    {
        using var ada = await sofa.AdaSignedInAsync(Cancellation);
        using var response = await ada.GetWithTokenAsync($"/api/workspaces/{await WorkspaceAsync()}/projects/{key}/work-items", Cancellation);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<WorkItem>>(Cancellation))!;
    }

    private async Task<List<InboxItem>> AdminsInboxAsync()
    {
        using var notices = sofa.CreateHttpClient(SofaServices.Notices);
        return (await notices.GetFromJsonAsync<List<InboxItem>>($"/workspaces/{await WorkspaceAsync()}/inbox/admins", Cancellation))!;
    }

    private async Task IntakeKnowsAsync(Guid projectId, string label) =>
        await EventuallyAsync(async () =>
            await CountAsync($"select count(*) from intake.request_bindings where project_id = '{projectId}' and label = '{label}'") == 1
                ? true
                : (bool?)null);

    private async Task<(long?, Guid?, string?)> AnswerOfAsync(Guid questionId)
    {
        await using var database = await DatabaseAsync();
        await using var query = new NpgsqlCommand(
            "select answered_by_comment_id, answered_by_user_id, answer from intake.question_comments where question_id = @id", database);
        query.Parameters.AddWithValue("id", questionId);
        await using var row = await query.ExecuteReaderAsync(Cancellation);
        Assert.True(await row.ReadAsync(Cancellation));
        return (row.GetInt64(0), row.GetGuid(1), row.GetString(2));
    }

    private async Task<long> CountAsync(string sql)
    {
        await using var database = await DatabaseAsync();
        await using var query = new NpgsqlCommand(sql, database);
        return (long)(await query.ExecuteScalarAsync(Cancellation))!;
    }

    private async Task<NpgsqlConnection> DatabaseAsync()
    {
        var database = new NpgsqlConnection(await sofa.App.GetConnectionStringAsync("sofa", Cancellation));
        await database.OpenAsync(Cancellation);
        return database;
    }

    private static async Task<T> EventuallyAsync<T>(Func<Task<T?>> find)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(ArrivesWithin);
        while (true)
        {
            if (await find() is { } found)
            {
                return found;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), timeout.Token);
        }
    }

    private async Task<bool> LogsAsync(string service, string text)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(ArrivesWithin);
        var logs = sofa.App.Services.GetRequiredService<ResourceLoggerService>();
        var resource = sofa.App.Services.GetRequiredService<DistributedApplicationModel>().Resources.Single(r => r.Name == service);
        try
        {
            await foreach (var batch in logs.WatchAsync(resource).WithCancellation(timeout.Token))
            {
                if (batch.Any(line => line.Content.Contains(text, StringComparison.Ordinal)))
                {
                    return true;
                }
            }
        }
        catch (OperationCanceledException) when (!Cancellation.IsCancellationRequested)
        {
        }

        return false;
    }

    private static Guid ProjectIdOf(JsonElement project) => project.GetProperty("id").GetGuid();

    private async Task<Guid> WorkspaceAsync() => (await sofa.SetUp).FirstUser.WorkspaceId;

    private sealed record WorkItem(string Key, string Title, Guid RequestId, RequestRef Request, DelegatingUser? DelegatedBy);

    private sealed record RequestRef(string Repository, long Number, string Url);

    private sealed record DelegatingUser(Guid UserId, string Name);

    private sealed record InboxItem(string Kind, string For, Guid SubjectId, string ProjectKey, string Title, string Detail, string Url);
}
