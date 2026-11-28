using System.Net;
using System.Net.Http.Json;
using Npgsql;
using Sofa.Cli;
using Sofa.Contracts;

namespace Sofa.IntegrationTests;

public class RunnerTests(SofaApp sofa)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_API_key_is_never_returned_once_saved()
    {
        var apiKey = $"sk-ant-api03-integration-{Guid.NewGuid():N}";
        using var ada = await sofa.AdaSignedInAsync(Cancellation);

        using var created = await ada.PostAsJsonAsync($"{await WorkspaceApiAsync()}/runners/api-key",
            new { name = "Ada's key", apiKey, forTheTeam = false }, Cancellation);
        var createdBody = await created.Content.ReadAsStringAsync(Cancellation);
        using var listed = await ada.GetWithTokenAsync($"{await WorkspaceApiAsync()}/runners", Cancellation);
        var listedBody = await listed.Content.ReadAsStringAsync(Cancellation);
        var runner = (await created.Content.ReadFromJsonAsync<Runner>(Cancellation))!;

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.True(runner.HasApiKey);
        Assert.DoesNotContain(apiKey, createdBody);
        Assert.DoesNotContain(apiKey, listedBody);
        Assert.DoesNotContain(await StoredApiKeysAsync(), stored => stored.Contains(apiKey, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Something_that_is_no_API_key_is_refused()
    {
        using var ada = await sofa.AdaSignedInAsync(Cancellation);

        using var response = await ada.PostAsJsonAsync($"{await WorkspaceApiAsync()}/runners/api-key",
            new { name = "Wrong", apiKey = "hunter2", forTheTeam = false }, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_subscription_whose_sign_in_ends_within_three_days_warns_its_User_in_their_Inbox_and_live()
    {
        var (uma, _) = await new TestRuns(sofa).UserOfTheirOwnAsync(FakeGitHub.Uma);
        var runner = await AddAsync(uma, "subscription", new { name = "Uma's Pro" });
        await using var live = await LiveUpdates.ConnectAsync(sofa, uma, (await sofa.SetUp).FirstUser.WorkspaceId, Cancellation);
        using var execution = sofa.CreateHttpClient(SofaServices.Execution);

        using var seen = await execution.PutAsJsonAsync($"/runners/{runner.Id}/sign-in",
            new { signedIn = true, endsAt = DateTimeOffset.UtcNow.AddDays(2) }, Cancellation);
        var warning = await EventuallyAsync(async () => (await InboxOfAsync(uma)).SingleOrDefault(i => i.Kind == "SubscriptionSignInEnding"));
        await EventuallyAsync(() => Task.FromResult(live.Heard.Any(h => h.Message == "inboxChanged") ? (bool?)true : null));
        using var signedInAgain = await execution.PutAsJsonAsync($"/runners/{runner.Id}/sign-in",
            new { signedIn = true, endsAt = DateTimeOffset.UtcNow.AddDays(30) }, Cancellation);
        var settled = await EventuallyAsync(async () => (await InboxOfAsync(uma)).Any(i => i.Kind == "SubscriptionSignInEnding") ? null : (bool?)true);

        Assert.Equal(HttpStatusCode.OK, seen.StatusCode);
        Assert.Equal(("Uma's Pro's sign-in ends soon", "/runners", runner.Id), (warning.Title, warning.Url, warning.SubjectId));
        Assert.Contains($"sofa runner login {runner.Id}", warning.Detail);
        Assert.True(settled);
    }

    [Fact]
    public async Task A_live_connection_is_refused_to_anyone_outside_the_Workspace()
    {
        using var gateway = sofa.CreateHttpClient(SofaServices.AppGateway);

        using var negotiating = await gateway.PostAsync(
            $"/api/workspaces/{(await sofa.SetUp).FirstUser.WorkspaceId}/live/negotiate?negotiateVersion=1&access_token=not-a-token", null, Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, negotiating.StatusCode);
    }

    [Fact]
    public async Task An_Admin_has_a_Runner_of_each_kind_each_with_its_limit()
    {
        using var ada = await sofa.AdaSignedInAsync(Cancellation);

        var subscription = await AddAsync(ada, "subscription", new { name = "Ada's Max" });
        var personalKey = await AddAsync(ada, "api-key", new { name = "Ada's API key", apiKey = "sk-ant-api03-personal", runsAtOnce = 3, forTheTeam = false });
        var teamKey = await AddAsync(ada, "api-key", new { name = "Engines' API key", apiKey = "sk-ant-api03-team", forTheTeam = true });
        var seen = await RunnersSeenByAsync(ada);

        Assert.Equal(("Subscription", "User", 2), (subscription.Kind, subscription.Owner.Kind, subscription.RunsAtOnce));
        Assert.Equal($"sofa-worker-{subscription.Id}", subscription.WorkerVolume);
        Assert.False(subscription.SignIn!.SignedIn);
        Assert.Equal(("ApiKey", "User", 3), (personalKey.Kind, personalKey.Owner.Kind, personalKey.RunsAtOnce));
        Assert.Equal(("ApiKey", "Team", 2), (teamKey.Kind, teamKey.Owner.Kind, teamKey.RunsAtOnce));
        Assert.Equal((await sofa.SetUp).FirstUser.TeamId, teamKey.Owner.Id);
        Assert.Superset(new[] { subscription.Id, personalKey.Id, teamKey.Id }.ToHashSet(), seen.Select(r => r.Id).ToHashSet());
    }

    [Fact]
    public async Task A_subscription_Runner_signed_in_with_sofa_runner_login_shows_until_when()
    {
        using var ada = await sofa.AdaSignedInAsync(Cancellation);
        var runner = await AddAsync(ada, "subscription", new { name = "Ada's Pro", runsAtOnce = 1 });
        using var execution = new HttpClient { BaseAddress = sofa.AddressOf(SofaServices.Execution) };
        var docker = new DockerThatSignsIn("""{"loggedIn":true,"authMethod":"claude.ai","loginExpiresAt":"2026-12-24T10:00:00.000Z","email":"ada@example.com"}""");

        var exitCode = await new RunnerSignIn(execution, docker, new StringWriter(), "sofa-worker")
            .LoginAsync(runner.Id.ToString(), inATerminal: true, Cancellation);
        var signedIn = (await RunnersSeenByAsync(ada)).Single(r => r.Id == runner.Id);

        Assert.Equal(RunnerSignIn.Done, exitCode);
        Assert.Contains(docker.Volumes, volume => volume == runner.WorkerVolume);
        Assert.True(signedIn.SignIn!.SignedIn);
        Assert.Equal(new DateTimeOffset(2026, 12, 24, 10, 0, 0, TimeSpan.Zero), signedIn.SignIn.EndsAt);
        Assert.NotNull(signedIn.SignIn.CheckedAt);
    }

    [Fact]
    public async Task A_User_added_to_the_Team_sees_the_team_Runner_and_no_one_elses_personal_Runner()
    {
        var workspaceApi = await WorkspaceApiAsync();
        var team = (await sofa.SetUp).FirstUser.TeamId;
        using var ada = await sofa.AdaSignedInAsync(Cancellation);
        var teamRunner = await AddAsync(ada, "api-key", new { name = "Shared key", apiKey = "sk-ant-api03-shared", forTheTeam = true });
        var adasRunner = await AddAsync(ada, "subscription", new { name = "Ada's own" });
        var erinId = await sofa.AddedByGitHubLoginAsync(FakeGitHub.Erin);
        using var erin = await sofa.SignedInWithGitHubAsync(FakeGitHub.Erin, Cancellation);

        var seenOnJoining = await RunnersSeenByAsync(erin);
        using var removed = await ada.SendAsync(HttpMethod.Delete, $"{workspaceApi}/teams/{team}/users/{erinId}", null, Cancellation);
        var seenOutsideTheTeam = await RunnersSeenByAsync(erin);
        using var addedBack = await ada.PostAsJsonAsync($"{workspaceApi}/teams/{team}/users", new { userId = erinId }, Cancellation);
        var seenBackInTheTeam = await RunnersSeenByAsync(erin);

        Assert.Contains(seenOnJoining, r => r.Id == teamRunner.Id);
        Assert.DoesNotContain(seenOnJoining, r => r.Id == adasRunner.Id);
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);
        Assert.DoesNotContain(seenOutsideTheTeam, r => r.Id == teamRunner.Id);
        Assert.Equal(HttpStatusCode.NoContent, addedBack.StatusCode);
        Assert.Contains(seenBackInTheTeam, r => r.Id == teamRunner.Id);
    }

    [Fact]
    public async Task Only_an_Admin_adds_a_team_Runner_or_changes_one()
    {
        using var ada = await sofa.AdaSignedInAsync(Cancellation);
        var teamRunner = await AddAsync(ada, "api-key", new { name = "Admins' key", apiKey = "sk-ant-api03-admins", forTheTeam = true });
        using var bea = await sofa.BeaSignedInAsync(Cancellation);

        using var adding = await bea.PostAsJsonAsync($"{await WorkspaceApiAsync()}/runners/api-key",
            new { name = "Bea's team key", apiKey = "sk-ant-api03-bea", forTheTeam = true }, Cancellation);
        using var limiting = await bea.SendAsync(HttpMethod.Put,
            $"{await WorkspaceApiAsync()}/runners/{teamRunner.Id}/runs-at-once", new { runsAtOnce = 9 }, Cancellation);
        using var adaLimits = await ada.SendAsync(HttpMethod.Put,
            $"{await WorkspaceApiAsync()}/runners/{teamRunner.Id}/runs-at-once", new { runsAtOnce = 4 }, Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, adding.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, limiting.StatusCode);
        Assert.Equal(HttpStatusCode.OK, adaLimits.StatusCode);
        Assert.Equal(4, (await adaLimits.Content.ReadFromJsonAsync<Runner>(Cancellation))!.RunsAtOnce);
    }

    [Fact]
    public async Task A_limit_of_no_Runs_at_once_is_refused()
    {
        using var ada = await sofa.AdaSignedInAsync(Cancellation);

        using var response = await ada.PostAsJsonAsync($"{await WorkspaceApiAsync()}/runners/subscription",
            new { name = "Shared Max", runsAtOnce = 0 }, Cancellation);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private async Task<Runner> AddAsync(WebClientSignIn user, string kind, object runner)
    {
        using var created = await user.PostAsJsonAsync($"{await WorkspaceApiAsync()}/runners/{kind}", runner, Cancellation);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<Runner>(Cancellation))!;
    }

    private async Task<List<Runner>> RunnersSeenByAsync(WebClientSignIn user)
    {
        using var response = await user.GetWithTokenAsync($"{await WorkspaceApiAsync()}/runners", Cancellation);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<Runner>>(Cancellation))!;
    }

    private async Task<List<string>> StoredApiKeysAsync()
    {
        await using var database = new NpgsqlConnection(await sofa.App.GetConnectionStringAsync("sofa", Cancellation));
        await database.OpenAsync(Cancellation);
        await using var query = new NpgsqlCommand("select sealed_key from connections.api_keys", database);
        await using var reader = await query.ExecuteReaderAsync(Cancellation);
        var stored = new List<string>();
        while (await reader.ReadAsync(Cancellation))
        {
            stored.Add(reader.GetString(0));
        }

        Assert.NotEmpty(stored);
        return stored;
    }

    private async Task<List<InboxItem>> InboxOfAsync(WebClientSignIn user)
    {
        using var read = await user.GetWithTokenAsync($"{await WorkspaceApiAsync()}/inbox", Cancellation);
        read.EnsureSuccessStatusCode();
        return (await read.Content.ReadFromJsonAsync<List<InboxItem>>(Cancellation))!;
    }

    private static async Task<T> EventuallyAsync<T>(Func<Task<T?>> find)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(TestRuns.RunsSettleWithin);
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

    private sealed class DockerThatSignsIn(string status) : IDocker
    {
        public List<string> Volumes { get; } = [];

        public Task<int> RunAttachedToTheTerminalAsync(IReadOnlyList<string> arguments, CancellationToken cancellation)
        {
            Volumes.Add(arguments[arguments.ToList().IndexOf("-v") + 1].Split(':')[0]);
            return Task.FromResult(0);
        }

        public Task<(int ExitCode, string Output)> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellation) =>
            Task.FromResult(arguments[0] == "image" ? (0, "") : (0, status));
    }

    private sealed record Runner(
        Guid Id, string Name, string Kind, string Vendor, Owner Owner, int RunsAtOnce, bool HasApiKey, string? WorkerVolume, SignIn? SignIn);

    private sealed record Owner(string Kind, Guid Id);

    private sealed record InboxItem(string Kind, Guid SubjectId, string Title, string Detail, string Url);

    private sealed record SignIn(bool SignedIn, DateTimeOffset? EndsAt, DateTimeOffset? CheckedAt);
}
