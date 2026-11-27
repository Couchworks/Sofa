# Providers

How Sofa reaches outside systems. Vocabulary follows `CONTEXT.md`; see ADRs 0012, 0018 and 0019.

A Project binds Providers of each kind, such as Requests from GitHub issues in `acme/web`, Knowledge in `acme/web`'s `docs/`, code in `acme/web` and `acme/api`, and Previews from `acme/web`'s Deployments. Each works through a Connection, which holds the sign-in, hands out short-lived tokens and turns incoming webhooks into events.

## Request Providers (Intake)

```csharp
interface IRequestSource   { IAsyncEnumerable<RequestRef> FindNew(ProjectBinding b); Task<RequestSnapshot> Read(RequestRef r); }
interface IRequestReporter { Task<ReportRef> Post(RequestRef r, Report report); Task Update(ReportRef existing, Report report); }
interface IRequestState    { Task Set(RequestRef r, RequestState s); }               // optional
interface IRequestFiler    { Task<RequestRef> File(ProjectBinding b, NewRequest n); } // optional
```

- **Coming in.** A User marks an outside ticket for Sofa, by a label (such as `sofa`) on a GitHub issue or a set status in Jira, or imports it from Sofa. The User whose linked account marked it becomes its delegating User; a ticket marked by an account linked to no User waits in the Project's Admins' Inbox.
- **Reports.** One per Phase that reports: a summary, the detail collapsed beneath it, and a link into Sofa.
- **Questions** may be answered in reply to Sofa's comment on the Request, but only by accounts linked to Users with access to the Project; anyone else's reply is ignored.
- **State**, where the Provider can keep it and the Project turns it on: labels such as `sofa: planning` or `sofa: awaiting validation`, and closing the ticket once its work reaches the main branch.
- **Follow-up Findings** stay Work Items in Sofa under the Autonomous Validation mode; under Gated, they are filed as new Requests marked as waiting for a User. Each Project may set this otherwise.
- Child Work Items are never filed outside (ADR 0001).

## Knowledge Providers (Knowledge)

```csharp
interface IKnowledgeReader    { Task<IReadOnlyList<DecisionRef>> SearchDecisions(string q); Task<Glossary> ReadGlossary(); Task<Document> Read(DocRef d); }
interface IKnowledgePublisher { Task<PublishedRef> PublishDecision(Decision d); Task<PublishedRef> PublishTerm(Term t); }
```

- Runs read Knowledge through MCP tools on the Agent Gateway (`search_knowledge`, `read_decision`, `read_glossary`), so plans follow what the team already decided.
- **In a Repository**, a decision becomes a numbered ADR in `docs/adr/` and a Term an entry of a `CONTEXT.md`-style glossary, each Published by a pull request of its own that Sofa merges straight away (ADR 0002).

## Code Providers (Code)

```csharp
interface IRepositoryAccess { Task<RepoToken> TokenFor(RepoRef r, Access a); }   // for Workers, scoped by their Sandbox
interface IBranches         { Task Create(RepoRef r, string name, string fromRef); Task UpdateFromBase(PullRef p); }
interface IPullRequests     { Task<PullRef> Open(NewPull n); Task Retarget(PullRef p, string newBase); Task<MergeResult> Merge(PullRef p); Task<PullState> Read(PullRef p); }
```

- **Stacking as pull requests.** Each child Work Item's pull request targets its Integration branch; each Integration branch's targets the Integration branch of what blocks it, or the main branch. When a blocker merges, the pull requests stacked on it are retargeted.
- Events come through the Connection: checks finished, pull requests merged or closed, pushes.
- The Code service only calls the host's API; anything needing a working copy is a Run (ADR 0019).

## Preview Providers (Code)

```csharp
interface IPreviewSource { Task<PreviewState> For(RepoRef r, string commitSha); }   // Pending | Ready(url, access) | Failed | None
```

- **GitHub** reads Deployments and their statuses for the head commit of the Integration branch. `environment_url` may be a web Preview or a download, for desktop and mobile builds alike.
- `access` names a credential held by Connections, such as a Cloudflare Access service token, for the Showcaser to get past sign-in.
- A Project's pipeline records a Deployment for each Preview it publishes; Sofa never reads comments meant for people (ADR 0003).

## Connections

- **GitHub**: a GitHub App. A self-hosted Sofa creates its own through GitHub's manifest flow; a hosted Sofa has one. Each Workspace installs it on its organisations, and each installation is a Connection.
  - The App is private to the organisation or account that owns it, so a self-hosted Sofa's App is created by the organisation it will be installed on.
  - Connections keeps the App's private key, webhook secret and client secret sealed by envelope encryption, and is the only service that ever opens them. Other services ask it for short-lived installation tokens, over HTTP (ADR 0027).
  - Webhooks reach the App Gateway, which passes them to Connections to check against the webhook secret; a delivery that fails is refused. Each accepted delivery is published as `GitHubEventReceived` to its own exchange (ADR 0026), at least once, carrying GitHub's delivery id.
  - GitHub doesn't retry a failed delivery, so on starting and every five minutes Connections lists the App's recent deliveries and asks GitHub to redeliver each that failed and never arrived, once.
- **Atlassian**: one OAuth connection per site, serving Jira and Confluence alike.
