using System.Text.RegularExpressions;

namespace Sofa.Intake.Questions;

public static partial class AnswerText
{
    public static string? Of(string comment)
    {
        var lines = comment.ReplaceLineEndings("\n").Split('\n').ToList();
        while (lines.Count > 0 && (string.IsNullOrWhiteSpace(lines[0]) || lines[0].TrimStart().StartsWith('>')))
        {
            lines.RemoveAt(0);
        }

        var answer = LeadingMentions().Replace(string.Join('\n', lines), "").Trim();
        return answer.Length == 0 ? null : answer;
    }

    [GeneratedRegex(@"^(\s*@[A-Za-z0-9][A-Za-z0-9-]*(\[bot\])?[,:]?)+\s*")]
    private static partial Regex LeadingMentions();
}
