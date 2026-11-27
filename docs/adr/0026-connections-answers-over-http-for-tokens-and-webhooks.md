# Connections hands out tokens and takes GitHub's webhooks over HTTP, not through the broker

Two things pass between Connections and other services over HTTP, although ADR 0012 sends changes through the broker. Installation tokens are handed to Intake and Code in the answer to an HTTP request, so a token never rests in an outbox or inbox table, where it would sit unencrypted until the table is cleaned. GitHub's webhooks are passed on by the App Gateway's reverse proxy to Connections, which checks each against the App's webhook secret before answering, so GitHub sees a refused delivery refused at once, and the secret never leaves Connections. Commands that change what Connections keeps, such as creating the App or adding a Connection, still go through the broker.

## Considered Options

- **Tokens as replies on the broker**: one way of talking for everything, but every token issued would be written to Postgres twice, in the sender's outbox and the receiver's inbox.
- **The App Gateway checks webhooks itself**: one hop fewer, but the webhook secret would leave Connections, and the App Gateway would hold a credential of its own.
- **Webhooks sent on as commands and answered by reply**: no HTTP between services, but every delivery written to two tables before GitHub hears back, and a slow broker would time GitHub out.

## Consequences

- Connections serves HTTP only inside Sofa's network; the App Gateway is the one way in from outside.
