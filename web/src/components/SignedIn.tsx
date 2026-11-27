import { useQuery } from '@tanstack/react-query'
import { type ReactNode, useEffect } from 'react'
import { signedInUser, startSigningIn } from '@/auth/session'
import { Centered } from '@/components/Centered'
import { useWorkspace } from '@/components/workspace'

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

  return children
}

const pages = [
  { href: '/', label: 'Home' },
  { href: '/projects', label: 'Projects' },
  { href: '/runners', label: 'Runners' },
  { href: '/team', label: 'Team', adminsOnly: true },
]

export function Navigation() {
  const { workspace } = useWorkspace()
  return (
    <nav aria-label="Sofa" className="flex flex-wrap gap-4 text-sm">
      {pages
        .filter((page) => !page.adminsOnly || workspace?.isAdmin)
        .map((page) => (
          <a key={page.href} href={page.href} className="text-muted-foreground hover:text-foreground underline-offset-4 hover:underline">
            {page.label}
          </a>
        ))}
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
