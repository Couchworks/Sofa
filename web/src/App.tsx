import { api } from '@/api/client'
import { AdminArea } from '@/admin/AdminArea'
import { Badge } from '@/components/ui/badge'

export default function App() {
  const status = api.useQuery('get', '/api/status')

  return (
    <main className="mx-auto flex min-h-svh w-full max-w-xl flex-col gap-6 p-4">
      <header className="flex items-center justify-between gap-2">
        <h1 className="text-lg font-semibold">Sofa</h1>
        <div className="flex items-center gap-2">
          {status.isPending && <Badge variant="secondary">Checking…</Badge>}
          {status.isError && <Badge variant="destructive">Unreachable</Badge>}
          {status.data && (
            <>
              <span className="text-muted-foreground text-sm">{status.data.environment}</span>
              <Badge>Up</Badge>
            </>
          )}
        </div>
      </header>
      <AdminArea />
    </main>
  )
}
