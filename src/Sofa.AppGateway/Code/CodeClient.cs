using System.Net;
using Sofa.Contracts;
using Sofa.Contracts.Code;

namespace Sofa.AppGateway.Code;

public sealed class CodeClient(HttpClient code)
{
    public static readonly Uri Address = new($"http://{SofaServices.Code}");

    public async Task<List<PullRequestOfWorkItem>?> PullRequestsOfAsync(
        Guid workspaceId, string key, string workItemKey, CancellationToken cancellation)
    {
        using var response = await code.GetAsync(
            $"/workspaces/{workspaceId}/projects/{Uri.EscapeDataString(key)}/work-items/{Uri.EscapeDataString(workItemKey)}/pull-requests",
            cancellation);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<List<PullRequestOfWorkItem>>(cancellation);
    }
}
