# A push outside a Run's own branch is caught and undone

A Run's Repository token comes from Code, which asks Connections for an installation token for only its Project's Repositories, with only what its Sandbox allows: read-only gets `contents`, `pull_requests` and `metadata` to read; pushing its own branch gets `contents` and `pull_requests` to write. A Run's own branch is its Work Item's, `sofa/<key>` (`sofa/test-run-<run>` for a test Run), so its retries push the same branch. GitHub can't limit a token to one branch, so Code catches what the token allows beyond it: it keeps a grant for each token it hands out, and judges every `push` event made by Sofa's own GitHub App. A push to a ref that is no running Run's own branch in that Repository is undone through GitHub's API (a new ref deleted, a moved one put back, a deleted one made again), and `PushOutsideItsSandboxCaught` names every running Run that could have made it; Execution ends each as *refused*, *left its Sandbox*, so it waits for a User rather than being retried. Code remembers what it undid, so GitHub's event for its own undoing isn't caught again.

## Considered Options

- **A ruleset on each Repository** allowing Sofa's App only `sofa/*`: prevention instead of repair, but it needs the App to administer Repositories, and every Run pushes as the same App, so a ruleset can't tell one Run's branch from another's.
- **A git proxy in the Agent Gateway** that reads each push's ref updates: exact per Run, but the token would still reach GitHub's API, where the contents and refs endpoints push without git.

## Consequences

- A forbidden push lands for the seconds until Code hears of it: checks and workflows on that branch may start. A Repository should still protect its main branch.
- When several Runs could push to a Repository, all of them end, as the App's event can't say which one pushed.
- An installation token lasts an hour; a Run running longer loses its access to GitHub, until tokens are refreshed for a running Run.
- Code hands out Repository tokens over HTTP inside Sofa's network, as Connections does (ADR 0027); Workers can't reach it (ADR 0045).
