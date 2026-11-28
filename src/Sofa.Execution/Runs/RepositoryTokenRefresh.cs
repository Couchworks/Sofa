namespace Sofa.Execution.Runs;

public static class RepositoryTokenRefresh
{
    public const long MostOutputRead = 4096;

    public static readonly TimeSpan TryingAgainAfter = TimeSpan.FromSeconds(30);

    public static TimeSpan DueIn(DateTimeOffset expiresAt, DateTimeOffset now, TimeSpan before)
    {
        var due = expiresAt - before - now;
        return due > TimeSpan.Zero ? due : TimeSpan.Zero;
    }

    public static DateTimeOffset TryAgainAt(DateTimeOffset now, TimeSpan before) => now + before + TryingAgainAfter;
}
