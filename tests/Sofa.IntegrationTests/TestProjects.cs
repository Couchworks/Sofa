using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Sofa.IntegrationTests;

public static class TestProjects
{
    public const string DemoRepository = "acme/demo";

    public static async Task<JsonElement> CreateAsync(
        SofaApp sofa, string key, string label = "sofa", bool keepsIssueState = false, string requestRepository = DemoRepository)
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

        return await created.Content.ReadFromJsonAsync<JsonElement>();
    }
}
