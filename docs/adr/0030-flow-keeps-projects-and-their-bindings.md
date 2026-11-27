# Flow keeps Projects and their Provider bindings

A Project, its key and its Flow live in Flow, together with the bindings to its Providers: the Connection, Repository and label its Requests come from, and the Connection and Repositories its code lives in. Flow already owns the Flow, its Phases and every Agent's configuration (ADR 0012), and Work Items are named by the Project's key, so the Project is where they meet. Intake and Code don't keep bindings of their own: when they start using them (milestone 1, slices 5 and 9) they learn a Project's bindings from Flow.

A Project's key is unique in its Workspace and never changes, since Work Items, branches and pull requests carry it. Creating a Project gives it milestone 1's Flow and its own default Agents, Triager, Lead and Builder, each registered with Identity as an actor owned by the Workspace's Team, so any User of the Team may delegate to them.

## Considered Options

- **Each binding in the service that uses it**, the Request Provider's in Intake and the Code Provider's in Code: each service owns all it reads, but a Project's settings would be spread over three services, each with its own database, before any of them uses them, and the web client would join them on every read.
- **A Projects service of its own**: a clear home, but a twelfth service whose only job is a few rows Flow reads on every Work Item.

## Consequences

- Flow checks a binding against Connections when it is made: the Connection must be the Workspace's, and its Repositories ones the Connection's installation reaches.
- Intake and Code will need Flow's bindings, by event or by reading over HTTP, which slices 5 and 9 settle.
