import { useQueryClient } from '@tanstack/react-query'
import { useState } from 'react'
import { api } from '@/api/client'
import type { components } from '@/api/schema'
import { Textarea } from '@/components/Textarea'
import { problemOf } from '@/components/workspace'
import { Button } from '@/components/ui/button'
import { Label } from '@/components/ui/label'
import { isAboutTheInbox, isAboutWorkItems } from '@/live/liveConnection'
import { Contribution } from './Contribution'

type WorkItem = components['schemas']['WorkItemDetails']
type Wait = components['schemas']['WaitDetails']

type Props = { workspaceId: string; projectKey: string; workItem: WorkItem; wait: Wait }

function useSettled() {
  const queries = useQueryClient()
  return () => void queries.invalidateQueries({ predicate: (query) => isAboutWorkItems(query) || isAboutTheInbox(query) })
}

export function WaitActions(props: Props) {
  switch (props.wait.kind) {
    case 'Question':
      return <AnswerTheQuestion {...props} />
    case 'Approval':
      return <ApproveOrSendBack {...props} />
    case 'RunGoneWrong':
      return <RetryTheRun {...props} />
    case 'PullRequestClosedUnmerged':
      return (
        <p className="text-sm">
          {props.wait.why} Reopen it on GitHub to wait for it again
          {props.workItem.pullRequest && (
            <>
              {': '}
              <a className="underline underline-offset-4" href={props.workItem.pullRequest.url}>
                {props.workItem.pullRequest.repository}#{String(props.workItem.pullRequest.number)}
              </a>
            </>
          )}
          .
        </p>
      )
  }
}

function AnswerTheQuestion({ workspaceId, projectKey, workItem, wait }: Props) {
  const [answer, setAnswer] = useState('')
  const settled = useSettled()
  const answering = api.useMutation('post', '/api/workspaces/{workspaceId}/projects/{key}/work-items/{workItemKey}/questions/{questionId}/answer', {
    onSuccess: settled,
  })
  const question = workItem.questions.find((asked) => asked.id === wait.waitId)
  const id = `answer-${wait.waitId}`

  return (
    <form
      aria-label={`Answer ${workItem.key}'s Question`}
      className="flex flex-col gap-2"
      onSubmit={(event) => {
        event.preventDefault()
        answering.mutate({
          params: { path: { workspaceId, key: projectKey, workItemKey: workItem.key, questionId: wait.waitId } },
          body: { answer: answer.trim() },
        })
      }}
    >
      <p className="text-sm font-medium whitespace-pre-wrap">{question?.text ?? wait.why}</p>
      <Label htmlFor={id}>Your answer</Label>
      <Textarea id={id} value={answer} onChange={(event) => setAnswer(event.target.value)} />
      <p className="text-muted-foreground text-xs">A new {question?.phase ?? ''} Run reads it. Answering here also settles the Question on the issue.</p>
      <div>
        <Button type="submit" disabled={answer.trim().length === 0 || answering.isPending}>
          Answer
        </Button>
      </div>
      {answering.isError && (
        <p role="alert" className="text-destructive text-sm">
          {problemOf(answering.error, 'The answer couldn’t be given.')}
        </p>
      )}
    </form>
  )
}

function ApproveOrSendBack({ workspaceId, projectKey, workItem, wait }: Props) {
  const [comment, setComment] = useState('')
  const settled = useSettled()
  const path = { workspaceId, key: projectKey, workItemKey: workItem.key, phase: workItem.phase! }
  const approving = api.useMutation('post', '/api/workspaces/{workspaceId}/projects/{key}/work-items/{workItemKey}/phases/{phase}/approval', {
    onSuccess: settled,
  })
  const sendingBack = api.useMutation('post', '/api/workspaces/{workspaceId}/projects/{key}/work-items/{workItemKey}/phases/{phase}/sending-back', {
    onSuccess: settled,
  })
  const failure = approving.error ?? sendingBack.error
  const deciding = approving.isPending || sendingBack.isPending
  const id = `send-back-${wait.waitId}`

  return (
    <div className="flex flex-col gap-4">
      <Contribution workspaceId={workspaceId} runId={wait.waitId} />
      <div className="flex flex-col gap-2 border-t pt-4">
        <div>
          <Button disabled={deciding} onClick={() => approving.mutate({ params: { path } })}>
            Approve the {workItem.phase}
          </Button>
        </div>
        <Label htmlFor={id}>Or send it back, saying what to change</Label>
        <Textarea id={id} value={comment} onChange={(event) => setComment(event.target.value)} />
        <div>
          <Button
            variant="outline"
            disabled={deciding || comment.trim().length === 0}
            onClick={() => sendingBack.mutate({ params: { path }, body: { comment: comment.trim() } })}
          >
            Send back
          </Button>
        </div>
        {failure && (
          <p role="alert" className="text-destructive text-sm">
            {problemOf(failure, 'The decision couldn’t be taken.')}
          </p>
        )}
      </div>
    </div>
  )
}

function RetryTheRun({ workspaceId, wait }: Props) {
  const settled = useSettled()
  const retrying = api.useMutation('post', '/api/workspaces/{workspaceId}/runs/{runId}/retry', { onSuccess: settled })

  return (
    <div className="flex flex-col gap-2">
      <p className="text-sm">{wait.why}</p>
      <div>
        <Button disabled={retrying.isPending} onClick={() => retrying.mutate({ params: { path: { workspaceId, runId: wait.waitId } } })}>
          Retry
        </Button>
      </div>
      {retrying.isError && (
        <p role="alert" className="text-destructive text-sm">
          {problemOf(retrying.error, 'The Run couldn’t be retried.')}
        </p>
      )}
    </div>
  )
}
