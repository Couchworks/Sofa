using Sofa.Contracts;
using Sofa.Contracts.Code;

namespace Sofa.Execution.Runs;

public sealed record RepositoryTokenAnswer(RepositoryToken? Token, string? Problem);

public sealed class CodeRepositoryTokens(HttpClient code)
{
    public static readonly Uri Address = new($"http://{SofaServices.Code}");

    public async Task<RepositoryTokenAnswer> ForAsync(Run run, CancellationToken cancellation)
    {
        using var response = await code.PostAsJsonAsync($"/runs/{run.Id}/repository-token",
            new RepositoryTokenRequest(run.WorkspaceId, run.ProjectId, run.Sandbox, run.OwnBranch), cancellation);
        if (!response.IsSuccessStatusCode)
        {
            var problem = await response.Content.ReadFromJsonAsync<Problem>(cancellation);
            return new RepositoryTokenAnswer(null, problem?.Title ?? $"Code answered {(int)response.StatusCode}.");
        }

        return new RepositoryTokenAnswer(await response.Content.ReadFromJsonAsync<RepositoryToken>(cancellation), null);
    }

    private sealed record Problem(string? Title);
}
