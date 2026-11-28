import { createContext, useContext } from 'react'

export type LiveState = 'connecting' | 'live' | 'offline'

export const LiveStateContext = createContext<LiveState>('offline')

export function useLiveState() {
  return useContext(LiveStateContext)
}
