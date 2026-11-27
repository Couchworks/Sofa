import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { render } from '@testing-library/react'
import type { ReactNode } from 'react'
import { vi } from 'vitest'

type Answer = { status?: number; body?: unknown }
type Routes = Record<string, Answer | ((request: Request) => Answer | Promise<Answer>)>

export function fakeGateway(routes: Routes) {
  const requests: Request[] = []
  const fetch = vi.fn(async (request: Request) => {
    requests.push(request.clone())
    const route = routes[`${request.method} ${new URL(request.url).pathname}`]
    if (!route) return new Response(null, { status: 404 })
    const { status = 200, body } = typeof route === 'function' ? await route(request) : route
    return body === undefined ? new Response(null, { status }) : Response.json(body, { status })
  })
  vi.stubGlobal('fetch', fetch)
  return { requests }
}

export function renderWithQueries(ui: ReactNode) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  return render(<QueryClientProvider client={queryClient}>{ui}</QueryClientProvider>)
}
