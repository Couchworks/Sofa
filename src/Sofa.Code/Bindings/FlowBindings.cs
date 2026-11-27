using System.Net;
using Sofa.Contracts;
using Sofa.Contracts.Flow;

namespace Sofa.Code.Bindings;

public sealed class FlowBindings(HttpClient flow)
{
    public static readonly Uri Address = new($"http://{SofaServices.Flow}");

    public async Task<ProjectBound?> CurrentAsync(Guid workspaceId, Guid projectId, CancellationToken cancellation)
    {
        using var response = await flow.GetAsync($"/workspaces/{workspaceId}/projects-by-id/{projectId}/binding", cancellation);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectBound>(cancellation);
    }
}
