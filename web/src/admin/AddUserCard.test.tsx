import { cleanup, fireEvent, screen } from '@testing-library/react'
import { afterEach, expect, test, vi } from 'vitest'
import { fakeGateway, renderWithQueries } from '@/test/fakeGateway'
import { AddUserCard } from './AddUserCard'

afterEach(() => {
  cleanup()
  vi.unstubAllGlobals()
})

const workspaceId = '0195e7a0-0000-7000-8000-000000000002'

test('adds someone to the Workspace by their GitHub login', async () => {
  const { requests } = fakeGateway({
    [`POST /api/workspaces/${workspaceId}/users`]: {
      status: 201,
      body: { userId: '0195e7a0-0000-7000-8000-000000000009', gitHubLogin: 'bea-gh' },
    },
  })
  renderWithQueries(<AddUserCard workspaceId={workspaceId} />)

  fireEvent.change(screen.getByLabelText('GitHub login'), { target: { value: ' bea-gh ' } })
  fireEvent.click(screen.getByRole('button', { name: 'Add' }))

  expect(await screen.findByText('bea-gh is added')).toBeDefined()
  expect(await requests[0].json()).toEqual({ gitHubLogin: 'bea-gh' })
})

test('says why someone could not be added', async () => {
  fakeGateway({
    [`POST /api/workspaces/${workspaceId}/users`]: {
      status: 409,
      body: { title: 'bea-gh already belongs to this Workspace.', status: 409 },
    },
  })
  renderWithQueries(<AddUserCard workspaceId={workspaceId} />)

  fireEvent.change(screen.getByLabelText('GitHub login'), { target: { value: 'bea-gh' } })
  fireEvent.click(screen.getByRole('button', { name: 'Add' }))

  expect(await screen.findByText('bea-gh already belongs to this Workspace.')).toBeDefined()
})
