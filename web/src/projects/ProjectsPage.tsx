import { useState } from 'react'
import { api } from '@/api/client'
import { Page } from '@/components/SignedIn'
import { problemOf, useWorkspace } from '@/components/workspace'
import { Alert, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { goTo } from '@/lib/navigation'
import { type Binding, noBinding } from './binding'
import { BindingFields } from './BindingFields'

export function ProjectsPage() {
  const { workspace } = useWorkspace()
  return (
    <Page title="Projects">
      {workspace && <ProjectList workspaceId={workspace.id} />}
      {workspace?.isAdmin && <NewProjectCard workspaceId={workspace.id} />}
    </Page>
  )
}

function ProjectList({ workspaceId }: { workspaceId: string }) {
  const projects = api.useQuery('get', '/api/workspaces/{workspaceId}/projects', { params: { path: { workspaceId } } })

  if (projects.isPending) return <Badge variant="secondary">Loading…</Badge>
  if (projects.isError) return <Badge variant="destructive">Unreachable</Badge>
  if (projects.data.length === 0) {
    return <p className="text-muted-foreground text-sm">No Projects yet.</p>
  }

  return (
    <ul className="flex flex-col gap-2">
      {projects.data.map((project) => (
        <li key={project.id}>
          <a href={`/projects/${project.key}`} className="flex flex-wrap items-baseline gap-2 hover:underline">
            <span className="font-mono font-medium">{project.key}</span>
            <span>{project.name}</span>
            <span className="text-muted-foreground text-xs">
              issues in {project.requestRepository} labelled {project.requestLabel}
            </span>
          </a>
        </li>
      ))}
    </ul>
  )
}

function NewProjectCard({ workspaceId }: { workspaceId: string }) {
  const [key, setKey] = useState('')
  const [name, setName] = useState('')
  const [binding, setBinding] = useState<Binding>(noBinding)
  const creating = api.useMutation('post', '/api/workspaces/{workspaceId}/projects', {
    onSuccess: (project) => goTo(`/projects/${project.key}`),
  })

  return (
    <Card>
      <CardHeader>
        <CardTitle>New Project</CardTitle>
        <CardDescription>
          Its key names its Work Items, such as DEMO-1, and never changes. It starts with milestone 1&apos;s Flow and its
          Triager, Lead and Builder.
        </CardDescription>
      </CardHeader>
      <form
        onSubmit={(event) => {
          event.preventDefault()
          creating.mutate({
            params: { path: { workspaceId } },
            body: { key: key.trim(), name: name.trim(), binding },
          })
        }}
      >
        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor="project-key">Key</Label>
            <Input id="project-key" placeholder="DEMO" value={key} onChange={(event) => setKey(event.target.value.toUpperCase())} />
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="project-name">Name</Label>
            <Input id="project-name" value={name} onChange={(event) => setName(event.target.value)} />
          </div>
          <BindingFields workspaceId={workspaceId} binding={binding} onChange={setBinding} />
          {creating.isError && (
            <Alert variant="destructive">
              <AlertTitle>{problemOf(creating.error, 'The Project couldn’t be created')}</AlertTitle>
            </Alert>
          )}
        </CardContent>
        <CardFooter className="mt-4">
          <Button type="submit" disabled={creating.isPending}>
            Create
          </Button>
        </CardFooter>
      </form>
    </Card>
  )
}
