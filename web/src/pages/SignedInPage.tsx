import { useEffect, useState } from 'react'
import { finishSigningIn } from '@/auth/session'
import { Centered } from '@/components/Centered'
import { Button } from '@/components/ui/button'
import { replaceWith } from '@/lib/navigation'

export function SignedInPage() {
  const [failure, setFailure] = useState<string>()

  useEffect(() => {
    finishSigningIn().then(
      () => replaceWith('/'),
      (error: unknown) => setFailure(error instanceof Error ? error.message : String(error)),
    )
  }, [])

  return (
    <Centered>
      {failure ? (
        <>
          <p role="alert" className="text-destructive text-sm">
            Signing in didn&apos;t finish: {failure}
          </p>
          <Button variant="outline" onClick={() => replaceWith('/')}>
            Try again
          </Button>
        </>
      ) : (
        <p className="text-muted-foreground text-sm">Signing in…</p>
      )}
    </Centered>
  )
}
