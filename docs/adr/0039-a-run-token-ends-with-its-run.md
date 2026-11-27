# A Run token ends with its Run

The Agent Gateway serves a Run only while Execution says it is *running*: on every call to its MCP server it asks Execution for the Run its token names, over HTTP, and refuses the call once the Run has ended, whatever the token's own expiry (ADR 0025). Execution is then the one place that says whether a Run may still act, and a token copied out of an ended Run's container is worth nothing.

## Considered Options

- **Revoking each token at Identity when the Run ends**: tokens refused everywhere, but the Agent Gateway checks tokens itself (ADR 0025) and would have to ask Identity on every call instead, for the same round trip.
- **The Agent Gateway keeping the ended Runs from Execution's events**: no call per request, but a gap between a Run ending and the Gateway hearing it, and state to rebuild when it restarts.

## Consequences

- Each MCP call waits on Execution as well as the Agent Gateway.
