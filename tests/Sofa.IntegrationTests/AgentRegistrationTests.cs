using System.Net;
using System.Net.Http.Json;
using Npgsql;

namespace Sofa.IntegrationTests;

public class AgentRegistrationTests(SofaApp sofa)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task An_Agent_Identity_refuses_shows_as_not_registered_with_its_reason_until_Retry_registers_it()
    {
        var workspaceId = (await sofa.SetUp).FirstUser.WorkspaceId;
        var lead = (await ProjectForRuns.CreateAsync(sofa, "REFUSE", Cancellation)).Cast("Lead");
        using var bea = await sofa.BeaSignedInAsync(Cancellation);
        var ownedBy = await OwnerInFlowAsync(lead.Id);

        await OwnInFlowAsync(lead.Id, Guid.NewGuid());
        using var retried = await bea.PostAsJsonAsync($"/api/workspaces/{workspaceId}/projects/REFUSE/agents/{lead.Id}/registration", null, Cancellation);
        var refused = await ProjectForRuns.ReadUntilAsync(bea, workspaceId, "REFUSE",
            project => project.Cast("Lead").Registration == "Refused", Cancellation);
        await OwnInFlowAsync(lead.Id, ownedBy);
        using var retriedAgain = await bea.PostAsJsonAsync($"/api/workspaces/{workspaceId}/projects/REFUSE/agents/{lead.Id}/registration", null, Cancellation);
        var registered = await ProjectForRuns.RegisteredAsync(bea, workspaceId, "REFUSE", Cancellation);

        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal("Another Agent already has that id.", refused.Cast("Lead").RegistrationRefusedBecause);
        Assert.Equal(HttpStatusCode.OK, retriedAgain.StatusCode);
        Assert.Null(registered.Cast("Lead").RegistrationRefusedBecause);
    }

    private async Task<Guid> OwnerInFlowAsync(Guid agentId)
    {
        await using var database = await OpenAsync();
        await using var read = new NpgsqlCommand("select owner_team_id from flow.agents where id = @id", database);
        read.Parameters.AddWithValue("id", agentId);
        return (Guid)(await read.ExecuteScalarAsync(Cancellation))!;
    }

    private async Task OwnInFlowAsync(Guid agentId, Guid teamId)
    {
        await using var database = await OpenAsync();
        await using var change = new NpgsqlCommand("update flow.agents set owner_team_id = @team where id = @id", database);
        change.Parameters.AddWithValue("id", agentId);
        change.Parameters.AddWithValue("team", teamId);
        Assert.Equal(1, await change.ExecuteNonQueryAsync(Cancellation));
    }

    private async Task<NpgsqlConnection> OpenAsync()
    {
        var database = new NpgsqlConnection(await sofa.App.GetConnectionStringAsync("sofa", Cancellation));
        await database.OpenAsync(Cancellation);
        return database;
    }
}
