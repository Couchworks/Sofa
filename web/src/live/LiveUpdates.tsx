import { HubConnectionState } from '@microsoft/signalr'
import { useQueryClient } from '@tanstack/react-query'
import { type ReactNode, useEffect, useState } from 'react'
import { useWorkspace } from '@/components/workspace'
import { connectionTo, hearLiveUpdates } from './liveConnection'
import { type LiveState, LiveStateContext } from './liveState'

export function LiveUpdates({ children }: { children: ReactNode }) {
  const { workspace } = useWorkspace()
  const queries = useQueryClient()
  const [state, setState] = useState<LiveState>('connecting')
  const workspaceId = workspace?.id

  useEffect(() => {
    if (!workspaceId) return
    const connection = connectionTo(workspaceId)
    hearLiveUpdates(connection, queries)
    connection.onreconnecting(() => setState('connecting'))
    connection.onreconnected(() => {
      setState('live')
      void queries.invalidateQueries()
    })
    connection.onclose(() => setState('offline'))
    let stopped = false
    connection.start().then(
      () => !stopped && setState('live'),
      () => !stopped && setState('offline'),
    )
    return () => {
      stopped = true
      if (connection.state !== HubConnectionState.Disconnected) void connection.stop()
    }
  }, [workspaceId, queries])

  return <LiveStateContext.Provider value={state}>{children}</LiveStateContext.Provider>
}
