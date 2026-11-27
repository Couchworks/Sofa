import { api } from '@/api/client'
import { Badge } from '@/components/ui/badge'
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card'

const addedOn = new Intl.DateTimeFormat(undefined, { dateStyle: 'medium' })

export function ConnectionsCard() {
  const connections = api.useQuery('get', '/api/connections')

  return (
    <Card>
      <CardHeader>
        <CardTitle>Connections</CardTitle>
        <CardDescription>This Workspace’s links to outside systems.</CardDescription>
      </CardHeader>
      <CardContent>
        {connections.isPending && <Badge variant="secondary">Checking…</Badge>}
        {connections.isError && <Badge variant="destructive">Unreachable</Badge>}
        {connections.data?.length === 0 && (
          <p className="text-muted-foreground text-sm">
            No Connections yet. Install the GitHub App on an organisation or account to add one.
          </p>
        )}
        {connections.data && connections.data.length > 0 && (
          <ul className="flex flex-col gap-3">
            {connections.data.map((connection) => (
              <li key={connection.id} className="flex items-center justify-between gap-2">
                <div className="flex flex-col">
                  <span className="font-medium">{connection.account}</span>
                  <span className="text-muted-foreground text-xs">
                    {connection.system} {connection.accountType.toLowerCase()}, added{' '}
                    {addedOn.format(new Date(connection.addedAt))}
                  </span>
                </div>
                {connection.suspended ? (
                  <Badge variant="destructive">Suspended</Badge>
                ) : (
                  <Badge variant="secondary">Active</Badge>
                )}
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  )
}
