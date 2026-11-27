using System.Net;
using System.Net.Http.Json;
using Sofa.Cli;
using Sofa.Contracts.Execution;

namespace Sofa.UnitTests.Cli;

public class RunnerSignInTests
{
    private const string Image = "sofa-worker";
    private static readonly Guid RunnerId = Guid.NewGuid();
    private static readonly string Volume = $"sofa-worker-{RunnerId}";

    private const string SignedInStatus = """
        {"loggedIn":true,"authMethod":"claude.ai","apiProvider":"firstParty","loginExpiresAt":"2026-12-24T10:00:00.000Z",
         "email":"ada@example.com","orgId":"00000000-0000-0000-0000-000000000000","subscriptionType":"max"}
        """;

    private static CancellationToken Cancellation => TestContext.Current.CancellationToken;

    [Fact]
    public void A_subscription_sign_in_is_read_with_when_it_ends_and_nothing_else()
    {
        Assert.Equal(new SignInSeen(true, new DateTimeOffset(2026, 12, 24, 10, 0, 0, TimeSpan.Zero)), ClaudeAuthStatus.Read(SignedInStatus));
    }

    [Theory]
    [InlineData("""{"loggedIn":false}""")]
    [InlineData("""{"loggedIn":true,"authMethod":"console","loginExpiresAt":"2026-12-24T10:00:00.000Z"}""")]
    public void A_volume_not_signed_in_to_a_subscription_is_not_signed_in(string status)
    {
        Assert.Equal(new SignInSeen(false, null), ClaudeAuthStatus.Read(status));
    }

    [Theory]
    [InlineData("")]
    [InlineData("Not logged in")]
    [InlineData("[]")]
    public void Output_that_is_no_status_says_nothing(string status)
    {
        Assert.Null(ClaudeAuthStatus.Read(status));
    }

    [Fact]
    public void Signing_in_runs_Claude_Codes_own_sign_in_on_the_Runners_volume_and_reads_its_status_from_a_fresh_container()
    {
        Assert.Equal(["run", "-it", "--rm", "-v", $"{Volume}:/claude", Image, "claude", "auth", "login", "--claudeai"],
            WorkerContainers.SignIn(Volume, Image));
        Assert.Equal(["run", "--rm", "-v", $"{Volume}:/claude", Image, "claude", "auth", "status", "--json"],
            WorkerContainers.SignInStatus(Volume, Image));
    }

    [Fact]
    public async Task Logging_in_signs_in_on_the_volume_then_tells_Execution_only_whether_and_until_when()
    {
        var execution = new FakeExecution(new RunnerToSignIn(RunnerId, Guid.NewGuid(), "Ada's Max", RunnerKind.Subscription, Volume));
        var docker = new FakeDocker(SignedInStatus);
        var output = new StringWriter();

        var exitCode = await SignIn(execution, docker, output).LoginAsync(RunnerId.ToString(), inATerminal: true, Cancellation);

        Assert.Equal(RunnerSignIn.Done, exitCode);
        Assert.Equal(WorkerContainers.SignIn(Volume, Image), docker.Attached.Single());
        Assert.Contains(WorkerContainers.SignInStatus(Volume, Image), docker.Ran);
        Assert.Equal(new SignInSeen(true, new DateTimeOffset(2026, 12, 24, 10, 0, 0, TimeSpan.Zero)), execution.Recorded);
        Assert.DoesNotContain("ada@example.com", execution.RecordedBody);
        Assert.Contains("Ada's Max is signed in", output.ToString());
    }

    [Fact]
    public async Task Logging_in_outside_a_terminal_starts_no_container()
    {
        var execution = new FakeExecution(new RunnerToSignIn(RunnerId, Guid.NewGuid(), "Ada's Max", RunnerKind.Subscription, Volume));
        var docker = new FakeDocker(SignedInStatus);

        var exitCode = await SignIn(execution, docker, new StringWriter()).LoginAsync(RunnerId.ToString(), inATerminal: false, Cancellation);

        Assert.Equal(RunnerSignIn.Failed, exitCode);
        Assert.Empty(docker.Attached);
        Assert.Null(execution.Recorded);
    }

    [Fact]
    public async Task An_API_key_Runner_has_nothing_to_sign_in()
    {
        var execution = new FakeExecution(new RunnerToSignIn(RunnerId, Guid.NewGuid(), "Team key", RunnerKind.ApiKey, null));
        var docker = new FakeDocker(SignedInStatus);

        var exitCode = await SignIn(execution, docker, new StringWriter()).LoginAsync(RunnerId.ToString(), inATerminal: true, Cancellation);

        Assert.Equal(RunnerSignIn.Failed, exitCode);
        Assert.Empty(docker.Attached);
    }

    [Fact]
    public async Task A_volume_Sofa_did_not_name_is_never_mounted()
    {
        var execution = new FakeExecution(new RunnerToSignIn(RunnerId, Guid.NewGuid(), "Odd", RunnerKind.Subscription, "someone-elses-volume"));
        var docker = new FakeDocker(SignedInStatus);

        var exitCode = await SignIn(execution, docker, new StringWriter()).CheckAsync(RunnerId.ToString(), Cancellation);

        Assert.Equal(RunnerSignIn.Failed, exitCode);
        Assert.Empty(docker.Ran);
    }

    [Fact]
    public async Task Without_the_Worker_image_it_says_how_to_build_it()
    {
        var execution = new FakeExecution(new RunnerToSignIn(RunnerId, Guid.NewGuid(), "Ada's Max", RunnerKind.Subscription, Volume));
        var docker = new FakeDocker(SignedInStatus, imageBuilt: false);
        var output = new StringWriter();

        var exitCode = await SignIn(execution, docker, output).LoginAsync(RunnerId.ToString(), inATerminal: true, Cancellation);

        Assert.Equal(RunnerSignIn.Failed, exitCode);
        Assert.Contains("docker build -t sofa-worker worker", output.ToString());
        Assert.Empty(docker.Attached);
    }

    private static RunnerSignIn SignIn(FakeExecution execution, FakeDocker docker, TextWriter output) =>
        new(new HttpClient(execution) { BaseAddress = new Uri("http://execution") }, docker, output, Image);

    private sealed class FakeDocker(string status, bool imageBuilt = true) : IDocker
    {
        public List<IReadOnlyList<string>> Attached { get; } = [];

        public List<IReadOnlyList<string>> Ran { get; } = [];

        public Task<int> RunAttachedToTheTerminalAsync(IReadOnlyList<string> arguments, CancellationToken cancellation)
        {
            Attached.Add(arguments);
            return Task.FromResult(0);
        }

        public Task<(int ExitCode, string Output)> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellation)
        {
            Ran.Add(arguments);
            return Task.FromResult(arguments[0] == "image" ? (imageBuilt ? 0 : 1, "") : (0, status));
        }
    }

    private sealed class FakeExecution(RunnerToSignIn runner) : HttpMessageHandler
    {
        public SignInSeen? Recorded { get; private set; }

        public string RecordedBody { get; private set; } = "";

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            if (request.RequestUri!.AbsolutePath != $"/runners/{runner.Id}/sign-in")
            {
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            }

            if (request.Method == HttpMethod.Get)
            {
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(runner) };
            }

            RecordedBody = await request.Content!.ReadAsStringAsync(cancellationToken);
            Recorded = await request.Content.ReadFromJsonAsync<SignInSeen>(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK);
        }
    }
}
