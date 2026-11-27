# Each Run gets a fresh Worker container

Execution starts a new Worker container for every Run and removes it when the Run ends: through Docker on one box, and as Kubernetes Jobs in a cluster, behind one interface. Agents run code they wrote, so nothing a Run leaves behind (files, processes, credentials) may reach the next Run. A warm pool of ready containers may come later to cut start-up time, as long as each still serves one Run.

## Considered Options

- **Long-lived Workers that claim one Run after another**: faster starts and simpler scheduling, but every Run inherits whatever the previous one left behind.

## Consequences

- A subscription Worker (ADR 0009) needs its User's sign-in to outlive the container, so it is kept on a volume of that User's own, mounted only into their Runs.
