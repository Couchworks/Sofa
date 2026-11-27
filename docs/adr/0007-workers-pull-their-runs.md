# Workers pull their Runs, whether Sofa hosts them or not

Every place that executes Runs is a Worker that claims its Runs from Sofa over one protocol, sends heartbeats, streams its transcript back and reports how the Run ended. The first Workers are containers or pods Sofa hosts itself, running a vendor's agent on a fresh checkout; a User's own machine follows on the same protocol, since Sofa can't reach a machine behind NAT that sleeps and changes networks (as Cronker found, its ADR 0014). Only outside agent services, such as Anthropic's Managed Agents, are started by Sofa pushing to them, each through an adapter. One protocol means one box and Kubernetes differ only in how many Workers run.

## Considered Options

- **Only outside services and users' machines**, never running agent code on Sofa's own infrastructure: less to operate, but every team Runner depends on one vendor's hosted service, and subscriptions couldn't run hosted at all.
- **Sofa pushes to each Worker**: the usual shape for servers, but a second protocol for users' machines, which can't be reached.

## Consequences

- Sofa runs agent code, so a hosted Worker isolates each Run from the others and from Sofa's own services and secrets.
