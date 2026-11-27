import { useState } from 'react'
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/alert'
import { Button } from '@/components/ui/button'
import { readReturnFromGitHub } from './readReturnFromGitHub'

function forgetReturnFromGitHub() {
  window.history.replaceState(null, '', window.location.pathname)
}

export function ReturnFromGitHub() {
  const [returned, setReturned] = useState(() => readReturnFromGitHub(window.location.search))
  if (!returned) return null

  const dismiss = () => {
    forgetReturnFromGitHub()
    setReturned(null)
  }

  return (
    <Alert variant={returned.problem === null ? 'default' : 'destructive'}>
      <AlertTitle>{returned.title}</AlertTitle>
      {returned.problem && <AlertDescription>{returned.problem}</AlertDescription>}
      <div>
        <Button variant="ghost" size="sm" onClick={dismiss}>
          Dismiss
        </Button>
      </div>
    </Alert>
  )
}
