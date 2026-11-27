using System.Net;
using Sofa.Contracts;
using Sofa.Contracts.Identity;

namespace Sofa.Intake.Requests;

public sealed class IdentityGitHubAccounts(HttpClient identity)
{
    public static readonly Uri Address = new($"http://{SofaServices.Identity}");

    public Task<GitHubAccountHolder?> HolderWithAccessToTheWorkspacesProjectsAsync(
        Guid workspaceId, long gitHubAccountId, CancellationToken cancellation) =>
        HolderInWorkspaceAsync(workspaceId, gitHubAccountId, cancellation);

    public async Task<GitHubAccountHolder?> HolderInWorkspaceAsync(Guid workspaceId, long gitHubAccountId, CancellationToken cancellation)
    {
        using var response = await identity.GetAsync($"/workspaces/{workspaceId}/users/by-github-account/{gitHubAccountId}", cancellation);
        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<GitHubAccountHolder>(cancellation);
    }
}
