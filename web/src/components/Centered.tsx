import type { ReactNode } from 'react'

export function Centered({ children }: { children: ReactNode }) {
  return <main className="flex min-h-svh flex-col items-center justify-center gap-4 p-4">{children}</main>
}
