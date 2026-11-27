using System.Net;
using System.Net.Http.Json;

namespace Sofa.IntegrationTests;

public sealed record InstalledConnection(Guid ConnectionId, string BackInTheWebClient)
{
    public static async Task<InstalledConnection> InstallAsync(SofaApp sofa)
    {
        await sofa.TheGitHubApp;
        var workspaceId = (await sofa.SetUp).FirstUser.WorkspaceId;
        using var ada = await sofa.AdaSignedInAsync(CancellationToken.None);

        using var started = await ada.PostAsJsonAsync($"/api/workspaces/{workspaceId}/github-app/installations", null, CancellationToken.None);
        started.EnsureSuccessStatusCode();
        var installation = (await started.Content.ReadFromJsonAsync<Installation>())!;
        var state = WebClientSignIn.StateIn(new Uri(installation.InstallUrl));

        using var returned = await ada.GetAsync(
            $"/github-app/installed?installation_id={FakeGitHub.InstallationId}&setup_action=install&state={Uri.EscapeDataString(state)}",
            CancellationToken.None);
        if (returned.StatusCode != HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException($"Coming back from installing the App answered {returned.StatusCode}.");
        }

        var backInTheWebClient = returned.Headers.Location!.OriginalString;
        using var listed = await ada.GetWithTokenAsync($"/api/workspaces/{workspaceId}/connections", CancellationToken.None);
        var connections = (await listed.Content.ReadFromJsonAsync<List<Connection>>())!;
        return new InstalledConnection(
            connections.Single(connection => connection.Account == FakeGitHub.Organisation).Id, backInTheWebClient);
    }

    private sealed record Installation(string InstallUrl);

    private sealed record Connection(Guid Id, string Account);
}
