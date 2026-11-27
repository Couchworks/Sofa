import { ConnectionsCard } from './ConnectionsCard'
import { GitHubAppCard } from './GitHubAppCard'
import { ReturnFromGitHub } from './ReturnFromGitHub'

export function AdminArea() {
  return (
    <section className="flex w-full max-w-sm flex-col gap-4">
      <ReturnFromGitHub />
      <GitHubAppCard />
      <ConnectionsCard />
    </section>
  )
}
