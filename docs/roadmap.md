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
- **Frontends.** The web client comes first; desktop and mobile follow, as clients of the same API (ADR 0020).
- **Web client libraries.** shadcn/ui with Tailwind for components, TanStack Query beside the generated API client, TanStack Table for lists, React Hook Form for forms. A diff viewer is chosen when code review's screens are.

## Milestones

Each is usable before the next starts.

1. **One agent, one Request.** Spec: `docs/specs/milestone-1.md`. A GitHub label brings a Request in; Triage, Spec and Plan run Solo and Implement opens a pull request to the main branch. Runs use Claude Code in a fresh Worker container on either an API key or the delegating User's own subscription (ADR 0009). Reports go back to the issue; Contributions and transcripts are kept in Records; a minimal web Inbox answers Questions and Approve Gates. Aspire, Identity, Connections (the GitHub App) and both Gateways as far as this needs.
2. **Claude Code on the web.** A personal Runner firing a User's claude.ai routines, one per model (ADR 0021); Sofa reachable over public HTTPS with OAuth for MCP connectors; Local-only Requests kept off it.
3. **Many agents.** Panel, Synthesis, Discussion and Review-and-triage; Split into child Work Items; Integration branches and the merge queue; Findings.
4. **Validate.** Previews from Deployments, Showcase and the Validation guide, the Gated and Autonomous Validation modes, Stacks and accepting part of one, Corrections.
5. **Knowledge and Usage.** Reading and Publishing Knowledge; Usage.
6. **Later.** A User's own machine, Managed Agents, other vendors, Jira and Confluence.

Milestone 1 also warns a User in their Inbox before their subscription's sign-in on a Worker ends, until the prototype shows whether Runs keep it alive.

Sofa isn't used to build itself yet; that starts once it runs on small real Projects.

## Known gaps

Small things left behind by merged slices, waiting for a slice of their own or a `fix:`.

- **Unclaimed API keys.** An API key saved for a Runner that Execution then refuses stays sealed in Connections, never read (from #5). Connections should remove a key no Runner claimed, when the Runner is refused and in a periodic sweep.
- **Removing a binding.** Flow can't remove a Project's Request Provider binding yet; Intake's side is unit-tested only (from #6).
- **A Report posted twice.** A crash between GitHub accepting a Report's comment and Intake saving its id posts it again on retry (from #6).
- **An issue in several Projects.** An issue matching the bindings of several Projects becomes a Request in each (from #6).
- **Delegation fixed at labelling.** A Request's delegating User is whoever labelled it, and can't be changed afterwards (from #6).
- **One Execution per box.** Execution runs as a single copy; running several needs claiming Runs safely (from #7).
- **Repository tokens past an hour.** A Run's installation token expires after an hour while a Run may last two; a running Run should get a fresh one (from slice 9).
- **A caught push ends every Run that could have made it.** GitHub's push event names only Sofa's App, so every running Run able to push to that Repository ends (from slice 9).
- **Deleting a Transcript.** A secret the scrubbing missed stays until a User can delete the Transcript (from slice 9).
- **Linux hosts.** The Worker network is tested on Docker Desktop only; on Linux, depending on Docker's version, the box's address on an internal network's bridge may still answer a Worker (from slice 9).
- **A symlink race on attachments.** For Runs allowed to write files, a file can be swapped for a link between the path check and the copy out of the container (from #8).
- **A Contribution of a stopped Run.** A Run stopped between `finish` and its end can keep its Contribution (from #8).
- **A GitHub call per read.** Every `read_work_item` reads the issue from GitHub, spending the installation's rate limit (from #8).
- **Open endpoints on the box.** Connections' and Code's token endpoints answer any caller on the box; running on several machines needs service-to-service authentication (from #9).
- **Leftover relays.** If Execution is killed, a Run's relay container and networks stay behind until removed by hand (from #9).
- **Protecting the main branch.** A push outside its Sandbox stands for a few seconds before Code puts it back, so a Project's Repositories should still protect their main branch (from #9).
- **A subscription's refresh untested.** The host Claude Code refreshes a subscription's sign-in through was allowed from reading its binary; no Run has refreshed one yet (from #9).
