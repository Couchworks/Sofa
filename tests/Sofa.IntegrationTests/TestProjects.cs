using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Sofa.IntegrationTests;

public static class TestProjects
{
    public const string DemoRepository = "acme/demo";

    public static async Task<JsonElement> CreateAsync(
        SofaApp sofa,
        string key,
        string label = "sofa",
        bool keepsIssueState = false,
        string requestRepository = DemoRepository,
        bool holdsAfterTriage = true)
    {
        var workspaceId = (await sofa.SetUp).FirstUser.WorkspaceId;
        var connectionId = (await sofa.TheConnection).ConnectionId;
        using var ada = await sofa.AdaSignedInAsync(CancellationToken.None);
        using var created = await ada.PostAsJsonAsync($"/api/workspaces/{workspaceId}/projects", new
        {
            key,
            name = $"The {key} Project",
            binding = new
            {
                connectionId,
                requestRepository,
                label,
                codeRepositories = new[] { DemoRepository },
                keepsIssueState,
            },
        }, CancellationToken.None);
        if (created.StatusCode != HttpStatusCode.Created)
        {
            throw new InvalidOperationException($"Creating Project {key} answered {created.StatusCode}: {await created.Content.ReadAsStringAsync()}");
        }

        var project = await created.Content.ReadFromJsonAsync<JsonElement>();
        return holdsAfterTriage ? await HoldAfterTriageAsync(ada, workspaceId, project) : project;
    }

    public static async Task<JsonElement> ChangePhaseAsync(
        WebClientSignIn user, Guid workspaceId, JsonElement project, string phase, string? gate = null, string? instructions = null)
    {
        var current = project.GetProperty("phases").EnumerateArray().Single(p => p.GetProperty("name").GetString() == phase);
        using var changed = await user.SendAsync(HttpMethod.Put,
            $"/api/workspaces/{workspaceId}/projects/{project.GetProperty("key").GetString()}/phases/{phase}", new
            {
                castAgentId = current.GetProperty("castAgentId").GetGuid(),
                gate = gate ?? current.GetProperty("gate").GetString(),
                sandbox = current.GetProperty("sandbox").GetString(),
                instructions,
            }, CancellationToken.None);
        if (changed.StatusCode != HttpStatusCode.OK)
        {
            throw new InvalidOperationException($"Changing {phase} answered {changed.StatusCode}: {await changed.Content.ReadAsStringAsync()}");
        }

        return await changed.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static Task<JsonElement> HoldAfterTriageAsync(WebClientSignIn ada, Guid workspaceId, JsonElement project) =>
        ChangePhaseAsync(ada, workspaceId, project, "Triage", gate: "Approve");
}
