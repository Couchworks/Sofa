namespace Sofa.Flow.WorkItems;

public static class WorkItemRules
{
    public const int LongestTitle = 256;
    public const int LongestUrl = 500;
    public const int LongestWhy = 2000;
    public const int LongestComment = 20_000;

    public static string TitleOf(string? title) => Cut(string.IsNullOrWhiteSpace(title) ? "(untitled)" : title.Trim(), LongestTitle);

    public static string WhyOf(string why) => Cut(why.Trim(), LongestWhy);

    public static string? CommentProblem(string? comment) =>
        comment is { Length: > LongestComment } ? $"A comment has at most {LongestComment} characters." : null;

    private static string Cut(string text, int longest) => text.Length <= longest ? text : text[..(longest - 1)] + "…";
}
