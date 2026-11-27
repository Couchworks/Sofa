using Sofa.Contracts;
using Sofa.Contracts.Records;

namespace Sofa.AppGateway.Records;

public sealed class RecordsClient(HttpClient records)
{
    public static readonly Uri Address = new($"http://{SofaServices.Records}");

    public async Task<Transcript> TranscriptOfAsync(Guid workspaceId, Guid runId, int? after, CancellationToken cancellation) =>
        (await records.GetFromJsonAsync<Transcript>(
            $"/workspaces/{workspaceId}/runs/{runId}/transcript{(after is { } line ? $"?after={line}" : "")}", cancellation))!;
}
