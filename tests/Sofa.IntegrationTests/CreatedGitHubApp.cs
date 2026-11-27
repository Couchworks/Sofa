using System.Net;
using System.Net.Http.Json;

namespace Sofa.IntegrationTests;

public sealed record CreatedGitHubApp(GitHubAppCreationForm Form, string BackInTheWebClient)
{
    public const string OneTimeCode = "code-github-hands-back-once";

    public static async Task<CreatedGitHubApp> CreateAsync(SofaApp sofa)
    {
        var workspaceId = (await sofa.SetUp).FirstUser.WorkspaceId;
        using var ada = await sofa.AdaSignedInAsync(CancellationToken.None);
        using var started = await ada.PostAsJsonAsync(
            $"/api/workspaces/{workspaceId}/github-app/creations", new { organisation = FakeGitHub.Organisation }, CancellationToken.None);
        started.EnsureSuccessStatusCode();
        var form = (await started.Content.ReadFromJsonAsync<GitHubAppCreationForm>())!;
        var state = WebClientSignIn.StateIn(new Uri(form.FormAction));

        using var returned = await ada.GetAsync(
            $"/github-app/created?code={OneTimeCode}&state={Uri.EscapeDataString(state)}", CancellationToken.None);
        if (returned.StatusCode != HttpStatusCode.Redirect)
        {
            throw new InvalidOperationException($"Coming back from creating the App answered {returned.StatusCode}.");
        }

        return new CreatedGitHubApp(form, returned.Headers.Location!.OriginalString);
    }
}

public sealed record GitHubAppCreationForm(string FormAction, string Manifest);
