using System.Net;
using Sofa.Contracts;
using Sofa.Contracts.Flow;
using Sofa.Contracts.Intake;
using Sofa.Contracts.Records;

namespace Sofa.AgentGateway.WorkItems;

public sealed class FlowWorkItems(HttpClient flow)
{
    public static readonly Uri Address = new($"http://{SofaServices.Flow}");

    public async Task<WorkItemDetails?> WorkItemAsync(Guid workspaceId, Guid workItemId, CancellationToken cancellation)
    {
        using var response = await flow.GetAsync($"/workspaces/{workspaceId}/work-items/{workItemId}", cancellation);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<WorkItemDetails>(cancellation);
    }
}

public abstract record RequestRead
{
    public sealed record Now(RequestNow Request) : RequestRead;

    public sealed record Unread(string Why) : RequestRead;
}

public sealed class IntakeRequests(HttpClient intake)
{
    public static readonly Uri Address = new($"http://{SofaServices.Intake}");

    public async Task<RequestRead> RequestNowAsync(Guid workspaceId, Guid requestId, CancellationToken cancellation)
    {
        try
        {
            using var response = await intake.GetAsync($"/workspaces/{workspaceId}/requests/{requestId}/now", cancellation);
            if (response.IsSuccessStatusCode)
            {
                return new RequestRead.Now((await response.Content.ReadFromJsonAsync<RequestNow>(cancellation))!);
            }

            return new RequestRead.Unread(response.StatusCode switch
            {
                HttpStatusCode.NotFound => "Intake has no such Request.",
                HttpStatusCode.Gone => "the issue is no longer on GitHub, or Sofa may no longer read it.",
                _ => $"Intake answered {(int)response.StatusCode}.",
            });
        }
        catch (HttpRequestException failed)
        {
            return new RequestRead.Unread($"Intake couldn't be reached: {failed.Message}");
        }
    }
}

public sealed class RecordsContributions(HttpClient records)
{
    public static readonly Uri Address = new($"http://{SofaServices.Records}");

    public async Task<IReadOnlyList<ContributionDetails>> OnAsync(Guid workspaceId, Guid workItemId, CancellationToken cancellation) =>
        (await records.GetFromJsonAsync<List<ContributionDetails>>(
            $"/workspaces/{workspaceId}/work-items/{workItemId}/contributions", cancellation))!;
}
