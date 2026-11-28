import { afterEach, expect, test, vi } from 'vitest'
import { aPageOfThisSiteOrHome, finishSigningIn, startSigningIn, userManager } from './session'

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

test('signing in remembers the page being opened and comes back to it', async () => {
  vi.stubGlobal('fetch', vi.fn(async () => Response.json({ needed: false })))
  window.history.replaceState(null, '', '/projects/DEMO/work-items/DEMO-4?tab=runs')
  const signinRedirect = vi.spyOn(userManager, 'signinRedirect').mockResolvedValue()
  vi.spyOn(userManager, 'signinRedirectCallback').mockResolvedValue({ state: { returnTo: '/projects/DEMO/work-items/DEMO-4?tab=runs' } } as never)

  await startSigningIn()
  const page = await finishSigningIn()

  expect(signinRedirect).toHaveBeenCalledWith({ state: { returnTo: '/projects/DEMO/work-items/DEMO-4?tab=runs' } })
  expect(page).toBe('/projects/DEMO/work-items/DEMO-4?tab=runs')
  window.history.replaceState(null, '', '/')
})

test('signing in comes back only to a page of this site, and otherwise home', () => {
  expect(aPageOfThisSiteOrHome('/inbox')).toBe('/inbox')
  expect(aPageOfThisSiteOrHome('https://elsewhere.example/')).toBe('/')
  expect(aPageOfThisSiteOrHome('//elsewhere.example/')).toBe('/')
  expect(aPageOfThisSiteOrHome('/\\elsewhere.example/')).toBe('/')
  expect(aPageOfThisSiteOrHome('/signed-in?code=x')).toBe('/')
  expect(aPageOfThisSiteOrHome(undefined)).toBe('/')
})

test('the web client signs in as the public client sofa-web with PKCE and comes back to /signed-in', () => {
  const settings = userManager.settings

  expect(settings.client_id).toBe('sofa-web')
  expect(settings.response_type).toBe('code')
  expect(settings.redirect_uri).toBe(`${window.location.origin}/signed-in`)
  expect(settings.scope).toBe('openid profile email offline_access')
  expect(settings.disablePKCE).toBe(false)
})
