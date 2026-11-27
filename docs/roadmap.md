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
- **Frontends.** The web client comes first; desktop and mobile follow, as clients of the same API.

## Milestones

Each is usable before the next starts.

1. **One agent, one Request.** A GitHub label brings a Request in; Triage, Spec and Plan run Solo and Implement opens a pull request to the main branch. Runs use Claude Code in a fresh Worker container on either an API key or the delegating User's own subscription (ADR 0009). Reports go back to the issue; Contributions and transcripts are kept in Records; a minimal web Inbox answers Questions and Approve Gates. Aspire, Identity, Connections (the GitHub App) and both Gateways as far as this needs.
2. **Many agents.** Panel, Synthesis, Discussion and Review-and-triage; Split into child Work Items; Integration branches and the merge queue; Findings.
3. **Validate.** Previews from Deployments, Showcase and the Validation guide, the Gated and Autonomous Validation modes, Stacks and accepting part of one, Corrections.
4. **Knowledge and Usage.** Reading and Publishing Knowledge; Usage.
5. **Later.** A User's own machine, Managed Agents, other vendors, Jira and Confluence.

Sofa isn't used to build itself yet; that starts once it runs on small real Projects.

## Open

- The web client's framework.
