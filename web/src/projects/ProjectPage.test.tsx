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

const triager = { id: 'a1', name: 'Triager', role: 'Triager', instructions: 'Triage it.', vendor: 'Anthropic', model: 'haiku' }
const lead = { id: 'a2', name: 'Lead', role: 'Lead', instructions: 'Spec and plan it.', vendor: 'Anthropic', model: 'opus' }
const builder = { id: 'a3', name: 'Builder', role: 'Builder', instructions: 'Build it.', vendor: 'Anthropic', model: 'opus' }
const demo = {
  id: 'p1',
  key: 'DEMO',
  name: 'The demo',
  requests: { connectionId: 'c1', repository: 'acme/demo', label: 'sofa', keepsIssueState: false },
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
    connectionId: 'c1', requestRepository: 'acme/demo', label: 'sofa', codeRepositories: ['acme/demo', 'acme/web'], keepsIssueState: false,
  })
})

test('an Admin turns on keeping the issue’s state', async () => {
  const { requests } = fakeGateway({
    'GET /api/me': { body: me(true) },
    [`GET ${project}`]: { body: demo },
    [`GET /api/workspaces/${workspaceId}/connections`]: {
      body: [{ id: 'c1', system: 'GitHub', account: 'acme', accountType: 'Organization', addedAt: '2026-11-27T12:00:00Z', suspended: false }],
    },
    [`GET /api/workspaces/${workspaceId}/connections/c1/repositories`]: { body: [{ fullName: 'acme/demo', private: false }] },
    [`PUT ${project}/binding`]: { body: { ...demo, requests: { ...demo.requests, keepsIssueState: true } } },
  })
  renderWithQueries(<ProjectPage projectKey="DEMO" />)

  expect(await screen.findByText('left alone')).toBeDefined()
  fireEvent.click(await screen.findByRole('button', { name: 'Change binding' }))
  fireEvent.click(await screen.findByRole('checkbox', { name: /Keep the issue's state/ }))
  fireEvent.click(screen.getByRole('button', { name: 'Save binding' }))

  await waitFor(() => expect(requests.some((request) => request.method === 'PUT')).toBe(true))
  expect((await requests.find((request) => request.method === 'PUT')!.json()).keepsIssueState).toBe(true)
})

const workItems = [
  {
    key: 'DEMO-2',
    title: 'Something from outside',
    requestId: 'r2',
    request: { repository: 'acme/demo', number: 20, url: 'https://github.com/acme/demo/issues/20' },
    delegatedBy: null,
    openedAt: '2026-11-27T13:00:00Z',
  },
  {
    key: 'DEMO-1',
    title: 'Add a dark mode toggle',
    requestId: 'r1',
    request: { repository: 'acme/demo', number: 12, url: 'https://github.com/acme/demo/issues/12' },
    delegatedBy: { userId: 'u1', name: 'Ada Lovelace' },
    openedAt: '2026-11-27T12:00:00Z',
  },
]

test('lists the Project’s Work Items with their key, title, Request and delegating User', async () => {
  fakeGateway({
    'GET /api/me': { body: me(false) },
    [`GET ${project}`]: { body: demo },
    [`GET ${project}/work-items`]: { body: workItems },
  })
  renderWithQueries(<ProjectPage projectKey="DEMO" />)

  const list = await screen.findByRole('list', { name: 'Work Items' })
  const [outside, demo1] = within(list).getAllByRole('listitem')
  expect(within(demo1).getByText('DEMO-1')).toBeDefined()
  expect(within(demo1).getByText('Add a dark mode toggle')).toBeDefined()
  expect(within(demo1).getByRole('link', { name: 'acme/demo#12' }).getAttribute('href')).toBe('https://github.com/acme/demo/issues/12')
  expect(within(demo1).getByText('delegated by Ada Lovelace')).toBeDefined()
  expect(within(outside).getByText('Waiting for a delegating User')).toBeDefined()
})

test('a Work Item’s address shows its Project with the Work Item marked', async () => {
  fakeGateway({
    'GET /api/me': { body: me(false) },
    [`GET ${project}`]: { body: demo },
    [`GET ${project}/work-items`]: { body: workItems },
  })
  renderWithQueries(<ProjectPage projectKey="DEMO" workItemKey="DEMO-1" />)

  const list = await screen.findByRole('list', { name: 'Work Items' })
  expect(within(list).getAllByRole('listitem').map((item) => item.getAttribute('aria-current'))).toEqual([null, 'true'])
})

test('a Project with no Work Items says how to bring one in', async () => {
  fakeGateway({ 'GET /api/me': { body: me(false) }, [`GET ${project}`]: { body: demo }, [`GET ${project}/work-items`]: { body: [] } })
  renderWithQueries(<ProjectPage projectKey="DEMO" />)

  expect(await screen.findByText(/label an issue to bring a Request in/)).toBeDefined()
})
