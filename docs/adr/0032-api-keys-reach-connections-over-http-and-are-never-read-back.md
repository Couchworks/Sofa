# API keys reach Connections over HTTP and are never read back

A Runner's API key goes from the App Gateway to Connections in the body of an HTTP request, which Connections seals with envelope encryption (bound to the key's id) before it answers with the id alone; Execution keeps that id on the Runner. No API of Sofa's returns an API key once saved: the web client shows that a Runner has one, never what it is. Only a Run will open it, through Connections, when Execution starts it (milestone 1, slice 6). This adds to ADR 0027: a command carrying the key over the broker would leave it unencrypted in the App Gateway's outbox and Connections' inbox until they are cleaned.

## Considered Options

- **A command on the broker**, like every other change to what Connections keeps: one way of talking, but the plaintext key written to Postgres twice.
- **Execution keeping the key**: no call to Connections, but a second service holding credentials, against ADR 0012.

## Consequences

- If Execution refuses the Runner after the key is saved, the sealed key stays in Connections unused; nothing can read it back.
- Replacing a key means saving a new one; the old one is never shown to compare.
