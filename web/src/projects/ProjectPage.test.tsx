import { cleanup, fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import { fakeGateway, renderWithQueries } from '@/test/fakeGateway'
import { me, workspaceId } from '@/test/signedIn'
import { ProjectPage } from './ProjectPage'

vi.mock('@/auth/session', async () => (await import('@/test/signedIn')).signedInSession)

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

const registered = { registration: 'Registered', registrationRefusedBecause: null }
const triager = { id: 'a1', name: 'Triager', role: 'Triager', instructions: 'Triage it.', vendor: 'Anthropic', model: 'haiku', ...registered }
const lead = { id: 'a2', name: 'Lead', role: 'Lead', instructions: 'Spec and plan it.', vendor: 'Anthropic', model: 'opus', ...registered }
const builder = { id: 'a3', name: 'Builder', role: 'Builder', instructions: 'Build it.', vendor: 'Anthropic', model: 'opus', ...registered }
const demo = {
  id: 'p1',
  key: 'DEMO',
  name: 'The demo',
  requests: { connectionId: 'c1', repository: 'acme/demo', label: 'sofa' },
  code: { connectionId: 'c1', repositories: ['acme/demo'] },
  phases: [
    { name: 'Triage', position: 0, pattern: 'Solo', castAgentId: 'a1', gate: 'Auto', sandbox: 'ReadOnly' },
    { name: 'Spec', position: 1, pattern: 'Solo', castAgentId: 'a2', gate: 'Approve', sandbox: 'ReadOnly' },
    { name: 'Plan', position: 2, pattern: 'Solo', castAgentId: 'a2', gate: 'Approve', sandbox: 'ReadOnly' },
    { name: 'Implement', position: 3, pattern: 'Solo', castAgentId: 'a3', gate: 'Auto', sandbox: 'OwnBranch' },
  ],
  agents: [triager, lead, builder],
}
const project = `/api/workspaces/${workspaceId}/projects/DEMO`

test('shows the Project’s bindings, its Flow in order and its three Agents', async () => {
  fakeGateway({ 'GET /api/me': { body: me(false) }, [`GET ${project}`]: { body: demo } })

  renderWithQueries(<ProjectPage projectKey="DEMO" />)

  expect(await screen.findByText('The demo')).toBeDefined()
  expect(screen.getByText(/issues in acme\/demo labelled/)).toBeDefined()
  const phases = screen.getAllByRole('form', { name: /Phase$/ })
  expect(phases.map((phase) => phase.getAttribute('aria-label'))).toEqual([
    'Triage Phase', 'Spec Phase', 'Plan Phase', 'Implement Phase',
  ])
  expect((within(phases[3]).getByLabelText('Sandbox') as HTMLSelectElement).value).toBe('OwnBranch')
  expect(screen.getAllByRole('form', { name: /Agent$/ })).toHaveLength(3)
})

test('a User with Write changes the Lead’s model', async () => {
  const { requests } = fakeGateway({
    'GET /api/me': { body: me(false) },
    [`GET ${project}`]: { body: demo },
    [`PUT ${project}/agents/a2`]: { body: { ...demo, agents: [triager, { ...lead, model: 'sonnet' }, builder] } },
  })
  renderWithQueries(<ProjectPage projectKey="DEMO" />)

  const leadForm = await screen.findByRole('form', { name: 'Lead Agent' })
  fireEvent.change(within(leadForm).getByLabelText('Model'), { target: { value: 'sonnet' } })
  fireEvent.click(within(leadForm).getByRole('button', { name: 'Save Lead' }))

  await waitFor(() => expect(requests.some((request) => request.method === 'PUT')).toBe(true))
  const put = requests.find((request) => request.method === 'PUT')!
  expect(await put.json()).toEqual({ role: 'Lead', instructions: 'Spec and plan it.', vendor: 'Anthropic', model: 'sonnet' })
})

test('a User with Write recasts a Phase and changes its Gate', async () => {
  const { requests } = fakeGateway({
    'GET /api/me': { body: me(false) },
    [`GET ${project}`]: { body: demo },
    [`PUT ${project}/phases/Plan`]: { body: demo },
  })
  renderWithQueries(<ProjectPage projectKey="DEMO" />)

  const plan = await screen.findByRole('form', { name: 'Plan Phase' })
  fireEvent.change(within(plan).getByLabelText('Cast'), { target: { value: 'a3' } })
  fireEvent.change(within(plan).getByLabelText('Gate'), { target: { value: 'ReviewAfterwards' } })
  fireEvent.click(within(plan).getByRole('button', { name: 'Save' }))

  await waitFor(() => expect(requests.some((request) => request.method === 'PUT')).toBe(true))
  expect(await requests.find((request) => request.method === 'PUT')!.json()).toEqual({
    castAgentId: 'a3', gate: 'ReviewAfterwards', sandbox: 'ReadOnly',
  })
})

test('only an Admin is offered to change the binding', async () => {
  fakeGateway({ 'GET /api/me': { body: me(false) }, [`GET ${project}`]: { body: demo } })
  renderWithQueries(<ProjectPage projectKey="DEMO" />)

  await screen.findByText('The demo')

  expect(screen.queryByRole('button', { name: 'Change binding' })).toBeNull()
})

test('an Admin changes the binding to more Repositories the Connection reaches', async () => {
  const { requests } = fakeGateway({
    'GET /api/me': { body: me(true) },
    [`GET ${project}`]: { body: demo },
    [`GET /api/workspaces/${workspaceId}/connections`]: {
      body: [{ id: 'c1', system: 'GitHub', account: 'acme', accountType: 'Organization', addedAt: '2026-11-27T12:00:00Z', suspended: false }],
    },
    [`GET /api/workspaces/${workspaceId}/connections/c1/repositories`]: {
      body: [{ fullName: 'acme/demo', private: false }, { fullName: 'acme/web', private: true }],
    },
    [`PUT ${project}/binding`]: { body: demo },
  })
  renderWithQueries(<ProjectPage projectKey="DEMO" />)

  fireEvent.click(await screen.findByRole('button', { name: 'Change binding' }))
  fireEvent.click(await screen.findByLabelText('acme/web'))
  fireEvent.click(screen.getByRole('button', { name: 'Save binding' }))

  await waitFor(() => expect(requests.some((request) => request.method === 'PUT')).toBe(true))
  expect(await requests.find((request) => request.method === 'PUT')!.json()).toEqual({
    connectionId: 'c1', requestRepository: 'acme/demo', label: 'sofa', codeRepositories: ['acme/demo', 'acme/web'],
  })
})

test('an Agent Identity refused shows as not registered with the reason, and Retry asks Identity again', async () => {
  const refusedLead = { ...lead, registration: 'Refused', registrationRefusedBecause: 'Another Agent already has that id.' }
  const { requests } = fakeGateway({
    'GET /api/me': { body: me(false) },
    [`GET ${project}`]: { body: { ...demo, agents: [triager, refusedLead, builder] } },
    [`POST ${project}/agents/a2/registration`]: { body: { ...demo, agents: [triager, { ...lead, registration: 'Pending' }, builder] } },
  })
  renderWithQueries(<ProjectPage projectKey="DEMO" />)

  const leadForm = await screen.findByRole('form', { name: 'Lead Agent' })
  expect(within(leadForm).getByText('Not registered')).toBeDefined()
  expect(within(leadForm).getByText('Another Agent already has that id.')).toBeDefined()
  expect(within(screen.getByRole('form', { name: 'Builder Agent' })).queryByText('Not registered')).toBeNull()
  fireEvent.click(within(leadForm).getByRole('button', { name: 'Retry' }))

  await waitFor(() => expect(requests.some((request) => request.method === 'POST')).toBe(true))
  expect(requests.find((request) => request.method === 'POST')!.url).toContain('/projects/DEMO/agents/a2/registration')
})
