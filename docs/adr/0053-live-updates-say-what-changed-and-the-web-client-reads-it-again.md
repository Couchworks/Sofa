# Live updates say what changed, and the web client reads it again

The App Gateway serves a SignalR hub to every User of a Workspace at `/api/workspaces/{workspaceId}/live`, behind the same check as the rest of the Workspace's API. It hears three events on the broker, `WorkItemChanged` from Flow, `RunChanged` from Execution and `InboxChanged` from Notices, each published once its service has saved the change, and passes each on as a small message naming what changed: a Work Item's key, a Run's id and state, or that an Inbox changed. Work Items and Runs go to everyone in the Workspace, an Inbox only to its User or, for the Admins' Inbox, to the Admins. The web client then reads what changed again through the REST API it already uses, by invalidating the matching TanStack queries, so a live message never carries a Work Item, a Run or an Inbox item itself.

A browser can't set headers on a WebSocket, so for this path only, the App Gateway takes the access token from the `access_token` query parameter, as SignalR's client sends it, and puts it in the `Authorization` header before checking it like any other.

## Considered Options

- **Messages carrying the changed records**: no read after each message, but a second shape of every record to keep in step with the API, and each message would have to know what its receiver may see.
- **Polling**: nothing new to run, but every page asking every few seconds, and a change showing only on the next poll.
- **Server-sent events**: simpler than SignalR, but the roadmap settles SignalR, whose client reconnects by itself and later carries a backplane.

## Consequences

- A change costs one read per page showing it; a Run's Transcript still grows without a message, so the web client reads a running Run's Transcript again every few seconds.
- The App Gateway now has handlers of its own and listens on its queue. With several App Gateway replicas each event would reach only one of them, so they need a backplane and a queue each, as the roadmap foresees.
- A live connection keeps the access it was opened with: someone removed from the Workspace keeps hearing which Work Items change until they reconnect, though they can read none of them.
- An access token in a query string can reach logs; only the live path accepts one.
