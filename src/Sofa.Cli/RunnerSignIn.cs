using System.Net;
using System.Net.Http.Json;
using Sofa.Contracts.Execution;

namespace Sofa.Cli;

public sealed class RunnerSignIn(HttpClient execution, IDocker docker, TextWriter output, string image)
{
    public const int Done = 0;
    public const int Failed = 1;

    public const string VolumesSofaNames = "sofa-worker-";

    public async Task<int> LoginAsync(string runner, bool inATerminal, CancellationToken cancellation)
    {
        if (await SubscriptionRunnerAsync(runner, cancellation) is not { } toSignIn || !await ImageIsBuiltAsync(cancellation))
        {
            return Failed;
        }

        if (!inATerminal)
        {
            await output.WriteLineAsync("Signing in needs a terminal: run `sofa runner login` in one, not through a pipe.");
            return Failed;
        }

        await output.WriteLineAsync($"Signing {toSignIn.Name} in. Claude Code shows a link: open it, sign in with your subscription, and paste the code back here.");
        var exitCode = await docker.RunAttachedToTheTerminalAsync(WorkerContainers.SignIn(toSignIn.WorkerVolume!, image), cancellation);
        if (exitCode != 0)
        {
            await output.WriteLineAsync($"Claude Code's sign-in ended with exit code {exitCode}; checking what the volume holds anyway.");
        }

        return await RecordAsync(toSignIn, cancellation);
    }

    public async Task<int> CheckAsync(string runner, CancellationToken cancellation)
    {
        if (await SubscriptionRunnerAsync(runner, cancellation) is not { } toSignIn || !await ImageIsBuiltAsync(cancellation))
        {
            return Failed;
        }

        return await RecordAsync(toSignIn, cancellation);
    }

    private async Task<int> RecordAsync(RunnerToSignIn runner, CancellationToken cancellation)
    {
        var (_, statusJson) = await docker.RunAsync(WorkerContainers.SignInStatus(runner.WorkerVolume!, image), cancellation);
        if (ClaudeAuthStatus.Read(statusJson) is not { } seen)
        {
            await output.WriteLineAsync("Claude Code didn't say whether the volume is signed in; nothing was recorded.");
            return Failed;
        }

        using var recorded = await execution.PutAsJsonAsync($"/runners/{runner.Id}/sign-in", seen, cancellation);
        if (!recorded.IsSuccessStatusCode)
        {
            await output.WriteLineAsync($"Execution didn't record the sign-in: it answered {(int)recorded.StatusCode}.");
            return Failed;
        }

        await output.WriteLineAsync(seen.SignedIn
            ? $"{runner.Name} is signed in to your subscription{(seen.EndsAt is { } endsAt ? $" until {endsAt:yyyy-MM-dd HH:mm} UTC" : "")}."
            : $"{runner.Name} is not signed in to a subscription. Run `sofa runner login {runner.Id}` to sign in.");
        return seen.SignedIn ? Done : Failed;
    }

    private async Task<RunnerToSignIn?> SubscriptionRunnerAsync(string runner, CancellationToken cancellation)
    {
        if (!Guid.TryParse(runner, out var runnerId))
        {
            await output.WriteLineAsync($"'{runner}' is not a Runner's id; the Runners page in Sofa shows the command with it.");
            return null;
        }

        HttpResponseMessage found;
        try
        {
            found = await execution.GetAsync($"/runners/{runnerId}/sign-in", cancellation);
        }
        catch (HttpRequestException unreachable)
        {
            await output.WriteLineAsync($"Couldn't reach Execution at {execution.BaseAddress}: {unreachable.Message} Is Sofa running? Set SOFA_EXECUTION to its address.");
            return null;
        }

        using (found)
        {
            if (found.StatusCode == HttpStatusCode.NotFound)
            {
                await output.WriteLineAsync($"Sofa has no Runner {runnerId}.");
                return null;
            }

            found.EnsureSuccessStatusCode();
            var toSignIn = await found.Content.ReadFromJsonAsync<RunnerToSignIn>(cancellation);
            if (toSignIn is not { Kind: RunnerKind.Subscription, WorkerVolume: { } volume } || !volume.StartsWith(VolumesSofaNames, StringComparison.Ordinal))
            {
                await output.WriteLineAsync($"{toSignIn?.Name ?? runner} is not a subscription Runner, so there is nothing to sign in.");
                return null;
            }

            return toSignIn;
        }
    }

    private async Task<bool> ImageIsBuiltAsync(CancellationToken cancellation)
    {
        var (exitCode, _) = await docker.RunAsync(WorkerContainers.ImageCheck(image), cancellation);
        if (exitCode == 0)
        {
            return true;
        }

        await output.WriteLineAsync($"The Worker image {image} isn't built on this box. Build it first: docker build -t {image} worker");
        return false;
    }
}
