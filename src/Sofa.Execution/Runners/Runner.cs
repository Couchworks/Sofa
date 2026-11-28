using Sofa.Contracts.Execution;
using Sofa.Contracts.Flow;

namespace Sofa.Execution.Runners;

public sealed class Runner
{
    public required Guid Id { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required string Name { get; set; }

    public required RunnerKind Kind { get; init; }

    public required Vendor Vendor { get; init; }

    public Guid? OwnerUserId { get; init; }

    public Guid? OwnerTeamId { get; init; }

    public required int RunsAtOnce { get; set; }

    public Guid? ApiKeyId { get; init; }

    public string? WorkerVolume { get; init; }

    public bool SignedIn { get; set; }

    public DateTimeOffset? SignInEndsAt { get; set; }

    public DateTimeOffset? SignInCheckedAt { get; set; }

    public DateTimeOffset CreatedAt { get; init; }

    public void Saw(SignInSeen seen, DateTimeOffset now)
    {
        SignedIn = seen.SignedIn;
        SignInEndsAt = seen.SignedIn ? seen.EndsAt : null;
        SignInCheckedAt = now;
    }

    public SubscriptionSignInSeen? SignInSeen =>
        Kind == RunnerKind.Subscription && OwnerUserId is { } userId && SignInCheckedAt is { } checkedAt
            ? new SubscriptionSignInSeen(WorkspaceId, userId, Id, Name, SignedIn, SignInEndsAt, checkedAt)
            : null;

    public RunnerOwner Owner => OwnerUserId is { } userId ? RunnerOwner.User(userId) : RunnerOwner.Team(OwnerTeamId!.Value);

    public RunnerDetails Details => new(
        Id,
        Name,
        Kind,
        Vendor,
        Owner,
        RunsAtOnce,
        HasApiKey: ApiKeyId is not null,
        WorkerVolume,
        Kind == RunnerKind.Subscription ? new SubscriptionSignIn(SignedIn, SignInEndsAt, SignInCheckedAt) : null);
}
