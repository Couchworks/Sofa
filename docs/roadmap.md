# Roadmap

Vocabulary follows `CONTEXT.md`; hard-to-reverse decisions are in `docs/adr/`.

## Settled

Choices that aren't ADRs but shouldn't be reopened without reason.

- **API.** REST with OpenAPI from ASP.NET Core; each frontend's client is generated from it.
- **Gateways.** YARP for both the App Gateway and the Agent Gateway; the official C# MCP SDK serves MCP on the Agent Gateway.
- **Live updates.** SignalR on the App Gateway, with a backplane once it runs as several replicas.
- **Observability.** OpenTelemetry in every service.
- **Secrets.** Connections encrypts what it holds with envelope encryption behind an interface: a key from configuration on one box, a key vault later.
- **First Runners.** A Sofa-hosted Worker running Claude Code on an API key, and one running Claude Code on a User's own subscription. A User's own machine, Managed Agents and other vendors come after.
