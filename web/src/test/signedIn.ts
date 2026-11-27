import { vi } from 'vitest'

export const workspaceId = '0195e7a0-0000-7000-8000-000000000002'
export const teamId = '0195e7a0-0000-7000-8000-000000000003'

export function me(isAdmin: boolean) {
  return {
    id: '0195e7a0-0000-7000-8000-000000000001',
    name: 'Ada Lovelace',
    email: 'ada@example.com',
    gitHubLogin: null,
    workspaces: [{ id: workspaceId, name: "Ada's Workspace", isAdmin, teams: [{ id: teamId, name: 'Engines' }] }],
  }
}

export const signedInSession = {
  signedInUser: vi.fn(async () => ({ access_token: 'token' })),
  accessToken: vi.fn(async () => 'token'),
  startSigningIn: vi.fn(),
  signOut: vi.fn(),
}
