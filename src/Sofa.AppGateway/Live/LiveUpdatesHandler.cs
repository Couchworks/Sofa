using Microsoft.AspNetCore.SignalR;
using Sofa.Contracts.Execution;
using Sofa.Contracts.Flow;
using Sofa.Contracts.Notices;

namespace Sofa.AppGateway.Live;

public static class LiveUpdatesHandler
{
    public static Task Handle(WorkItemChanged changed, IHubContext<LiveHub> hub, CancellationToken cancellation) =>
        hub.Clients.Group(LiveGroups.OfTheWorkspace(changed.WorkspaceId))
            .SendAsync(LiveMessages.WorkItemChanged, new WorkItemChangedLive(changed.ProjectKey, changed.WorkItemKey), cancellation);

    public static Task Handle(RunChanged changed, IHubContext<LiveHub> hub, CancellationToken cancellation) =>
        hub.Clients.Group(LiveGroups.OfTheWorkspace(changed.WorkspaceId))
            .SendAsync(LiveMessages.RunChanged, new RunChangedLive(changed.RunId, changed.WorkItemId, changed.State.ToString()), cancellation);

    public static Task Handle(InboxChanged changed, IHubContext<LiveHub> hub, CancellationToken cancellation) =>
        hub.Clients.Group(changed.UserId is { } userId
                ? LiveGroups.OfTheUser(changed.WorkspaceId, userId)
                : LiveGroups.OfTheAdmins(changed.WorkspaceId))
            .SendAsync(LiveMessages.InboxChanged, cancellation);
}
