import { expect, test } from 'vitest'
import { isWorthRetrying } from './retries'

test('a refusal such as a missing Work Item is not asked again', () => {
  expect(isWorthRetrying(0, { status: 404, title: 'The Workspace has no such Work Item.' })).toBe(false)
  expect(isWorthRetrying(0, { status: 403 })).toBe(false)
})

test('a failure of Sofa or the network is asked again, three times at most', () => {
  expect(isWorthRetrying(0, { status: 502 })).toBe(true)
  expect(isWorthRetrying(2, new TypeError('Failed to fetch'))).toBe(true)
  expect(isWorthRetrying(3, { status: 503 })).toBe(false)
})
