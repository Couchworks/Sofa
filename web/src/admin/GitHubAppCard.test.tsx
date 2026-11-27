import { cleanup, fireEvent, screen, waitFor } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import { fakeGateway, renderWithQueries } from '@/test/fakeGateway'
import { GitHubAppCard } from './GitHubAppCard'

const navigation = vi.hoisted(() => ({ goTo: vi.fn(), replaceWith: vi.fn() }))
vi.mock('@/lib/navigation', () => navigation)

const workspaceId = '0195e7a0-0000-7000-8000-000000000002'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
  document.body.innerHTML = ''
})

const notCreated = {
  created: false,
  name: null,
  htmlUrl: null,
  installUrl: null,
  publicAddress: 'https://sofa.example.dev/',
  gitHubCanReachPublicAddress: true,
}

test('sends the manifest Connections made to GitHub as a form', async () => {
  const submit = vi.spyOn(HTMLFormElement.prototype, 'submit').mockImplementation(() => {})
  const { requests } = fakeGateway({
    [`GET /api/workspaces/${workspaceId}/github-app`]: { body: notCreated },
    [`POST /api/workspaces/${workspaceId}/github-app/creations`]: {
      body: {
        formAction: 'https://github.com/organizations/acme/settings/apps/new?state=abc',
        manifest: '{"name":"Sofa acme"}',
      },
    },
  })
  renderWithQueries(<GitHubAppCard workspaceId={workspaceId} />)

  fireEvent.change(await screen.findByLabelText('Organisation'), { target: { value: ' acme ' } })
  fireEvent.click(screen.getByRole('button', { name: 'Create on GitHub' }))

  await waitFor(() => expect(submit).toHaveBeenCalled())
  const form = submit.mock.contexts[0] as HTMLFormElement
  expect(form.action).toBe('https://github.com/organizations/acme/settings/apps/new?state=abc')
  expect(form.method).toBe('post')
  expect((form.elements.namedItem('manifest') as HTMLInputElement).value).toBe('{"name":"Sofa acme"}')
  const creation = requests.find((request) => request.method === 'POST')!
  expect(await creation.json()).toEqual({ organisation: 'acme', name: null })
})

test('warns that webhooks stay off when GitHub cannot reach Sofa', async () => {
  fakeGateway({
    [`GET /api/workspaces/${workspaceId}/github-app`]: {
      body: { ...notCreated, publicAddress: 'http://localhost:5176/', gitHubCanReachPublicAddress: false },
    },
  })

  renderWithQueries(<GitHubAppCard workspaceId={workspaceId} />)

  expect(await screen.findByText('GitHub can’t reach http://localhost:5176/')).toBeDefined()
})

test('says why Connections refused to start the App', async () => {
  fakeGateway({
    [`GET /api/workspaces/${workspaceId}/github-app`]: { body: notCreated },
    [`POST /api/workspaces/${workspaceId}/github-app/creations`]: {
      status: 409,
      body: { title: 'Sofa’s GitHub App has already been created.', status: 409 },
    },
  })
  renderWithQueries(<GitHubAppCard workspaceId={workspaceId} />)

  fireEvent.click(await screen.findByRole('button', { name: 'Create on GitHub' }))

  expect(await screen.findByText('Sofa’s GitHub App has already been created.')).toBeDefined()
})

test('installs a created App through Sofa, which adds the state GitHub hands back', async () => {
  fakeGateway({
    [`GET /api/workspaces/${workspaceId}/github-app`]: {
      body: {
        ...notCreated,
        created: true,
        name: 'Sofa acme',
        htmlUrl: 'https://github.com/apps/sofa-acme',
        installUrl: 'https://github.com/apps/sofa-acme/installations/new',
      },
    },
    [`POST /api/workspaces/${workspaceId}/github-app/installations`]: {
      body: { installUrl: 'https://github.com/apps/sofa-acme/installations/new?state=signed' },
    },
  })
  renderWithQueries(<GitHubAppCard workspaceId={workspaceId} />)

  expect(await screen.findByText('Sofa acme')).toBeDefined()
  fireEvent.click(screen.getByRole('button', { name: 'Install on GitHub' }))

  await waitFor(() =>
    expect(navigation.goTo).toHaveBeenCalledWith('https://github.com/apps/sofa-acme/installations/new?state=signed'),
  )
})
