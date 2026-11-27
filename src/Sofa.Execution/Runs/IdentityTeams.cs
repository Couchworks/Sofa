using System.Net;
using Sofa.Contracts;
using Sofa.Contracts.Identity;

namespace Sofa.Execution.Runs;

public sealed class IdentityTeams(HttpClient identity)
{
    public static readonly Uri Address = new($"http://{SofaServices.Identity}");

    public async Task<IReadOnlyCollection<Guid>> TeamsOfAsync(Guid userId, Guid workspaceId, CancellationToken cancellation)
    {
        using var response = await identity.GetAsync($"/users/{userId}", cancellation);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return [];
        }

        response.EnsureSuccessStatusCode();
        var profile = await response.Content.ReadFromJsonAsync<UserProfile>(cancellation);
        return profile?.Workspaces.Where(w => w.Id == workspaceId).SelectMany(w => w.Teams).Select(t => t.Id).ToList() ?? [];
    }
}
