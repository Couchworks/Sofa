# Milestone 1: One agent, one Request

**Status:** agreed. Vocabulary follows `CONTEXT.md`; decisions are in `docs/adr/`.

## Goal

A User labels a GitHub issue, and Sofa takes it from there: one Agent per Phase triages it, writes a spec, plans it and implements it as a pull request, asking the User only where a Gate or a Question says so, on the User's own subscription or an API key. Everything each Run did can be looked at afterwards.

## The demo

1. In a small test Repository, Ada labels issue #12, *Add a dark mode toggle*, with `sofa`.
2. Sofa brings it in as a Request and makes its root Work Item, `DEMO-1`, delegated by Ada. Triage picks the Full Track and posts nothing.
3. Spec's Lead asks one Question: *Should the toggle follow the system setting by default?* It appears in Ada's Inbox and as Sofa's comment on the issue. Ada answers by replying on the issue.
4. A new Spec Run writes the spec. It waits for Ada's approval in the Inbox, and the spec's summary is posted to the issue as a Report.
5. Ada approves. Plan's Lead writes the plan, which waits for approval too, and is reported.
6. Ada approves. Implement's Builder opens a pull request to the main branch, naming `DEMO-1`, with a screenshot of the toggle in its Contribution.
7. Ada reviews and merges the pull request on GitHub. Sofa sees the merge, finishes `DEMO-1`, posts a last Report and closes the issue.
8. On `DEMO-1`'s page Ada opens every Run: its Brief, Contribution and Transcript.

The demo runs once with Ada's subscription Runner and once with a team API key Runner.

## In scope

- The default Flow (`docs/default-flow.md`) cut down to **Triage → Spec → Plan → Implement**, every Phase Solo, with both Tracks: Small skips Spec and leaves Plan's Gate at Review afterwards.
- Gates (Auto, Approve, Review afterwards), Questions and their answers in the Inbox or on the issue.
- Implement's pull request targets the main branch directly; the User reviews and merges it on GitHub.
- Runners: a personal subscription Runner, a personal API key Runner and a team API key Runner, each with a limit on Runs at once.
- Workers: a fresh Docker container per Run (ADR 0017) running Claude Code, with the read-only and own-branch Sandboxes.
- One Workspace with one Team is enough to demo, but every record carries its Workspace (ADR 0005).
- The web client: sign-in, the Inbox, a Work Item's page with its Runs, Projects, Agents and Runners.

## Out of scope

Kept for later milestones: Claude Code on the web (2); Panel, Synthesis, Discussion, Review, Split, child Work Items, Integration branches, the merge queue and Findings (3); Previews, Showcase, Validate, Stacks and Corrections (4); Knowledge and Usage (5); Kubernetes, a User's own machine, Managed Agents, other vendors, Jira and Confluence (6). Also left out: Teams beyond one per Workspace, desktop and mobile clients, Local-only (nothing in this milestone runs off the box).

## Slices

Each slice is built and shown working before the next one that depends on it. Slices 2 and 3 can go side by side, and so can 6 and 7.

### 1. Skeleton

- One .NET 11 solution: the eleven services of ADR 0012 as empty ASP.NET Core projects, a shared library for contracts, and an Aspire app host starting them with Postgres and RabbitMQ (ADRs 0013, 0014, 0016).
- Wolverine wired into every service, with its outbox in the service's own schema; one test message travels from Flow to Execution and back.
- The web client: Vite, React, TypeScript, Tailwind and shadcn/ui, started by the app host, with an API client generated from the App Gateway's OpenAPI description.
- GitHub Actions builds and tests both on every pull request.

**Shown when:** one command starts everything, Aspire's dashboard shows every service healthy, and the web client loads through the App Gateway.

### 2. Identity and sign-in

- Workspaces, Teams, Users; the first User created with a one-time setup code printed in the log, as in Cronker.
- OpenIddict in Identity (ADR 0015): the web client signs in with authorization code and PKCE, by email and password.
- Signing in with GitHub, through the GitHub App of slice 3, which also links the User's GitHub account to them.
- Agents as actors (id, name, owner) in Identity; tokens for a Run, issued to Execution, valid for that Run only.

**Shown when:** the first User signs in to the web client, links their GitHub account, and a Run token is issued and checked by the Agent Gateway.

### 3. Connections and the GitHub App

- The App created through GitHub's manifest flow from the web client; its credentials kept by Connections with envelope encryption (key from configuration).
- Each installation of the App on an organisation or account is a Connection of the Workspace.
- GitHub's webhooks arrive at the App Gateway, are checked against the App's secret, and become events on the broker.
- Short-lived installation tokens handed to Intake and Code on request.

**Shown when:** an Admin creates and installs the App, the Connection shows in the web client, and labelling an issue shows its event in the dashboard.

### 4. Projects, Agents and Runners

- Projects with a key (such as `DEMO`), bound to a Request Provider (a Repository and its label) and a Code Provider (Repositories) through a Connection.
- Every Project starts with milestone 1's Flow and its default Agents: Triager (Haiku), Lead (Opus), Builder (Opus). Agents' configuration (role, instructions, vendor, model) lives in Flow; editing it is a form in the web client.
- Runners in Execution: a personal subscription Runner (its Worker volume and its sign-in state, never the sign-in itself), personal and team API key Runners (keys held by Connections), each with its limit on Runs at once.
- Signing a subscription Runner in, from a terminal on the box (see Settled).

**Shown when:** a Project is bound to the test Repository, and the User has one Runner of each kind.

### 5. Intake

- A labelled issue in a bound Repository becomes a Request and its root Work Item, keyed `<PROJECT>-<n>`; the User whose linked account added the label is its delegating User, and a label by anyone else puts it in the Project's Admins' Inbox.
- Reports: one comment per Phase that reports, a summary with the detail collapsed beneath and a link into Sofa.
- Answers to Questions given in reply on the issue, only from accounts linked to Users with access to the Project.
- Keeping the issue's state when the Project turns it on: labels for the Phase, and closing the issue when its work reaches the main branch.

**Shown when:** labelling an issue makes a Work Item visible in the web client, and a test Report appears on the issue.

### 6. Execution and Workers

- The queue: Runs wait for their Runner's limit, highest Priority first; a Run spends the delegating User's personal Runner for its vendor, else their Team's (ADR 0009).
- Each Run gets a fresh container (ADR 0017) with its Sandbox: a token for its Repositories from Code (read-only, or allowed to push its own branch), its Run token, and nothing else of Sofa's. A subscription Run also mounts its User's volume.
- Claude Code runs headless with the Agent's model (`--model`), never `--bare` on a subscription, with Sofa's MCP server and only that Sandbox's tools allowed.
- Run states running, finished, failed, lost, stopped and refused; a failed Run is retried up to its limit, then waits for a User with Retry.
- The Transcript streams to Records as the Run goes.

**Shown when:** a Run of a test Brief finishes on each kind of Runner, two Runs beyond a limit of one run one after the other, and a killed container ends as failed and is retried.

### 7. The Agent Gateway's MCP tools

- `read_work_item`: the Work Item, its Request, earlier Contributions, Questions and their answers.
- `ask`: ends the Run with a Question.
- `finish`: ends the Run with an Outcome and its Contribution, with attachments such as screenshots.
- The Brief names these tools, so Runs don't spend a turn finding them.

**Shown when:** the prototype's Brief runs against the real Gateway instead of `gateway.mjs`.

### 8. Flow

- Work Items going Triage → Spec → Plan → Implement by their Runs' Outcomes; Tracks; Gates; Questions starting a new Run of their Phase once answered.
- Briefs: Sofa's preamble, the Phase's instructions for the Agent's role, the Work Item's key.
- Implement's Builder pushes its branch and opens a pull request to the main branch with the Work Item's key in its title; Code reports it.
- The Work Item finishes when Code reports its pull request merged; a pull request closed unmerged sends it to its delegating User's Inbox.
- Screenshots: the Worker image gains a headless browser, and Implement's Sandbox lets its Run build and start the Project's software and screenshot it, so the Builder attaches a PNG of what it changed to its Contribution (step 6 of the demo). Where the browser runs and what the Sandbox allows gets an ADR.

**Shown when:** the demo runs end to end, driven through the API, with a real screenshot attached by Implement.

### 9. Records and Code

- Records keeps Contributions and Transcripts, converted to one format with known secret patterns scrubbed before storing, as Cronker does (its ADR 0006), and files on local disk behind the storage interface.
- Code hands out Repository tokens scoped to a Sandbox, reads pull requests, and turns GitHub's pull request events into events of its own; it never keeps a working copy (ADR 0019).

**Shown when:** a finished Run's Transcript and screenshot are readable through the API, with a test secret scrubbed.

### 10. The web client

- The Inbox: Questions to answer, Gates to approve (the spec or plan shown in full, approved or sent back with a comment), Runs gone wrong with Retry, Requests waiting for a delegating User, and a warning before a subscription's sign-in ends.
- A Work Item's page: its Phases, each Run with its Brief, Contribution and Transcript, and its pull request.
- Projects, Agents and Runners pages for slice 4's settings.
- Live updates over SignalR, so the Inbox and a Work Item's page change as Runs do.

**Shown when:** the demo runs end to end from the web client.

## Settled

1. **Signing a subscription Runner in.** Relaying the sign-in through Sofa's web client (showing the link, taking the code back) may count as Sofa intermediating it, which Anthropic's terms forbid. So in this milestone a User signs in from a terminal on the box (`sofa runner login <runner>`); a way through the web client waits until it is checked against the terms.
2. **Who reviews code.** Without a Review Phase, the User reviews Implement's pull request on GitHub.
3. **Repository layout.** One repository: `src/` for the services and shared libraries, `web/` for the web client, `worker/` for the Worker image, `docs/` as now.
4. **Tests.** Unit tests per service; integration tests through Aspire's test host with real Postgres and RabbitMQ; one end-to-end test of the demo against a stub Runner that plays back recorded Runs, so CI spends no tokens.
