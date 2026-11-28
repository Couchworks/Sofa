using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Sofa.Execution.Workers;

public sealed class DockerWorkerContainers : IWorkerContainers
{
    public static IReadOnlyList<string> RunArguments(WorkerContainerSpec spec)
    {
        List<string> arguments = ["run", "--rm", "--interactive", "--name", spec.Name, "--network", spec.Network.Name];
        foreach (var (host, address) in spec.Network.HostsAt)
        {
            arguments.Add($"--add-host={host}:{address}");
        }

        foreach (var (name, value) in spec.Labels)
        {
            arguments.AddRange(["--label", $"{name}={value}"]);
        }

        if (spec.Volume is { } volume)
        {
            arguments.AddRange(["--volume", $"{volume.Name}:{volume.MountedAt}"]);
        }

        foreach (var name in spec.Environment.Keys)
        {
            arguments.AddRange(["--env", name]);
        }

        arguments.Add(spec.Image);
        arguments.AddRange(spec.Command);
        return arguments;
    }

    public Task<IWorkerContainer> StartAsync(WorkerContainerSpec spec, CancellationToken cancellation)
    {
        var start = Docker(RunArguments(spec));
        start.RedirectStandardInput = true;
        foreach (var (name, value) in spec.Environment)
        {
            start.Environment[name] = value;
        }

        var process = Process.Start(start) ?? throw new InvalidOperationException("Docker didn't start.");
        return Task.FromResult<IWorkerContainer>(new DockerContainer(process, spec.Name, this));
    }

    public async Task RemoveAsync(string name, CancellationToken cancellation)
    {
        using var docker = Process.Start(Docker(["rm", "--force", name])) ?? throw new InvalidOperationException("Docker didn't start.");
        await Task.WhenAll(docker.StandardOutput.ReadToEndAsync(cancellation), docker.StandardError.ReadToEndAsync(cancellation));
        await docker.WaitForExitAsync(cancellation);
    }

    private static ProcessStartInfo Docker(IReadOnlyList<string> arguments)
    {
        var start = new ProcessStartInfo("docker")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }

        return start;
    }

    private sealed class DockerContainer : IWorkerContainer
    {
        private const int ErrorLinesKept = 20;

        private readonly Process _docker;
        private readonly string _name;
        private readonly DockerWorkerContainers _containers;
        private readonly Queue<string> _lastErrors = new();
        private readonly Task _errorsRead;

        public DockerContainer(Process docker, string name, DockerWorkerContainers containers)
        {
            _docker = docker;
            _name = name;
            _containers = containers;
            _errorsRead = KeepTheLastErrorsAsync();
        }

        public async IAsyncEnumerable<string> ReadLinesAsync([EnumeratorCancellation] CancellationToken cancellation)
        {
            while (await _docker.StandardOutput.ReadLineAsync(cancellation) is { } line)
            {
                yield return line;
            }
        }

        public async Task TellAsync(string line, CancellationToken cancellation)
        {
            await _docker.StandardInput.WriteLineAsync(line.AsMemory(), cancellation);
            await _docker.StandardInput.FlushAsync(cancellation);
        }

        public async Task<WorkerExit> WaitForExitAsync(CancellationToken cancellation)
        {
            await _docker.WaitForExitAsync(cancellation);
            await _errorsRead;
            lock (_lastErrors)
            {
                return new WorkerExit(_docker.ExitCode, string.Join('\n', _lastErrors));
            }
        }

        public Task StopAsync(CancellationToken cancellation) => _containers.RemoveAsync(_name, cancellation);

        public async Task<OutputInside> RunInsideAsync(IReadOnlyList<string> command, long mostBytesRead, CancellationToken cancellation)
        {
            using var docker = Process.Start(Docker(["exec", _name, .. command])) ?? throw new InvalidOperationException("Docker didn't start.");
            var errors = docker.StandardError.ReadToEndAsync(cancellation);
            using var output = new MemoryStream();
            var buffer = new byte[81920];
            var cutShort = false;
            int read;
            while ((read = await docker.StandardOutput.BaseStream.ReadAsync(buffer, cancellation)) > 0)
            {
                var kept = (int)Math.Min(read, mostBytesRead - output.Length);
                output.Write(buffer, 0, kept);
                if (kept < read)
                {
                    cutShort = true;
                    docker.Kill(entireProcessTree: true);
                    break;
                }
            }

            await docker.WaitForExitAsync(cancellation);
            await errors;
            return new OutputInside(docker.ExitCode, output.ToArray(), cutShort);
        }

        public async ValueTask DisposeAsync()
        {
            if (!_docker.HasExited)
            {
                await StopAsync(CancellationToken.None);
            }

            if (!_docker.HasExited)
            {
                _docker.Kill(entireProcessTree: true);
            }

            _docker.Dispose();
        }

        private async Task KeepTheLastErrorsAsync()
        {
            while (await _docker.StandardError.ReadLineAsync() is { } line)
            {
                lock (_lastErrors)
                {
                    _lastErrors.Enqueue(line);
                    if (_lastErrors.Count > ErrorLinesKept)
                    {
                        _lastErrors.Dequeue();
                    }
                }
            }
        }
    }
}
