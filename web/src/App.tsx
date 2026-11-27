import { HomePage } from '@/pages/HomePage'
import { SetupPage } from '@/pages/SetupPage'
import { SignedInPage } from '@/pages/SignedInPage'
import { SignInPage } from '@/pages/SignInPage'
import { ProjectPage } from '@/projects/ProjectPage'
import { ProjectsPage } from '@/projects/ProjectsPage'
import { RunnersPage } from '@/runners/RunnersPage'
import { TeamPage } from '@/team/TeamPage'
import { setupPath, signedInPath } from '@/auth/session'

export const signInPath = '/sign-in'
const projectPathPrefix = '/projects/'

export default function App({ location = window.location }: { location?: Pick<Location, 'pathname' | 'search'> }) {
  switch (location.pathname) {
    case setupPath:
      return <SetupPage />
    case signInPath:
      return (
        <SignInPage
          returnUrl={new URLSearchParams(location.search).get('returnUrl')}
          problem={new URLSearchParams(location.search).get('problem')}
        />
      )
    case signedInPath:
      return <SignedInPage />
    case '/projects':
      return <ProjectsPage />
    case '/runners':
      return <RunnersPage />
    case '/team':
      return <TeamPage />
    default:
      return location.pathname.startsWith(projectPathPrefix) ? (
        <ProjectPage projectKey={decodeURIComponent(location.pathname.slice(projectPathPrefix.length))} />
      ) : (
        <HomePage />
      )
  }
}
