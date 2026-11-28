using Sofa.Contracts.Execution;

namespace Sofa.Notices.SignIns;

public sealed class RunnerSignIn
{
    public required Guid RunnerId { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required Guid UserId { get; init; }

    public required string RunnerName { get; set; }

    public bool SignedIn { get; set; }

    public DateTimeOffset? EndsAt { get; set; }

    public DateTimeOffset SeenAt { get; set; }

    public DateTimeOffset? RefusedARunAt { get; set; }

    public bool Hear(SubscriptionSignInSeen seen)
    {
        if (seen.At < SeenAt)
        {
            return false;
        }

        RunnerName = seen.RunnerName;
        SignedIn = seen.SignedIn;
        EndsAt = seen.SignedIn ? seen.EndsAt : null;
        SeenAt = seen.At;
        if (seen.SignedIn)
        {
            RefusedARunAt = null;
        }

        return true;
    }

    public void HearSignedOut(SubscriptionSignedOut signedOut, DateTimeOffset now)
    {
        RunnerName = signedOut.RunnerName;
        SignedIn = false;
        EndsAt = null;
        RefusedARunAt = now;
    }
}

public static class SignInWarnings
{
    public static readonly TimeSpan WarnBefore = TimeSpan.FromDays(3);

    public static bool EndsSoon(RunnerSignIn signIn, DateTimeOffset now) =>
        signIn is { SignedIn: true, EndsAt: { } endsAt } && endsAt - now <= WarnBefore;

    public static bool RefusesRuns(RunnerSignIn signIn) => signIn is { SignedIn: false, RefusedARunAt: not null };

    public static string SignInCommand(Guid runnerId) => $"sofa runner login {runnerId}";
}
