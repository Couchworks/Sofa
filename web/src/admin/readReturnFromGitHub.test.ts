import { expect, test } from 'vitest'
import { readReturnFromGitHub } from './readReturnFromGitHub'

test('reads a created App', () => {
  expect(readReturnFromGitHub('?github-app=done')?.problem).toBeNull()
})

test('reads why an installation did not become a Connection', () => {
  expect(readReturnFromGitHub('?connection=failed&problem=GitHub%20doesn%E2%80%99t%20know%20it')).toEqual({
    title: 'The installation didn’t become a Connection',
    problem: 'GitHub doesn’t know it',
  })
})

test('reads nothing when Sofa was not opened from GitHub', () => {
  expect(readReturnFromGitHub('')).toBeNull()
})
