using System.Net;
using Sofa.Contracts;
using Sofa.Contracts.Records;

namespace Sofa.Flow.Reports;

public sealed class RecordsContributions(HttpClient records)
{
    public static readonly Uri Address = new($"http://{SofaServices.Records}");

    public async Task<ContributionDetails?> OfAsync(Guid workspaceId, Guid runId, CancellationToken cancellation)
    {
        using var response = await records.GetAsync($"/workspaces/{workspaceId}/runs/{runId}/contribution", cancellation);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ContributionDetails>(cancellation);
    }
}
