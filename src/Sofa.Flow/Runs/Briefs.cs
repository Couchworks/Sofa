using Sofa.Contracts.Flow;
using Sofa.Flow.Projects;

namespace Sofa.Flow.Runs;

public static class Briefs
{
    public static string For(Project project, WorkItems.WorkItem workItem, Phase phase, Agent agent) =>
        $"""
        You are the {agent.Role} of Project {project.Key}, on a Run of its {phase.Name} Phase on Work Item {workItem.Key}: {workItem.Title}.{TrackOf(workItem.Track)}

        Sofa takes each Work Item through its Project's Flow one Phase at a time, a Run at a time. Each Run does its Phase's part and ends; the next Run knows only what earlier Runs wrote in their Contributions, the Questions they asked with their answers, and the comments of Users who sent a Phase back. So write your reasoning into your Contribution, not only your conclusion. Read your Work Item with Sofa's read_work_item tool first. End the Run with Sofa's finish tool, or, if you can't go on without a User's answer, with Sofa's ask tool; then stop. Its key, {workItem.Key}, starts the title of any pull request you open.

        ## As the {agent.Role}

        {agent.Instructions.Trim()}

        ## In the {phase.Name} Phase

        {phase.Instructions.Trim()}
        """;

    private static string TrackOf(Track? track) => track switch
    {
        Track.Small => " It is on the Small Track: it has no spec, and its plan is reviewed by a User afterwards.",
        Track.Full => " It is on the Full Track: a User approves its spec and its plan before the work goes on.",
        _ => "",
    };
}
