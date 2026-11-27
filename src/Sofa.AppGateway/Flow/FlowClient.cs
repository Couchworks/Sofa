using System.Net;
using Sofa.Contracts;
using Sofa.Contracts.Flow;

namespace Sofa.AppGateway.Flow;

public sealed class FlowClient(HttpClient flow)
{
    public static readonly Uri Address = new($"http://{SofaServices.Flow}");

    public async Task<List<ProjectSummary>> ProjectsOf(Guid workspaceId, CancellationToken cancellation) =>
        (await flow.GetFromJsonAsync<List<ProjectSummary>>($"/workspaces/{workspaceId}/projects", cancellation))!;

    public async Task<ProjectDetails?> ProjectAsync(Guid workspaceId, string key, CancellationToken cancellation)
    {
        using var response = await flow.GetAsync($"/workspaces/{workspaceId}/projects/{Uri.EscapeDataString(key)}", cancellation);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ProjectDetails>(cancellation);
    }
}
