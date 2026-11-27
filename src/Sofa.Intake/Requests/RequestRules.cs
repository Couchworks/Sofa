namespace Sofa.Intake.Requests;

public static class RequestRules
{
    public const int LongestTitle = 1024;

    public static string TitleOf(string title)
    {
        var trimmed = title.Trim();
        return trimmed.Length <= LongestTitle ? trimmed : trimmed[..LongestTitle];
    }
}
