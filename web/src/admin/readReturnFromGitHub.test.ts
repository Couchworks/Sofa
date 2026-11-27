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

test('reads why a GitHub account was not linked', () => {
  expect(readReturnFromGitHub('?github-link=failed&problem=It%20belongs%20to%20another%20User')).toEqual({
    title: 'Your GitHub account wasn’t linked',
    problem: 'It belongs to another User',
  })
})

test('reads GitHub’s own return after an installation changed as a note, not a failure', () => {
  expect(readReturnFromGitHub('?connection=updated')).toEqual({
    title: 'The installation was changed on GitHub. Sofa follows it through GitHub’s events.',
    problem: null,
  })
})
