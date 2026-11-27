using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Sofa.Contracts;
using Sofa.Contracts.Identity;

namespace Sofa.IntegrationTests;

public class RunTokenTests(SofaApp sofa)
{
    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public async Task A_Run_token_Identity_issues_to_Execution_is_accepted_by_the_Agent_Gateway()
    {
        var agentId = await RegisterAgentAsync("Builder");
        var issued = await AskExecutionForRunTokenAsync(agentId);

        using var response = await AgentGatewayWith(issued.Token);
        var holder = await response.Content.ReadFromJsonAsync<RunTokenHolder>(Cancellation);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(issued.RunId, holder!.RunId);
        Assert.Equal(agentId, holder.AgentId);
        Assert.Equal("Builder", holder.AgentName);
        Assert.Equal((await sofa.SetUp).FirstUser.WorkspaceId, holder.WorkspaceId);
    }

    [Fact]
    public async Task A_made_up_Run_token_is_refused_by_the_Agent_Gateway()
    {
        var agentId = await RegisterAgentAsync("Lead");
        var madeUp = TokenSignedBySomeoneElse(agentId, await IdentitysIssuerAsync());

        using var response = await AgentGatewayWith(madeUp);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_Users_token_is_refused_by_the_Agent_Gateway()
    {
        var user = (await sofa.SetUp).FirstUser;
        using var browser = new WebClientSignIn(sofa.AddressOf(SofaServices.AppGateway));
        var tokens = await browser.SignInAsync(user.Email, user.Password, Cancellation);

        using var response = await AgentGatewayWith(tokens.AccessToken);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task A_Run_token_is_refused_by_the_App_Gateway()
    {
        var issued = await AskExecutionForRunTokenAsync(await RegisterAgentAsync("Triager"));
        using var gateway = sofa.CreateHttpClient(SofaServices.AppGateway);
        gateway.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", issued.Token);

        using var response = await gateway.GetAsync("/api/me", Cancellation);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Identity_issues_no_Run_token_for_an_Agent_it_does_not_know()
    {
        using var execution = sofa.CreateHttpClient(SofaServices.Execution);

        using var response = await execution.PostAsJsonAsync("/diagnostics/run-tokens", new { agentId = Guid.NewGuid() }, Cancellation);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task An_Agent_owned_by_a_Team_of_another_Workspace_is_not_registered()
    {
        var workspaceId = (await sofa.SetUp).FirstUser.WorkspaceId;
        using var flow = sofa.CreateHttpClient(SofaServices.Flow);

        using var response = await flow.PostAsJsonAsync("/diagnostics/agents",
            new { workspaceId, name = "Stray", owner = AgentOwner.Team(Guid.NewGuid()) }, Cancellation);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
    }

    [Fact]
    public async Task An_Agent_may_be_owned_by_the_Team()
    {
        var installation = await sofa.SetUp;
        using var flow = sofa.CreateHttpClient(SofaServices.Flow);

        using var response = await flow.PostAsJsonAsync("/diagnostics/agents",
            new { installation.FirstUser.WorkspaceId, name = "Team Builder", owner = AgentOwner.Team(installation.FirstUser.TeamId) },
            Cancellation);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<Guid> RegisterAgentAsync(string name)
    {
        var user = (await sofa.SetUp).FirstUser;
        using var flow = sofa.CreateHttpClient(SofaServices.Flow);
        using var response = await flow.PostAsJsonAsync("/diagnostics/agents",
            new { user.WorkspaceId, name, owner = AgentOwner.User(user.Id) }, Cancellation);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AgentRegistration>(Cancellation))!.AgentId;
    }

    private async Task<IssuedRunToken> AskExecutionForRunTokenAsync(Guid agentId)
    {
        using var execution = sofa.CreateHttpClient(SofaServices.Execution);
        using var response = await execution.PostAsJsonAsync("/diagnostics/run-tokens", new { agentId }, Cancellation);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<IssuedRunToken>(Cancellation))!;
    }

    private async Task<HttpResponseMessage> AgentGatewayWith(string token)
    {
        using var gateway = sofa.CreateHttpClient(SofaServices.AgentGateway);
        gateway.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return await gateway.GetAsync("/diagnostics/run", Cancellation);
    }

    private async Task<string> IdentitysIssuerAsync()
    {
        using var gateway = sofa.CreateHttpClient(SofaServices.AppGateway);
        var discovery = await gateway.GetFromJsonAsync<JsonElement>("/.well-known/openid-configuration", Cancellation);
        return discovery.GetProperty("issuer").GetString()!;
    }

    private static string TokenSignedBySomeoneElse(Guid agentId, string issuer)
    {
        using var someoneElsesKey = RSA.Create(2048);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var header = Encode(new { alg = "RS256", typ = "at+jwt", kid = "made-up" });
        var payload = Encode(new Dictionary<string, object>
        {
            ["iss"] = issuer,
            ["aud"] = SofaServices.AgentGateway,
            ["sub"] = agentId.ToString(),
            [SofaClaims.Run] = Guid.NewGuid().ToString(),
            [SofaClaims.Workspace] = Guid.NewGuid().ToString(),
            ["iat"] = now,
            ["exp"] = now + 3600,
        });
        var signature = someoneElsesKey.SignData(
            Encoding.ASCII.GetBytes($"{header}.{payload}"), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        return $"{header}.{payload}.{WebClientSignIn.Base64Url(signature)}";
    }

    private static string Encode(object value) => WebClientSignIn.Base64Url(JsonSerializer.SerializeToUtf8Bytes(value));

    private sealed record IssuedRunToken(Guid RunId, Guid AgentId, string Token);

    private sealed record RunTokenHolder(Guid RunId, Guid AgentId, string AgentName, Guid WorkspaceId);
}
