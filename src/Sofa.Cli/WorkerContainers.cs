using System.Diagnostics;

namespace Sofa.Cli;

public interface IDocker
{
    Task<int> RunAttachedToTheTerminalAsync(IReadOnlyList<string> arguments, CancellationToken cancellation);

    Task<(int ExitCode, string Output)> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellation);
}

public static class WorkerContainers
{
    public const string WhereClaudeCodeKeepsItsSignIn = "/claude";

    public static IReadOnlyList<string> ImageCheck(string image) => ["image", "inspect", "--format", "{{.Id}}", image];

    public static IReadOnlyList<string> SignIn(string volume, string image) =>
        ["run", "-it", "--rm", "-v", $"{volume}:{WhereClaudeCodeKeepsItsSignIn}", image, "claude", "auth", "login", "--claudeai"];

    public static IReadOnlyList<string> SignInStatus(string volume, string image) =>
        ["run", "--rm", "-v", $"{volume}:{WhereClaudeCodeKeepsItsSignIn}", image, "claude", "auth", "status", "--json"];
}

public sealed class DockerOnThisBox : IDocker
{
    public async Task<int> RunAttachedToTheTerminalAsync(IReadOnlyList<string> arguments, CancellationToken cancellation)
    {
        using var docker = Process.Start(StartInfo(arguments, captureOutput: false))
            ?? throw new InvalidOperationException("Docker didn't start.");
        await docker.WaitForExitAsync(cancellation);
        return docker.ExitCode;
    }

    public async Task<(int ExitCode, string Output)> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellation)
    {
        using var docker = Process.Start(StartInfo(arguments, captureOutput: true))
            ?? throw new InvalidOperationException("Docker didn't start.");
        var output = docker.StandardOutput.ReadToEndAsync(cancellation);
        var errors = docker.StandardError.ReadToEndAsync(cancellation);
        await docker.WaitForExitAsync(cancellation);
        await errors;
        return (docker.ExitCode, await output);
    }

    private static ProcessStartInfo StartInfo(IReadOnlyList<string> arguments, bool captureOutput)
    {
        var start = new ProcessStartInfo("docker")
        {
            UseShellExecute = false,
            RedirectStandardOutput = captureOutput,
            RedirectStandardError = captureOutput,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        return start;
    }
}
