using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR.Client;
using Sofa.Contracts;

namespace Sofa.IntegrationTests;

public sealed class LiveUpdates : IAsyncDisposable
{
    private readonly HubConnection _connection;

    private LiveUpdates(HubConnection connection)
    {
        _connection = connection;
        foreach (var message in new[] { "workItemChanged", "runChanged" })
        {
            _connection.On<JsonElement>(message, said => Heard.Enqueue((message, said)));
        }

        _connection.On("inboxChanged", () => Heard.Enqueue(("inboxChanged", default)));
    }

    public ConcurrentQueue<(string Message, JsonElement Said)> Heard { get; } = new();

    public static async Task<LiveUpdates> ConnectAsync(SofaApp sofa, WebClientSignIn user, Guid workspaceId, CancellationToken cancellation)
    {
        var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(sofa.AddressOf(SofaServices.AppGateway), $"/api/workspaces/{workspaceId}/live"),
                options => options.AccessTokenProvider = () => Task.FromResult<string?>(user.Tokens!.AccessToken))
            .Build();
        var live = new LiveUpdates(connection);
        await connection.StartAsync(cancellation);
        return live;
    }

    public bool HeardThatWorkItemChanged(string workItemKey) =>
        Heard.Any(h => h.Message == "workItemChanged" && h.Said.GetProperty("workItemKey").GetString() == workItemKey);

    public bool HeardThatRunChanged(Guid runId) =>
        Heard.Any(h => h.Message == "runChanged" && h.Said.GetProperty("runId").GetGuid() == runId);

    public bool HeardThatTheInboxChanged => Heard.Any(h => h.Message == "inboxChanged");

    public ValueTask DisposeAsync() => _connection.DisposeAsync();
}
