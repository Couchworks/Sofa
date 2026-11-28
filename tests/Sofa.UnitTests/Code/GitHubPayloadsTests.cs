using Sofa.Code.GitHub;

namespace Sofa.UnitTests.Code;

public class GitHubPayloadsTests
{
    [Fact]
    public void A_pull_request_payload_gives_its_title_branches_and_whether_it_merged()
    {
        var changed = GitHubPayloads.ReadPullRequest("""
            {"action":"closed","number":7,"pull_request":{"number":7,"title":"DEMO-1: Toggle","html_url":"https://github.com/acme/demo/pull/7",
             "state":"closed","merged":true,"head":{"ref":"sofa/DEMO-1"},"base":{"ref":"main"},
             "created_at":"2026-11-27T09:00:00Z","closed_at":"2026-11-27T10:00:00Z","merged_at":"2026-11-27T10:00:00Z"},
             "repository":{"full_name":"acme/demo"}}
            """, DateTimeOffset.UnixEpoch)!;

        Assert.Equal(("closed", "acme/demo", 7L, "DEMO-1: Toggle", "sofa/DEMO-1", "main"),
            (changed.Action, changed.Repository, changed.Number, changed.Title, changed.HeadBranch, changed.BaseBranch));
        Assert.Equal((false, true), (changed.Open, changed.Merged));
        Assert.Equal(new DateTimeOffset(2026, 11, 27, 10, 0, 0, TimeSpan.Zero), changed.At);
    }

    [Fact]
    public void A_push_payload_gives_its_ref_commits_and_who_pushed()
    {
        var pushed = GitHubPayloads.ReadPush("""
            {"ref":"refs/heads/main","before":"aaa","after":"bbb","created":false,"deleted":false,
             "repository":{"full_name":"acme/demo"},"pusher":{"name":"sofa-acme[bot]"},"sender":{"login":"sofa-acme[bot]","type":"Bot"}}
            """)!;

        Assert.Equal(new Pushed("acme/demo", "refs/heads/main", "aaa", "bbb", false, false, "sofa-acme[bot]"), pushed);
    }

    [Fact]
    public void A_payload_that_is_not_what_it_should_be_gives_nothing()
    {
        Assert.Null(GitHubPayloads.ReadPullRequest("{}", DateTimeOffset.UnixEpoch));
        Assert.Null(GitHubPayloads.ReadPush("not json"));
    }
}
