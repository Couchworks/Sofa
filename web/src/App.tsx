import { InboxPage } from '@/inbox/InboxPage'
import { HomePage } from '@/pages/HomePage'
import { SetupPage } from '@/pages/SetupPage'
import { SignedInPage } from '@/pages/SignedInPage'
import { SignInPage } from '@/pages/SignInPage'
import { ProjectPage } from '@/projects/ProjectPage'
import { ProjectsPage } from '@/projects/ProjectsPage'
import { RunnersPage } from '@/runners/RunnersPage'
import { TeamPage } from '@/team/TeamPage'
import { WorkItemPage } from '@/workItems/WorkItemPage'
import { setupPath, signedInPath } from '@/auth/session'

export const signInPath = '/sign-in'
const projectPathPrefix = '/projects/'
const workItemsSegment = '/work-items/'

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
    case '/inbox':
      return <InboxPage />
    case '/projects':
      return <ProjectsPage />
    case '/runners':
      return <RunnersPage />
    case '/team':
      return <TeamPage />
    default:
      return location.pathname.startsWith(projectPathPrefix) ? <ProjectRoute path={location.pathname} /> : <HomePage />
  }
}

function ProjectRoute({ path }: { path: string }) {
  const [projectKey, workItemKey] = path.slice(projectPathPrefix.length).split(workItemsSegment)
  return workItemKey ? (
    <WorkItemPage projectKey={decodeURIComponent(projectKey)} workItemKey={decodeURIComponent(workItemKey)} />
  ) : (
    <ProjectPage projectKey={decodeURIComponent(projectKey)} />
  )
}
