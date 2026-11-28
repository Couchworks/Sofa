import { act, cleanup, fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import { fakeGateway, renderWithQueries } from '@/test/fakeGateway'
import { liveServer } from '@/test/liveServer'
import { me } from '@/test/signedIn'
import { api, asking, awaitingTheSpec, theSpec, workItemApi } from '@/test/workItems'
import { InboxPage } from './InboxPage'

vi.mock('@/auth/session', async () => (await import('@/test/signedIn')).signedInSession)

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

const inbox = `${api}/inbox`

function item(kind: string, overrides: Record<string, unknown> = {}) {
  return {
    id: `item-${kind}`,
    kind,
    for: 'TheUser',
    subjectId: 'q1',
    projectKey: 'DEMO',
    workItemKey: 'DEMO-1',
    title: 'DEMO-1: Add a dark mode toggle',
    detail: 'Its Spec Run asked: Should the toggle follow the system setting by default?',
    url: 'https://github.com/acme/demo/issues/12',
    raisedAt: '2026-11-28T09:03:00Z',
    ...overrides,
  }
}

test('an empty Inbox says what would come to it', async () => {
  fakeGateway({ 'GET /api/me': { body: me(false) }, [`GET ${inbox}`]: { body: [] } })

  renderWithQueries(<InboxPage />)

  expect(await screen.findByText(/Nothing waits for you/)).toBeDefined()
})

test('a Question is answered from the Inbox', async () => {
  const { requests } = fakeGateway({
    'GET /api/me': { body: me(false) },
    [`GET ${inbox}`]: { body: [item('Question')] },
    [`GET ${workItemApi}`]: { body: asking },
    [`POST ${workItemApi}/questions/q1/answer`]: { body: awaitingTheSpec },
  })
  renderWithQueries(<InboxPage />)

  const card = await screen.findByLabelText('DEMO-1: Add a dark mode toggle')
  expect(within(card).getByRole('link', { name: 'DEMO-1: Add a dark mode toggle' }).getAttribute('href')).toBe('/projects/DEMO/work-items/DEMO-1')
  const form = await within(card).findByRole('form', { name: "Answer DEMO-1's Question" })
  expect(within(form).getByText('Should the toggle follow the system setting by default?')).toBeDefined()
  fireEvent.change(within(form).getByLabelText('Your answer'), { target: { value: 'Yes, follow it.' } })
  fireEvent.click(within(form).getByRole('button', { name: 'Answer' }))

  await waitFor(() => expect(requests.some((request) => request.method === 'POST')).toBe(true))
  expect(await requests.find((request) => request.method === 'POST')!.json()).toEqual({ answer: 'Yes, follow it.' })
})

test('a Gate shows the spec in full, with its screenshot, and is approved', async () => {
  const { requests } = fakeGateway({
    'GET /api/me': { body: me(false) },
    [`GET ${inbox}`]: { body: [item('Approval', { subjectId: 'run-spec-2', detail: 'Its Spec waits for a User’s approval.' })] },
    [`GET ${workItemApi}`]: { body: awaitingTheSpec },
    [`GET ${api}/runs/run-spec-2/contribution`]: { body: theSpec },
    [`GET ${api}/runs/run-spec-2/contribution/attachments/att1`]: { body: 'png' },
    [`POST ${workItemApi}/phases/Spec/approval`]: { body: { ...awaitingTheSpec, state: 'Running', waiting: null } },
  })
  renderWithQueries(<InboxPage />)

  expect(await screen.findByRole('heading', { name: 'The spec' })).toBeDefined()
  expect(screen.getByText('It follows the system setting')).toBeDefined()
  expect((await screen.findByRole('img', { name: 'mock-up.png' })).getAttribute('src')).toMatch(/^data:image\/png;base64,/)
  fireEvent.click(screen.getByRole('button', { name: 'Approve the Spec' }))

  await waitFor(() => expect(requests.some((request) => request.method === 'POST')).toBe(true))
  expect(new URL(requests.find((request) => request.method === 'POST')!.url).pathname).toBe(`${workItemApi}/phases/Spec/approval`)
})

test('a Gate is sent back with a comment', async () => {
  const { requests } = fakeGateway({
    'GET /api/me': { body: me(false) },
    [`GET ${inbox}`]: { body: [item('Approval', { subjectId: 'run-spec-2' })] },
    [`GET ${workItemApi}`]: { body: awaitingTheSpec },
    [`GET ${api}/runs/run-spec-2/contribution`]: { body: { ...theSpec, attachments: [] } },
    [`POST ${workItemApi}/phases/Spec/sending-back`]: { body: awaitingTheSpec },
  })
  renderWithQueries(<InboxPage />)

  const sendBack = await screen.findByRole('button', { name: 'Send back' })
  expect((sendBack as HTMLButtonElement).disabled).toBe(true)
  fireEvent.change(screen.getByLabelText(/send it back/), { target: { value: 'Keep the toggle in the header.' } })
  fireEvent.click(sendBack)

  await waitFor(() => expect(requests.some((request) => request.method === 'POST')).toBe(true))
  expect(await requests.find((request) => request.method === 'POST')!.json()).toEqual({ comment: 'Keep the toggle in the header.' })
})

test('a Run gone wrong is retried from the Inbox', async () => {
  const goneWrong = {
    ...asking,
    state: 'AwaitsUser',
    waiting: { waitId: 'run-spec-1', kind: 'RunGoneWrong', why: 'Its Spec Run ended Refused: Sign in again.', since: '2026-11-28T09:03:00Z' },
  }
  const { requests } = fakeGateway({
    'GET /api/me': { body: me(false) },
    [`GET ${inbox}`]: { body: [item('RunGoneWrong', { subjectId: 'run-spec-1', detail: goneWrong.waiting.why })] },
    [`GET ${workItemApi}`]: { body: goneWrong },
    [`POST ${api}/runs/run-spec-1/retry`]: { body: {} },
  })
  renderWithQueries(<InboxPage />)

  expect(await screen.findByText('Its Spec Run ended Refused: Sign in again.')).toBeDefined()
  fireEvent.click(screen.getByRole('button', { name: 'Retry' }))

  await waitFor(() => expect(requests.some((request) => request.method === 'POST')).toBe(true))
  expect(new URL(requests.find((request) => request.method === 'POST')!.url).pathname).toBe(`${api}/runs/run-spec-1/retry`)
})

test('a pull request closed unmerged links to it', async () => {
  const closed = {
    ...asking,
    state: 'AwaitsUser',
    waiting: { waitId: 'w-pr', kind: 'PullRequestClosedUnmerged', why: 'Its pull request was closed without being merged.', since: '2026-11-28T09:03:00Z' },
    pullRequest: { repository: 'acme/demo', number: 7, url: 'https://github.com/acme/demo/pull/7', state: 'ClosedUnmerged' },
  }
  fakeGateway({
    'GET /api/me': { body: me(false) },
    [`GET ${inbox}`]: { body: [item('PullRequestClosedUnmerged', { subjectId: 'w-pr' })] },
    [`GET ${workItemApi}`]: { body: closed },
  })
  renderWithQueries(<InboxPage />)

  expect((await screen.findByRole('link', { name: 'acme/demo#7' })).getAttribute('href')).toBe('https://github.com/acme/demo/pull/7')
})

test('a Request waiting for a delegating User and a sign-in ending say what to do, with no Work Item', async () => {
  fakeGateway({
    'GET /api/me': { body: me(true) },
    [`GET ${inbox}`]: {
      body: [
        item('RequestAwaitingDelegation', {
          for: 'TheWorkspacesAdmins',
          workItemKey: null,
          title: 'Something from outside',
          detail: 'acme/demo#20 was labelled by mallory, whose GitHub account is linked to no User of the Workspace.',
          url: 'https://github.com/acme/demo/issues/20',
        }),
        item('SubscriptionSignInEnding', {
          projectKey: '',
          workItemKey: null,
          title: "Ada's Max's sign-in ends soon",
          detail: 'Run `sofa runner login r1` on Sofa’s box to sign it in again.',
          url: '/runners',
        }),
      ],
    },
  })
  renderWithQueries(<InboxPage />)

  expect((await screen.findByRole('link', { name: 'The issue on GitHub' })).getAttribute('href')).toBe('https://github.com/acme/demo/issues/20')
  expect(screen.getByText("Ada's Max's sign-in ends soon")).toBeDefined()
  expect(screen.getByRole('link', { name: 'Your Runners' }).getAttribute('href')).toBe('/runners')
})

test('an item whose Work Item no longer waits for it says so', async () => {
  fakeGateway({
    'GET /api/me': { body: me(false) },
    [`GET ${inbox}`]: { body: [item('Question')] },
    [`GET ${workItemApi}`]: { body: { ...asking, state: 'Running', waiting: null } },
  })
  renderWithQueries(<InboxPage />)

  expect(await screen.findByText(/It no longer waits for this/)).toBeDefined()
})

test('the Inbox changes as its items do, over the live connection', async () => {
  let reads = 0
  fakeGateway({
    'GET /api/me': { body: me(false) },
    [`GET ${inbox}`]: () => {
      reads++
      return { body: reads === 1 ? [] : [item('Question')] }
    },
    [`GET ${workItemApi}`]: { body: asking },
  })
  renderWithQueries(<InboxPage />)
  await screen.findByText(/Nothing waits for you/)

  act(() => liveServer.say('inboxChanged'))

  expect(await screen.findByRole('form', { name: "Answer DEMO-1's Question" })).toBeDefined()
})
