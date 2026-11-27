import createFetchClient from 'openapi-fetch'
import createClient from 'openapi-react-query'
import { accessToken } from '@/auth/session'
import type { paths } from './schema'

const fetchLookedUpOnEachCall = (request: Request) => globalThis.fetch(request)

export const fetchClient = createFetchClient<paths>({
  baseUrl: window.location.origin,
  fetch: fetchLookedUpOnEachCall,
})

fetchClient.use({
  async onRequest({ request }) {
    const token = await accessToken()
    if (token) {
      request.headers.set('Authorization', `Bearer ${token}`)
    }
    return request
  },
})

export const api = createClient(fetchClient)
