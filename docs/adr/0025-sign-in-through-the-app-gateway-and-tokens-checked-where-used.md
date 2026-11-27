# Sign-in goes through the App Gateway, and each service checks tokens itself

Identity's OAuth endpoints (`/connect/…`, `/.well-known/…`) and its account endpoints (`/account/…`: setup and signing in) are reached only through the App Gateway, which forwards them unchanged, so the web client, its sign-in and its API share Sofa's one public address, and Identity stays on the internal network. The sign-in page is the web client's own (ADR 0020): it posts the email and password to Identity, which keeps a cookie scoped to `/connect` for its authorization endpoint only; the web client then holds the tokens from the authorization code flow with PKCE.

Access tokens are signed, unencrypted JWTs, each for one audience: `app-gateway` for a User, `agent-gateway` for a Run. The services that accept them check them themselves, against the keys in Identity's discovery document, which they read over the internal network, so no request waits on Identity. Identity's signing, encryption and Data Protection keys are kept in its own schema, so a restart signs nobody out.

A Run token is issued to Execution, a confidential client, at the token endpoint by the client credentials grant, naming the Run and its Agent; it is never sent over the broker, whose outboxes would keep it in the database.

## Considered Options

- **Introspection on every request**: tokens revocable at once, but every call through a gateway waits on Identity.
- **A backend for the web client** in the App Gateway, holding the tokens behind a cookie: no tokens in the browser, but the App Gateway becomes a second OAuth client with its own sessions, and the desktop and mobile clients still need the code flow.
- **A sign-in page rendered by Identity**: no account endpoints to forward, but a second frontend technology.
- **Run tokens by a command and its reply on the broker**: like every other request between services, but the token would sit in Execution's and Identity's durable inbox and outbox tables.

## Consequences

- A token stays valid until it expires even if its User or Run ends sooner: an hour for a User's access token, twelve hours for a Run's. Revoking a Run's token when the Run ends waits for Execution's Run states (milestone 1, slice 6).
- The issuer is Sofa's public address, which Identity is told; the redirect addresses of the web client are its public origins.
- The account endpoints are not in the App Gateway's OpenAPI description, so the web client types its two calls to them by hand.
