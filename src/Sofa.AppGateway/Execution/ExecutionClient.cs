using System.Net;
using Sofa.AppGateway.Access;
using Sofa.Contracts;
using Sofa.Contracts.Execution;

namespace Sofa.AppGateway.Execution;

public sealed class ExecutionClient(HttpClient execution)
{
    public static readonly Uri Address = new($"http://{SofaServices.Execution}");

    public async Task<List<RunnerDetails>> RunnersSeenBy(SignedInWorkspaceUser user, CancellationToken cancellation)
    {
        var teams = string.Concat(user.TeamIds.Select(teamId => $"&teamId={teamId}"));
        return (await execution.GetFromJsonAsync<List<RunnerDetails>>(
            $"/workspaces/{user.WorkspaceId}/runners?userId={user.UserId}&admin={user.IsAdmin.ToString().ToLowerInvariant()}{teams}",
            cancellation))!;
    }

    public async Task<List<RunDetails>> RunsOf(Guid workspaceId, CancellationToken cancellation) =>
        (await execution.GetFromJsonAsync<List<RunDetails>>($"/workspaces/{workspaceId}/runs", cancellation))!;

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
