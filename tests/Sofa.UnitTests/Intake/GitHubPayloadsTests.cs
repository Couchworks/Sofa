using Sofa.Intake.GitHub;

namespace Sofa.UnitTests.Intake;

public class GitHubPayloadsTests
{
    [Fact]
    public void Reads_who_labelled_which_issue_with_what()
    {
        var labeled = GitHubPayloads.ReadLabeled("""
            {"action":"labeled","label":{"name":"sofa"},
             "issue":{"number":12,"title":"Add a dark mode toggle","html_url":"https://github.com/acme/demo/issues/12"},
             "repository":{"full_name":"acme/demo"},"sender":{"id":5001,"login":"ada-gh","type":"User"}}
            """);

        Assert.Equal(new IssueLabeled("acme/demo", 12, "Add a dark mode toggle", "https://github.com/acme/demo/issues/12", "sofa",
            new GitHubAccountInPayload(5001, "ada-gh", "User")), labeled);
    }

    [Fact]
    public void A_pull_request_labelled_is_no_Request()
    {
        Assert.Null(GitHubPayloads.ReadLabeled("""
            {"label":{"name":"sofa"},"issue":{"number":3,"pull_request":{"url":"x"}},
             "repository":{"full_name":"acme/demo"},"sender":{"id":1,"login":"a"}}
            """));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("""{"issue":{"number":1},"repository":{"full_name":"acme/demo"},"sender":{"id":1}}""")]
    public void A_payload_missing_what_a_label_needs_is_ignored(string payload)
    {
        Assert.Null(GitHubPayloads.ReadLabeled(payload));
    }

    [Fact]
    public void Reads_who_commented_on_which_issue()
    {
        var commented = GitHubPayloads.ReadCommented("""
            {"action":"created","issue":{"number":12},
             "comment":{"id":991,"body":"Yes, follow it.","user":{"id":5002,"login":"bea-gh","type":"User"}},
             "repository":{"full_name":"acme/demo"}}
            """);

        Assert.Equal(new IssueCommented("acme/demo", 12, 991, "Yes, follow it.", new GitHubAccountInPayload(5002, "bea-gh", "User")), commented);
    }

    [Fact]
    public void Tells_a_bot_from_a_person()
    {
        var commented = GitHubPayloads.ReadCommented("""
            {"issue":{"number":12},"comment":{"id":1,"body":"Report","user":{"id":9,"login":"sofa[bot]","type":"Bot"}},
             "repository":{"full_name":"acme/demo"}}
            """);

        Assert.True(commented!.By.IsBot);
    }
}
