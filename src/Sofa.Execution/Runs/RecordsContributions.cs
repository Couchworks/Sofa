using System.Net.Http.Headers;
using System.Text.Json;
using Sofa.Contracts;
using Sofa.Contracts.Records;

namespace Sofa.Execution.Runs;

public sealed record AttachmentCopied(string Name, byte[] Content);

public sealed class RecordsContributions(HttpClient records)
{
    public static readonly Uri Address = new($"http://{SofaServices.Records}");

    public static readonly TimeSpan TakesAtMost = TimeSpan.FromMinutes(2);

    public async Task KeepAsync(Run run, string outcome, string markdown, IReadOnlyList<AttachmentCopied> attachments, CancellationToken cancellation)
    {
        using var form = new MultipartFormDataContent();
        form.Add(new StringContent(JsonSerializer.Serialize(
            new ContributionToKeep(run.WorkItemId, run.Phase, run.AgentId, outcome, markdown), JsonSerializerOptions.Web)),
            ContributionForms.Contribution);
        foreach (var attachment in attachments)
        {
            var file = new ByteArrayContent(attachment.Content);
            file.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            form.Add(file, ContributionForms.Attachment, attachment.Name);
        }

        using var response = await records.PutAsync($"/workspaces/{run.WorkspaceId}/runs/{run.Id}/contribution", form, cancellation);
        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"Records answered {(int)response.StatusCode} to Run {run.Id}'s Contribution: {await response.Content.ReadAsStringAsync(cancellation)}",
                inner: null,
                response.StatusCode);
        }
    }
}
