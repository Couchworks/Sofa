import { useQuery } from '@tanstack/react-query'
import { type ReactNode, useEffect } from 'react'
import { signedInUser, startSigningIn } from '@/auth/session'
import { Centered } from '@/components/Centered'
import { useWorkspace } from '@/components/workspace'
import { Badge } from '@/components/ui/badge'
import { useInbox } from '@/inbox/useInbox'
import { LiveUpdates } from '@/live/LiveUpdates'
import { useLiveState } from '@/live/liveState'

export function SignedIn({ children }: { children: ReactNode }) {
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

  return <LiveUpdates>{children}</LiveUpdates>
}

const pages = [
  { href: '/', label: 'Home' },
  { href: '/inbox', label: 'Inbox' },
  { href: '/projects', label: 'Projects' },
  { href: '/runners', label: 'Runners' },
  { href: '/team', label: 'Team', adminsOnly: true },
]

const liveStates = { connecting: 'Connecting…', live: 'Live', offline: 'Not live' }

export function Navigation() {
  const { workspace } = useWorkspace()
  const inbox = useInbox(workspace?.id)
  const live = useLiveState()
  const waiting = inbox.data?.length ?? 0
  return (
    <nav aria-label="Sofa" className="flex flex-wrap items-center gap-4 text-sm">
      {pages
        .filter((page) => !page.adminsOnly || workspace?.isAdmin)
        .map((page) => (
          <a key={page.href} href={page.href} className="text-muted-foreground hover:text-foreground underline-offset-4 hover:underline">
            {page.label}
            {page.href === '/inbox' && waiting > 0 && (
              <Badge className="ml-1" aria-label={`${waiting} waiting`}>
                {waiting}
              </Badge>
            )}
          </a>
        ))}
      <Badge variant={live === 'live' ? 'secondary' : 'outline'} className="ml-auto" title="Pages change as Runs do while live">
        {liveStates[live]}
      </Badge>
    </nav>
  )
}

export function Page({ title, children }: { title: string; children: ReactNode }) {
  return (
    <SignedIn>
      <main className="mx-auto flex min-h-svh w-full max-w-3xl flex-col gap-6 p-4">
        <Navigation />
        <h1 className="font-heading text-xl font-medium">{title}</h1>
        {children}
      </main>
    </SignedIn>
  )
}
