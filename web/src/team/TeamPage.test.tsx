import { cleanup, fireEvent, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import { fakeGateway, renderWithQueries } from '@/test/fakeGateway'
import { me, teamId, workspaceId } from '@/test/signedIn'
import { TeamPage } from './TeamPage'

vi.mock('@/auth/session', async () => (await import('@/test/signedIn')).signedInSession)

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

const api = `/api/workspaces/${workspaceId}`
const ada = { userId: me(true).id, name: 'Ada Lovelace', gitHubLogin: 'ada-gh' }
const bea = { userId: '0195e7a0-0000-7000-8000-000000000009', name: 'Bea Kowalski', gitHubLogin: 'bea-gh' }

test('an Admin adds a User of the Workspace to the Team and removes another', async () => {
  const { requests } = fakeGateway({
    'GET /api/me': { body: me(true) },
    [`GET ${api}/teams`]: { body: [{ id: teamId, name: 'Engines', members: [ada] }] },
    [`GET ${api}/users`]: { body: [{ ...ada, email: null, isAdmin: true }, { ...bea, email: null, isAdmin: false }] },
    [`POST ${api}/teams/${teamId}/users`]: { status: 204 },
    [`DELETE ${api}/teams/${teamId}/users/${ada.userId}`]: { status: 204 },
  })
  renderWithQueries(<TeamPage />)

  await screen.findByRole('option', { name: 'Bea Kowalski (bea-gh)' })
  fireEvent.change(screen.getByLabelText('Add to Engines'), { target: { value: bea.userId } })
  fireEvent.click(screen.getByRole('button', { name: 'Add' }))
  fireEvent.click(screen.getByRole('button', { name: 'Remove Ada Lovelace' }))

  await waitFor(() => expect(requests.filter((request) => request.method !== 'GET')).toHaveLength(2))
  expect(await requests.find((request) => request.method === 'POST')!.json()).toEqual({ userId: bea.userId })
  expect(requests.some((request) => request.method === 'DELETE')).toBe(true)
})

test('a User who is no Admin is told only an Admin changes the Team', async () => {
  const { requests } = fakeGateway({ 'GET /api/me': { body: me(false) } })

  renderWithQueries(<TeamPage />)

  expect(await screen.findByText('Only an Admin changes who is in the Team.')).toBeDefined()
  expect(requests.some((request) => new URL(request.url).pathname.endsWith('/teams'))).toBe(false)
})
