import { AddUserCard } from './AddUserCard'
import { ConnectionsCard } from './ConnectionsCard'
import { GitHubAppCard } from './GitHubAppCard'

export function AdminArea({ workspaceId }: { workspaceId: string }) {
  return (
    <section className="flex w-full max-w-sm flex-col gap-4">
      <GitHubAppCard workspaceId={workspaceId} />
      <ConnectionsCard workspaceId={workspaceId} />
      <AddUserCard workspaceId={workspaceId} />
    </section>
  )
}
