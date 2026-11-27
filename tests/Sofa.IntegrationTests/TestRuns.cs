using System.Net;
using System.Net.Http.Json;
using Aspire.Hosting.ApplicationModel;
using Microsoft.Extensions.DependencyInjection;
using Sofa.Contracts;

namespace Sofa.IntegrationTests;

public sealed class TestRuns(SofaApp sofa)
{
    public static readonly TimeSpan RunsSettleWithin = TimeSpan.FromSeconds(90);

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    public async Task<(WebClientSignIn User, Guid UserId)> UserOfTheirOwnAsync(GitHubAccountOnFake account)
    {
        var userId = await sofa.UserOnceAsync(account);
        return (await sofa.SignedInWithGitHubAsync(account, Cancellation), userId);
    }

    public async Task<Guid> StartAsync(
        Guid delegatingUserId, string phase, string? task = null, int? retryLimit = null, string? projectKey = null)
    {
        var project = projectKey ?? (await sofa.TheProjectForRuns).Key;
        using var flow = sofa.CreateHttpClient(SofaServices.Flow);
        using var started = await flow.PostAsJsonAsync("/diagnostics/test-runs", new
        {
            workspaceId = (await sofa.SetUp).FirstUser.WorkspaceId,
            projectKey = project,
            phase,
            delegatingUserId,
            task,
            retryLimit,
        }, Cancellation);
        Assert.Equal(HttpStatusCode.Accepted, started.StatusCode);
        return (await started.Content.ReadFromJsonAsync<TestRunStarted>(Cancellation))!.RunId;
    }

    public async Task<Run> UntilAsync(WebClientSignIn user, Guid runId, Func<Run, bool> until)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(RunsSettleWithin);
        Run? last = null;
        try
        {
            while (true)
            {
                using var read = await user.GetWithTokenAsync($"{await WorkspaceApiAsync()}/runs/{runId}", timeout.Token);
                if (read.StatusCode == HttpStatusCode.OK)
                {
                    last = (await read.Content.ReadFromJsonAsync<Run>(timeout.Token))!;
                    if (until(last))
                    {
                        return last;
                    }
                }

                await Task.Delay(TimeSpan.FromMilliseconds(200), timeout.Token);
            }
        }
        catch (OperationCanceledException) when (!Cancellation.IsCancellationRequested)
        {
            throw new TimeoutException($"Run {runId} never got there; it was last {last?.State}: {last?.Why}");
        }
    }

    public async Task RunningContainerAsync(WebClientSignIn user, Guid runId)
    {
        await UntilAsync(user, runId, r => r.State == "Running");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(RunsSettleWithin);
        while ((await TranscriptAsync(user, runId)).Entries.Count == 0)
        {
            await Task.Delay(TimeSpan.FromMilliseconds(200), timeout.Token);
        }
    }

    public async Task<List<TranscriptEntry>> WholeTranscriptAsync(WebClientSignIn user, Guid runId)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(RunsSettleWithin);
        while (true)
        {
            var transcript = await TranscriptAsync(user, runId);
            if (transcript.Entries.Count > 0 && transcript.Entries[^1].Text?.StartsWith("Ended:", StringComparison.Ordinal) == true)
            {
                return transcript.Entries;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(200), timeout.Token);
        }
    }

    public async Task<Transcript> TranscriptAsync(WebClientSignIn user, Guid runId, int? after = null)
    {
        using var read = await user.GetWithTokenAsync(
            $"{await WorkspaceApiAsync()}/runs/{runId}/transcript{(after is { } line ? $"?after={line}" : "")}", Cancellation);
        read.EnsureSuccessStatusCode();
        var transcript = (await read.Content.ReadFromJsonAsync<Transcript>(Cancellation))!;
        Assert.Equal(transcript.Entries.OrderBy(e => e.Line).ThenBy(e => e.Part), transcript.Entries);
        return transcript;
    }

    public async Task<Runner> AddRunnerAsync(WebClientSignIn user, string kind, object runner)
    {
        using var created = await user.PostAsJsonAsync($"{await WorkspaceApiAsync()}/runners/{kind}", runner, Cancellation);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        return (await created.Content.ReadFromJsonAsync<Runner>(Cancellation))!;
    }

    public async Task<List<Runner>> RunnersSeenByAsync(WebClientSignIn user)
    {
        using var response = await user.GetWithTokenAsync($"{await WorkspaceApiAsync()}/runners", Cancellation);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<List<Runner>>(Cancellation))!;
    }

    public async Task<string> LoggedByAsync(string service, Func<string, bool> matching)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Cancellation);
        timeout.CancelAfter(RunsSettleWithin);
        var logs = sofa.App.Services.GetRequiredService<ResourceLoggerService>();
        var resource = sofa.App.Services.GetRequiredService<DistributedApplicationModel>()
            .Resources.Single(r => r.Name == service);
        await foreach (var batch in logs.WatchAsync(resource).WithCancellation(timeout.Token))
        {
            if (batch.Select(line => line.Content).FirstOrDefault(matching) is { } content)
            {
                return content;
            }
        }

        throw new InvalidOperationException($"{service} never logged it.");
    }

    public async Task<string> WorkspaceApiAsync() => $"/api/workspaces/{(await sofa.SetUp).FirstUser.WorkspaceId}";

    public sealed record TestRunStarted(Guid RunId);

    public sealed record Run(
        Guid Id,
        string State,
        string? Why,
        string? RefusedAs,
        Guid? RunnerId,
        int Attempt,
        Guid? RetryOf,
        Guid? RetriedBy,
        bool WaitsForRetry,
        string? Summary,
        DateTimeOffset? StartedAt,
        DateTimeOffset? EndedAt);

    public sealed record Transcript(Guid RunId, List<TranscriptEntry> Entries, int ReadUpToLine);

    public sealed record TranscriptEntry(
        int Line,
        int Part,
        string Role,
        string? Text,
        string? ToolName,
        System.Text.Json.JsonElement? ToolInput,
        bool ToolInputCut,
        string? ToolResult,
        bool ToolResultCut,
        int? ToolResultLine,
        DateTimeOffset At)
    {
        public string Everything => $"{Text} {ToolName} {ToolInput?.GetRawText()} {ToolResult}";
    }

    public sealed record Runner(Guid Id, string Kind, Owner Owner, string? WorkerVolume, SignIn? SignIn);

    public sealed record Owner(string Kind, Guid Id);

    public sealed record SignIn(bool SignedIn, DateTimeOffset? EndsAt, DateTimeOffset? CheckedAt);
}
