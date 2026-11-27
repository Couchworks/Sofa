namespace Sofa.Contracts.Connections;

public sealed record GitHubEventReceived(
    Guid DeliveryId,
    string Event,
    string? Action,
    long? InstallationId,
    Guid? ConnectionId,
    Guid? WorkspaceId,
    string Payload)
{
    public string Name => Action is null ? Event : $"{Event}.{Action}";
}
