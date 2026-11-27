# Sofa

A software factory where AI agents take a Request through a Flow of Phases on their own, while every step stays open to human review, at the time or afterwards. The user doesn't wait for the agents; the agents wait for the user only where a Gate says so.

## Language

### Structure

**Workspace**:
The tenant: a group of Users and Teams sharing Projects, Agents and Runners. Nothing crosses Workspaces.
_Avoid_: Organization, tenant (in the product), account

**Team**:
A group of Users within a Workspace that shares Projects, Agents and Runners.
_Avoid_: Group, squad, department

**Project**:
The container of work for one product or set of Repositories, with its Flow and its Providers.
_Avoid_: Board, space, product

**User**:
A human who signs in to Sofa and belongs to one or more Workspaces.
_Avoid_: Person, member, account

### Work

**Request**:
A ticket in an outside system, such as a GitHub issue or a Jira issue, brought in through a Provider when a User marks it for Sofa (such as with a label) or imports it. The User who marked it is its delegating User, when their outside account is linked to one. Sofa reads it and posts Reports back to it, and may keep its state there, such as labels for the Phase it is in; it never holds Sofa's working state.
_Avoid_: Ticket (for the outside one), issue, external ticket

**Work Item**:
Sofa's own unit of work. A Request becomes one root Work Item, which may be split into child Work Items, each run through the Flow on its own and possibly blocking others.
_Avoid_: Ticket, task, job, internal ticket

**Flow**:
The ordered Phases a Project's Work Items go through, such as Spec, Plan, Implement, Review, Integrate and Validate.
_Avoid_: Pipeline, workflow, board

**Phase**:
One stage of a Flow: a Pattern, the Agents cast in it, its Gate and its Sandbox.
_Avoid_: Step, stage, column

**Pattern**:
The shape in which Agents work together inside a Phase, chosen from a fixed set: Solo, Panel, Synthesis, Discussion and Review-and-triage. A Pattern goes on only once every Run it asks for has made its Contribution: Runs waiting for a Runner run later, one after another if need be, and a failed Run is retried up to its retry limit, after which the Work Item waits for a User.
_Avoid_: Strategy, template, mode

**Track**:
How much of the Flow a Request goes through, chosen by Triage and changed by a User at will: **Small** (no Spec, a plan by one Agent, no Approve Gate on it) or **Full** (every Phase).
_Avoid_: Size, lane, mode

**Split**:
Sofa making the child Work Items, and the Blocks between them, that an approved plan lists. It is no agent's work: the plan's synthesis lists them, and approving the plan makes them.
_Avoid_: Breakdown, decomposition, sub-tasking

**Finding**:
One issue a Review Run raises on a Work Item's work, settled by the Review's triage as **fix** (the Work Item goes back to Implement, as a round), **follow-up** (new work: a Work Item in Sofa under the Autonomous Validation mode, or else a new Request filed in the Request Provider, marked as waiting for a User, as the Project sets) or **dismiss** (with the reason kept).
_Avoid_: Comment, issue, remark, defect

**Validation guide**:
What the Showcase Phase writes for Validation: what changed, where to look in the Preview, a screenshot per acceptance criterion of the spec, and links to the code.
_Avoid_: Demo, release notes, walkthrough

**Gate**:
What a Phase does when it ends: **Auto** (go straight on), **Approve** (wait for a User before going on) or **Review afterwards** (go on, and leave the result Unverified until a User looks).
_Avoid_: Checkpoint, approval step, blocker

**Validation**:
A User's judgement on finished work in its Preview, with the code that made it: **Accepted**, or not, in which case they file a Correction. Accepting a Request accepts every Request of its Stack not yet accepted, which then reach the main branch in order; accepting one lower in a Stack leaves those above it waiting. Validation never re-runs finished work.
_Avoid_: Approval, sign-off, QA, verification

**Blocks**:
How one Request relates to another whose work needs its own: the blocked Request's Integration branch starts from those of every Request blocking it, and its work never reaches the main branch before theirs. A User sets it by hand, or an Agent adds it, such as from a Spec or Plan Phase; only a User removes one.
_Avoid_: Depends on, dependency, prerequisite

**Stack**:
A Request together with every Request that blocks it, directly or through others.
_Avoid_: Chain, train, batch

**Correction**:
A Request a User files after not accepting work, saying what to change. It is blocked by the Request it corrects, so it builds on that work rather than redoing it.
_Avoid_: Fix, rework, reopen

**Validation mode**:
How a Project's finished work reaches its main branch: **Autonomous** (it merges when its Flow ends, and waits Unverified for Validation afterwards) or **Gated** (it waits on its Integration branch until a User Accepts it). A Request may override its Project's mode.
_Avoid_: Autonomy level, auto mode

**Integration branch**:
The branch where a Request's Work Items come together before they reach the main branch, with a Preview of its own.
_Avoid_: Feature branch, staging branch

**Question**:
What a Run asks a User when it can't go on, ending the Run. The answer, given in Sofa's Inbox or in reply to Sofa's comment on the Request by someone linked to a User with access to the Project, starts a new Run of the same Phase. Runs never wait for an answer.
_Avoid_: Prompt, clarification, blocker

**Report**:
What Sofa posts back to a Request when a Phase ends: a summary, such as the plan agreed or the decisions made, with a link to the full detail.
_Avoid_: Update, sync, comment (for the whole thing)

### Agents and Runs

**Agent**:
A configured AI participant: a role and its instructions, and the vendor's agent and model it runs on, such as Claude Code on Opus. It belongs to a User or a Team. Which Runner a Run of it spends is chosen when the Run starts.
_Avoid_: Bot, persona, agent profile

**Delegation**:
A User's handing of a Request to the agents, after which its Flow runs without them. The delegating User is the one whose personal Runners its Runs spend.
_Avoid_: Assignment, dispatch, hand-off

**Runner**:
A set of credentials Sofa may start Runs with, for one vendor: an API key, or a subscription signed in on a Worker. A **personal** Runner spends one User's credentials and runs only the Runs of Requests that User delegated; a **team** Runner spends a Team's API key, for anyone in the Team. Each has its own limit on Runs at once; Runs beyond it wait, highest Priority first.
_Avoid_: Provider, backend, engine

**Local-only**:
A mark on a Request whose work needs the Workspace's own machines or network, and so never runs where those can't be reached: on Claude Code on the web, on Managed Agents, or on Workers of a Sofa the Workspace doesn't run itself. Its Work Items inherit it, and a Run that would start there anyway is refused, saying why.
_Avoid_: On-premises, private, offline

**Worker**:
A place that executes Runs and claims them from Sofa: a container or pod that Sofa hosts, or later a User's own machine. A Worker holding a subscription's sign-in serves only that subscription's User.
_Avoid_: Executor, host, node, agent (for the machine)

**Run**:
One execution of one Agent on one Work Item within one Phase, making one Contribution: one plan of a Panel, one synthesis, one comment of a Discussion round.
_Avoid_: Session, job, execution

**Sandbox**:
What a Run may reach: the network, the Repositories and what it may push, and the tools it may use without asking. A Phase picks one of a few built-in Sandboxes, such as read-only for a Review or pushing to its own branch for an Implement, which a Project may adjust.
_Avoid_: Permissions, allowlist, environment, policy

**Priority**:
How urgent a Request is: Urgent, High, Medium, Low or None. Its Work Items and their Runs inherit it.
_Avoid_: Severity, importance, rank

**Contribution**:
What a Run produces, such as a spec, a plan, a critique, a review finding, a screenshot or code. Later Runs read earlier Contributions; agents share context only this way.
_Avoid_: Output, artifact, result, report

### Records and knowledge

**Working Record**:
Everything Sofa itself keeps about how work was done: Work Items, Runs, Contributions, transcripts, proposals and their discussion. It is the material for reviewing a step after the fact.
_Avoid_: Audit log, history, internal store

**Knowledge**:
Final, agreed material that every User should find without Sofa, such as accepted decisions and terminology. It lives in a Provider, never only in Sofa.
_Avoid_: Docs, wiki, documentation

**Publish**:
Sofa handing a proposal from the Working Record to a Knowledge Provider once it is agreed, such as an accepted decision or a new Term. A decision a plan marks as new is Published when a User approves the plan with it unchanged; any other waits in the Inbox for its own acceptance.
_Avoid_: Export, sync, promote

**Usage**:
What Sofa counts about the work it does: Runs, their cost, screenshots taken, Previews opened and the like, per Workspace, Team, Project, Agent and Runner, so limits and spending can be seen before they are hit.
_Avoid_: Analytics, metrics, telemetry, stats

**Provider**:
A connection to an outside system that Sofa works through, of one kind: Requests (GitHub Issues, Jira), Knowledge (Confluence, a repository's docs), code (GitHub, GitLab) or Previews (Cloudflare and the like).
_Avoid_: Integration, plugin, connector, adapter

**Connection**:
A Workspace's link to one outside system, such as its GitHub organisation or its Atlassian site, with the sign-in Sofa uses there. One Connection may serve several Providers: a GitHub Connection serves Requests, Knowledge, code and Previews alike.
_Avoid_: Integration, credential, account, installation

**Preview**:
A running or downloadable version of a Project's software with a set of changes in it, built and hosted by the Project's own pipeline outside Sofa, which only learns where it is.
_Avoid_: Review build, staging, demo, environment
