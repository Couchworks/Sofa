# The services and their boundaries

The backend is split, from the start, into services each owning its own data and one reason to change:

| Service | Owns |
|---|---|
| **App Gateway** | The public API for web, desktop and mobile clients, live updates, and incoming webhooks from outside systems, passed to Connections |
| **Agent Gateway** | The MCP server agents use and the Workers' claim protocol; the only thing Workers can reach |
| **Identity** | Workspaces, Teams, Users, access, tokens, and every Agent as an actor: its id, name, owner and tokens |
| **Connections** | Each Workspace's Connections to outside systems and the credentials Sofa holds, encrypted: outside sign-ins, app installations and team Runners' API keys. It hands short-lived tokens to the services that need them and turns incoming webhooks into events |
| **Intake** | Bringing Requests in from Request Providers and posting Reports back |
| **Flow** | Work Items, Flows, Phases, Patterns, Gates, Blocks and Stacks, Delegation, and every Agent's configuration: its role, instructions, vendor and model |
| **Execution** | Runners, the queue of Runs and their limits, retries, Run states, and the claim protocol behind the Agent Gateway |
| **Records** | The Working Record: Contributions, transcripts, screenshots and proposals |
| **Code** | Code Providers, Integration branches, stacking and rebasing, the merge queue, and learning Previews' addresses |
| **Knowledge** | Reading a team's existing Knowledge for Runs, and Publishing what Users accept |
| **Notices** | Each User's Inbox and its notifications, and Usage, until either grows into a service of its own |

Workers are a container image, not a service: they run agent code, so they hold no database and no way into the internal network, only the Agent Gateway.

Services change one another's state only through commands and events on a message broker, each publishing through an outbox so a crash loses nothing; they read one another's current state over HTTP only where they need it at that moment. Each owns its own data: on one box, a schema each in one database, which can become separate databases later.

## Considered Options

- **Agents wholly in Flow**: Flow would need no other service to start Runs, but every service naming who did something would ask the busiest one, and tokens would be issued for actors Identity doesn't know. **Wholly in Identity**: one place for every actor, but prompts and model choices, which change often, would live in the service that should change least. Splitting the actor from its configuration keeps both small; creating an Agent touches both.
- **Knowledge inside Intake**: both talk to outside systems, often the same vendor's, but incoming work and a team's knowledge differ in data, triggers and rules. What they share, the sign-ins and API clients, lives in Connections and a shared client library instead.
- **Each service keeping its own outside credentials**: no Connections service, but one GitHub sign-in copied into three services, three places to rotate it, and no single receiver for GitHub's one stream of webhooks.
- **One gateway**: less to run, but agents and people would share one surface to lock down and scale.

## Consequences

- Subscription sign-ins are never among the credentials Connections holds; they stay on their Worker (ADR 0009).
- Which message broker, database and local orchestration tooling are used is decided separately.
