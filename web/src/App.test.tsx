import { cleanup, screen } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import App from './App'
import { fakeGateway, renderWithQueries } from './test/fakeGateway'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

test('shows the App Gateway up when its status answers', async () => {
  const { requests } = fakeGateway({
    'GET /api/status': { body: { name: 'Sofa', environment: 'Development', time: '2026-11-27T12:00:00Z' } },
  })

  renderWithQueries(<App />)

  expect(await screen.findByText('Up')).toBeDefined()
  expect(screen.getByText('Development')).toBeDefined()
  expect(requests.some((request) => new URL(request.url).pathname === '/api/status')).toBe(true)
})

test('shows the App Gateway unreachable when its status fails', async () => {
  fakeGateway({ 'GET /api/status': { status: 502 } })

  renderWithQueries(<App />)

  expect((await screen.findAllByText('Unreachable')).length).toBeGreaterThan(0)
})
