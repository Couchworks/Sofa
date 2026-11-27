using System.Net;
using System.Text;
using Microsoft.Extensions.Options;
using Sofa.Contracts.Identity;
using Sofa.Execution.Runs;

namespace Sofa.UnitTests.Execution;

public class RunTokenClientTests
{
    private static readonly Guid RunId = Guid.NewGuid();
    private static readonly Guid AgentId = Guid.NewGuid();

    [Fact]
    public async Task Asks_Identity_for_a_token_naming_the_Run_and_its_Agent_with_Executions_secret()
    {
        var identity = new IdentityAnswering(HttpStatusCode.OK, """{"access_token":"token","expires_in":43200}""");

        var outcome = await ClientFor(identity).RequestAsync(RunId, AgentId, TestContext.Current.CancellationToken);

        Assert.Equal(new RunTokenOutcome.Issued("token", TimeSpan.FromHours(12)), outcome);
        Assert.Equal("/connect/token", identity.Asked!.AbsolutePath);
        Assert.Contains("grant_type=client_credentials", identity.Form);
        Assert.Contains($"client_id={RunTokens.ClientId}", identity.Form);
        Assert.Contains("client_secret=the-secret", identity.Form);
        Assert.Contains($"{RunTokens.RunIdParameter}={RunId}", identity.Form);
        Assert.Contains($"{RunTokens.AgentIdParameter}={AgentId}", identity.Form);
    }

    [Fact]
    public async Task Passes_Identitys_reason_on_when_it_refuses()
    {
        var identity = new IdentityAnswering(HttpStatusCode.BadRequest,
            """{"error":"invalid_grant","error_description":"Identity knows no Agent."}""");

        var outcome = await ClientFor(identity).RequestAsync(RunId, AgentId, TestContext.Current.CancellationToken);

        Assert.Equal(new RunTokenOutcome.Refused("Identity knows no Agent."), outcome);
    }

    private static RunTokenClient ClientFor(IdentityAnswering identity) =>
        new(new HttpClient(identity) { BaseAddress = RunTokenClient.IdentityAddress },
            Options.Create(new RunTokenClientOptions { ClientSecret = "the-secret" }));

    private sealed class IdentityAnswering(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public Uri? Asked { get; private set; }

        public string Form { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Asked = request.RequestUri;
            Form = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
