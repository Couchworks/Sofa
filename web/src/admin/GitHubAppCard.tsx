import { useForm } from 'react-hook-form'
import { api } from '@/api/client'
import type { components } from '@/api/schema'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardFooter, CardHeader, CardTitle } from '@/components/ui/card'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import { goTo } from '@/lib/navigation'
import { sendManifestToGitHub } from './sendManifestToGitHub'

type GitHubAppStatus = components['schemas']['GitHubAppStatus']
type NewGitHubApp = { organisation: string; name: string }

export function GitHubAppCard({ workspaceId }: { workspaceId: string }) {
  const app = api.useQuery('get', '/api/workspaces/{workspaceId}/github-app', { params: { path: { workspaceId } } })

  return (
    <Card>
      <CardHeader>
        <CardTitle>GitHub App</CardTitle>
        <CardDescription>
          Sofa reaches GitHub through an App of its own. Each organisation or account it is installed on becomes a
          Connection.
        </CardDescription>
      </CardHeader>
      {app.isPending && (
        <CardContent>
          <Badge variant="secondary">Checking…</Badge>
        </CardContent>
      )}
      {app.isError && (
        <CardContent>
          <Badge variant="destructive">Unreachable</Badge>
        </CardContent>
      )}
      {app.data &&
        (app.data.created ? (
          <CreatedApp app={app.data} workspaceId={workspaceId} />
        ) : (
          <NewApp app={app.data} workspaceId={workspaceId} />
        ))}
    </Card>
  )
}

function CreatedApp({ app, workspaceId }: { app: GitHubAppStatus; workspaceId: string }) {
  const installation = api.useMutation('post', '/api/workspaces/{workspaceId}/github-app/installations', {
    onSuccess: ({ installUrl }) => goTo(installUrl),
  })

  return (
    <>
      <CardContent className="flex items-center justify-between gap-2">
        <a className="font-medium underline-offset-4 hover:underline" href={app.htmlUrl ?? undefined}>
          {app.name}
        </a>
        <Badge>Created</Badge>
      </CardContent>
      {installation.isError && (
        <CardContent>
          <Alert variant="destructive">
            <AlertTitle>{installation.error?.title ?? 'The installation couldn’t be started'}</AlertTitle>
          </Alert>
        </CardContent>
      )}
      <CardFooter>
        <Button
          disabled={installation.isPending}
          onClick={() => installation.mutate({ params: { path: { workspaceId } } })}
        >
          Install on GitHub
        </Button>
      </CardFooter>
    </>
  )
}

function NewApp({ app, workspaceId }: { app: GitHubAppStatus; workspaceId: string }) {
  const { register, handleSubmit } = useForm<NewGitHubApp>({ defaultValues: { organisation: '', name: '' } })
  const creation = api.useMutation('post', '/api/workspaces/{workspaceId}/github-app/creations', {
    onSuccess: sendManifestToGitHub,
  })

  const create = handleSubmit(({ organisation, name }) =>
    creation.mutate({
      params: { path: { workspaceId } },
      body: { organisation: organisation.trim() || null, name: name.trim() || null },
    }),
  )

  return (
    <form onSubmit={create}>
      <CardContent className="flex flex-col gap-4">
        {!app.gitHubCanReachPublicAddress && (
          <Alert>
            <AlertTitle>GitHub can’t reach {app.publicAddress}</AlertTitle>
            <AlertDescription>
              The App will be created with its webhooks off. Run Sofa behind a public address, such as a tunnel, before
              creating it to receive GitHub’s events.
            </AlertDescription>
          </Alert>
        )}
        <div className="flex flex-col gap-2">
          <Label htmlFor="organisation">Organisation</Label>
          <Input id="organisation" placeholder="Leave empty for your own account" {...register('organisation')} />
        </div>
        <div className="flex flex-col gap-2">
          <Label htmlFor="name">Name on GitHub</Label>
          <Input id="name" placeholder="Sofa and the organisation’s name" {...register('name')} />
        </div>
        {creation.isError && (
          <Alert variant="destructive">
            <AlertTitle>{creation.error?.title ?? 'The App couldn’t be started'}</AlertTitle>
          </Alert>
        )}
      </CardContent>
      <CardFooter className="mt-4">
        <Button type="submit" disabled={creation.isPending}>
          Create on GitHub
        </Button>
      </CardFooter>
    </form>
  )
}
