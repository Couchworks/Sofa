import createFetchClient from 'openapi-fetch'
import createClient from 'openapi-react-query'
import type { paths } from './schema'

const fetchLookedUpOnEachCall = (request: Request) => globalThis.fetch(request)

export const fetchClient = createFetchClient<paths>({
  baseUrl: window.location.origin,
  fetch: fetchLookedUpOnEachCall,
})

export const api = createClient(fetchClient)
