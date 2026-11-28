import { workspaceId } from './signedIn'

export const api = `/api/workspaces/${workspaceId}`
export const project = `${api}/projects/DEMO`
export const workItemApi = `${project}/work-items/DEMO-1`
export const ada = '0195e7a0-0000-7000-8000-000000000001'

export const demoProject = {
  id: 'p1',
  key: 'DEMO',
  name: 'The demo',
  requests: { connectionId: 'c1', repository: 'acme/demo', label: 'sofa', keepsIssueState: true },
  code: { connectionId: 'c1', repositories: ['acme/demo'] },
  phases: [
    { name: 'Triage', instructions: '', position: 0, pattern: 'Solo', castAgentId: 'a1', gate: 'Auto', sandbox: 'ReadOnly' },
    { name: 'Spec', instructions: '', position: 1, pattern: 'Solo', castAgentId: 'a2', gate: 'Approve', sandbox: 'ReadOnly' },
    { name: 'Plan', instructions: '', position: 2, pattern: 'Solo', castAgentId: 'a2', gate: 'Approve', sandbox: 'ReadOnly' },
    { name: 'Implement', instructions: '', position: 3, pattern: 'Solo', castAgentId: 'a3', gate: 'Auto', sandbox: 'OwnBranch' },
  ],
  agents: [],
}

export const triageRun = { runId: 'run-triage', phase: 'Triage', startedAt: '2026-11-28T09:00:00Z', retryOf: null, endedAs: 'Finished: full', endedAt: '2026-11-28T09:01:00Z' }
export const askingRun = { runId: 'run-spec-1', phase: 'Spec', startedAt: '2026-11-28T09:02:00Z', retryOf: null, endedAs: 'Finished, asking a Question', endedAt: '2026-11-28T09:03:00Z' }
export const specRun = { runId: 'run-spec-2', phase: 'Spec', startedAt: '2026-11-28T09:10:00Z', retryOf: null, endedAs: 'Finished: specified', endedAt: '2026-11-28T09:12:00Z' }

export const question = {
  id: 'q1',
  runId: 'run-spec-1',
  phase: 'Spec',
  text: 'Should the toggle follow the system setting by default?',
  askedAt: '2026-11-28T09:03:00Z',
  answer: null,
  answeredByUserId: null,
  answeredAt: null,
}

export const asking = {
  id: 'w1',
  key: 'DEMO-1',
  title: 'Add a dark mode toggle',
  projectId: 'p1',
  requestId: 'r1',
  request: { repository: 'acme/demo', number: 12, url: 'https://github.com/acme/demo/issues/12' },
  delegatingUserId: ada,
  openedAt: '2026-11-28T09:00:00Z',
  questions: [question],
  state: 'AwaitsAnswer',
  phase: 'Spec',
  track: 'Full',
  trackChosenByAUser: false,
  waiting: { waitId: 'q1', kind: 'Question', why: `Its Spec Run asked: ${question.text}`, since: '2026-11-28T09:03:00Z' },
  runs: [triageRun, askingRun],
  phaseEnds: [
    { runId: 'run-triage', phase: 'Triage', outcome: 'full', gate: 'Auto', standing: 'WentOn', endedAt: '2026-11-28T09:01:00Z', decidedByUserId: null, comment: null, decidedAt: null },
  ],
  pullRequest: null,
  doneAt: null,
}

export const awaitingTheSpec = {
  ...asking,
  questions: [{ ...question, answer: 'Yes, follow it.', answeredByUserId: ada, answeredAt: '2026-11-28T09:05:00Z' }],
  state: 'AwaitsApproval',
  waiting: { waitId: 'run-spec-2', kind: 'Approval', why: 'Its Spec waits for a User’s approval.', since: '2026-11-28T09:12:00Z' },
  runs: [triageRun, askingRun, specRun],
  phaseEnds: [
    ...asking.phaseEnds,
    { runId: 'run-spec-2', phase: 'Spec', outcome: 'specified', gate: 'Approve', standing: 'AwaitsApproval', endedAt: '2026-11-28T09:12:00Z', decidedByUserId: null, comment: null, decidedAt: null },
  ],
}

export const theSpec = {
  runId: 'run-spec-2',
  workItemId: 'w1',
  phase: 'Spec',
  agentId: 'a2',
  outcome: 'specified',
  markdown: '# The spec\n\nA toggle in the header.\n\n- [ ] It follows the system setting',
  attachments: [{ id: 'att1', name: 'mock-up.png', contentType: 'image/png', size: 1024 }],
  madeAt: '2026-11-28T09:12:00Z',
}

export function runDetails(runId: string, overrides: Record<string, unknown> = {}) {
  return {
    id: runId,
    workspaceId: 'ws',
    projectId: 'p1',
    workItemId: 'w1',
    phase: 'Spec',
    agentId: 'a2',
    model: 'opus',
    sandbox: 'ReadOnly',
    delegatingUserId: ada,
    priority: 'None',
    state: 'Finished',
    why: null,
    refusedAs: null,
    runnerId: 'r1',
    attempt: 1,
    retriesLeft: 2,
    retryOf: null,
    retriedBy: null,
    waitsForRetry: false,
    outcome: 'specified',
    question: null,
    queuedAt: '2026-11-28T09:10:00Z',
    startedAt: '2026-11-28T09:10:00Z',
    endedAt: '2026-11-28T09:12:00Z',
    brief: 'You are the **Lead**. Write the spec of DEMO-1.',
    ...overrides,
  }
}

export const userNames = [{ userId: ada, name: 'Ada Lovelace' }]
