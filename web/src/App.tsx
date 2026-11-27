import { HomePage } from '@/pages/HomePage'
import { SetupPage } from '@/pages/SetupPage'
import { SignedInPage } from '@/pages/SignedInPage'
import { SignInPage } from '@/pages/SignInPage'
import { setupPath, signedInPath } from '@/auth/session'

export const signInPath = '/sign-in'

export default function App({ location = window.location }: { location?: Pick<Location, 'pathname' | 'search'> }) {
  switch (location.pathname) {
    case setupPath:
      return <SetupPage />
    case signInPath:
      return <SignInPage returnUrl={new URLSearchParams(location.search).get('returnUrl')} />
    case signedInPath:
      return <SignedInPage />
    default:
      return <HomePage />
  }
}
