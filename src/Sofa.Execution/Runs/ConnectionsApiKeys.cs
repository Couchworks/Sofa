using System.Net;
using Microsoft.Extensions.Options;
using Sofa.Contracts;
using Sofa.Contracts.Connections;

namespace Sofa.Execution.Runs;

public sealed class ConnectionsApiKeys(HttpClient connections, IOptions<RunTokenClientOptions> options)
{
    public static readonly Uri Address = new($"http://{SofaServices.Connections}");

    public async Task<string?> OpenForAsync(Guid runId, Guid workspaceId, Guid apiKeyId, CancellationToken cancellation)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/workspaces/{workspaceId}/api-keys/{apiKeyId}/opened-for-a-run")
        {
            Content = JsonContent.Create(new ApiKeyForARun(runId)),
        };
        request.Headers.Add(ApiKeysForRuns.ExecutionsSecretHeader, options.Value.ConnectionsSecret
            ?? throw new InvalidOperationException($"Execution needs {RunTokenClientOptions.Section}:ConnectionsSecret to open API keys for Runs."));
        using var response = await connections.SendAsync(request, cancellation);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<OpenedApiKey>(cancellation))?.ApiKey;
    }
}
