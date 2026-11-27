using Sofa.Contracts.Flow;

namespace Sofa.Flow.WorkItems;

public sealed class WorkItem
{
    public required Guid Id { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required Guid ProjectId { get; init; }

    public required int Number { get; init; }

    public required string Key { get; init; }

    public required string Title { get; init; }

    public required Guid RequestId { get; init; }

    public required string RequestRepository { get; init; }

    public required long RequestNumber { get; init; }

    public required string RequestUrl { get; init; }

    public Guid? DelegatingUserId { get; set; }

    public required DateTimeOffset OpenedAt { get; init; }

    public RequestRef Request => new(RequestRepository, RequestNumber, RequestUrl);

    public static string KeyOf(string projectKey, int number) => $"{projectKey}-{number}";
}
