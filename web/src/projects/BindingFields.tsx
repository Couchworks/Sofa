import { api } from '@/api/client'
import { NativeSelect } from '@/components/NativeSelect'
import { Input } from '@/components/ui/input'
import { Label } from '@/components/ui/label'
import type { Binding } from './binding'

export function BindingFields({
  workspaceId,
  binding,
  onChange,
}: {
  workspaceId: string
  binding: Binding
  onChange: (binding: Binding) => void
}) {
  const connections = api.useQuery('get', '/api/workspaces/{workspaceId}/connections', {
    params: { path: { workspaceId } },
  })
  const repositories = api.useQuery(
    'get',
    '/api/workspaces/{workspaceId}/connections/{connectionId}/repositories',
    { params: { path: { workspaceId, connectionId: binding.connectionId } } },
    { enabled: binding.connectionId !== '' },
  )
  const reached = repositories.data ?? []

  const toggleCode = (repository: string, included: boolean) =>
    onChange({
      ...binding,
      codeRepositories: included
        ? [...binding.codeRepositories, repository]
        : binding.codeRepositories.filter((name) => name !== repository),
    })

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-col gap-2">
        <Label htmlFor="binding-connection">Connection</Label>
        <NativeSelect
          id="binding-connection"
          value={binding.connectionId}
          onChange={(event) =>
            onChange({ ...binding, connectionId: event.target.value, requestRepository: '', codeRepositories: [] })
          }
        >
          <option value="">Choose a Connection…</option>
          {connections.data?.map((connection) => (
            <option key={connection.id} value={connection.id}>
              {connection.system} {connection.account}
            </option>
          ))}
        </NativeSelect>
        {connections.data?.length === 0 && (
          <p className="text-muted-foreground text-xs">Install the GitHub App on the home page to add a Connection.</p>
        )}
      </div>
      {repositories.isError && (
        <p role="alert" className="text-destructive text-sm">
          The Connection couldn&apos;t say which Repositories it reaches.
        </p>
      )}
      {binding.connectionId !== '' && (
        <>
          <div className="flex flex-col gap-2">
            <Label htmlFor="binding-request-repository">Requests come from issues in</Label>
            <NativeSelect
              id="binding-request-repository"
              value={binding.requestRepository}
              onChange={(event) => onChange({ ...binding, requestRepository: event.target.value })}
            >
              <option value="">Choose a Repository…</option>
              {reached.map((repository) => (
                <option key={repository.fullName} value={repository.fullName}>
                  {repository.fullName}
                </option>
              ))}
            </NativeSelect>
          </div>
          <div className="flex flex-col gap-2">
            <Label htmlFor="binding-label">Labelled</Label>
            <Input
              id="binding-label"
              value={binding.label}
              onChange={(event) => onChange({ ...binding, label: event.target.value })}
            />
          </div>
          <fieldset className="flex flex-col gap-2">
            <legend className="mb-2 text-sm font-medium">Code lives in</legend>
            {reached.map((repository) => (
              <label key={repository.fullName} className="flex items-center gap-2 text-sm">
                <input
                  type="checkbox"
                  checked={binding.codeRepositories.includes(repository.fullName)}
                  onChange={(event) => toggleCode(repository.fullName, event.target.checked)}
                />
                {repository.fullName}
              </label>
            ))}
          </fieldset>
        </>
      )}
    </div>
  )
}
