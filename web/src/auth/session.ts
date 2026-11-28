import { UserManager, WebStorageStateStore } from 'oidc-client-ts'
import { isSetupNeeded } from './account'
import { goTo } from '@/lib/navigation'

export const webClientId = 'sofa-web'
export const signedInPath = '/signed-in'
export const setupPath = '/setup'

export function createUserManager(origin: string) {
  return new UserManager({
    authority: origin,
    client_id: webClientId,
    redirect_uri: origin + signedInPath,
    post_logout_redirect_uri: origin + '/',
    response_type: 'code',
    scope: 'openid profile email offline_access',
    userStore: new WebStorageStateStore({ store: window.sessionStorage }),
    automaticSilentRenew: true,
    revokeTokensOnSignout: true,
    revokeTokenTypes: ['refresh_token'],
  })
}

export const userManager = createUserManager(window.location.origin)

export async function signedInUser() {
  const user = await userManager.getUser()
  return user && !user.expired ? user : null
}

export async function accessToken() {
  return (await signedInUser())?.access_token
}

export async function startSigningIn() {
  if (await isSetupNeeded()) {
    goTo(setupPath)
    return
  }
  await userManager.signinRedirect({ state: { returnTo: thePageBeingOpened() } })
}

export async function finishSigningIn() {
  const user = await userManager.signinRedirectCallback()
  return aPageOfThisSiteOrHome((user.state as { returnTo?: unknown } | undefined)?.returnTo)
}

function thePageBeingOpened() {
  const { pathname, search, hash } = window.location
  return pathname + search + hash
}

export function aPageOfThisSiteOrHome(returnTo: unknown) {
  const isAPageOfThisSite = typeof returnTo === 'string' && returnTo.startsWith('/') && !returnTo.startsWith('//') && !returnTo.startsWith('/\\')
  return isAPageOfThisSite && !returnTo.startsWith(signedInPath) ? returnTo : '/'
}

export async function signOut() {
  await userManager.signoutRedirect()
}
