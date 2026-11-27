import { QueryClient, QueryClientProvider } from '@tanstack/react-query'
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react'
import { afterEach, beforeEach, expect, test, vi } from 'vitest'
import App from './App'

const session = vi.hoisted(() => ({
  signedInUser: vi.fn(),
  accessToken: vi.fn(),
  startSigningIn: vi.fn(),
  finishSigningIn: vi.fn(),
  signOut: vi.fn(),
  userManager: { signinRedirect: vi.fn() },
  setupPath: '/setup',
  signedInPath: '/signed-in',
}))
vi.mock('@/auth/session', () => session)

const navigation = vi.hoisted(() => ({ goTo: vi.fn(), replaceWith: vi.fn() }))
vi.mock('@/lib/navigation', () => navigation)

const ada = {
  id: '0195e7a0-0000-7000-8000-000000000001',
  name: 'Ada Lovelace',
  email: 'ada@example.com',
  gitHubLogin: null,
  workspaces: [
    { id: '0195e7a0-0000-7000-8000-000000000002', name: "Ada's Workspace", isAdmin: true, teams: [{ id: '0195e7a0-0000-7000-8000-000000000003', name: 'Engines' }] },
  ],
}

beforeEach(() => {
  session.signedInUser.mockResolvedValue({ access_token: 'adas-token' })
  session.accessToken.mockResolvedValue('adas-token')
})

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  vi.clearAllMocks()
})

function renderAt(pathname: string, search = '') {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } })
  render(
    <QueryClientProvider client={queryClient}>
      <App location={{ pathname, search }} />
    </QueryClientProvider>,
  )
}

function answering(answers: Record<string, () => Response>) {
  const fetch = vi.fn(async (input: Request | string) => {
    const path = new URL(typeof input === 'string' ? input : input.url, 'http://sofa.test').pathname
    return answers[path]?.() ?? new Response(null, { status: 404 })
  })
  vi.stubGlobal('fetch', fetch)
  return fetch
}

function requestTo(fetch: ReturnType<typeof answering>, path: string) {
  return fetch.mock.calls
    .map(([input]) => input)
    .find((input): input is Request => typeof input !== 'string' && new URL(input.url).pathname === path)
}

test('shows who is signed in, asking the App Gateway with their token', async () => {
  const fetch = answering({
    '/api/me': () => Response.json(ada),
    '/api/status': () => Response.json({ name: 'Sofa', environment: 'Development', time: '2026-11-27T12:00:00Z' }),
  })

  renderAt('/')

  expect(await screen.findByText('Ada Lovelace')).toBeDefined()
  expect(screen.getByText('ada@example.com')).toBeDefined()
  expect(screen.getByText("Ada's Workspace")).toBeDefined()
  expect(screen.getByText('Engines')).toBeDefined()
  expect(screen.getByText('Admin')).toBeDefined()
  expect(await screen.findByText('Up')).toBeDefined()
  expect(requestTo(fetch, '/api/me')!.headers.get('Authorization')).toBe('Bearer adas-token')
})

test('starts signing in when no one is signed in', async () => {
  session.signedInUser.mockResolvedValue(null)
  answering({})

  renderAt('/')

  await waitFor(() => expect(session.startSigningIn).toHaveBeenCalledOnce())
})

test('signs out from the button', async () => {
  answering({ '/api/me': () => Response.json(ada) })
  renderAt('/')

  fireEvent.click(await screen.findByRole('button', { name: 'Sign out' }))

  expect(session.signOut).toHaveBeenCalledOnce()
})

test('shows the App Gateway unreachable when its status fails', async () => {
  answering({ '/api/me': () => Response.json(ada), '/api/status': () => new Response(null, { status: 502 }) })

  renderAt('/')

  expect(await screen.findByText('Unreachable')).toBeDefined()
})

function fillIn(label: string, value: string) {
  fireEvent.change(screen.getByLabelText(label), { target: { value } })
}

test('signing in posts the email and password, then goes back to the authorization request', async () => {
  const fetch = answering({ '/account/sign-in': () => new Response(null, { status: 204 }) })
  renderAt('/sign-in', '?returnUrl=%2Fconnect%2Fauthorize%3Fclient_id%3Dsofa-web')

  fillIn('Email', 'ada@example.com')
  fillIn('Password', 'a long enough password')
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))

  await waitFor(() => expect(navigation.goTo).toHaveBeenCalledWith('/connect/authorize?client_id=sofa-web'))
  const [, init] = fetch.mock.calls.find(([path]) => path === '/account/sign-in') as unknown as [string, RequestInit]
  expect(JSON.parse(init.body as string)).toEqual({ email: 'ada@example.com', password: 'a long enough password' })
})

test('a refused sign-in says why and stays on the page', async () => {
  answering({
    '/account/sign-in': () => Response.json({ title: "That email and password don't match a User." }, { status: 401 }),
  })
  renderAt('/sign-in', '?returnUrl=%2Fconnect%2Fauthorize%3F')

  fillIn('Email', 'ada@example.com')
  fillIn('Password', 'wrong')
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))

  expect((await screen.findByRole('alert')).textContent).toBe("That email and password don't match a User.")
  expect(navigation.goTo).not.toHaveBeenCalled()
})

test('after signing in, a return address elsewhere is not followed', async () => {
  answering({ '/account/sign-in': () => new Response(null, { status: 204 }) })
  renderAt('/sign-in', '?returnUrl=https%3A%2F%2Fevil.example%2Fconnect%2Fauthorize%3F')

  fillIn('Email', 'ada@example.com')
  fillIn('Password', 'a long enough password')
  fireEvent.click(screen.getByRole('button', { name: 'Sign in' }))

  await waitFor(() => expect(navigation.goTo).toHaveBeenCalledWith('/'))
})

test('a User who is no Admin sees no admin pages', async () => {
  const bea = { ...ada, workspaces: [{ ...ada.workspaces[0], isAdmin: false }] }
  answering({ '/api/me': () => Response.json(bea) })

  renderAt('/')

  expect(await screen.findByRole('button', { name: 'Link your GitHub account' })).toBeDefined()
  expect(screen.queryByText('GitHub App')).toBeNull()
})

test('an Admin sees the admin pages of the Workspace they administer', async () => {
  const fetch = answering({ '/api/me': () => Response.json(ada) })

  renderAt('/')

  expect(await screen.findByText('GitHub App')).toBeDefined()
  await waitFor(() => expect(requestTo(fetch, `/api/workspaces/${ada.workspaces[0].id}/github-app`)).toBeDefined())
})

test('linking a GitHub account goes to GitHub where Sofa says', async () => {
  answering({
    '/api/me': () => Response.json(ada),
    '/api/me/github-link': () => Response.json({ authorizeUrl: 'https://github.com/login/oauth/authorize?state=s' }),
  })
  renderAt('/')

  fireEvent.click(await screen.findByRole('button', { name: 'Link your GitHub account' }))

  await waitFor(() => expect(navigation.goTo).toHaveBeenCalledWith('https://github.com/login/oauth/authorize?state=s'))
})

test('a linked GitHub account is shown instead of the link button', async () => {
  answering({ '/api/me': () => Response.json({ ...ada, gitHubLogin: 'ada-gh' }) })

  renderAt('/')

  expect(await screen.findByText('Signs in with GitHub as ada-gh.')).toBeDefined()
  expect(screen.queryByRole('button', { name: 'Link your GitHub account' })).toBeNull()
})

test('signing in with GitHub keeps the authorization request to come back to', async () => {
  answering({ '/account/github': () => Response.json({ available: true }) })
  renderAt('/sign-in', '?returnUrl=%2Fconnect%2Fauthorize%3Fclient_id%3Dsofa-web')

  fireEvent.click(await screen.findByRole('button', { name: 'Sign in with GitHub' }))

  expect(navigation.goTo).toHaveBeenCalledWith(
    '/account/github/sign-in?returnUrl=%2Fconnect%2Fauthorize%3Fclient_id%3Dsofa-web',
  )
})

test('no GitHub button until the GitHub App exists', async () => {
  const fetch = answering({ '/account/github': () => Response.json({ available: false }) })
  renderAt('/sign-in')

  await waitFor(() => expect(fetch).toHaveBeenCalled())
  expect(screen.queryByRole('button', { name: 'Sign in with GitHub' })).toBeNull()
})

test('a GitHub sign-in Sofa refused says why', async () => {
  answering({ '/account/github': () => Response.json({ available: true }) })

  renderAt('/sign-in', '?problem=No%20User%20has%20the%20GitHub%20account%20mallory.')

  expect((await screen.findByRole('alert')).textContent).toBe('No User has the GitHub account mallory.')
})

function fillInSetup(setupCode: string) {
  fillIn('Setup code', setupCode)
  fillIn('Your name', 'Ada Lovelace')
  fillIn('Email', 'ada@example.com')
  fillIn('Password', 'a long enough password')
  fillIn('Workspace', "Ada's Workspace")
  fillIn('Team', 'Engines')
}

test('setting up with the logged code creates the first User and signs them in', async () => {
  const fetch = answering({ '/account/setup': () => Response.json({}, { status: 201 }) })
  renderAt('/setup')

  fillInSetup('sofa_setup_logged')
  fireEvent.click(screen.getByRole('button', { name: 'Set up' }))

  await waitFor(() => expect(session.userManager.signinRedirect).toHaveBeenCalledOnce())
  const [, init] = fetch.mock.calls[0] as unknown as [string, RequestInit]
  expect(JSON.parse(init.body as string)).toMatchObject({ setupCode: 'sofa_setup_logged', teamName: 'Engines' })
})

test('a wrong setup code is refused with the reason', async () => {
  answering({
    '/account/setup': () => Response.json({ title: "That setup code is not right. Use the latest one in Identity's log." }, { status: 403 }),
  })
  renderAt('/setup')

  fillInSetup('sofa_setup_guessed')
  fireEvent.click(screen.getByRole('button', { name: 'Set up' }))

  expect((await screen.findByRole('alert')).textContent).toContain('setup code is not right')
  expect(session.userManager.signinRedirect).not.toHaveBeenCalled()
})

test('problems with fields are shown beside them', async () => {
  answering({
    '/account/setup': () =>
      Response.json({ title: 'One or more validation errors occurred.', errors: { password: ['Choose a password of at least 10 characters.'] } }, { status: 400 }),
  })
  renderAt('/setup')

  fillInSetup('sofa_setup_logged')
  fireEvent.click(screen.getByRole('button', { name: 'Set up' }))

  expect(await screen.findByText('Choose a password of at least 10 characters.')).toBeDefined()
  expect(screen.getByLabelText('Password').getAttribute('aria-invalid')).toBe('true')
})

test('coming back from Identity finishes signing in and goes home', async () => {
  session.finishSigningIn.mockResolvedValue(undefined)

  renderAt('/signed-in')

  await waitFor(() => expect(navigation.replaceWith).toHaveBeenCalledWith('/'))
})
