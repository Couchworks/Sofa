import { act, cleanup, fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import { fakeGateway, renderWithQueries } from '@/test/fakeGateway'
import { liveServer } from '@/test/liveServer'
import { me } from '@/test/signedIn'
import {
  api,
  asking,
  awaitingTheSpec,
  demoProject,
  project,
  runDetails,
  specRun,
  theSpec,
  userNames,
  workItemApi,
} from '@/test/workItems'
import { WorkItemPage } from './WorkItemPage'

vi.mock('@/auth/session', async () => (await import('@/test/signedIn')).signedInSession)

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

function gateway(workItem: unknown, more: Parameters<typeof fakeGateway>[0] = {}) {
  return fakeGateway({
    'GET /api/me': { body: me(false) },
    [`GET ${project}`]: { body: demoProject },
    [`GET ${api}/user-names`]: { body: userNames },
    [`GET ${workItemApi}`]: { body: workItem },
    ...more,
  })
}

test('shows the Work Item’s state, Request, delegating User, Track and each Phase with its Runs', async () => {
  gateway(awaitingTheSpec)

  renderWithQueries(<WorkItemPage projectKey="DEMO" workItemKey="DEMO-1" />)

  expect(await screen.findByText('Add a dark mode toggle')).toBeDefined()
  expect(screen.getAllByText('Waiting for approval').length).toBeGreaterThan(0)
  expect(screen.getByRole('link', { name: 'acme/demo#12' }).getAttribute('href')).toBe('https://github.com/acme/demo/issues/12')
  expect(await screen.findByText('delegated by Ada Lovelace')).toBeDefined()
  expect((within(screen.getByRole('form', { name: 'Track' })).getByLabelText('Track') as HTMLSelectElement).value).toBe('Full')
  const phases = await screen.findAllByRole('region', { name: /Phase$/ })
  expect(phases.map((phase) => phase.getAttribute('aria-label'))).toEqual(['Triage Phase', 'Spec Phase', 'Plan Phase', 'Implement Phase'])
  expect(within(phases[1]).getAllByRole('group')).toHaveLength(2)
  expect(within(phases[2]).getByText('No Run yet.')).toBeDefined()
})

test('opening a Run shows its Brief, its Contribution with its screenshot as an image, and its Transcript', async () => {
  gateway(awaitingTheSpec, {
    [`GET ${api}/runs/${specRun.runId}`]: { body: runDetails(specRun.runId) },
    [`GET ${api}/runs/${specRun.runId}/contribution`]: { body: theSpec },
    [`GET ${api}/runs/${specRun.runId}/contribution/attachments/att1`]: { body: 'png' },
    [`GET ${api}/runs/${specRun.runId}/transcript`]: {
      body: {
        runId: specRun.runId,
        readUpToLine: 3,
        entries: [
          { line: 1, part: 1, role: 'System', text: 'Claude Code started on opus. Tools allowed: Read.', toolName: null, toolInput: null, toolInputCut: false, toolResult: null, toolResultCut: false, toolResultLine: null, at: '2026-11-28T09:10:00Z' },
          { line: 2, part: 1, role: 'Assistant', text: null, toolName: 'mcp__sofa__read_work_item', toolInput: { key: 'DEMO-1' }, toolInputCut: false, toolResult: 'DEMO-1: Add a dark mode toggle', toolResultCut: false, toolResultLine: 3, at: '2026-11-28T09:10:01Z' },
        ],
      },
    },
  })
  renderWithQueries(<WorkItemPage projectKey="DEMO" workItemKey="DEMO-1" />)
  const spec = await screen.findByRole('region', { name: 'Spec Phase' })
  const run = within(spec).getAllByRole('group').at(-1)!

  fireEvent.click(within(run).getByText(/^Run of/))
  ;(run as HTMLDetailsElement).open = true
  fireEvent(run, new Event('toggle'))

  expect(await within(run).findByText('Write the spec of DEMO-1.', { exact: false })).toBeDefined()
  const contributions = await within(screen.getByRole('main')).findAllByRole('region', { name: 'Contribution' })
  expect(contributions.length).toBeGreaterThan(0)
  expect(within(run).getByRole('heading', { name: 'The spec' })).toBeDefined()
  expect((await within(run).findByRole('img', { name: 'mock-up.png' })).getAttribute('src')).toMatch(/^data:image\/png;base64,/)
  expect(await within(run).findByText('mcp__sofa__read_work_item')).toBeDefined()
  expect(within(run).getByText(/Tools allowed: Read/)).toBeDefined()
})

test('a User with Write changes the Track from the page', async () => {
  const { requests } = gateway(asking, {
    [`PUT ${workItemApi}/track`]: { body: { ...asking, track: 'Small', trackChosenByAUser: true } },
  })
  renderWithQueries(<WorkItemPage projectKey="DEMO" workItemKey="DEMO-1" />)

  const track = await screen.findByRole('form', { name: 'Track' })
  fireEvent.change(within(track).getByLabelText('Track'), { target: { value: 'Small' } })
  fireEvent.click(within(track).getByRole('button', { name: 'Change the Track' }))

  await waitFor(() => expect(requests.some((request) => request.method === 'PUT')).toBe(true))
  expect(await requests.find((request) => request.method === 'PUT')!.json()).toEqual({ track: 'Small' })
})

test('a done Work Item’s Track can’t be changed, and the page says why', async () => {
  gateway({ ...asking, state: 'Done', waiting: null, doneAt: '2026-11-28T10:00:00Z' })
  renderWithQueries(<WorkItemPage projectKey="DEMO" workItemKey="DEMO-1" />)

  const track = await screen.findByRole('form', { name: 'Track' })

  expect((within(track).getByLabelText('Track') as HTMLSelectElement).disabled).toBe(true)
  expect(within(track).getByText(/It is done, so its Track no longer changes anything\./)).toBeDefined()
  expect(within(track).queryByText(/Phases still ahead/)).toBeNull()
})

test('its Question is answered from the page, and its answers say who gave them', async () => {
  const { requests } = gateway(asking, {
    [`POST ${workItemApi}/questions/q1/answer`]: { body: awaitingTheSpec },
  })
  renderWithQueries(<WorkItemPage projectKey="DEMO" workItemKey="DEMO-1" />)

  const form = await screen.findByRole('form', { name: "Answer DEMO-1's Question" })
  fireEvent.change(within(form).getByLabelText('Your answer'), { target: { value: 'Yes, follow it.' } })
  fireEvent.click(within(form).getByRole('button', { name: 'Answer' }))

  await waitFor(() => expect(requests.some((request) => request.method === 'POST')).toBe(true))
  expect(await requests.find((request) => request.method === 'POST')!.json()).toEqual({ answer: 'Yes, follow it.' })
  expect(within(screen.getByRole('list', { name: 'Questions' })).getByText('Not answered yet')).toBeDefined()
})

test('an answered Question says who answered it', async () => {
  gateway(awaitingTheSpec, { [`GET ${api}/runs/run-spec-2/contribution`]: { body: { ...theSpec, attachments: [] } } })
  renderWithQueries(<WorkItemPage projectKey="DEMO" workItemKey="DEMO-1" />)

  const questions = await screen.findByRole('list', { name: 'Questions' })

  expect(await within(questions).findByText(/answered by Ada Lovelace/)).toBeDefined()
})

test('a Plan left Unverified is marked as looked at', async () => {
  const unverified = {
    ...awaitingTheSpec,
    state: 'AwaitsPullRequest',
    phase: 'Implement',
    track: 'Small',
    waiting: null,
    questions: [],
    runs: [{ ...specRun, runId: 'run-plan', phase: 'Plan', endedAs: 'Finished: planned' }],
    phaseEnds: [
      { runId: 'run-plan', phase: 'Plan', outcome: 'planned', gate: 'ReviewAfterwards', standing: 'Unverified', endedAt: '2026-11-28T09:20:00Z', decidedByUserId: null, comment: null, decidedAt: null },
    ],
    pullRequest: { repository: 'acme/demo', number: 7, url: 'https://github.com/acme/demo/pull/7', state: 'Open' },
  }
  const { requests } = gateway(unverified, { [`POST ${workItemApi}/phases/Plan/approval`]: { body: unverified } })
  renderWithQueries(<WorkItemPage projectKey="DEMO" workItemKey="DEMO-1" />)

  fireEvent.click(await screen.findByRole('button', { name: 'Mark the Plan as looked at' }))

  await waitFor(() => expect(requests.some((request) => request.method === 'POST')).toBe(true))
  expect(screen.getByRole('link', { name: 'acme/demo#7' }).getAttribute('href')).toBe('https://github.com/acme/demo/pull/7')
  expect(screen.getByText('skipped on the Small Track')).toBeDefined()
})

test('the page changes as its Work Item does, over the live connection', async () => {
  let reads = 0
  gateway(undefined, {
    [`GET ${workItemApi}`]: () => {
      reads++
      return { body: reads === 1 ? asking : { ...asking, state: 'Running', waiting: null } }
    },
  })
  renderWithQueries(<WorkItemPage projectKey="DEMO" workItemKey="DEMO-1" />)
  await screen.findByRole('form', { name: "Answer DEMO-1's Question" })

  act(() => liveServer.say('workItemChanged', { projectKey: 'DEMO', workItemKey: 'DEMO-1' }))

  expect(await screen.findByText('Running')).toBeDefined()
  expect(screen.queryByRole('form', { name: "Answer DEMO-1's Question" })).toBeNull()
})
