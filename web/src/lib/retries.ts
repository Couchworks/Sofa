const mostRetries = 3

export function isWorthRetrying(failures: number, error: unknown) {
  const status = (error as { status?: unknown } | null | undefined)?.status
  const refusedForGood = typeof status === 'number' && status >= 400 && status < 500
  return !refusedForGood && failures < mostRetries
}
