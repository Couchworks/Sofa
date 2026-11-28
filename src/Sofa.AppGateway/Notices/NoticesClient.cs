using Sofa.Contracts;

namespace Sofa.AppGateway.Notices;

public sealed record InboxItem(
    Guid Id,
    string Kind,
    string For,
    Guid SubjectId,
    string ProjectKey,
    string? WorkItemKey,
    string Title,
    string Detail,
    string Url,
    DateTimeOffset RaisedAt);

public sealed class NoticesClient(HttpClient notices)
{
    public static readonly Uri Address = new($"http://{SofaServices.Notices}");

    public async Task<List<InboxItem>> InboxOfAsync(Guid workspaceId, Guid userId, CancellationToken cancellation) =>
        (await notices.GetFromJsonAsync<List<InboxItem>>($"/workspaces/{workspaceId}/inbox/users/{userId}", cancellation))!;

    public async Task<List<InboxItem>> AdminsInboxAsync(Guid workspaceId, CancellationToken cancellation) =>
        (await notices.GetFromJsonAsync<List<InboxItem>>($"/workspaces/{workspaceId}/inbox/admins", cancellation))!;
}
