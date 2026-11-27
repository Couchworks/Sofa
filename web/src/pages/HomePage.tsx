import { useQuery } from '@tanstack/react-query'
import { useEffect } from 'react'
import { AdminArea } from '@/admin/AdminArea'
import { ReturnFromGitHub } from '@/admin/ReturnFromGitHub'
import { api } from '@/api/client'
import { signedInUser, signOut, startSigningIn } from '@/auth/session'
import { Centered } from '@/components/Centered'
import { Badge } from '@/components/ui/badge'
import { Button } from '@/components/ui/button'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'
import { goTo } from '@/lib/navigation'

export function HomePage() {
  const session = useQuery({ queryKey: ['session'], queryFn: signedInUser })
  const signedOut = session.isSuccess && session.data === null

  useEffect(() => {
    if (signedOut) {
      void startSigningIn()
    }
  }, [signedOut])

  if (!session.data) {
    return (
      <Centered>
        <p className="text-muted-foreground text-sm">Signing in…</p>
      </Centered>
    )
  }

  return (
    <Centered>
      <div className="flex w-full max-w-sm flex-col gap-4">
        <ReturnFromGitHub />
      </div>
      <SignedInUser />
      <AdministeredWorkspace />
      <GatewayStatus />
    </Centered>
  )
}

function AdministeredWorkspace() {
  const me = api.useQuery('get', '/api/me')
  const administered = me.data?.workspaces.find((workspace) => workspace.isAdmin)
  return administered ? <AdminArea workspaceId={administered.id} /> : null
}

function SignedInUser() {
  const me = api.useQuery('get', '/api/me')

  return (
    <Card className="w-full max-w-sm">
      <CardHeader>
        <CardTitle>{me.data?.name ?? 'Signed in'}</CardTitle>
        <CardDescription>
          {me.data
            ? (me.data.email ?? me.data.gitHubLogin)
            : me.isError
              ? 'Sofa could not say who you are.'
              : 'Finding out who you are…'}
        </CardDescription>
      </CardHeader>
      <CardContent className="grid gap-4">
        {me.data?.workspaces.map((workspace) => (
          <div key={workspace.id} className="flex flex-wrap items-center gap-2 text-sm">
            <span className="font-medium">{workspace.name}</span>
            {workspace.isAdmin && <Badge variant="secondary">Admin</Badge>}
            {workspace.teams.map((team) => (
              <Badge key={team.id} variant="outline">
                {team.name}
              </Badge>
            ))}
          </div>
        ))}
        {me.data && <GitHubAccount login={me.data.gitHubLogin} />}
        <Button variant="outline" onClick={() => void signOut()}>
          Sign out
        </Button>
      </CardContent>
    </Card>
  )
}

function GitHubAccount({ login }: { login: string | null }) {
  const link = api.useMutation('post', '/api/me/github-link', { onSuccess: ({ authorizeUrl }) => goTo(authorizeUrl) })

  if (login) {
    return <p className="text-muted-foreground text-sm">Signs in with GitHub as {login}.</p>
  }

  return (
    <div className="flex flex-col gap-2">
      <Button variant="outline" disabled={link.isPending} onClick={() => link.mutate({})}>
        Link your GitHub account
      </Button>
      {link.isError && (
        <p role="alert" className="text-destructive text-sm">
          {link.error?.title ?? 'Linking couldn’t start.'}
        </p>
      )}
    </div>
  )
}

function GatewayStatus() {
  const status = api.useQuery('get', '/api/status')

  return (
    <Card className="w-full max-w-sm" size="sm">
      <CardHeader>
        <CardTitle>Sofa</CardTitle>
        <CardDescription>The App Gateway, as the web client sees it.</CardDescription>
      </CardHeader>
      <CardContent className="flex items-center justify-between gap-2">
        {status.isPending && <Badge variant="secondary">Checking…</Badge>}
        {status.isError && <Badge variant="destructive">Unreachable</Badge>}
        {status.data && (
          <>
            <Badge>Up</Badge>
            <span className="text-muted-foreground text-sm">{status.data.environment}</span>
          </>
        )}
      </CardContent>
    </Card>
  )
}
