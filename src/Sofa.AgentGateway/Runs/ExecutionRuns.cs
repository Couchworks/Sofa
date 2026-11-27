using System.Net;
using Sofa.Contracts;
using Sofa.Contracts.Execution;

namespace Sofa.AgentGateway.Runs;

public sealed class ExecutionRuns(HttpClient execution)
{
    public static readonly Uri Address = new($"http://{SofaServices.Execution}");

    public async Task<RunDetails?> RunAsync(Guid workspaceId, Guid runId, CancellationToken cancellation)
    {
        using var response = await execution.GetAsync($"/workspaces/{workspaceId}/runs/{runId}", cancellation);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<RunDetails>(cancellation);
    }
}
