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

1. **One agent, one Request.** *Built* (#1–#12): its demo ran end to end from the web client on real GitHub. Spec: `docs/specs/milestone-1.md`. A GitHub label brings a Request in; Triage, Spec and Plan run Solo and Implement opens a pull request to the main branch. Runs use Claude Code in a fresh Worker container on either an API key or the delegating User's own subscription (ADR 0009). Reports go back to the issue; Contributions and transcripts are kept in Records; a minimal web Inbox answers Questions and Approve Gates. Aspire, Identity, Connections (the GitHub App) and both Gateways as far as this needs.
1b. **Ready for real projects.** The Known gaps that stop Sofa on a small real Project: package registries, everyday shell commands, conflicting pull requests, the pull request's base, the last Report, a Track changed to Full, and an intermittent test. Spec: `docs/specs/milestone-1b.md`.
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
- **Work Items from before the Flow.** Work Items opened before slice 8 have no Phase and never start (from slice 8).
- **Building with packages.** Implement can't reach a package registry, so a Project whose build downloads packages can't be built or screenshotted in a Run (ADR 0050, from slice 8).
- **Implement's scripts can read its credentials.** `npm run` and `node` in Implement run code the agent may write, past the tool rules that keep it from the API key or sign-in (ADR 0050, from slice 8).
- **The pull request's base.** A Work Item finishes when any pull request naming it is merged, whatever branch it merged into (from slice 8).
- **The last Report is an edit.** The merge is reported by editing the Implement Report, which GitHub notifies nobody of (from slice 8).
- **Triage can't look for duplicates.** A read-only Run's token can't read issues and its Sandbox allows no `gh issue`, so Triage can't search the Repository's issues as its instructions ask (from slice 8).
- **Commands Claude Code asks about.** Shell loops, pipes into other commands, `VAR=… command` and `rm` ask for approval that no one gives, so a Run spends turns rephrasing them (from slice 8).
- **Two app hosts started together.** An app host refuses a Postgres volume a running container uses, but two started within seconds of each other both pass the check (ADR 0052).
- **A branch's migrations reach the shared installation.** Starting a branch that adds migrations on `sofa-postgres-data` leaves `main` running on a newer schema, unless the branch starts on a volume of its own (ADR 0052).
- **Live updates on one App Gateway.** Live updates reach the App Gateway on its one queue, so several replicas need a SignalR backplane and a queue each (ADR 0053, from slice 10).
- **A live connection outlives its access.** A live connection keeps the Workspace it was opened for until it reconnects, even once its User is removed or its token has expired (ADR 0053, from slice 10).
- **A Transcript read again, not pushed.** The web client reads a running Run's Transcript again every three seconds rather than hearing each line (ADR 0053, from slice 10).
- **A sign-in warning without a push.** A sign-in entering its last three days appears in the Inbox on the next read of it, not live, as nothing happens at that moment (from slice 10).
- **A Track changed to Full goes on.** Changing a Work Item to the Full Track after Spec was skipped doesn't send it back to Spec (from slice 10).
- **A pull request that can't merge.** When another merge leaves a Work Item's pull request conflicting, Sofa doesn't notice: the Work Item waits for it with nothing in the Inbox, and no Run resolves the conflict (from slice 10).
- **Who stopped a Run.** A Run a User stopped now reads "Stopped by a User", no longer naming who, since the Inbox showed a raw User id (from #12).
- **An intermittent integration test.** One run of the Run and Flow integration tests failed once and passed twice after; which test failed wasn't kept (from #12).
