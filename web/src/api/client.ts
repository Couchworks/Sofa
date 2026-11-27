import createFetchClient from 'openapi-fetch'
import createClient from 'openapi-react-query'
import type { paths } from './schema'

// The web client is served through the App Gateway, so the API is on the same origin.
// fetch is looked up on each call rather than once, so tests can stub it.
export const fetchClient = createFetchClient<paths>({
  baseUrl: window.location.origin,
  fetch: (request) => globalThis.fetch(request),
})

export const api = createClient(fetchClient)
