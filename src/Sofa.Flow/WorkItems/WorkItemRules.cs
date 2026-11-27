namespace Sofa.Flow.WorkItems;

public static class WorkItemRules
{
    public const int LongestTitle = 256;
    public const int LongestUrl = 500;

    public static string TitleOf(string? title)
    {
        var trimmed = string.IsNullOrWhiteSpace(title) ? "(untitled)" : title.Trim();
        return trimmed.Length <= LongestTitle ? trimmed : trimmed[..(LongestTitle - 1)] + "…";
    }
}
