namespace Sofa.Execution.Workers;

public sealed record WorkerContainerSpec(
    string Name,
    string Image,
    WorkerVolume? Volume,
    IReadOnlyDictionary<string, string> Environment,
    IReadOnlyList<string> Command,
    IReadOnlyDictionary<string, string> Labels);

public sealed record WorkerVolume(string Name, string MountedAt);

public sealed record WorkerExit(int ExitCode, string LastErrors);

public sealed record OutputInside(int ExitCode, byte[] Output, bool CutShort);

public interface IWorkerContainers
{
    Task<IWorkerContainer> StartAsync(WorkerContainerSpec spec, CancellationToken cancellation);

    Task RemoveAsync(string name, CancellationToken cancellation);
}

public interface IWorkerContainer : IAsyncDisposable
{
    IAsyncEnumerable<string> ReadLinesAsync(CancellationToken cancellation);

    Task TellAsync(string line, CancellationToken cancellation);

    Task<WorkerExit> WaitForExitAsync(CancellationToken cancellation);

    Task<OutputInside> RunInsideAsync(IReadOnlyList<string> command, long mostBytesRead, CancellationToken cancellation);

    Task StopAsync(CancellationToken cancellation);
}
