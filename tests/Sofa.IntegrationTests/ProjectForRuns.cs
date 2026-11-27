using System.Net.Http.Json;

namespace Sofa.IntegrationTests;

public sealed record ProjectForRuns(string Key, IReadOnlyList<ProjectForRuns.Agent> Agents)
{
    private static readonly TimeSpan RegisteredWithin = TimeSpan.FromSeconds(30);

    public Agent Cast(string role) => Agents.Single(agent => agent.Role == role);

    public static async Task<ProjectForRuns> CreateAsync(SofaApp sofa, string key, CancellationToken cancellation)
    {
        var workspaceId = (await sofa.SetUp).FirstUser.WorkspaceId;
        var connectionId = (await sofa.TheConnection).ConnectionId;
        using var ada = await sofa.AdaSignedInAsync(cancellation);
        using var created = await ada.PostAsJsonAsync($"/api/workspaces/{workspaceId}/projects", new
        {
            key,
            name = $"Runs of {key}",
            binding = new { connectionId, requestRepository = "acme/demo", label = "sofa", codeRepositories = new[] { "acme/demo" } },
        }, cancellation);
        created.EnsureSuccessStatusCode();
        return await RegisteredAsync(ada, workspaceId, key, cancellation);
    }

    public static async Task<ProjectForRuns> RegisteredAsync(
        WebClientSignIn user, Guid workspaceId, string key, CancellationToken cancellation) =>
        await ReadUntilAsync(user, workspaceId, key, project => project.Agents.All(agent => agent.Registration == "Registered"), cancellation);

    public static async Task<ProjectForRuns> ReadUntilAsync(
        WebClientSignIn user, Guid workspaceId, string key, Func<ProjectForRuns, bool> until, CancellationToken cancellation)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(RegisteredWithin);
        while (true)
        {
            using var read = await user.GetWithTokenAsync($"/api/workspaces/{workspaceId}/projects/{key}", timeout.Token);
            read.EnsureSuccessStatusCode();
            var project = (await read.Content.ReadFromJsonAsync<ProjectForRuns>(timeout.Token))!;
            if (until(project))
            {
                return project;
            }

            await Task.Delay(TimeSpan.FromMilliseconds(250), timeout.Token);
        }
    }

    public sealed record Agent(Guid Id, string Name, string Role, string Model, string Registration, string? RegistrationRefusedBecause);
}
