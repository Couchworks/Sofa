# Code links pull requests to Work Items by the key in their title or branch

Code turns GitHub's `pull_request` events into its own `PullRequestOpened`, `PullRequestMerged` and `PullRequestClosedUnmerged`, each naming the Work Item whose key, such as `DEMO-1`, is in the pull request's title, or else its head branch, for each Project whose Code Provider binds that Repository. Code keeps the pull requests it linked, and sends an event only when one's state changes, so a delivery heard twice sends one. Reading a Work Item's pull requests reads each from GitHub again, which also catches a change whose event was missed. Like Intake (ADR 0034), Code keeps its own copy of each Project's Code Provider binding from Flow's `ProjectBound` events, and changes Repositories only through GitHub's API (ADR 0019).

## Considered Options

- **Linking by the branch alone** (`sofa/<key>`): exact for Sofa's own pull requests, but a User's pull request for a Work Item, or one renamed, would never be linked.
- **Asking Flow for the Work Item** while handling each event: one place for Work Items, but GitHub's events would wait on Flow, and a key naming no Work Item is Flow's to judge anyway.

## Consequences

- A key that names no Work Item, such as one mentioned in passing, still makes an event; Flow ignores Work Items it doesn't have.
- A pull request naming two keys is linked to the first in its title.
