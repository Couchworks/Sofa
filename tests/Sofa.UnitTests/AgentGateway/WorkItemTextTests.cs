using Sofa.AgentGateway.WorkItems;
using Sofa.Contracts.Execution;
using Sofa.Contracts.Flow;
using Sofa.Contracts.Intake;
using Sofa.Contracts.Records;

namespace Sofa.UnitTests.AgentGateway;

public class WorkItemTextTests
{
    private static readonly DateTimeOffset Asked = new(2026, 11, 27, 14, 5, 0, TimeSpan.Zero);
    private static readonly Guid WorkspaceId = Guid.NewGuid();
    private static readonly RequestRef Issue = new("acme/demo", 12, "https://github.com/acme/demo/issues/12");

    private static RunDetails RunIn(PhaseName phase) => new(
        Guid.NewGuid(), WorkspaceId, Guid.NewGuid(), Guid.NewGuid(), phase, Guid.NewGuid(), "opus", Sandbox.ReadOnly, Guid.NewGuid(),
        Priority.None, RunState.Running, null, null, Guid.NewGuid(), 1, 2, null, null, false, null, null, Asked, Asked, null);

    private static WorkItemDetails WorkItem(params QuestionDetails[] questions) => new(
        Guid.NewGuid(), "DEMO-1", "Add a dark mode toggle", Guid.NewGuid(), Guid.NewGuid(), Issue, Guid.NewGuid(), Asked, questions,
        WorkItemState.Running, PhaseName.Spec, Track: null, Waiting: null, Runs: [], PhaseEnds: [], PullRequest: null, DoneAt: null);

    private static RequestRead.Now IssueNow(string title, string body) =>
        new(new RequestNow(Guid.NewGuid(), Issue.Repository, Issue.Number, Issue.Url, title, body, "open"));

    [Fact]
    public void It_shows_the_Request_as_it_is_now_not_as_it_came_in()
    {
        var text = WorkItemText.Of(new WorkItemRead(
            RunIn(PhaseName.Spec), WorkItem(), IssueNow("Add a dark mode toggle that remembers", "Remember the choice."), []));

        Assert.StartsWith("# DEMO-1: Add a dark mode toggle\n", text);
        Assert.Contains("This Run is in the Spec Phase.", text);
        Assert.Contains("**Add a dark mode toggle that remembers** (open)\n\nRemember the choice.", text);
        Assert.Contains("No Run has asked a Question", text);
        Assert.Contains("No Run has made a Contribution", text);
    }

    [Fact]
    public void An_issue_Sofa_couldnt_read_falls_back_to_its_title_when_it_came_in()
    {
        var text = WorkItemText.Of(new WorkItemRead(
            RunIn(PhaseName.Spec), WorkItem(), new RequestRead.Unread("the issue is no longer on GitHub."), []));

        Assert.Contains("Sofa couldn't read the issue now: the issue is no longer on GitHub. Its title when it came in was \"Add a dark mode toggle\".", text);
    }

    [Fact]
    public void It_shows_each_Question_with_its_answer_or_that_it_waits_for_one()
    {
        var answered = new QuestionDetails(Guid.NewGuid(), Guid.NewGuid(), PhaseName.Spec, "Follow the system?\nOr not?", Asked,
            "Yes, follow it.", Guid.NewGuid(), Asked.AddHours(1));
        var open = new QuestionDetails(Guid.NewGuid(), Guid.NewGuid(), PhaseName.Spec, "Which icon?", Asked.AddHours(2), null, null, null);

        var text = WorkItemText.Of(new WorkItemRead(RunIn(PhaseName.Spec), WorkItem(answered, open), IssueNow("T", ""), []));

        Assert.Contains("### Asked in the Spec Phase, 2026-11-27 14:05 UTC\n\n> Follow the system?\n> Or not?\n\n**Answered 2026-11-27 15:05 UTC:**\n\nYes, follow it.", text);
        Assert.Contains("> Which icon?\n\n*Not answered yet.*", text);
        Assert.Contains("*The issue has no description.*", text);
    }

    [Fact]
    public void It_shows_earlier_Contributions_with_their_Outcome_and_attachments()
    {
        var contribution = new ContributionDetails(Guid.NewGuid(), Guid.NewGuid(), PhaseName.Triage, Guid.NewGuid(), "full",
            "Full Track: it touches every page.", [new AttachmentDetails(Guid.NewGuid(), "pages.png", "image/png", 2048)], Asked);

        var text = WorkItemText.Of(new WorkItemRead(RunIn(PhaseName.Spec), WorkItem(), IssueNow("T", "B"), [contribution]));

        Assert.Contains("### Triage, ended `full`, 2026-11-27 14:05 UTC\n\nFull Track: it touches every page.\n\nAttached: pages.png (image/png, 2048 bytes)", text);
    }

    [Fact]
    public void A_test_Run_on_no_Work_Item_is_told_so()
    {
        Assert.Contains("test Run of the Triage Phase", WorkItemText.NoWorkItem(RunIn(PhaseName.Triage)));
    }

    [Fact]
    public void It_shows_the_Track_and_what_a_User_said_sending_a_Phase_back()
    {
        var sentBack = new PhaseEndDetails(Guid.NewGuid(), PhaseName.Spec, "specified", Gate.Approve, PhaseEndStanding.SentBack, Asked,
            Guid.NewGuid(), "Say what the toggle remembers.", Asked.AddMinutes(5));
        var workItem = WorkItem() with { Track = Track.Full, PhaseEnds = [sentBack] };

        var text = WorkItemText.Of(new WorkItemRead(RunIn(PhaseName.Spec), workItem, IssueNow("Add a dark mode toggle", ""), []));

        Assert.Contains("The Work Item is on the Full Track.", text);
        Assert.Contains("## What Users decided at the Gates\n\n### Spec sent back, 2026-11-27 14:10 UTC\n\n> Say what the toggle remembers.", text);
    }
}
