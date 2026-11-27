using System.Net;
using System.Text;
using Sofa.Connections.GitHub;

namespace Sofa.UnitTests.Connections;

public class GitHubUserAccountTests
{
    private static readonly Uri WebAddress = new("https://github.test/");
    private static readonly Uri Callback = new("https://sofa.example.dev/account/github/callback");

    [Fact]
    public async Task Trades_GitHubs_code_for_a_user_access_token_with_the_Apps_client_secret()
    {
        var gitHub = new Answering(("POST", "/login/oauth/access_token", """{"access_token":"ghu_1","token_type":"bearer"}"""));

        var token = await ApiOver(gitHub).UserAccessToken(WebAddress, "Iv23.client", "secret", "the-code", Callback, Cancellation);

        Assert.Equal("ghu_1", token);
        Assert.Contains("\"client_secret\":\"secret\"", gitHub.Bodies.Single());
        Assert.Contains("\"redirect_uri\":\"https://sofa.example.dev/account/github/callback\"", gitHub.Bodies.Single());
    }

    [Fact]
    public async Task A_code_GitHub_does_not_accept_gives_no_token()
    {
        var gitHub = new Answering(("POST", "/login/oauth/access_token", """{"error":"bad_verification_code"}"""));

        Assert.Null(await ApiOver(gitHub).UserAccessToken(WebAddress, "Iv23.client", "secret", "used", Callback, Cancellation));
    }

    [Fact]
    public async Task Reads_only_the_primary_email_GitHub_has_verified()
    {
        var gitHub = new Answering(("GET", "/user/emails", """
            [{"email":"old@example.com","primary":false,"verified":true},
             {"email":"ada@example.com","primary":true,"verified":true}]
            """));

        Assert.Equal("ada@example.com", await ApiOver(gitHub).VerifiedPrimaryEmail("ghu_1", Cancellation));
    }

    [Fact]
    public async Task An_unverified_primary_email_is_no_email()
    {
        var gitHub = new Answering(("GET", "/user/emails", """[{"email":"ada@example.com","primary":true,"verified":false}]"""));

        Assert.Null(await ApiOver(gitHub).VerifiedPrimaryEmail("ghu_1", Cancellation));
    }

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    private static GitHubApi ApiOver(HttpMessageHandler gitHub) =>
        new(new HttpClient(gitHub) { BaseAddress = new Uri("https://api.github.test/") });

    private sealed class Answering(params (string Method, string Path, string Json)[] answers) : HttpMessageHandler
    {
        public List<string> Bodies { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.Content is not null)
            {
                Bodies.Add(await request.Content.ReadAsStringAsync(cancellationToken));
            }

            var answer = answers.SingleOrDefault(a => a.Method == request.Method.Method && a.Path == request.RequestUri!.AbsolutePath);
            return answer.Json is null
                ? new HttpResponseMessage(HttpStatusCode.NotFound) { RequestMessage = request }
                : new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(answer.Json, Encoding.UTF8, "application/json"),
                    RequestMessage = request,
                };
        }
    }
}
