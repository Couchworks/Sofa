import { cleanup, screen } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import { fakeGateway, renderWithQueries } from '@/test/fakeGateway'
import { ConnectionsCard } from './ConnectionsCard'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

test('lists the Workspace’s Connections', async () => {
  fakeGateway({
    'GET /api/workspaces/0195e7a0-0000-7000-8000-000000000002/connections': {
      body: [
        {
          id: '0190a0a0-0000-7000-8000-000000000001',
          system: 'GitHub',
          account: 'acme',
          accountType: 'Organization',
          addedAt: '2026-11-27T12:00:00Z',
          suspended: false,
        },
        {
          id: '0190a0a0-0000-7000-8000-000000000002',
          system: 'GitHub',
          account: 'ada',
          accountType: 'User',
          addedAt: '2026-11-27T12:00:00Z',
          suspended: true,
        },
      ],
    },
  })

  renderWithQueries(<ConnectionsCard workspaceId="0195e7a0-0000-7000-8000-000000000002" />)

  expect(await screen.findByText('acme')).toBeDefined()
  expect(screen.getByText('ada')).toBeDefined()
  expect(screen.getByText('Suspended')).toBeDefined()
})

test('says how to add the first Connection', async () => {
  fakeGateway({ 'GET /api/workspaces/0195e7a0-0000-7000-8000-000000000002/connections': { body: [] } })

  renderWithQueries(<ConnectionsCard workspaceId="0195e7a0-0000-7000-8000-000000000002" />)

  expect(await screen.findByText(/Install the GitHub App/)).toBeDefined()
})
