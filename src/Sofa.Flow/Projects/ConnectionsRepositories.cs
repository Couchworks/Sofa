using System.Net;
using Microsoft.AspNetCore.Mvc;
using Sofa.Contracts;
using Sofa.Contracts.Connections;

namespace Sofa.Flow.Projects;

public sealed record RepositoriesReached(IReadOnlyDictionary<Guid, IReadOnlyList<string>> ByConnection, string? Problem);

public sealed class ConnectionsRepositories(HttpClient connections)
{
    public static readonly Uri Address = new($"http://{SofaServices.Connections}");

    public async Task<RepositoriesReached> ReachedByAsync(
        Guid workspaceId, IEnumerable<Guid> connectionIds, CancellationToken cancellation)
    {
        var byConnection = new Dictionary<Guid, IReadOnlyList<string>>();
        foreach (var connectionId in connectionIds.Distinct())
        {
            using var response = await connections.GetAsync(
                $"/workspaces/{workspaceId}/connections/{connectionId}/repositories", cancellation);
            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return new RepositoriesReached(byConnection, "The Workspace has no such Connection.");
            }

            if (!response.IsSuccessStatusCode)
            {
                var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>(cancellation);
                return new RepositoriesReached(byConnection,
                    $"The Connection couldn't say which Repositories it reaches: {problem?.Title ?? response.StatusCode.ToString()}");
            }

            var repositories = await response.Content.ReadFromJsonAsync<List<ConnectionRepository>>(cancellation) ?? [];
            byConnection[connectionId] = repositories.Select(repository => repository.FullName).ToList();
        }

        return new RepositoriesReached(byConnection, null);
    }
}
