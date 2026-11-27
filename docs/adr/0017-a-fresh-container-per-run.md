# Each Run gets a fresh Worker container

Execution starts a new Worker container for every Run and removes it when the Run ends: through Docker on one box, and as Kubernetes Jobs in a cluster, behind one interface. Agents run code they wrote, so nothing a Run leaves behind (files, processes, credentials) may reach the next Run. A warm pool of ready containers may come later to cut start-up time, as long as each still serves one Run.

## Considered Options

- **Long-lived Workers that claim one Run after another**: faster starts and simpler scheduling, but every Run inherits whatever the previous one left behind.

## Consequences

- A subscription Worker (ADR 0009) needs its User's sign-in to outlive the container, so it is kept on a volume of that User's own, mounted only into their Runs.
- A prototype proved this on 2026-11-27 (branch `prototype/subscription-worker`): Claude Code signed in once on the User's volume ran Runs headless on the subscription in fresh containers, three at once included, each ending by `finish` with its own token. Still open there: whether two containers refreshing one sign-in at the same moment break it, and whether Runs extend a sign-in past its end (about four weeks after signing in).
- The Worker passes each Agent's model to Claude Code, which otherwise takes the account's default, and never runs a subscription's Run with `--bare`, which ignores a subscription's sign-in.
