import { api } from '@/api/client'

export function useWorkspace() {
  const me = api.useQuery('get', '/api/me')
  const workspace = me.data?.workspaces[0]
  return { me, workspace }
}

export function problemOf(error: unknown, otherwise: string) {
  return (error as { title?: string } | null | undefined)?.title ?? otherwise
}
