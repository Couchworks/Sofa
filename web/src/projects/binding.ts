export type Binding = {
  connectionId: string
  requestRepository: string
  label: string
  codeRepositories: string[]
  keepsIssueState: boolean
}

export const noBinding: Binding = { connectionId: '', requestRepository: '', label: 'sofa', codeRepositories: [], keepsIssueState: false }
