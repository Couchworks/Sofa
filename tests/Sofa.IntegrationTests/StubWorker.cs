using System.Collections.Concurrent;
using System.Diagnostics;

namespace Sofa.IntegrationTests;

public static class StubWorker
{
    public const string Image = "sofa-stub-worker";

    private static readonly ConcurrentBag<string> VolumesMade = [];

    public static async Task BuildAsync(CancellationToken cancellation)
    {
        var (exitCode, output) = await DockerAsync(["build", "--quiet", "--tag", Image, StubWorkerDirectory()], cancellation);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Couldn't build the stub Worker image: {output}");
        }
    }

    public static async Task SignInOnTheVolumeAsync(string volume, CancellationToken cancellation)
    {
        VolumesMade.Add(volume);
        var (exitCode, output) = await DockerAsync(
            ["run", "--rm", "--volume", $"{volume}:/claude", Image, "claude", "sign-in-on-the-volume"], cancellation);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Couldn't sign the stub in on {volume}: {output}");
        }
    }

    public static void WillMake(string volume) => VolumesMade.Add(volume);

    public static async Task KillAsync(Guid runId, CancellationToken cancellation)
    {
        var (exitCode, output) = await DockerAsync(["kill", $"sofa-run-{runId}"], cancellation);
        if (exitCode != 0)
        {
            throw new InvalidOperationException($"Couldn't kill Run {runId}'s container: {output}");
        }
    }

    public static async Task RemoveTheWorkerNetworkAsync(int agentGatewayPort)
    {
        var network = $"sofa-workers-{agentGatewayPort}";
        await DockerAsync(["rm", "--force", $"{network}-relay"], CancellationToken.None);
        await DockerAsync(["network", "rm", $"{network}-egress"], CancellationToken.None);
        await DockerAsync(["network", "rm", network], CancellationToken.None);
    }

    public static async Task RemoveTheVolumesMadeAsync()
    {
        foreach (var volume in VolumesMade.Distinct())
        {
            await DockerAsync(["volume", "rm", "--force", volume], CancellationToken.None);
        }
    }

    private static string StubWorkerDirectory()
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Sofa.slnx")))
            {
                return Path.Combine(directory.FullName, "tests", "stub-worker");
            }
        }

        throw new InvalidOperationException("Couldn't find the repository, and the stub Worker in it.");
    }

    private static async Task<(int ExitCode, string Output)> DockerAsync(IReadOnlyList<string> arguments, CancellationToken cancellation)
    {
        var start = new ProcessStartInfo("docker") { RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        using var docker = Process.Start(start)!;
        var output = docker.StandardOutput.ReadToEndAsync(cancellation);
        var errors = docker.StandardError.ReadToEndAsync(cancellation);
        await docker.WaitForExitAsync(cancellation);
        return (docker.ExitCode, await output + await errors);
    }
}
