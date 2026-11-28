import { HubConnectionBuilder, HubConnectionState, type HubConnection } from '@microsoft/signalr'
import type { Query, QueryClient } from '@tanstack/react-query'
import { accessToken } from '@/auth/session'

export type WorkItemChanged = { projectKey: string; workItemKey: string }
export type RunChanged = { runId: string; workItemId: string; state: string }

const workspaceApi = '/api/workspaces/{workspaceId}'
const runPaths = [`${workspaceApi}/runs/{runId}`, `${workspaceApi}/runs/{runId}/transcript`, `${workspaceApi}/runs/{runId}/contribution`]

function pathOf(query: Query) {
  return typeof query.queryKey[1] === 'string' ? query.queryKey[1] : ''
}

function pathParametersOf(query: Query) {
  return ((query.queryKey[2] as { params?: { path?: Record<string, string> } } | undefined)?.params?.path ?? {}) as Record<string, string>
}

export function isAboutWorkItems(query: Query) {
  return pathOf(query).startsWith(`${workspaceApi}/projects/{key}/work-items`)
}

export function isAboutTheRun(runId: string) {
  return (query: Query) => runPaths.includes(pathOf(query)) && pathParametersOf(query).runId === runId
}

export function isAboutTheInbox(query: Query) {
  return pathOf(query) === `${workspaceApi}/inbox`
}

export function isAboutRuns(query: Query) {
  return pathOf(query) === `${workspaceApi}/runs`
}

export function hearLiveUpdates(connection: HubConnection, queries: QueryClient) {
  connection.on('workItemChanged', () => {
    void queries.invalidateQueries({ predicate: (query) => isAboutWorkItems(query) || isAboutTheInbox(query) })
  })
  connection.on('runChanged', ({ runId }: RunChanged) => {
    void queries.invalidateQueries({ predicate: (query) => isAboutTheRun(runId)(query) || isAboutRuns(query) })
  })
  connection.on('inboxChanged', () => {
    void queries.invalidateQueries({ predicate: isAboutTheInbox })
  })
}

export function connectionTo(workspaceId: string) {
  return new HubConnectionBuilder()
    .withUrl(`${window.location.origin}/api/workspaces/${workspaceId}/live`, { accessTokenFactory: async () => (await accessToken()) ?? '' })
    .withAutomaticReconnect()
    .build()
}

export function isConnected(connection: HubConnection) {
  return connection.state === HubConnectionState.Connected
}
