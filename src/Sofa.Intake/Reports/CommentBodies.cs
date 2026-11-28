using Sofa.Contracts.Flow;

namespace Sofa.Intake.Reports;

public static class CommentBodies
{
    public const string NoDetail = "_No detail._";

    public static string Report(PhaseName phase, string workItemKey, string summary, string detail, Uri inSofa) =>
        $"""
        ### {phase} · {workItemKey}

        {Trimmed(summary)}

        <details>
        <summary>Detail</summary>

        {(string.IsNullOrWhiteSpace(detail) ? NoDetail : Trimmed(detail))}

        </details>

        [See {workItemKey} in Sofa]({inSofa})
        """;

    public static string Question(PhaseName phase, string workItemKey, string question, Uri inSofa) =>
        $"""
        ### Question from {phase} · {workItemKey}

        {Trimmed(question)}

        Reply on this issue to answer it. Sofa takes the first reply from someone with access to the Project, or answer it in Sofa's Inbox.

        [See {workItemKey} in Sofa]({inSofa})
        """;

    public static string QuestionAnsweredInSofa(PhaseName phase, string workItemKey, string question, string answer, Uri inSofa) =>
        $"""
        ### Question from {phase} · {workItemKey}

        {Trimmed(question)}

        **Answered in Sofa:**

        {Quoted(answer)}

        A reply here no longer answers it.

        [See {workItemKey} in Sofa]({inSofa})
        """;

    private static string Quoted(string text) =>
        string.Join('\n', Trimmed(text).ReplaceLineEndings("\n").Split('\n').Select(line => line.Length == 0 ? ">" : $"> {line}"));

    private static string Trimmed(string text) => text.Trim();
}
