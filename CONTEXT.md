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
A ticket in an outside system, such as a GitHub issue or a Jira issue, brought in through a Provider. Sofa reads it and posts Reports back to it; it never holds Sofa's working state.
_Avoid_: Ticket (for the outside one), issue, external ticket

**Work Item**:
Sofa's own unit of work. A Request becomes one root Work Item, which may be split into child Work Items, each run through the Flow on its own and possibly blocking others.
_Avoid_: Ticket, task, job, internal ticket

**Flow**:
The ordered Phases a Project's Work Items go through, such as Spec, Plan, Implement, Review, Integrate and Validate.
_Avoid_: Pipeline, workflow, board

**Phase**:
One stage of a Flow: a Pattern, the Agents cast in it, and its Gate.
_Avoid_: Step, stage, column

**Pattern**:
The shape in which Agents work together inside a Phase, chosen from a fixed set: Solo, Panel, Synthesis, Discussion, Split, and Review-and-triage.
_Avoid_: Strategy, template, mode

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

**Report**:
What Sofa posts back to a Request when a Phase ends: a summary, such as the plan agreed or the decisions made, with a link to the full detail.
_Avoid_: Update, sync, comment (for the whole thing)

### Agents and Runs

**Agent**:
A configured AI participant: a role and its instructions, a model, and the Runner it runs through. It belongs to a User or a Team, and acts for them.
_Avoid_: Bot, worker, persona, agent profile

**Runner**:
A way for Sofa to start Runs with someone's credentials: a Claude subscription, the Claude API, a local machine, or later other providers. A **personal** Runner spends one User's credentials; a **team** Runner spends a Team's.
_Avoid_: Executor, backend, engine

**Run**:
One execution of one Agent on one Work Item within one Phase.
_Avoid_: Session, job, execution

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
Sofa handing a proposal from the Working Record to a Knowledge Provider once it is agreed, such as an accepted decision or a new Term.
_Avoid_: Export, sync, promote

**Usage**:
What Sofa counts about the work it does: Runs, their cost, screenshots taken, Previews opened and the like, per Workspace, Team, Project, Agent and Runner, so limits and spending can be seen before they are hit.
_Avoid_: Analytics, metrics, telemetry, stats

**Provider**:
A connection to an outside system that Sofa works through, of one kind: Requests (GitHub Issues, Jira), Knowledge (Confluence, a repository's docs), code (GitHub, GitLab) or Previews (Cloudflare and the like).
_Avoid_: Integration, plugin, connector, adapter

**Preview**:
A running or downloadable version of a Project's software with a set of changes in it, built and hosted by the Project's own pipeline outside Sofa, which only learns where it is.
_Avoid_: Review build, staging, demo, environment
