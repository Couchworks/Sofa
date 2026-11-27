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

type Team = components['schemas']['WorkspaceTeam']

export function TeamPage() {
  const { workspace } = useWorkspace()
  return (
    <Page title="Team">
      {workspace && !workspace.isAdmin && <p className="text-muted-foreground text-sm">Only an Admin changes who is in the Team.</p>}
      {workspace?.isAdmin && <Teams workspaceId={workspace.id} />}
    </Page>
  )
}

function Teams({ workspaceId }: { workspaceId: string }) {
  const teams = api.useQuery('get', '/api/workspaces/{workspaceId}/teams', { params: { path: { workspaceId } } })

  if (teams.isPending) return <Badge variant="secondary">Loading…</Badge>
  if (teams.isError) return <Badge variant="destructive">Unreachable</Badge>

  return (
    <>
      {teams.data.map((team) => (
        <TeamCard key={team.id} workspaceId={workspaceId} team={team} onChanged={() => void teams.refetch()} />
      ))}
    </>
  )
}

function TeamCard({ workspaceId, team, onChanged }: { workspaceId: string; team: Team; onChanged: () => void }) {
  const users = api.useQuery('get', '/api/workspaces/{workspaceId}/users', { params: { path: { workspaceId } } })
  const [adding, setAdding] = useState('')
  const add = api.useMutation('post', '/api/workspaces/{workspaceId}/teams/{teamId}/users', {
    onSuccess: () => {
      setAdding('')
      onChanged()
    },
  })
  const remove = api.useMutation('delete', '/api/workspaces/{workspaceId}/teams/{teamId}/users/{userId}', { onSuccess: onChanged })
  const outside = users.data?.filter((user) => !team.members.some((member) => member.userId === user.userId)) ?? []
  const failed = add.error ?? remove.error

  return (
    <Card>
      <CardHeader>
        <CardTitle>{team.name}</CardTitle>
        <CardDescription>Everyone in the Team may spend its team Runners.</CardDescription>
      </CardHeader>
      <CardContent className="flex flex-col gap-4">
        <ul className="flex flex-col gap-2">
          {team.members.map((member) => (
            <li key={member.userId} className="flex items-center justify-between gap-2 text-sm">
              <span>
                {member.name}
                {member.gitHubLogin && <span className="text-muted-foreground"> · {member.gitHubLogin}</span>}
              </span>
              <Button
                variant="outline"
                size="sm"
                disabled={remove.isPending}
                onClick={() => remove.mutate({ params: { path: { workspaceId, teamId: team.id, userId: member.userId } } })}
              >
                Remove {member.name}
              </Button>
            </li>
          ))}
        </ul>
        {outside.length > 0 && (
          <form
            className="flex items-end gap-2"
            onSubmit={(event) => {
              event.preventDefault()
              add.mutate({ params: { path: { workspaceId, teamId: team.id } }, body: { userId: adding } })
            }}
          >
            <div className="flex flex-1 flex-col gap-1">
              <Label htmlFor={`team-${team.id}-add`}>Add to {team.name}</Label>
              <NativeSelect id={`team-${team.id}-add`} value={adding} onChange={(event) => setAdding(event.target.value)}>
                <option value="">Choose a User…</option>
                {outside.map((user) => (
                  <option key={user.userId} value={user.userId}>
                    {user.name}
                    {user.gitHubLogin ? ` (${user.gitHubLogin})` : ''}
                  </option>
                ))}
              </NativeSelect>
            </div>
            <Button type="submit" disabled={adding === '' || add.isPending}>
              Add
            </Button>
          </form>
        )}
        {failed && (
          <p role="alert" className="text-destructive text-sm">
            {problemOf(failed, 'The Team couldn’t be changed.')}
          </p>
        )}
      </CardContent>
    </Card>
  )
}
