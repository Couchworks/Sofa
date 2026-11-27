type Return = { title: string; problem: string | null }

const successes: Record<string, string> = {
  'github-app': 'Sofa’s GitHub App is created. Install it on an organisation or account to add a Connection.',
  connection: 'The installation is now a Connection of this Workspace.',
  'github-link': 'Your GitHub account is linked. You can sign in with GitHub from now on.',
}

const failures: Record<string, string> = {
  'github-app': 'The GitHub App wasn’t created',
  connection: 'The installation didn’t become a Connection',
  'github-link': 'Your GitHub account wasn’t linked',
}

export function readReturnFromGitHub(search: string): Return | null {
  const query = new URLSearchParams(search)
  for (const what of Object.keys(successes)) {
    const outcome = query.get(what)
    if (outcome === 'done') return { title: successes[what], problem: null }
    if (outcome === 'failed') return { title: failures[what], problem: query.get('problem') }
  }
  return null
}
