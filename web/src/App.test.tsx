import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, render, screen } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import App from './App'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function renderApp() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={queryClient}>
      <App />
    </QueryClientProvider>,
  )
}

test('shows the App Gateway up when its status answers', async () => {
  const fetch = vi.fn(async () =>
    Response.json({ name: 'Sofa', environment: 'Development', time: '2026-11-27T12:00:00Z' }),
  )
  vi.stubGlobal('fetch', fetch)

  renderApp()

  expect(await screen.findByText('Up')).toBeDefined()
  expect(screen.getByText('Development')).toBeDefined()
  expect(new URL((fetch.mock.calls[0] as unknown as [Request])[0].url).pathname).toBe('/api/status')
})

test('shows the App Gateway unreachable when its status fails', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => new Response(null, { status: 502 })))

  renderApp()

  expect(await screen.findByText('Unreachable')).toBeDefined()
})
