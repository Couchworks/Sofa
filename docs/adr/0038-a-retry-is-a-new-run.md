# A retry is a new Run

When a Run fails or is lost, Execution retries it as a new Run with the same Brief, Agent and Work Item, linked to the one it retries, rather than starting the same Run again. Each attempt keeps its own Run token, Runner, Transcript and end, so the Working Record shows every attempt and why each ended, and a token issued for a failed attempt never works for the next. A Run is retried by Execution while its retries last (its retry limit, from the Run's Phase), then waits for a User to Retry it, which starts one more with the limit afresh. Retrying a Run Identity or a subscription refused is a User's choice only: retrying it unchanged would be refused again.

## Considered Options

- **One Run with attempts inside it**: one id for Flow to follow, but a Transcript and a token shared across attempts, and a Run whose state goes back from *failed* to *running*.

## Consequences

- Flow follows a Run's retries by `RunEnded`, which names the Run retrying it, if any.
