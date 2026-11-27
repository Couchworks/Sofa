import { api } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

export default function App() {
  const status = api.useQuery('get', '/api/status')

  return (
    <main className="flex min-h-svh items-center justify-center p-4">
      <Card className="w-full max-w-sm">
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
    </main>
  )
}
