# GitHub's returns to Sofa carry a signed, single-use state

GitHub sends the browser back to the App Gateway after creating or installing Sofa's App (`/github-app/created`, `/github-app/installed`), with no bearer token, since the web client keeps its tokens to itself (ADR 0025). So pressing **Create** or **Install** mints a `state` that GitHub hands back: the App Gateway signs it with HMAC-SHA256 under a key from configuration, and it names what it is for, the Admin and their Workspace, a random nonce and when it expires (30 minutes). The same response sets a cookie holding the nonce, scoped to `/github-app`. A return is refused unless its state is signed, unexpired, for that return, and in the browser holding the cookie, and unless whoever started it is still an Admin of the Workspace. Connections keeps every nonce it has acted on until it expires and refuses one it has seen, so a state works once.

## Considered Options

- **A random state kept by Connections**, as slice 3 did for creating the App: nothing to sign, but the App Gateway would have to ask Connections who a return belongs to before it could check them, and installations had no state at all.
- **ASP.NET Data Protection in the App Gateway**: no key to configure, but its keys live on the gateway's own disk, so a second gateway or a fresh container couldn't read a state the first one made.
- **The state alone, without the cookie**: one moving part fewer, but an Admin of another Workspace could hand someone a link with their own state and have that person's installation become their Connection.

## Consequences

- The signing key is an app host parameter (`app-gateway-github-return-signing-key`), generated once; changing it only fails returns already under way.
- GitHub's own return after someone changes an installation (`setup_action=update`, with no state) changes nothing in Sofa, which hears of the change through webhooks, so it lands back in the web client with a note. Any other return without a valid state, such as a new installation started on GitHub, is refused and says to start again from Sofa.
