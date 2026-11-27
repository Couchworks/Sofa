import { useState } from 'react'
import { api } from '@/api/client'
import type { components } from '@/api/schema'
import { Page } from '@/components/SignedIn'
import { problemOf, useWorkspace } from '@/components/workspace'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'

type Runner = components['schemas']['RunnerDetails']

const defaultRunsAtOnce = 2
const signInEnds = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' })

function signInCommand(runnerId: string) {
  return `sofa runner login ${runnerId}`
}

export function RunnersPage() {
  const { workspace } = useWorkspace()
  if (!workspace) return <Page title="Runners">{null}</Page>

  return (
    <Page title="Runners">
      <RunnerList workspaceId={workspace.id} />
      <AddSubscriptionRunner workspaceId={workspace.id} />
      <AddApiKeyRunner workspaceId={workspace.id} forTheTeam={false} />
      {workspace.isAdmin && <AddApiKeyRunner workspaceId={workspace.id} forTheTeam />}
    </Page>
  )
}

function useRunners(workspaceId: string) {
  return api.useQuery('get', '/api/workspaces/{workspaceId}/runners', { params: { path: { workspaceId } } })
}

function RunnerList({ workspaceId }: { workspaceId: string }) {
  const runners = useRunners(workspaceId)

  if (runners.isPending) return <Badge variant="secondary">Loading…</Badge>
  if (runners.isError) return <Badge variant="destructive">Unreachable</Badge>
  if (runners.data.length === 0) {
    return <p className="text-muted-foreground text-sm">No Runners yet: add one below to start Runs on.</p>
  }

  return (
    <ul className="flex flex-col gap-3">
      {runners.data.map((runner) => (
        <li key={runner.id}>
          <RunnerItem workspaceId={workspaceId} runner={runner} onChanged={() => void runners.refetch()} />
        </li>
      ))}
    </ul>
  )
}

function RunnerItem({ workspaceId, runner, onChanged }: { workspaceId: string; runner: Runner; onChanged: () => void }) {
  const [runsAtOnce, setRunsAtOnce] = useState(String(runner.runsAtOnce))
  const limiting = api.useMutation('put', '/api/workspaces/{workspaceId}/runners/{runnerId}/runs-at-once', { onSuccess: onChanged })
  const id = `runner-${runner.id}`

  return (
    <Card size="sm">
      <CardHeader>
        <CardTitle className="flex flex-wrap items-center gap-2">
          {runner.name}
          <Badge variant="outline">{runner.owner.kind === 'Team' ? 'Team' : 'Personal'}</Badge>
          <Badge variant="outline">{runner.kind === 'Subscription' ? 'Subscription' : 'API key'}</Badge>
        </CardTitle>
        <CardDescription>{runner.kind === 'ApiKey' ? 'Its API key is saved, and never shown again.' : <SignInState runner={runner} />}</CardDescription>
      </CardHeader>
      <CardContent className="mt-2 flex flex-col gap-3">
        {runner.kind === 'Subscription' && (
          <div className="flex flex-col gap-1">
            <span className="text-muted-foreground text-xs">Sign it in from a terminal on Sofa&apos;s box:</span>
            <code className="bg-muted rounded px-2 py-1 font-mono text-xs break-all">{signInCommand(runner.id)}</code>
          </div>
        )}
        <form
          className="flex items-end gap-2"
          onSubmit={(event) => {
            event.preventDefault()
            limiting.mutate({ params: { path: { workspaceId, runnerId: runner.id } }, body: { runsAtOnce: Number(runsAtOnce) } })
          }}
        >
          <div className="flex flex-col gap-1">
            <Label htmlFor={`${id}-runs-at-once`}>Runs at once</Label>
            <Input
              id={`${id}-runs-at-once`}
              type="number"
              min={1}
              className="w-24"
              value={runsAtOnce}
              onChange={(event) => setRunsAtOnce(event.target.value)}
            />
          </div>
          <Button type="submit" variant="outline" disabled={Number(runsAtOnce) === runner.runsAtOnce || limiting.isPending}>
            Save
          </Button>
        </form>
        {limiting.isError && (
          <p role="alert" className="text-destructive text-sm">
            {problemOf(limiting.error, 'The limit couldn’t be changed.')}
          </p>
        )}
      </CardContent>
    </Card>
  )
}

function SignInState({ runner }: { runner: Runner }) {
  if (!runner.signIn?.signedIn) return <>Not signed in yet.</>
  return runner.signIn.endsAt ? <>Signed in until {signInEnds.format(new Date(runner.signIn.endsAt))}.</> : <>Signed in.</>
}

function AddSubscriptionRunner({ workspaceId }: { workspaceId: string }) {
  const [name, setName] = useState('')
  const runners = useRunners(workspaceId)
  const adding = api.useMutation('post', '/api/workspaces/{workspaceId}/runners/subscription', {
    onSuccess: () => {
      setName('')
      void runners.refetch()
    },
  })

  return (
    <Card>
      <CardHeader>
        <CardTitle>Your subscription</CardTitle>
        <CardDescription>
          A personal Runner on your own Claude subscription, for the Requests you delegate. You sign it in yourself on Sofa&apos;s box;
          Sofa never sees the sign-in.
        </CardDescription>
      </CardHeader>
      <form
        onSubmit={(event) => {
          event.preventDefault()
          adding.mutate({ params: { path: { workspaceId } }, body: { name: name.trim(), runsAtOnce: defaultRunsAtOnce } })
        }}
      >
        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor="subscription-name">Name</Label>
            <Input id="subscription-name" placeholder="My Max plan" value={name} onChange={(event) => setName(event.target.value)} />
          </div>
          {adding.isSuccess && (
            <Alert>
              <AlertTitle>{adding.data.name} is added</AlertTitle>
              <AlertDescription>
                Sign it in from a terminal on Sofa&apos;s box: <code className="font-mono">{signInCommand(adding.data.id)}</code>
              </AlertDescription>
            </Alert>
          )}
          {adding.isError && (
            <Alert variant="destructive">
              <AlertTitle>{problemOf(adding.error, 'The Runner couldn’t be added')}</AlertTitle>
            </Alert>
          )}
        </CardContent>
        <CardFooter className="mt-4">
          <Button type="submit" disabled={adding.isPending}>
            Add subscription Runner
          </Button>
        </CardFooter>
      </form>
    </Card>
  )
}

function AddApiKeyRunner({ workspaceId, forTheTeam }: { workspaceId: string; forTheTeam: boolean }) {
  const [name, setName] = useState('')
  const [apiKey, setApiKey] = useState('')
  const runners = useRunners(workspaceId)
  const adding = api.useMutation('post', '/api/workspaces/{workspaceId}/runners/api-key', {
    onSuccess: () => {
      setName('')
      setApiKey('')
      void runners.refetch()
    },
  })
  const id = forTheTeam ? 'team-key' : 'personal-key'

  return (
    <Card>
      <CardHeader>
        <CardTitle>{forTheTeam ? 'The Team’s API key' : 'Your API key'}</CardTitle>
        <CardDescription>
          {forTheTeam
            ? 'A team Runner on an Anthropic API key, for anyone in the Team.'
            : 'A personal Runner on your own Anthropic API key, for the Requests you delegate.'}{' '}
          The key is kept encrypted and never shown again.
        </CardDescription>
      </CardHeader>
      <form
        onSubmit={(event) => {
          event.preventDefault()
          adding.mutate({
            params: { path: { workspaceId } },
            body: { name: name.trim(), apiKey: apiKey.trim(), runsAtOnce: defaultRunsAtOnce, forTheTeam },
          })
        }}
      >
        <CardContent className="flex flex-col gap-4">
          <div className="flex flex-col gap-2">
            <Label htmlFor={`${id}-name`}>Name</Label>
            <Input id={`${id}-name`} value={name} onChange={(event) => setName(event.target.value)} />
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor={`${id}-api-key`}>API key</Label>
            <Input
              id={`${id}-api-key`}
              type="password"
              autoComplete="off"
              placeholder="sk-ant-…"
              value={apiKey}
              onChange={(event) => setApiKey(event.target.value)}
            />
          </div>
          {adding.isSuccess && (
            <Alert>
              <AlertTitle>{adding.data.name} is added</AlertTitle>
              <AlertDescription>Its key is saved and won’t be shown again.</AlertDescription>
            </Alert>
          )}
          {adding.isError && (
            <Alert variant="destructive">
              <AlertTitle>{problemOf(adding.error, 'The Runner couldn’t be added')}</AlertTitle>
            </Alert>
          )}
        </CardContent>
        <CardFooter className="mt-4">
          <Button type="submit" disabled={adding.isPending}>
            {forTheTeam ? 'Add team Runner' : 'Add API key Runner'}
          </Button>
        </CardFooter>
      </form>
    </Card>
  )
}
