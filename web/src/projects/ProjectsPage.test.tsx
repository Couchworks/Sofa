import { cleanup, fireEvent, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import { fakeGateway, renderWithQueries } from '@/test/fakeGateway'
import { me, workspaceId } from '@/test/signedIn'
import { ProjectsPage } from './ProjectsPage'

vi.mock('@/auth/session', async () => (await import('@/test/signedIn')).signedInSession)
const navigation = vi.hoisted(() => ({ goTo: vi.fn(), replaceWith: vi.fn() }))
vi.mock('@/lib/navigation', () => navigation)

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  vi.clearAllMocks()
})

const projects = `/api/workspaces/${workspaceId}/projects`
const summary = { id: 'p1', key: 'DEMO', name: 'The demo', requestRepository: 'acme/demo', requestLabel: 'sofa' }

test('lists the Workspace’s Projects for every User, with no way to create one unless an Admin', async () => {
  fakeGateway({ 'GET /api/me': { body: me(false) }, [`GET ${projects}`]: { body: [summary] } })

  renderWithQueries(<ProjectsPage />)

  expect((await screen.findByText('DEMO')).closest('a')!.getAttribute('href')).toBe('/projects/DEMO')
  expect(screen.queryByText('New Project')).toBeNull()
})

test('an Admin creates a Project bound to a Repository through a Connection, and goes to it', async () => {
  const { requests } = fakeGateway({
    'GET /api/me': { body: me(true) },
    [`GET ${projects}`]: { body: [] },
    [`GET /api/workspaces/${workspaceId}/connections`]: {
      body: [{ id: 'c1', system: 'GitHub', account: 'acme', accountType: 'Organization', addedAt: '2026-11-27T12:00:00Z', suspended: false }],
    },
    [`GET /api/workspaces/${workspaceId}/connections/c1/repositories`]: {
      body: [{ fullName: 'acme/demo', private: false }, { fullName: 'acme/web', private: true }],
    },
    [`POST ${projects}`]: { status: 201, body: { ...summary, requests: {}, code: {}, phases: [], agents: [] } },
  })
  renderWithQueries(<ProjectsPage />)

  fireEvent.change(await screen.findByLabelText('Key'), { target: { value: 'demo' } })
  fireEvent.change(screen.getByLabelText('Name'), { target: { value: 'The demo' } })
  fireEvent.change(screen.getByLabelText('Connection'), { target: { value: 'c1' } })
  await screen.findByRole('option', { name: 'acme/web' })
  fireEvent.change(screen.getByLabelText('Requests come from issues in'), { target: { value: 'acme/demo' } })
  fireEvent.click(screen.getByLabelText('acme/demo'))
  fireEvent.click(screen.getByRole('button', { name: 'Create' }))

  await waitFor(() => expect(navigation.goTo).toHaveBeenCalledWith('/projects/DEMO'))
  expect(await requests.find((request) => request.method === 'POST')!.json()).toEqual({
    key: 'DEMO',
    name: 'The demo',
    binding: { connectionId: 'c1', requestRepository: 'acme/demo', label: 'sofa', codeRepositories: ['acme/demo'] },
  })
})

test('a key already taken is refused, saying so', async () => {
  fakeGateway({
    'GET /api/me': { body: me(true) },
    [`GET ${projects}`]: { body: [summary] },
    [`GET /api/workspaces/${workspaceId}/connections`]: { body: [] },
    [`POST ${projects}`]: { status: 409, body: { title: 'The Workspace already has a Project DEMO; a key is never used twice.' } },
  })
  renderWithQueries(<ProjectsPage />)

  fireEvent.change(await screen.findByLabelText('Key'), { target: { value: 'DEMO' } })
  fireEvent.click(screen.getByRole('button', { name: 'Create' }))

  expect(await screen.findByText('The Workspace already has a Project DEMO; a key is never used twice.')).toBeDefined()
  expect(navigation.goTo).not.toHaveBeenCalled()
})
