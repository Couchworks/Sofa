# Claude Code on the web is a Runner Sofa pushes to

A User may run their Runs in Anthropic's cloud on their own subscription, through Claude Code on the web: they create one claude.ai routine per model with an API trigger, fixed to the Repository and Sofa's connector, and give Sofa each routine's trigger id and token. Starting a Run fires the routine with the Brief through the documented trigger endpoint; the session reaches Sofa over MCP like any Run. Sofa holds only those trigger tokens, each able to fire one routine, encrypted in Connections, and never the User's claude.ai sign-in (ADR 0009). It is a personal Runner pushed through an adapter (ADR 0007), as Cronker's subscription Runner was.

## Considered Options

- **Only Sofa-hosted Workers for subscriptions**: more control and fewer moving parts, but the User hosts every Run, and Anthropic's own environment, the plainest use of a subscription, is left out.

## Consequences

- Sofa must be reachable over public HTTPS (a tunnel on one box), and its Agent Gateway must accept claude.ai connectors by OAuth (ADR 0015).
- Sofa can't see a session end: Runs report their transcript through a hook committed to each Repository, and a Run that calls nothing for a set time becomes lost, as in Cronker.
- A routine fixes its model, so a User sets up one per model their Agents use; creating them stays the User's own act in claude.ai.
- Local-only Requests never run on it.
