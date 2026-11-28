import { useQuery } from '@tanstack/react-query'
import { api, fetchClient } from '@/api/client'
import type { components } from '@/api/schema'
import { problemOf } from '@/components/workspace'
import { Badge } from '@/components/ui/badge'
import { Markdown } from './Markdown'

type Attachment = components['schemas']['AttachmentDetails']

export function Contribution({ workspaceId, runId }: { workspaceId: string; runId: string }) {
  const contribution = api.useQuery('get', '/api/workspaces/{workspaceId}/runs/{runId}/contribution', {
    params: { path: { workspaceId, runId } },
  })

  if (contribution.isPending) return <Badge variant="secondary">Loading the Contribution…</Badge>
  if (contribution.isError) {
    return <p className="text-muted-foreground text-sm">{problemOf(contribution.error, 'The Run has made no Contribution.')}</p>
  }

  return (
    <section aria-label="Contribution" className="flex flex-col gap-3">
      <Badge variant="outline">Outcome: {contribution.data.outcome}</Badge>
      <Markdown>{contribution.data.markdown}</Markdown>
      {contribution.data.attachments.length > 0 && (
        <ul aria-label="Attachments" className="flex flex-col gap-3">
          {contribution.data.attachments.map((attachment) => (
            <li key={attachment.id}>
              <AttachmentView workspaceId={workspaceId} runId={runId} attachment={attachment} />
            </li>
          ))}
        </ul>
      )}
    </section>
  )
}

function readAsAddress(file: Blob) {
  return new Promise<string>((resolve, reject) => {
    const reader = new FileReader()
    reader.onload = () => resolve(reader.result as string)
    reader.onerror = () => reject(reader.error)
    reader.readAsDataURL(file)
  })
}

function useAttachmentAddress(workspaceId: string, runId: string, attachment: Attachment) {
  const file = useQuery({
    queryKey: ['attachment', workspaceId, runId, attachment.id],
    queryFn: async () => {
      const { data, error } = await fetchClient.GET('/api/workspaces/{workspaceId}/runs/{runId}/contribution/attachments/{attachmentId}', {
        params: { path: { workspaceId, runId, attachmentId: attachment.id } },
        parseAs: 'blob',
      })
      if (error || !data) throw new Error(`Sofa couldn’t give ${attachment.name}.`)
      return readAsAddress(new Blob([data as Blob], { type: attachment.contentType }))
    },
    staleTime: Infinity,
  })

  return { address: file.data, failed: file.isError }
}

function AttachmentView({ workspaceId, runId, attachment }: { workspaceId: string; runId: string; attachment: Attachment }) {
  const { address, failed } = useAttachmentAddress(workspaceId, runId, attachment)
  const isAnImage = attachment.contentType.startsWith('image/')

  if (failed) return <p className="text-destructive text-sm">Sofa couldn’t give {attachment.name}.</p>
  if (!address) return <Badge variant="secondary">Loading {attachment.name}…</Badge>

  return isAnImage ? (
    <figure className="flex flex-col gap-1">
      <img src={address} alt={attachment.name} className="max-h-[32rem] max-w-full rounded-lg border object-contain object-left" />
      <figcaption className="text-muted-foreground text-xs">{attachment.name}</figcaption>
    </figure>
  ) : (
    <a href={address} download={attachment.name} className="text-sm underline underline-offset-4">
      {attachment.name}
    </a>
  )
}
