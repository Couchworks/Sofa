# Identity is its own OAuth and OpenID Connect server, with OpenIddict

Identity issues every token in Sofa itself, with OpenIddict as a library inside it: sign-in for web, desktop and mobile clients (authorization code with PKCE), the device code flow for pairing a User's own machine later, and short-lived tokens for each Run. Keeping it inside Identity means no separate auth server to run on one box, and an Apache-licensed library rather than a commercial one.

## Considered Options

- **Keycloak**: a full identity server with SAML and SSO for SaaS customers, but a second server, in Java, to run and upgrade beside Sofa.
- **Duende IdentityServer**: the best-known .NET option, commercially licensed above a revenue threshold.

## Consequences

- MCP clients such as claude.ai connectors register themselves by dynamic client registration or Client ID Metadata Documents. If OpenIddict can't serve that, Keycloak replaces it.
