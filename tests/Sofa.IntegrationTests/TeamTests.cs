using System.Net;
using System.Net.Http.Json;

namespace Sofa.IntegrationTests;

public class TeamTests(SofaApp sofa)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Users_added_by_GitHub_login_join_the_Workspaces_Team()
    {
        var beaId = await sofa.BeaAddedByHerGitHubLogin;
        var installation = await sofa.SetUp;
        using var ada = await sofa.AdaSignedInAsync(Cancellation);

        using var response = await ada.GetWithTokenAsync($"/api/workspaces/{installation.FirstUser.WorkspaceId}/teams", Cancellation);
        var team = Assert.Single((await response.Content.ReadFromJsonAsync<List<Team>>(Cancellation))!);

        Assert.Equal(installation.FirstUser.TeamId, team.Id);
        Assert.Contains(team.Members, member => member.UserId == installation.FirstUser.Id);
        Assert.Contains(team.Members, member => member.UserId == beaId);
    }

    [Fact]
    public async Task Adding_someone_already_in_the_Team_is_refused()
    {
        var beaId = await sofa.BeaAddedByHerGitHubLogin;
        var installation = await sofa.SetUp;
        using var ada = await sofa.AdaSignedInAsync(Cancellation);

        using var response = await ada.PostAsJsonAsync(
            $"/api/workspaces/{installation.FirstUser.WorkspaceId}/teams/{installation.FirstUser.TeamId}/users", new { userId = beaId },
            Cancellation);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Only_an_Admin_sees_or_changes_who_is_in_the_Team()
    {
        var installation = await sofa.SetUp;
        var api = $"/api/workspaces/{installation.FirstUser.WorkspaceId}";
        using var bea = await sofa.BeaSignedInAsync(Cancellation);

        using var teams = await bea.GetWithTokenAsync($"{api}/teams", Cancellation);
        using var users = await bea.GetWithTokenAsync($"{api}/users", Cancellation);
        using var removing = await bea.SendAsync(HttpMethod.Delete,
            $"{api}/teams/{installation.FirstUser.TeamId}/users/{installation.FirstUser.Id}", null, Cancellation);

        Assert.Equal(HttpStatusCode.Forbidden, teams.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, users.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, removing.StatusCode);
    }

    private sealed record Team(Guid Id, string Name, List<Member> Members);

    private sealed record Member(Guid UserId, string Name);
}
