import { api } from '@/api/client'

export function useInbox(workspaceId: string | undefined) {
  return api.useQuery(
    'get',
    '/api/workspaces/{workspaceId}/inbox',
    { params: { path: { workspaceId: workspaceId ?? '' } } },
    { enabled: !!workspaceId },
  )
}
