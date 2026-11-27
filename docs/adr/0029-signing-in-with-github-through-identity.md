# Signing in with GitHub goes through Identity, and Connections trades the code

Signing in with GitHub uses the GitHub App's own OAuth (user-to-server), not a separate OAuth App, so there is one App to create and its callback, `/account/github/callback`, is in its manifest. Identity runs the flow, under `/account/github/…`, which the App Gateway already forwards to it: it keeps the state (hashed, single-use, ten minutes, tied to the browser by a cookie scoped to `/account/github`), and on return asks Connections over HTTP to trade GitHub's code for the account: its id, login, name and verified primary email. Connections holds the App's client secret, so the secret and the user's GitHub token never leave it, and the token is dropped once read. Identity then signs the User in with its own sign-in cookie and carries on the web client's authorization request.

A GitHub account signs in as the User it is linked to. At its first sign-in it is linked to the User an Admin added by that GitHub login, or else to the User whose email is the account's verified primary email; an account no User has is refused. When someone an Admin added by login turns out to have a User by email already, they are one person: the added User's Workspaces move to the User of the email, which takes the account. A signed-in User links their account from the web client through the same callback; the App Gateway asks Identity to start it and sets the state cookie.

## Considered Options

- **Identity holding the client secret**: no call to Connections, but a second service holding one of the App's credentials, against ADR 0012.
- **Trading the code by a command and reply on the broker**: the usual way to change another service's state, but this only reads, and the code, good for a user token for ten minutes, would rest in two outbox and inbox tables (as ADR 0027 argued for tokens).
- **Matching added Users by GitHub id**: safe against a login being renamed and reused before the first sign-in, but needs GitHub's API to look the login up when it is added. Logins are matched for now.

## Consequences

- Identity reaches Connections over the internal network, so the app host references it.
- The App asks GitHub for its users' email addresses (`emails: read`). An App created before this needs that permission and the callback URL added by hand (see the README).
