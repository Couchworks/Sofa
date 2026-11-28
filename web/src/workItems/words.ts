import type { components } from '@/api/schema'

type WorkItemState = components['schemas']['WorkItemState']
type PhaseEndStanding = components['schemas']['PhaseEndStanding']
type Gate = components['schemas']['Gate']
type RunState = components['schemas']['RunState']

export const workItemStates: Record<WorkItemState, string> = {
  AwaitsDelegation: 'Waiting for a delegating User',
  Running: 'Running',
  AwaitsAnswer: 'Waiting for an answer',
  AwaitsApproval: 'Waiting for approval',
  AwaitsPullRequest: 'Waiting for its pull request',
  AwaitsUser: 'Waiting for a User',
  Done: 'Done',
}

export const standings: Record<PhaseEndStanding, string> = {
  WentOn: 'went on',
  AwaitsApproval: 'waiting for approval',
  Approved: 'approved',
  SentBack: 'sent back',
  Unverified: 'Unverified',
  Verified: 'looked at',
}

export const gates: Record<Gate, string> = {
  Auto: 'Auto',
  Approve: 'Approve',
  ReviewAfterwards: 'Review afterwards',
}

export const runStates: Record<RunState, string> = {
  Waiting: 'Waiting for a Runner',
  Running: 'Running',
  Finished: 'Finished',
  Failed: 'Failed',
  Lost: 'Lost',
  Stopped: 'Stopped',
  Refused: 'Refused',
}

export function workItemPath(projectKey: string, workItemKey: string) {
  return `/projects/${encodeURIComponent(projectKey)}/work-items/${encodeURIComponent(workItemKey)}`
}

export function isWaitingOnAUser(state: WorkItemState) {
  return state === 'AwaitsAnswer' || state === 'AwaitsApproval' || state === 'AwaitsUser'
}

const moments = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' })

export function momentOf(at: string) {
  return moments.format(new Date(at))
}
