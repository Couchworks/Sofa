import { afterEach, expect, test, vi } from 'vitest'
import { startSigningIn, userManager } from './session'

const navigation = vi.hoisted(() => ({ goTo: vi.fn(), replaceWith: vi.fn() }))
vi.mock('@/lib/navigation', () => navigation)

afterEach(() => {
  vi.unstubAllGlobals()
  vi.restoreAllMocks()
})

test('a fresh installation is set up before anyone signs in', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => Response.json({ needed: true })))
  const signinRedirect = vi.spyOn(userManager, 'signinRedirect').mockResolvedValue()

  await startSigningIn()

  expect(navigation.goTo).toHaveBeenCalledWith('/setup')
  expect(signinRedirect).not.toHaveBeenCalled()
})

test('once set up, signing in goes to Identity by authorization code', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => Response.json({ needed: false })))
  const signinRedirect = vi.spyOn(userManager, 'signinRedirect').mockResolvedValue()

  await startSigningIn()

  expect(signinRedirect).toHaveBeenCalledOnce()
})

test('the web client signs in as the public client sofa-web with PKCE and comes back to /signed-in', () => {
  const settings = userManager.settings

  expect(settings.client_id).toBe('sofa-web')
  expect(settings.response_type).toBe('code')
  expect(settings.redirect_uri).toBe(`${window.location.origin}/signed-in`)
  expect(settings.scope).toBe('openid profile email offline_access')
  expect(settings.disablePKCE).toBe(false)
})
