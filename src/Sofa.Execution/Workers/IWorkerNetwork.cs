namespace Sofa.Execution.Workers;

public sealed record WorkerNetwork(string Name, IReadOnlyDictionary<string, string> HostsAt);

public sealed record WorkerPlacement(WorkerNetwork Network, Uri AgentGateway);

public interface IWorkerNetwork
{
    Task<WorkerPlacement> PlaceAsync(CancellationToken cancellation);

    Task RemoveAsync(CancellationToken cancellation);
}
