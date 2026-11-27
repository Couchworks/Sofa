import { cleanup, fireEvent, screen, waitFor, within } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import { fakeGateway, renderWithQueries } from '@/test/fakeGateway'
import { me, teamId, workspaceId } from '@/test/signedIn'
import { RunnersPage } from './RunnersPage'

vi.mock('@/auth/session', async () => (await import('@/test/signedIn')).signedInSession)

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

const runners = `/api/workspaces/${workspaceId}/runners`
const subscription = {
  id: '0195e7a0-0000-7000-8000-0000000000aa',
  name: "Ada's Max",
  kind: 'Subscription',
  vendor: 'Anthropic',
  owner: { kind: 'User', id: me(true).id },
  runsAtOnce: 2,
  hasApiKey: false,
  workerVolume: 'sofa-worker-0195e7a0-0000-7000-8000-0000000000aa',
  signIn: { signedIn: true, endsAt: '2026-12-24T10:00:00Z', checkedAt: '2026-11-27T12:00:00Z' },
}
const teamKey = {
  id: '0195e7a0-0000-7000-8000-0000000000bb',
  name: 'Engines’ key',
  kind: 'ApiKey',
  vendor: 'Anthropic',
  owner: { kind: 'Team', id: teamId },
  runsAtOnce: 2,
  hasApiKey: true,
  workerVolume: null,
  signIn: null,
}

test('shows each Runner with its kind and owner, and how to sign a subscription in from the box', async () => {
  fakeGateway({ 'GET /api/me': { body: me(false) }, [`GET ${runners}`]: { body: [subscription, teamKey] } })

  renderWithQueries(<RunnersPage />)

  expect(await screen.findByText(`sofa runner login ${subscription.id}`)).toBeDefined()
  expect(screen.getByText(/Signed in until/)).toBeDefined()
  expect(screen.getByText('Its API key is saved, and never shown again.')).toBeDefined()
  expect(screen.queryByText('The Team’s API key')).toBeNull()
})

test('an API key is sent once and gone from the page once saved', async () => {
  const { requests } = fakeGateway({
    'GET /api/me': { body: me(false) },
    [`GET ${runners}`]: { body: [] },
    [`POST ${runners}/api-key`]: { status: 201, body: { ...teamKey, name: 'Mine', owner: { kind: 'User', id: me(false).id } } },
  })
  renderWithQueries(<RunnersPage />)

  const card = (await screen.findByText('Your API key')).closest('[data-slot=card]') as HTMLElement
  fireEvent.change(within(card).getByLabelText('Name'), { target: { value: 'Mine' } })
  fireEvent.change(within(card).getByLabelText('API key'), { target: { value: 'sk-ant-api03-secret' } })
  fireEvent.click(within(card).getByRole('button', { name: 'Add API key Runner' }))

  expect(await within(card).findByText('Its key is saved and won’t be shown again.')).toBeDefined()
  expect((within(card).getByLabelText('API key') as HTMLInputElement).value).toBe('')
  expect(document.body.textContent).not.toContain('sk-ant-api03-secret')
  expect(await requests.find((request) => request.method === 'POST')!.json()).toEqual({
    name: 'Mine', apiKey: 'sk-ant-api03-secret', runsAtOnce: 2, forTheTeam: false,
  })
})

test('an Admin adds the Team’s API key Runner', async () => {
  const { requests } = fakeGateway({
    'GET /api/me': { body: me(true) },
    [`GET ${runners}`]: { body: [] },
    [`POST ${runners}/api-key`]: { status: 201, body: teamKey },
  })
  renderWithQueries(<RunnersPage />)

  const card = (await screen.findByText('The Team’s API key')).closest('[data-slot=card]') as HTMLElement
  fireEvent.change(within(card).getByLabelText('Name'), { target: { value: 'Engines’ key' } })
  fireEvent.change(within(card).getByLabelText('API key'), { target: { value: 'sk-ant-api03-team' } })
  fireEvent.click(within(card).getByRole('button', { name: 'Add team Runner' }))

  await waitFor(() => expect(requests.some((request) => request.method === 'POST')).toBe(true))
  expect((await requests.find((request) => request.method === 'POST')!.json()).forTheTeam).toBe(true)
})

test('adding a subscription Runner shows the command to sign it in', async () => {
  fakeGateway({
    'GET /api/me': { body: me(false) },
    [`GET ${runners}`]: { body: [] },
    [`POST ${runners}/subscription`]: { status: 201, body: { ...subscription, signIn: { signedIn: false, endsAt: null, checkedAt: null } } },
  })
  renderWithQueries(<RunnersPage />)

  fireEvent.change(await screen.findByLabelText('Name', { selector: '#subscription-name' }), { target: { value: "Ada's Max" } })
  fireEvent.click(screen.getByRole('button', { name: 'Add subscription Runner' }))

  expect(await screen.findByText(`sofa runner login ${subscription.id}`)).toBeDefined()
})
