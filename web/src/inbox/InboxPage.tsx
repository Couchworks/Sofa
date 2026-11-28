import { api } from '@/api/client'
import type { components } from '@/api/schema'
import { Page } from '@/components/SignedIn'
import { problemOf, useWorkspace } from '@/components/workspace'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { WaitActions } from '@/workItems/WaitActions'
import { useInbox } from './useInbox'
import { momentOf, workItemPath } from '@/workItems/words'

type InboxItem = components['schemas']['InboxItem']

const kinds: Record<string, string> = {
  Question: 'Question',
  Approval: 'Gate to approve',
  RunGoneWrong: 'Run gone wrong',
  PullRequestClosedUnmerged: 'Pull request closed unmerged',
  RequestAwaitingDelegation: 'Waiting for a delegating User',
  SubscriptionSignInEnding: 'Sign-in ending',
  SubscriptionSignedOut: 'Signed out',
}

export function InboxPage() {
  const { workspace } = useWorkspace()
  return <Page title="Inbox">{workspace && <InboxList workspaceId={workspace.id} />}</Page>
}

function InboxList({ workspaceId }: { workspaceId: string }) {
  const inbox = useInbox(workspaceId)

  if (inbox.isPending) return <Badge variant="secondary">Loading…</Badge>
  if (inbox.isError) {
    return (
      <p role="alert" className="text-destructive text-sm">
        {problemOf(inbox.error, 'Sofa could not read your Inbox.')}
      </p>
    )
  }
  if (inbox.data.length === 0) {
    return <p className="text-muted-foreground text-sm">Nothing waits for you. Sofa puts Questions, Gates and anything gone wrong here.</p>
  }

  return (
    <ul aria-label="Inbox" className="flex flex-col gap-4">
      {inbox.data.map((item) => (
        <li key={item.id}>
          <InboxCard workspaceId={workspaceId} item={item} />
        </li>
      ))}
    </ul>
  )
}

function InboxCard({ workspaceId, item }: { workspaceId: string; item: InboxItem }) {
  return (
    <Card aria-label={item.title}>
      <CardHeader>
        <CardTitle className="flex flex-wrap items-center gap-2">
          <Badge variant="outline">{kinds[item.kind] ?? item.kind}</Badge>
          {item.workItemKey ? (
            <a className="underline-offset-4 hover:underline" href={workItemPath(item.projectKey, item.workItemKey)}>
              {item.title}
            </a>
          ) : (
            item.title
          )}
        </CardTitle>
        <CardDescription>Since {momentOf(item.raisedAt)}</CardDescription>
      </CardHeader>
      <CardContent>
        {item.workItemKey ? (
          <WorkItemWait workspaceId={workspaceId} item={item} workItemKey={item.workItemKey} />
        ) : (
          <OutsideAWorkItem item={item} />
        )}
      </CardContent>
    </Card>
  )
}

function WorkItemWait({ workspaceId, item, workItemKey }: { workspaceId: string; item: InboxItem; workItemKey: string }) {
  const workItem = api.useQuery('get', '/api/workspaces/{workspaceId}/projects/{key}/work-items/{workItemKey}', {
    params: { path: { workspaceId, key: item.projectKey, workItemKey } },
  })

  if (workItem.isPending) return <Badge variant="secondary">Loading {workItemKey}…</Badge>
  if (workItem.isError) return <p className="text-sm">{item.detail}</p>
  if (workItem.data.waiting?.waitId !== item.subjectId) {
    return <p className="text-muted-foreground text-sm">{item.detail} It no longer waits for this.</p>
  }

  return <WaitActions workspaceId={workspaceId} projectKey={item.projectKey} workItem={workItem.data} wait={workItem.data.waiting} />
}

function OutsideAWorkItem({ item }: { item: InboxItem }) {
  const linkText =
    item.kind === 'RequestAwaitingDelegation' ? 'The issue on GitHub' : item.url === '/runners' ? 'Your Runners' : 'Open it'
  return (
    <div className="flex flex-col gap-2 text-sm">
      <p className="whitespace-pre-wrap">{item.detail}</p>
      <a className="underline underline-offset-4" href={item.url}>
        {linkText}
      </a>
    </div>
  )
}
