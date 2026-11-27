export interface SetupForm {
  setupCode: string
  name: string
  email: string
  password: string
  workspaceName: string
  teamName: string
}

export interface SignInForm {
  email: string
  password: string
}

export interface Refusal<Field extends string> {
  status: number
  message: string
  problems: Partial<Record<Field, string>>
}

interface Problem {
  title?: string
  errors?: Record<string, string[]>
}

export async function isSetupNeeded() {
  const response = await fetch('/account/setup')
  if (!response.ok) {
    throw new Error(`Identity answered ${response.status} when asked whether Sofa is set up.`)
  }
  const { needed } = (await response.json()) as { needed: boolean }
  return needed
}

export async function isGitHubSignInAvailable() {
  const response = await fetch('/account/github')
  if (!response.ok) return false
  const { available } = (await response.json()) as { available: boolean }
  return available
}

export function gitHubSignInUrl(returnUrl: string | null) {
  const safe = safeReturnUrl(returnUrl)
  return safe === '/' ? '/account/github/sign-in' : `/account/github/sign-in?returnUrl=${encodeURIComponent(safe)}`
}

export function setUp(form: SetupForm) {
  return post<keyof SetupForm>('/account/setup', form)
}

export function signIn(form: SignInForm) {
  return post<keyof SignInForm>('/account/sign-in', form)
}

async function post<Field extends string>(path: string, body: object): Promise<Refusal<Field> | null> {
  const response = await fetch(path, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify(body),
  })
  if (response.ok) {
    return null
  }

  const problem = (await response.json().catch(() => ({}))) as Problem
  const problems = Object.fromEntries(
    Object.entries(problem.errors ?? {}).map(([field, messages]) => [field, messages.join(' ')]),
  ) as Partial<Record<Field, string>>
  return {
    status: response.status,
    message: problem.title ?? `Sofa answered ${response.status}.`,
    problems,
  }
}

const authorizationEndpoint = '/connect/authorize?'

export function safeReturnUrl(returnUrl: string | null) {
  return returnUrl?.startsWith(authorizationEndpoint) ? returnUrl : '/'
}
