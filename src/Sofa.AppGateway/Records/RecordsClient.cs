using System.Net;
using Sofa.Contracts;
using Sofa.Contracts.Records;

namespace Sofa.AppGateway.Records;

public sealed record AttachmentFile(Stream Content, string ContentType, string? DownloadName, HttpResponseMessage Response);

public sealed class RecordsClient(HttpClient records)
{
    public static readonly Uri Address = new($"http://{SofaServices.Records}");

    public async Task<Transcript> TranscriptOfAsync(Guid workspaceId, Guid runId, int? after, CancellationToken cancellation) =>
        (await records.GetFromJsonAsync<Transcript>(
            $"/workspaces/{workspaceId}/runs/{runId}/transcript{(after is { } line ? $"?after={line}" : "")}", cancellation))!;

    public async Task<ContributionDetails?> ContributionOfAsync(Guid workspaceId, Guid runId, CancellationToken cancellation)
    {
        using var response = await records.GetAsync($"/workspaces/{workspaceId}/runs/{runId}/contribution", cancellation);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<ContributionDetails>(cancellation);
    }

    public async Task<AttachmentFile?> AttachmentAsync(Guid workspaceId, Guid runId, Guid attachmentId, CancellationToken cancellation)
    {
        var response = await records.GetAsync(
            $"/workspaces/{workspaceId}/runs/{runId}/contribution/attachments/{attachmentId}", HttpCompletionOption.ResponseHeadersRead, cancellation);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            response.Dispose();
            return null;
        }

        response.EnsureSuccessStatusCode();
        return new AttachmentFile(
            await response.Content.ReadAsStreamAsync(cancellation),
            response.Content.Headers.ContentType?.MediaType ?? "application/octet-stream",
            response.Content.Headers.ContentDisposition?.FileNameStar ?? response.Content.Headers.ContentDisposition?.FileName?.Trim('"'),
            response);
    }
}
