import { api } from '@/api/client'
import type { components } from '@/api/schema'
import { problemOf } from '@/components/workspace'
import { Badge } from '@/components/ui/badge'

type Entry = components['schemas']['TranscriptEntry']

export const runningTranscriptRefreshesEvery = 3000

export function TranscriptView({ workspaceId, runId, running }: { workspaceId: string; runId: string; running: boolean }) {
  const transcript = api.useQuery(
    'get',
    '/api/workspaces/{workspaceId}/runs/{runId}/transcript',
    { params: { path: { workspaceId, runId } } },
    { refetchInterval: running ? runningTranscriptRefreshesEvery : false },
  )

  if (transcript.isPending) return <Badge variant="secondary">Loading the Transcript…</Badge>
  if (transcript.isError) return <p className="text-destructive text-sm">{problemOf(transcript.error, 'Sofa couldn’t read the Transcript.')}</p>
  if (transcript.data.entries.length === 0) {
    return <p className="text-muted-foreground text-sm">{running ? 'Nothing yet: it fills as the Run goes.' : 'The Run left no Transcript.'}</p>
  }

  return (
    <ol aria-label="Transcript" className="flex flex-col gap-2">
      {transcript.data.entries.map((entry) => (
        <li key={`${entry.line}-${entry.part}`} className="flex flex-col gap-1 border-l-2 pl-3 text-sm">
          <EntryView entry={entry} />
        </li>
      ))}
    </ol>
  )
}

function EntryView({ entry }: { entry: Entry }) {
  const said = <span className="text-muted-foreground text-xs">{entry.role}</span>
  if (entry.toolName) {
    return (
      <>
        {said}
        <details>
          <summary className="cursor-pointer font-mono text-xs">{entry.toolName}</summary>
          <pre className="bg-muted mt-1 overflow-x-auto rounded p-2 text-xs">
            {typeof entry.toolInput === 'string' ? entry.toolInput : JSON.stringify(entry.toolInput, null, 2)}
            {entry.toolInputCut && '\n… cut'}
          </pre>
          {entry.toolResult !== null && (
            <pre className="mt-1 overflow-x-auto rounded border p-2 text-xs whitespace-pre-wrap">
              {entry.toolResult}
              {entry.toolResultCut && '\n… cut'}
            </pre>
          )}
        </details>
      </>
    )
  }

  return (
    <>
      {said}
      <p className={entry.role === 'System' ? 'text-muted-foreground whitespace-pre-wrap' : 'whitespace-pre-wrap'}>
        {entry.text ?? entry.toolResult}
      </p>
    </>
  )
}
