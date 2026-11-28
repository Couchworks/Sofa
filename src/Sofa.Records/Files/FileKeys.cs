using System.Text.RegularExpressions;

namespace Sofa.Records.Files;

public static partial class FileKeys
{
    public static string OfAttachment(Guid workspaceId, Guid runId, Guid attachmentId) =>
        $"workspaces/{workspaceId:N}/runs/{runId:N}/attachments/{attachmentId:N}";

    public static bool IsSafe(string key) =>
        key.Length is > 0 and <= 500 && key.Split('/').All(segment => SafeSegment().IsMatch(segment) && segment is not ("." or ".."));

    [GeneratedRegex("^[A-Za-z0-9._-]+$")]
    private static partial Regex SafeSegment();
}
