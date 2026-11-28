import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { api } from '@/api/client'
import type { components } from '@/api/schema'
import { NativeSelect } from '@/components/NativeSelect'
import { Page } from '@/components/SignedIn'
import { problemOf, useWorkspace } from '@/components/workspace'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { Label } from '@/components/ui/label'
import { isAboutWorkItems } from '@/live/liveConnection'
import { RunCard } from './RunCard'
import { WaitActions } from './WaitActions'
import { gates, isWaitingOnAUser, momentOf, standings, workItemStates } from './words'

type WorkItem = components['schemas']['WorkItemDetails']
type Phase = components['schemas']['PhaseDetails']
type PhaseEnd = components['schemas']['PhaseEndDetails']
type Track = NonNullable<components['schemas']['Track']>

const tracks: Record<Track, string> = {
  Full: 'Full: every Phase',
  Small: 'Small: no Spec, its plan reviewed afterwards',
}

export function WorkItemPage({ projectKey, workItemKey }: { projectKey: string; workItemKey: string }) {
  const { workspace } = useWorkspace()
  return (
    <Page title={workItemKey}>
      {workspace && <WorkItemView workspaceId={workspace.id} projectKey={projectKey} workItemKey={workItemKey} />}
    </Page>
  )
}

function useUserNames(workspaceId: string) {
  const names = api.useQuery('get', '/api/workspaces/{workspaceId}/user-names', { params: { path: { workspaceId } } })
  return (userId: string | null | undefined) =>
    (userId && names.data?.find((user) => user.userId === userId)?.name) ?? 'a User'
}

function WorkItemView({ workspaceId, projectKey, workItemKey }: { workspaceId: string; projectKey: string; workItemKey: string }) {
  const path = { workspaceId, key: projectKey, workItemKey }
  const workItem = api.useQuery('get', '/api/workspaces/{workspaceId}/projects/{key}/work-items/{workItemKey}', { params: { path } })
  const project = api.useQuery('get', '/api/workspaces/{workspaceId}/projects/{key}', { params: { path: { workspaceId, key: projectKey } } })
  const nameOf = useUserNames(workspaceId)

  if (workItem.isPending) return <Badge variant="secondary">Loading…</Badge>
  if (workItem.isError) {
    return (
      <p role="alert" className="text-destructive text-sm">
        {problemOf(workItem.error, 'Sofa could not find that Work Item.')}
      </p>
    )
  }

  const item = workItem.data
  return (
    <>
      <div className="flex flex-col gap-2">
        <p className="text-lg">{item.title}</p>
        <div className="text-muted-foreground flex flex-wrap items-center gap-2 text-sm">
          <Badge variant={item.state === 'Done' ? 'secondary' : isWaitingOnAUser(item.state) ? 'destructive' : 'default'}>
            {workItemStates[item.state]}
          </Badge>
          {item.phase && <span>in {item.phase}</span>}
          <a className="underline-offset-4 hover:underline" href={item.request.url}>
            {item.request.repository}#{String(item.request.number)}
          </a>
          <span>{item.delegatingUserId ? `delegated by ${nameOf(item.delegatingUserId)}` : 'no one delegated it yet'}</span>
          <a className="underline-offset-4 hover:underline" href={`/projects/${encodeURIComponent(projectKey)}`}>
            Project {projectKey}
          </a>
        </div>
      </div>
      {item.waiting && (
        <Card aria-label="What it waits for">
          <CardHeader>
            <CardTitle>Waiting for a User</CardTitle>
            <CardDescription>Since {momentOf(item.waiting.since)}</CardDescription>
          </CardHeader>
          <CardContent>
            <WaitActions workspaceId={workspaceId} projectKey={projectKey} workItem={item} wait={item.waiting} />
          </CardContent>
        </Card>
      )}
      <TrackCard key={item.track ?? 'not picked'} workspaceId={workspaceId} projectKey={projectKey} workItem={item} />
      <Card>
        <CardHeader>
          <CardTitle>Phases</CardTitle>
          <CardDescription>Each Phase it went through, with how it ended and every Run in it.</CardDescription>
        </CardHeader>
        <CardContent className="flex flex-col gap-4">
          {project.data?.phases.map((phase) => (
            <PhaseSection
              key={phase.name}
              workspaceId={workspaceId}
              projectKey={projectKey}
              workItem={item}
              phase={phase}
              nameOf={nameOf}
            />
          ))}
        </CardContent>
      </Card>
      <QuestionsCard workItem={item} nameOf={nameOf} />
      <PullRequestCard workItem={item} />
    </>
  )
}

function TrackCard({ workspaceId, projectKey, workItem }: { workspaceId: string; projectKey: string; workItem: WorkItem }) {
  const [track, setTrack] = useState<Track | ''>(workItem.track ?? '')
  const queries = useQueryClient()
  const changing = api.useMutation('put', '/api/workspaces/{workspaceId}/projects/{key}/work-items/{workItemKey}/track', {
    onSuccess: () => void queries.invalidateQueries({ predicate: isAboutWorkItems }),
  })
  const done = workItem.state === 'Done'

  return (
    <Card size="sm">
      <form
        aria-label="Track"
        onSubmit={(event) => {
          event.preventDefault()
          if (track) {
            changing.mutate({ params: { path: { workspaceId, key: projectKey, workItemKey: workItem.key } }, body: { track } })
          }
        }}
      >
        <CardHeader>
          <CardTitle>Track</CardTitle>
          <CardDescription>
            {workItem.track
              ? `${workItem.track}, ${workItem.trackChosenByAUser ? 'chosen by a User' : 'picked by Triage'}.`
              : 'Triage picks it, unless you choose one first.'}{' '}
            A change applies to the Phases still ahead.
          </CardDescription>
        </CardHeader>
        <CardContent className="mt-3 flex flex-wrap items-end gap-2">
          <div className="flex flex-col gap-1">
            <Label htmlFor="track">Track</Label>
            <NativeSelect id="track" value={track} disabled={done} onChange={(event) => setTrack(event.target.value as Track)}>
              {!workItem.track && <option value="">Not picked yet</option>}
              {Object.entries(tracks).map(([value, label]) => (
                <option key={value} value={value}>
                  {label}
                </option>
              ))}
            </NativeSelect>
          </div>
          <Button type="submit" variant="outline" disabled={done || !track || track === workItem.track || changing.isPending}>
            Change the Track
          </Button>
          {changing.isError && (
            <p role="alert" className="text-destructive w-full text-sm">
              {problemOf(changing.error, 'The Track couldn’t be changed.')}
            </p>
          )}
        </CardContent>
      </form>
    </Card>
  )
}

function PhaseSection({
  workspaceId,
  projectKey,
  workItem,
  phase,
  nameOf,
}: {
  workspaceId: string
  projectKey: string
  workItem: WorkItem
  phase: Phase
  nameOf: (userId: string | null | undefined) => string
}) {
  const runs = workItem.runs.filter((run) => run.phase === phase.name)
  const ends = workItem.phaseEnds.filter((end) => end.phase === phase.name)
  const skipped = workItem.track === 'Small' && phase.name === 'Spec' && runs.length === 0
  const current = workItem.phase === phase.name && workItem.state !== 'Done'

  return (
    <section aria-label={`${phase.name} Phase`} className="flex flex-col gap-2">
      <h2 className="flex flex-wrap items-center gap-2 font-medium">
        {phase.name}
        {current && <Badge>now</Badge>}
        {skipped && <Badge variant="outline">skipped on the Small Track</Badge>}
        <span className="text-muted-foreground text-xs font-normal">Gate: {gates[phase.gate]}</span>
      </h2>
      {ends.map((end) => (
        <PhaseEndLine key={end.runId} workspaceId={workspaceId} projectKey={projectKey} workItem={workItem} end={end} nameOf={nameOf} />
      ))}
      {runs.length === 0 && !skipped && <p className="text-muted-foreground text-sm">No Run yet.</p>}
      {runs.map((run) => (
        <RunCard key={run.runId} workspaceId={workspaceId} run={run} />
      ))}
    </section>
  )
}

function PhaseEndLine({
  workspaceId,
  projectKey,
  workItem,
  end,
  nameOf,
}: {
  workspaceId: string
  projectKey: string
  workItem: WorkItem
  end: PhaseEnd
  nameOf: (userId: string | null | undefined) => string
}) {
  const queries = useQueryClient()
  const lookingAt = api.useMutation('post', '/api/workspaces/{workspaceId}/projects/{key}/work-items/{workItemKey}/phases/{phase}/approval', {
    onSuccess: () => void queries.invalidateQueries({ predicate: isAboutWorkItems }),
  })
  const isTheLatestOfItsPhase = workItem.phaseEnds.filter((e) => e.phase === end.phase).at(-1)?.runId === end.runId

  return (
    <div className="flex flex-col gap-1 text-sm">
      <p>
        Ended <span className="font-mono">{end.outcome}</span> on {momentOf(end.endedAt)}:{' '}
        <Badge variant={end.standing === 'Unverified' ? 'destructive' : 'outline'}>{standings[end.standing]}</Badge>
        {end.decidedAt && (
          <span className="text-muted-foreground">
            {' '}
            by {nameOf(end.decidedByUserId)} on {momentOf(end.decidedAt)}
          </span>
        )}
      </p>
      {end.comment && <p className="text-muted-foreground border-l-2 pl-3 whitespace-pre-wrap">{end.comment}</p>}
      {end.standing === 'Unverified' && isTheLatestOfItsPhase && (
        <div>
          <Button
            variant="outline"
            disabled={lookingAt.isPending}
            onClick={() => lookingAt.mutate({ params: { path: { workspaceId, key: projectKey, workItemKey: workItem.key, phase: end.phase } } })}
          >
            Mark the {end.phase} as looked at
          </Button>
        </div>
      )}
    </div>
  )
}

function QuestionsCard({ workItem, nameOf }: { workItem: WorkItem; nameOf: (userId: string | null | undefined) => string }) {
  if (workItem.questions.length === 0) return null
  return (
    <Card>
      <CardHeader>
        <CardTitle>Questions</CardTitle>
        <CardDescription>What its Runs asked, and the answers they got.</CardDescription>
      </CardHeader>
      <CardContent>
        <ul aria-label="Questions" className="flex flex-col gap-3">
          {workItem.questions.map((question) => (
            <li key={question.id} className="flex flex-col gap-1 text-sm">
              <span className="text-muted-foreground text-xs">
                {question.phase} asked on {momentOf(question.askedAt)}
              </span>
              <p className="whitespace-pre-wrap">{question.text}</p>
              {question.answer ? (
                <p className="border-l-2 pl-3 whitespace-pre-wrap">
                  {question.answer}
                  <span className="text-muted-foreground block text-xs">
                    answered by {nameOf(question.answeredByUserId)}
                    {question.answeredAt && ` on ${momentOf(question.answeredAt)}`}
                  </span>
                </p>
              ) : (
                <Badge variant="destructive">Not answered yet</Badge>
              )}
            </li>
          ))}
        </ul>
      </CardContent>
    </Card>
  )
}

function PullRequestCard({ workItem }: { workItem: WorkItem }) {
  const pull = workItem.pullRequest
  return (
    <Card size="sm">
      <CardHeader>
        <CardTitle>Pull request</CardTitle>
        <CardDescription>
          {pull ? (
            <>
              <a className="underline underline-offset-4" href={pull.url}>
                {pull.repository}#{String(pull.number)}
              </a>{' '}
              is {pull.state === 'ClosedUnmerged' ? 'closed without being merged' : pull.state.toLowerCase()}
              {workItem.doneAt && `; the Work Item was done on ${momentOf(workItem.doneAt)}`}.
            </>
          ) : (
            'None yet: Implement opens one.'
          )}
        </CardDescription>
      </CardHeader>
    </Card>
  )
}
