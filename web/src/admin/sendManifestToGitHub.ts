import type { components } from '@/api/schema'

export type GitHubAppCreationForm = components['schemas']['GitHubAppCreationForm']

export function sendManifestToGitHub({ formAction, manifest }: GitHubAppCreationForm) {
  const form = document.createElement('form')
  form.method = 'post'
  form.action = formAction
  form.hidden = true
  const field = document.createElement('input')
  field.type = 'hidden'
  field.name = 'manifest'
  field.value = manifest
  form.append(field)
  document.body.append(form)
  form.submit()
}
