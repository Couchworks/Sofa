using Sofa.Contracts.Execution;
using Sofa.Contracts.Notices;
using Sofa.Notices.Data;
using Wolverine;

namespace Sofa.Notices.SignIns;

public static class SignInHandler
{
    public static async Task<OutgoingMessages> Handle(SubscriptionSignInSeen seen, NoticesDb db, CancellationToken cancellation)
    {
        if (await db.RunnerSignIns.FindAsync([seen.RunnerId], cancellation) is not { } signIn)
        {
            signIn = new RunnerSignIn { RunnerId = seen.RunnerId, WorkspaceId = seen.WorkspaceId, UserId = seen.UserId, RunnerName = seen.RunnerName };
            db.RunnerSignIns.Add(signIn);
        }

        if (!signIn.Hear(seen))
        {
            return [];
        }

        await db.SaveChangesAsync(cancellation);
        return [new InboxChanged(seen.WorkspaceId, seen.UserId)];
    }

    public static async Task<OutgoingMessages> Handle(
        SubscriptionSignedOut signedOut, NoticesDb db, TimeProvider time, ILogger<SubscriptionSignedOut> logger, CancellationToken cancellation)
    {
        var now = time.GetUtcNow();
        if (await db.RunnerSignIns.FindAsync([signedOut.RunnerId], cancellation) is not { } signIn)
        {
            signIn = new RunnerSignIn
            {
                RunnerId = signedOut.RunnerId, WorkspaceId = signedOut.WorkspaceId, UserId = signedOut.UserId, RunnerName = signedOut.RunnerName, SeenAt = now,
            };
            db.RunnerSignIns.Add(signIn);
        }

        signIn.HearSignedOut(signedOut, now);
        await db.SaveChangesAsync(cancellation);
        logger.LogInformation(
            "Put subscription Runner {RunnerId} ({RunnerName}) in the Inbox of User {UserId}, who must sign it in again: it refused Run {RunId}",
            signedOut.RunnerId, signedOut.RunnerName, signedOut.UserId, signedOut.RunId);
        return [new InboxChanged(signedOut.WorkspaceId, signedOut.UserId)];
    }
}
