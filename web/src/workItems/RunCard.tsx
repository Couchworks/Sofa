import { useState } from 'react'
import { api } from '@/api/client'
import type { components } from '@/api/schema'
import { problemOf } from '@/components/workspace'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Contribution } from './Contribution'
import { Markdown } from './Markdown'
import { TranscriptView } from './TranscriptView'
import { momentOf, runStates } from './words'

type WorkItemRun = components['schemas']['WorkItemRunDetails']
type Run = components['schemas']['RunDetails']

export function RunCard({ workspaceId, run }: { workspaceId: string; run: WorkItemRun }) {
  const [open, setOpen] = useState(false)

  return (
    <details
      aria-label={`${run.phase} Run of ${momentOf(run.startedAt)}`}
      className="rounded-lg border px-3 py-2"
      onToggle={(event) => setOpen(event.currentTarget.open)}
    >
      <summary className="flex cursor-pointer flex-wrap items-center gap-2 text-sm">
        <span>Run of {momentOf(run.startedAt)}</span>
        {run.retryOf && <Badge variant="outline">a retry</Badge>}
        <Badge variant={run.endedAs ? 'secondary' : 'default'}>{run.endedAs ?? 'Under way'}</Badge>
      </summary>
      {open && <RunDetailsView workspaceId={workspaceId} runId={run.runId} />}
    </details>
  )
}

function RunDetailsView({ workspaceId, runId }: { workspaceId: string; runId: string }) {
  const run = api.useQuery('get', '/api/workspaces/{workspaceId}/runs/{runId}', { params: { path: { workspaceId, runId } } })

  if (run.isPending) return <Badge variant="secondary">Loading the Run…</Badge>
  if (run.isError) return <p className="text-destructive text-sm">{problemOf(run.error, 'Sofa couldn’t find the Run.')}</p>

  const running = run.data.state === 'Running' || run.data.state === 'Waiting'
  return (
    <div className="mt-3 flex flex-col gap-4">
      <RunFacts workspaceId={workspaceId} run={run.data} />
      <details>
        <summary className="cursor-pointer text-sm font-medium">Brief</summary>
        <div className="mt-2">
          <Markdown>{run.data.brief ?? '_No Brief kept._'}</Markdown>
        </div>
      </details>
      {run.data.state === 'Finished' && run.data.outcome && <Contribution workspaceId={workspaceId} runId={runId} />}
      {run.data.question && (
        <p className="text-sm">
          <span className="text-muted-foreground">Asked: </span>
          {run.data.question.text}
        </p>
      )}
      <details open={running}>
        <summary className="cursor-pointer text-sm font-medium">Transcript</summary>
        <div className="mt-2">
          <TranscriptView workspaceId={workspaceId} runId={runId} running={running} />
        </div>
      </details>
    </div>
  )
}

function RunFacts({ workspaceId, run }: { workspaceId: string; run: Run }) {
  return (
    <div className="flex flex-col gap-2 text-sm">
      <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1">
        <dt className="text-muted-foreground">State</dt>
        <dd>{runStates[run.state]}</dd>
        {run.why && (
          <>
            <dt className="text-muted-foreground">Why</dt>
            <dd>{run.why}</dd>
          </>
        )}
        <dt className="text-muted-foreground">Model</dt>
        <dd>{run.model}</dd>
        <dt className="text-muted-foreground">Attempt</dt>
        <dd>{String(run.attempt)}</dd>
      </dl>
      <RunActions workspaceId={workspaceId} run={run} />
    </div>
  )
}

export function RunActions({ workspaceId, run }: { workspaceId: string; run: Run }) {
  const retrying = api.useMutation('post', '/api/workspaces/{workspaceId}/runs/{runId}/retry')
  const stopping = api.useMutation('post', '/api/workspaces/{workspaceId}/runs/{runId}/stop')
  const mayBeRetried = (run.state === 'Failed' || run.state === 'Lost' || run.state === 'Refused') && !run.retriedBy
  const mayBeStopped = run.state === 'Waiting' || run.state === 'Running'
  const failure = retrying.error ?? stopping.error

  if (!mayBeRetried && !mayBeStopped) return null
  return (
    <div className="flex flex-wrap items-center gap-2">
      {mayBeRetried && (
        <Button
          variant="outline"
          disabled={retrying.isPending}
          onClick={() => retrying.mutate({ params: { path: { workspaceId, runId: run.id } } })}
        >
          Retry
        </Button>
      )}
      {mayBeStopped && (
        <Button
          variant="outline"
          disabled={stopping.isPending}
          onClick={() => stopping.mutate({ params: { path: { workspaceId, runId: run.id } } })}
        >
          Stop
        </Button>
      )}
      {failure && (
        <p role="alert" className="text-destructive text-sm">
          {problemOf(failure, 'Sofa couldn’t do that.')}
        </p>
      )}
    </div>
  )
}
