import { useState } from 'react'
import { api } from '@/api/client'
import type { components } from '@/api/schema'
import { NativeSelect } from '@/components/NativeSelect'
import { Page } from '@/components/SignedIn'
import { problemOf, useWorkspace } from '@/components/workspace'
import { Textarea } from '@/components/Textarea'
import { Alert, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { Binding } from './binding'
import { BindingFields } from './BindingFields'

type Project = components['schemas']['ProjectDetails']
type Phase = components['schemas']['PhaseDetails']
type Agent = components['schemas']['AgentDetails']
type Gate = components['schemas']['Gate']
type Sandbox = components['schemas']['Sandbox']
type AgentRole = components['schemas']['AgentRole']

const gates: Record<Gate, string> = {
  Auto: 'Auto',
  Approve: 'Approve',
  ReviewAfterwards: 'Review afterwards',
}

const sandboxes: Record<Sandbox, string> = {
  ReadOnly: 'Read-only',
  OwnBranch: 'Pushes to its own branch',
  IntegrationBranch: 'Pushes to the Integration branch',
}

const roles: AgentRole[] = ['Triager', 'Lead', 'Builder']
const models = ['opus', 'sonnet', 'haiku']

export function ProjectPage({ projectKey, workItemKey }: { projectKey: string; workItemKey?: string }) {
  const { workspace } = useWorkspace()
  return (
    <Page title={`Project ${projectKey}`}>
      {workspace && (
        <ProjectSettings workspaceId={workspace.id} isAdmin={workspace.isAdmin} projectKey={projectKey} workItemKey={workItemKey} />
      )}
    </Page>
  )
}

function ProjectSettings({
  workspaceId,
  isAdmin,
  projectKey,
  workItemKey,
}: {
  workspaceId: string
  isAdmin: boolean
  projectKey: string
  workItemKey?: string
}) {
  const project = api.useQuery('get', '/api/workspaces/{workspaceId}/projects/{key}', {
    params: { path: { workspaceId, key: projectKey } },
  })

  if (project.isPending) return <Badge variant="secondary">Loading…</Badge>
  if (project.isError) return <p className="text-destructive text-sm">{problemOf(project.error, 'Sofa could not find that Project.')}</p>

  return (
    <>
      <p className="text-lg">{project.data.name}</p>
      <WorkItemsCard workspaceId={workspaceId} projectKey={project.data.key} highlighted={workItemKey} />
      <BindingCard workspaceId={workspaceId} project={project.data} isAdmin={isAdmin} onChanged={() => void project.refetch()} />
      <FlowCard workspaceId={workspaceId} project={project.data} onChanged={() => void project.refetch()} />
      <section className="flex flex-col gap-4">
        <h2 className="font-heading text-base font-medium">Agents</h2>
        {project.data.agents.map((agent) => (
          <AgentCard key={agent.id} workspaceId={workspaceId} projectKey={project.data.key} agent={agent} onChanged={() => void project.refetch()} />
        ))}
      </section>
    </>
  )
}

function BindingCard({
  workspaceId,
  project,
  isAdmin,
  onChanged,
}: {
  workspaceId: string
  project: Project
  isAdmin: boolean
  onChanged: () => void
}) {
  const [editing, setEditing] = useState<Binding | null>(null)
  const binding = api.useMutation('put', '/api/workspaces/{workspaceId}/projects/{key}/binding', {
    onSuccess: () => {
      setEditing(null)
      onChanged()
    },
  })

  return (
    <Card>
      <CardHeader>
        <CardTitle>Providers</CardTitle>
        <CardDescription>Where its Requests come from and where its code lives.</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {editing ? (
          <BindingFields workspaceId={workspaceId} binding={editing} onChange={setEditing} />
        ) : (
          <dl className="grid grid-cols-[auto_1fr] gap-x-4 gap-y-1 text-sm">
            <dt className="text-muted-foreground">Requests</dt>
            <dd>
              issues in {project.requests.repository} labelled <Badge variant="outline">{project.requests.label}</Badge>
            </dd>
            <dt className="text-muted-foreground">Code</dt>
            <dd>{project.code.repositories.join(', ')}</dd>
            <dt className="text-muted-foreground">Issue state</dt>
            <dd>{project.requests.keepsIssueState ? 'kept: labelled with its Phase, closed once merged' : 'left alone'}</dd>
          </dl>
        )}
        {binding.isError && (
          <Alert variant="destructive">
            <AlertTitle>{problemOf(binding.error, 'The binding couldn’t be changed')}</AlertTitle>
          </Alert>
        )}
      </CardContent>
      {isAdmin && (
        <CardFooter className="mt-4 gap-2">
          {editing ? (
            <>
              <Button
                disabled={binding.isPending}
                onClick={() => binding.mutate({ params: { path: { workspaceId, key: project.key } }, body: editing })}
              >
                Save binding
              </Button>
              <Button variant="outline" onClick={() => setEditing(null)}>
                Cancel
              </Button>
            </>
          ) : (
            <Button
              variant="outline"
              onClick={() =>
                setEditing({
                  connectionId: project.requests.connectionId,
                  requestRepository: project.requests.repository,
                  label: project.requests.label,
                  codeRepositories: project.code.repositories,
                  keepsIssueState: project.requests.keepsIssueState,
                })
              }
            >
              Change binding
            </Button>
          )}
        </CardFooter>
      )}
    </Card>
  )
}

function WorkItemsCard({ workspaceId, projectKey, highlighted }: { workspaceId: string; projectKey: string; highlighted?: string }) {
  const workItems = api.useQuery('get', '/api/workspaces/{workspaceId}/projects/{key}/work-items', {
    params: { path: { workspaceId, key: projectKey } },
  })

  return (
    <Card>
      <CardHeader>
        <CardTitle>Work Items</CardTitle>
        <CardDescription>Each Request labelled for Sofa becomes one, newest first.</CardDescription>
      </CardHeader>
      <CardContent>
        {workItems.isPending && <Badge variant="secondary">Loading…</Badge>}
        {workItems.isError && (
          <p role="alert" className="text-destructive text-sm">
            {problemOf(workItems.error, 'Sofa could not list the Work Items.')}
          </p>
        )}
        {workItems.data?.length === 0 && (
          <p className="text-muted-foreground text-sm">None yet: label an issue to bring a Request in.</p>
        )}
        {workItems.data && workItems.data.length > 0 && (
          <ul aria-label="Work Items" className="flex flex-col divide-y">
            {workItems.data.map((workItem) => (
              <li
                key={workItem.key}
                id={workItem.key}
                aria-current={workItem.key === highlighted ? 'true' : undefined}
                className="grid gap-1 py-2 text-sm aria-[current=true]:font-medium sm:grid-cols-[6rem_1fr_auto] sm:gap-4"
              >
                <span className="font-mono">{workItem.key}</span>
                <span>{workItem.title}</span>
                <span className="text-muted-foreground flex flex-col sm:items-end">
                  <a className="underline-offset-4 hover:underline" href={workItem.request.url}>
                    {workItem.request.repository}#{String(workItem.request.number)}
                  </a>
                  {workItem.delegatedBy ? (
                    <span>delegated by {workItem.delegatedBy.name}</span>
                  ) : (
                    <Badge variant="outline">Waiting for a delegating User</Badge>
                  )}
                </span>
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}

function FlowCard({ workspaceId, project, onChanged }: { workspaceId: string; project: Project; onChanged: () => void }) {
  return (
    <Card>
      <CardHeader>
        <CardTitle>Flow</CardTitle>
        <CardDescription>The Phases its Work Items go through, in order.</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        {project.phases.map((phase) => (
          <PhaseRow key={phase.name} workspaceId={workspaceId} project={project} phase={phase} onChanged={onChanged} />
        ))}
      </CardContent>
    </Card>
  )
}

function PhaseRow({
  workspaceId,
  project,
  phase,
  onChanged,
}: {
  workspaceId: string
  project: Project
  phase: Phase
  onChanged: () => void
}) {
  const [cast, setCast] = useState(phase.castAgentId)
  const [gate, setGate] = useState<Gate>(phase.gate)
  const [sandbox, setSandbox] = useState<Sandbox>(phase.sandbox)
  const changing = api.useMutation('put', '/api/workspaces/{workspaceId}/projects/{key}/phases/{phase}', { onSuccess: onChanged })
  const changed = cast !== phase.castAgentId || gate !== phase.gate || sandbox !== phase.sandbox
  const id = `phase-${phase.name}`

  return (
    <form
      aria-label={`${phase.name} Phase`}
      className="grid gap-2 sm:grid-cols-[6rem_1fr_1fr_1fr_auto] sm:items-end"
      onSubmit={(event) => {
        event.preventDefault()
        changing.mutate({
          params: { path: { workspaceId, key: project.key, phase: phase.name } },
          body: { castAgentId: cast, gate, sandbox },
        })
      }}
    >
      <div className="flex flex-col">
        <span className="font-medium">{phase.name}</span>
        <span className="text-muted-foreground text-xs">{phase.pattern}</span>
      </div>
      <div className="flex flex-col gap-1">
        <Label htmlFor={`${id}-cast`}>Cast</Label>
        <NativeSelect id={`${id}-cast`} value={cast} onChange={(event) => setCast(event.target.value)}>
          {project.agents.map((agent) => (
            <option key={agent.id} value={agent.id}>
              {agent.name}
            </option>
          ))}
        </NativeSelect>
      </div>
      <div className="flex flex-col gap-1">
        <Label htmlFor={`${id}-gate`}>Gate</Label>
        <NativeSelect id={`${id}-gate`} value={gate} onChange={(event) => setGate(event.target.value as Gate)}>
          {Object.entries(gates).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </NativeSelect>
      </div>
      <div className="flex flex-col gap-1">
        <Label htmlFor={`${id}-sandbox`}>Sandbox</Label>
        <NativeSelect id={`${id}-sandbox`} value={sandbox} onChange={(event) => setSandbox(event.target.value as Sandbox)}>
          {Object.entries(sandboxes).map(([value, label]) => (
            <option key={value} value={value}>
              {label}
            </option>
          ))}
        </NativeSelect>
      </div>
      <Button type="submit" variant="outline" disabled={!changed || changing.isPending}>
        Save
      </Button>
      {changing.isError && (
        <p role="alert" className="text-destructive text-sm sm:col-span-5">
          {problemOf(changing.error, 'The Phase couldn’t be changed.')}
        </p>
      )}
    </form>
  )
}

function AgentCard({
  workspaceId,
  projectKey,
  agent,
  onChanged,
}: {
  workspaceId: string
  projectKey: string
  agent: Agent
  onChanged: () => void
}) {
  const [role, setRole] = useState<AgentRole>(agent.role)
  const [model, setModel] = useState(agent.model)
  const [instructions, setInstructions] = useState(agent.instructions)
  const changing = api.useMutation('put', '/api/workspaces/{workspaceId}/projects/{key}/agents/{agentId}', { onSuccess: onChanged })
  const changed = role !== agent.role || model !== agent.model || instructions !== agent.instructions
  const id = `agent-${agent.id}`

  return (
    <Card size="sm">
      <form
        aria-label={`${agent.name} Agent`}
        onSubmit={(event) => {
          event.preventDefault()
          changing.mutate({
            params: { path: { workspaceId, key: projectKey, agentId: agent.id } },
            body: { role, instructions, vendor: agent.vendor, model },
          })
        }}
      >
        <CardHeader>
          <CardTitle>{agent.name}</CardTitle>
          <CardDescription>Claude Code, from {agent.vendor}</CardDescription>
        </CardHeader>
        <CardContent className="mt-3 flex flex-col gap-3">
          <div className="grid gap-3 sm:grid-cols-2">
            <div className="flex flex-col gap-1">
              <Label htmlFor={`${id}-role`}>Role</Label>
              <NativeSelect id={`${id}-role`} value={role} onChange={(event) => setRole(event.target.value as AgentRole)}>
                {roles.map((value) => (
                  <option key={value} value={value}>
                    {value}
                  </option>
                ))}
              </NativeSelect>
            </div>
            <div className="flex flex-col gap-1">
              <Label htmlFor={`${id}-model`}>Model</Label>
              <Input id={`${id}-model`} list={`${id}-models`} value={model} onChange={(event) => setModel(event.target.value.trim())} />
              <datalist id={`${id}-models`}>
                {models.map((value) => (
                  <option key={value} value={value} />
                ))}
              </datalist>
            </div>
          </div>
          <div className="flex flex-col gap-1">
            <Label htmlFor={`${id}-instructions`}>Instructions</Label>
            <Textarea id={`${id}-instructions`} value={instructions} onChange={(event) => setInstructions(event.target.value)} />
          </div>
          {changing.isError && (
            <p role="alert" className="text-destructive text-sm">
              {problemOf(changing.error, 'The Agent couldn’t be changed.')}
            </p>
          )}
        </CardContent>
        <CardFooter className="mt-3">
          <Button type="submit" variant="outline" disabled={!changed || changing.isPending}>
            Save {agent.name}
          </Button>
        </CardFooter>
      </form>
    </Card>
  )
}
