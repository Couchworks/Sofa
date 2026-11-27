# Sofa

A software factory where AI agents take a Request through a Flow of Phases. The vocabulary is in `CONTEXT.md`, decisions in `docs/adr/`, plans in `docs/roadmap.md` and `docs/specs/`.

## Layout

- `src/` the services of ADR 0012, `Sofa.Contracts`, `Sofa.ServiceDefaults`, the Aspire app host `Sofa.AppHost`, and `Sofa.Cli`, the `sofa` command run on the box
- `worker/` the Worker image: the unmodified Claude Code, nothing of Sofa's
- `tests/` unit tests and integration tests through Aspire's test host, and `tests/stub-worker/`, a Worker image whose `claude` plays back a recorded Run
- `web/` the web client (Vite, React, TypeScript, Tailwind, shadcn/ui)

## Running it

Needs the .NET 11 SDK (`global.json`), Node 24 or later, and Docker running for Postgres and RabbitMQ.

```sh
dotnet run --project src/Sofa.AppHost
```

This starts every service, Postgres, RabbitMQ and the web client, and prints the link to Aspire's dashboard. The web client is served through the App Gateway: open `app-gateway`'s address from the dashboard.

### Setting Sofa up and signing in

A fresh installation has no Users. While nobody has set it up, each start of Identity logs a new setup code: in the dashboard, open `identity`'s console logs and find `Sofa is not set up yet. Open it and enter this setup code: sofa_setup_…`. Open the web client, which shows the setup page, and enter the code with the first User's name, email and password and the names of their Workspace and Team. The code works once; the first User is then signed in, and signs in again later with their email and password, or with GitHub once their account is linked (see below).

Postgres keeps its data in a Docker volume, so Users, Workspaces, the GitHub App's sealed credentials and Connections survive restarts, and a setup code appears only on a fresh installation. Aspire keeps the passwords and keys it generates (Postgres's password, the key encryption key and the others) in `~/.aspire/secrets/<the app host's UserSecretsId>/`; keep them with the volume.

### Starting over

To go back to a fresh installation, stop the app host and remove the volume:

```sh
docker volume ls --filter name=postgres-data       # one per checkout, such as sofa.apphost-b0c5b6a1a1-postgres-data
docker volume rm <that volume>
```

The next start logs a new setup code. A GitHub App created before stays on GitHub; delete it there (Settings → Developer settings → GitHub Apps, or the organisation's) before creating a new one, as GitHub won't reuse its name.

### Run tokens

Execution asks Identity for a token valid for one Run of one Agent, and the Agent Gateway accepts only such tokens (ADR 0025), and only while Execution says the Run is running (ADR 0039). Diagnostic endpoints show it, on each service's own address from the dashboard:

```sh
curl -X POST $FLOW/diagnostics/agents -H 'content-type: application/json' \
  -d '{"workspaceId":"…","name":"Builder","owner":{"kind":"User","id":"…"}}'      # registers an Agent with Identity
curl -X POST $EXECUTION/diagnostics/run-tokens -H 'content-type: application/json' \
  -d '{"agentId":"…"}'                                                              # a Run token for a made-up Run
curl $AGENT_GATEWAY/diagnostics/run -H "Authorization: Bearer $TOKEN"              # 200 with the Run, 401 for any other token
```

The Workspace's and User's ids are in `/api/me` of the App Gateway, once signed in.

### Trying the GitHub App

Sofa creates its own GitHub App through GitHub's manifest flow, and GitHub must reach Sofa in two ways: in the browser, back to `/github-app/created`, `/github-app/installed` and, for signing in with GitHub, `/account/github/callback`; and for webhooks at `/webhooks/github`. All go to the App Gateway, so put it behind a public address with a tunnel and tell Sofa that address before creating the App:

```sh
cloudflared tunnel --url http://localhost:5176           # or: ngrok http 5176
dotnet user-secrets --project src/Sofa.AppHost set Sofa:PublicAddress https://<your-tunnel-address>/
dotnet run --project src/Sofa.AppHost
```

Port 5176 is the App Gateway's. The public address becomes Sofa's issuer and one of the web client's origins, and the App's addresses are made from it, so open the web client through it. Without a public address Sofa uses the App Gateway's local one, and the App is created with its webhooks off. A quick tunnel's address changes every run, while the App keeps the addresses it was created with; use a named tunnel or a fixed ngrok domain, or change the App's addresses on GitHub when it changes. After changing the public address, sign out and in again, as tokens name the issuer they came from.

1. Sign in as an Admin of your Workspace and choose **Create on GitHub**, naming the organisation to own the App, or none for your own account. The App is private, so it can only be installed on its owner.
2. GitHub shows the App to create; create it, and GitHub sends you back with the App made.
3. Choose **Install on GitHub** and pick the account. Back in Sofa, the installation shows as a Connection of your Workspace.
4. Choose **Link your GitHub account**, and authorize the App on GitHub. Sofa shows the GitHub login you sign in with; sign out, and **Sign in with GitHub** signs you in as yourself.
5. Label an issue in one of its Repositories. In Aspire's dashboard, Intake's structured logs show the `issues.labeled` event, with its trace through the App Gateway and Connections.

GitHub's returns from creating and installing the App carry a signed `state` that Sofa made when you pressed the button, naming you and your Workspace; it works once, for 30 minutes, in the browser that started it (ADR 0028). Any other return is refused: start again from Sofa. GitHub's own return after someone changes an installation's repositories changes nothing in Sofa and just lands back on the home page with a note, as Sofa hears of the change through GitHub's events.

#### Signing in with GitHub

Through the App's own sign-in (ADR 0029): a GitHub account signs in as the User it is linked to. An Admin adds someone by GitHub login (**Add a User** on the home page): Sofa asks GitHub whose login it is and keeps that account's id, so it refuses a login that is no person on GitHub, and a login renamed later still reaches the same person. An account signs in as the User holding its id, or else, at its first sign-in, is linked to the User whose email is the account's verified primary email. A GitHub account no User has is refused. A signed-in User links their account with **Link your GitHub account**.

#### An App created before signing in with GitHub

A new manifest can't change an App that exists, so an App created before this needs two changes by hand on GitHub, in the App's settings (your account's or the organisation's Settings → Developer settings → GitHub Apps → the App):

1. **General → Identifying and authorizing users → Callback URL**: add `https://<your-public-address>/account/github/callback`. Leave *Request user authorization (OAuth) during installation* off.
2. **Permissions & events → Account permissions → Email addresses**: *Read-only*. Save, then accept the new permission on each installation (GitHub asks on the installation's page).

Without the email permission, GitHub accounts still sign in when linked or added by login, but none is matched by email.

Users added by login before Sofa kept their GitHub account id, and who never signed in, are removed when Identity next starts; add them again.

Connections seals the App's private key, webhook secret and client secret with envelope encryption under a key encryption key from configuration (`Credentials:KeyEncryptionKey`). Locally Aspire generates it once and keeps it as `Parameters:connections-key-encryption-key` in `~/.aspire/secrets/<the app host's UserSecretsId>/Development.json`, not in user secrets; lose it and the App's credentials can't be opened, so the App must be created again. To rotate it, set a new key and list the old one under `Credentials:RetiredKeyEncryptionKeys`.

The pages for the GitHub App, Connections and adding Users are for a Workspace's Admins. Their API lives under `/api/workspaces/{workspaceId}/…`, and the App Gateway asks Identity whether the signed-in User is an Admin of that Workspace, or for the pages every User of it may use, whether they belong to it: 401 without a token, 403 for anyone else. The web client shows them for the Workspace the User administers; they live in `web/src/admin`.

### Projects, Agents and the Flow

**Projects** in the web client lists the Workspace's Projects. An Admin creates one with a key, such as `DEMO`, unique in the Workspace and never changed, and binds it through one of the Workspace's Connections to a Request Provider (the Repository whose issues come in, and the label that marks them for Sofa) and a Code Provider (one or more Repositories). Flow checks with Connections that the Connection is the Workspace's and reaches each Repository (ADR 0030). Only an Admin changes a binding.

Every new Project starts with milestone 1's Flow, Triage → Spec → Plan → Implement, every Phase Solo, with the Gates and Sandboxes of `docs/default-flow.md`, and its own Triager (Haiku), Lead (Opus) and Builder (Opus), registered with Identity as actors owned by the Workspace's Team. A Project's page shows them; every User of the Workspace has Write in its Projects (ADR 0031), so they change an Agent's role, instructions and model (`opus`, `sonnet`, `haiku` or a full model name, passed to Claude Code's `--model`) and a Phase's cast, Gate and Sandbox. Nothing runs the Flow yet.

Flow hears Identity's answer to each Agent's registration. An Agent Identity refused shows on the Project's page as *Not registered*, with Identity's reason and **Retry**, which asks Identity again; its Runs are refused until it is registered. Agents Identity hasn't answered for are asked again whenever Flow starts.

### Requests, Reports and answers

Intake brings Requests in from GitHub and writes back to their issues; it learns each Project's binding from Flow's `ProjectBound` events and keeps its own copy, which Flow sends again for every Project when it starts (ADR 0034).

- **Labelling an issue** in a Project's Repository with its label makes a Request and its root Work Item, `<KEY>-<n>`, numbered by Flow (ADR 0035). The User holding the GitHub account that added the label is its delegating User; an account linked to no User of the Workspace opens it with no one delegating, and Notices puts it in the Inbox of the Workspace's Admins. Labelling it again changes nothing. The Project's page lists its Work Items with their Request and who delegated them.
- **Reports** are one comment per Phase on the issue: the summary, the detail collapsed beneath it, and a link to the Work Item in Sofa. Posting again for that Phase edits the comment (ADR 0036). Until Flow sends them (slice 8), post one by hand: `curl -X POST localhost:5299/diagnostics/reports -H 'Content-Type: application/json' -d '{"requestId":"<the request id>","workItemKey":"DEMO-1","phase":"Spec","summary":"…","detail":"…"}'`, with the Request's id from `GET /api/workspaces/{workspaceId}/projects/DEMO/work-items`. `/diagnostics/questions` asks a Question the same way (`questionId`, `requestId`, `workItemKey`, `phase`, `question`).
- **Answers**: the first comment on the issue after an open Question, from a GitHub account linked to a User of the Workspace, answers it, without the quoted lines and @-mentions it starts with, and Flow receives the answer as an event (it logs it until slice 8). Anyone else's comment is ignored.
- **The issue's state**, when the binding turns on *Keep the issue's state*: the issue carries `sofa: <phase>` for its Work Item's Phase, and is closed once the work reaches the main branch. Nothing sends those events before slice 8; `/diagnostics/work-items/entered-phase` and `/diagnostics/work-items/reached-main-branch` stand in.

Port 5299 is Intake's; the diagnostics routes are reached only on the box, never through the App Gateway.

#### Showing it on GitHub

With the App installed on a test Repository (see *Trying the GitHub App*), and Sofa behind a public address so GitHub's webhooks arrive:

1. Sign in, link your GitHub account from the home page, and create Project `DEMO` bound to the test Repository with the label `sofa`.
2. On GitHub, create the label `sofa` in the Repository and add it to an issue. The Project's page shows `DEMO-1`, delegated by you.
3. Have someone whose GitHub account is linked to no User add `sofa` to another issue: it shows as waiting for a delegating User.
4. Post a Report with the `curl` above, then again with another summary: one comment on the issue, edited in place.
5. Ask a Question with `/diagnostics/questions`, reply on the issue as yourself and as the other account: only yours answers it, and Flow's log shows `Flow received the answer to Question …`.

### Runners

**Runners** lists the signed-in User's personal Runners and their Team's Runners; an Admin sees every team Runner. Each runs at most its limit of Runs at once (2 unless changed).

- **A subscription Runner** is always personal (ADR 0009). Execution keeps only its Worker volume's name (`sofa-worker-<runner id>`) and its sign-in state, never the sign-in.
- **An API key Runner**, personal or, made by an Admin, the Team's. The key goes to Connections over HTTP, is sealed like every other credential, and is never shown or returned again (ADR 0032).

#### Signing a subscription Runner in

From a terminal on the box running Sofa, with Docker (ADR 0033):

```sh
docker build -t sofa-worker worker                                  # once, and after changing worker/
dotnet run --project src/Sofa.Cli -- runner login <runner id>       # the Runners page shows this with the id
```

It starts a throwaway Worker container on the Runner's volume running `claude auth login --claudeai` in your terminal: open the link it shows, sign in with your Claude subscription, and paste the code back. A fresh container then reads `claude auth status`, and Execution records only whether the volume is signed in to a subscription and when that ends, which the Runners page shows. `runner status <runner id>` checks again without signing in. The command finds Execution at `http://localhost:5005`, where the app host serves it; set `SOFA_EXECUTION` for another address, and `SOFA_WORKER_IMAGE` for another image.

### Runs

Execution keeps the Runs and their queue. A Run waits for a Runner, highest Priority first, then oldest first: it spends its delegating User's personal Runner for its vendor (a subscription before an API key), else a Runner of their Team (ADR 0009), and never more Runs on a Runner than its limit. While it waits it says why, such as that every Runner it may spend is at its limit, or that its User has no Runner and no Team of theirs has one.

Each Run gets a fresh Worker container (ADR 0017), started by Execution through Docker on the box and watched by it (ADR 0037), holding its Run token and its Repository token and nothing else of Sofa's; a subscription Run mounts its Runner's volume, an API key Run gets its key from Connections. In the container, a subscription Run first prints `claude auth status`, which Execution records on the Runner, and Claude Code starts only once Execution sees the volume signed in; otherwise the Run is refused as *sign in again*, and Notices hears of it (logged until the Inbox shows it). Claude Code then runs headless (`claude -p`, never `--bare`) on the Agent's model, with only Sofa's MCP server, at the Agent Gateway's `/mcp`, and only the tools of its Phase's Sandbox allowed; the Brief names Sofa's tools, so the Run doesn't spend a turn finding them. Its `stream-json` output goes to Records as it comes, scrubbed of known secrets: the Run's Transcript (see *Transcripts* below).

A Run is *waiting*, *running*, then *finished* (ended by its Agent through Sofa's tool, for now the test tool `end_test_run`), *failed* (its container ended first, or ran over `Workers:LongestRun`, two hours), *lost* (Execution restarted while it ran), *stopped* by a User, or *refused* (Identity refused its Run token, its API key couldn't be opened, Code gave no token for its Repositories, its volume isn't signed in, or it may have pushed outside its own branch), always saying why. A failed or lost Run is retried as a new Run, linked to it (ADR 0038), until its retry limit; then it waits for a User's **Retry**, as a refused Run does.

The App Gateway serves `/api/workspaces/{workspaceId}/runs`, each Run at `…/runs/{runId}`, its Transcript at `…/runs/{runId}/transcript`, and `…/retry` and `…/stop` on it, to every User of the Workspace. Until Flow starts Runs (slice 8), Flow's diagnostic endpoint starts a test Run of a Phase's Agent, with the Agent's model and the Phase's Sandbox:

```sh
docker build -t sofa-worker worker                                  # the Worker image, once
curl -X POST $FLOW/diagnostics/test-runs -H 'content-type: application/json' \
  -d '{"workspaceId":"…","projectKey":"DEMO","phase":"Triage","delegatingUserId":"…","task":"Write a haiku about sofas."}'
curl $APP_GATEWAY/api/workspaces/…/runs/<runId> -H "Authorization: Bearer $TOKEN"              # its state, Runner and why
curl $APP_GATEWAY/api/workspaces/…/runs/<runId>/transcript -H "Authorization: Bearer $TOKEN"   # its Transcript
docker kill sofa-run-<runId>                                        # a killed container: failed, then retried
```

`priority` (Urgent, High, Medium, Low, None) and `retryLimit` (default 2) are optional. The containers reach the Agent Gateway only through the egress relay (see *The Sandbox's network* below), which reaches it at `host.docker.internal`, so it listens on every address of the box. The Worker image is `Workers:Image` of Execution (`sofa-worker`); the integration tests use `tests/stub-worker` instead, so they spend no tokens. Execution runs one copy on a box: two would start the same Run twice.

#### Transcripts

Records converts each line of a Run's output on arrival into entries (ADR 0040): `role` (`User`, `Assistant` or `System`), `text`, `toolName` and `toolInput` for a tool call, its `toolResult` once it comes, and `at`. The session's start is a system entry naming the model, the tools allowed and the credentials, and its end one saying how it ended, after how many turns and at what cost. Known secrets become `[scrubbed]` before anything is stored, in Execution before the line leaves for the broker and in Records again: Sofa's own tokens, GitHub's, Anthropic's API keys and OAuth tokens, AWS keys, Slack's, private-key blocks and bearer values. A tool input or result over 20 KB keeps its first 20 KB and says so (`toolInputCut`, `toolResultCut`). `…/transcript?after=<readUpToLine>` reads only what came since; a tool call shows again once its result arrives. Transcripts kept before this as raw lines were dropped.

#### Repository tokens

When a Run starts, Execution asks Code for its token, and Code asks Connections for an installation token for only the Project's Repositories (ADR 0041): a read-only Sandbox may read contents and pull requests; pushing its own branch may write both. A Run's own branch is `sofa/<Work Item key>`, or `sofa/test-run-<run id>` for a test Run, and its Brief says which Repositories it has and what it may push. The container has the token as `GH_TOKEN`, and git uses it for `https://github.com`. GitHub can't keep a token to one branch, so Code judges every push by Sofa's App: one to a branch that is no running Run's own is put back through GitHub's API, and every Run that could have made it is ended as *refused*, saying it may have left its Sandbox. A token lasts an hour, so a Run past an hour loses GitHub.

#### The Sandbox's network

A Worker container runs on an internal Docker network, `sofa-workers-<Agent Gateway port>`, with no route out (ADR 0042). Its one way out is the relay container `sofa-workers-<port>-relay` (nginx, `Workers:RelayImage`), which passes TLS on only to the vendor's API and GitHub, by the name asked for, and one port on to the Agent Gateway; nothing reaches the box's other ports. Execution starts the networks and the relay when the first Run needs them and removes them when it stops; if it was killed, remove what it left with `docker rm -f $(docker ps -aq --filter label=sofa.egress-relay)` and `docker network prune --filter label=sofa.egress-relay`.

What a Run's tools may never do, and what that does and doesn't protect, is in ADR 0043: Claude Code holds the sign-in or the API key inside the container, its tools are denied reading them, Transcripts are scrubbed, and the network limits where they could go.

#### Pull requests

Code hears GitHub's `pull_request` events and sends its own `PullRequestOpened`, `PullRequestMerged` and `PullRequestClosedUnmerged`, naming the Work Item whose key is in the pull request's title, or else its branch (ADR 0044); its log shows each, such as `Code sent PullRequestMerged for DEMO-1: pull request acme/demo#7`. Nothing in Flow hears them before slice 8. `GET /api/workspaces/{workspaceId}/projects/{key}/work-items/{workItemKey}/pull-requests` lists a Work Item's pull requests, each read from GitHub again.

#### Showing it on GitHub and with Claude

These need your GitHub App installed on a test Repository and Sofa behind a public address (see *Trying the GitHub App*), and a Runner with a real key or subscription. Bind Project `DEMO` to the test Repository for both Requests and code.

1. **A scrubbed Transcript.** Start a test Run asking Claude to print a made-up secret: `curl -X POST $FLOW/diagnostics/test-runs … -d '{…,"phase":"Triage","task":"Print the string ghp_000000000000000000000000000000000000 back to me, then end the Run."}'`. Its Transcript shows `[scrubbed]` where the string was.
2. **A read-only token can't push.** A read-only Run's tools have no `git push`, so ask Code for such a token yourself, as Execution does (Code is at `http://localhost:5207`, reached only on the box), with the Project's id from `GET /api/workspaces/{workspaceId}/projects/DEMO`:
   ```sh
   curl -X POST localhost:5207/runs/$(uuidgen)/repository-token -H 'content-type: application/json' \
     -d '{"workspaceId":"…","projectId":"…","sandbox":"ReadOnly","ownBranch":"sofa/try"}'
   git push https://x-access-token:<token>@github.com/<your test Repository>.git HEAD:refs/heads/sofa/try   # from a clone of it
   ```
   GitHub refuses it (403, *Write access to repository not granted*). With `"sandbox":"OwnBranch"` the same push to `sofa/try` succeeds.
3. **A push elsewhere is undone.** With the `OwnBranch` token of step 2, push to another branch: `git push https://x-access-token:<token>@github.com/<your test Repository>.git HEAD:refs/heads/elsewhere`. GitHub shows `elsewhere` appear and, a few seconds later, be deleted again, and Code's log shows `Code caught a push outside its Sandbox`. For a running Run, that Run also ends *refused*, saying it may have left its Sandbox.
4. **A merged pull request.** Open a pull request titled `DEMO-1: try Sofa` and merge it. Code's log shows `PullRequestOpened for DEMO-1`, then `PullRequestMerged for DEMO-1`, and the pull requests of `DEMO-1` list it as merged.
5. **The network.** Start a test Run of Implement with the task *Run `git clone https://github.com/<your test Repository>`, then `git clone http://host.docker.internal:5005/x` and `git clone https://example.com/x`, and say what each answered.* Only GitHub's clone works; the others can't resolve or connect. While it runs, `docker network inspect sofa-workers-<port>` shows only the Run's container and the relay.

### The Team

**Team**, for Admins, shows who is in the Workspace's Team, adds any User of the Workspace and removes members. Users added by GitHub login join the Workspace's Team, so they can spend its team Runners; those added before this were put in it when Identity next started.

### Databases

Identity, Connections, Flow, Execution, Intake, Notices and Records keep their records in their own schemas through EF Core migrations, applied when they start. After changing a model, add a migration with the local tool:

```sh
dotnet tool restore
SofaGeneratingMigrations=true dotnet ef migrations add <Name> --project src/Sofa.Identity --output-dir Data/Migrations
SofaGeneratingMigrations=true dotnet ef migrations add <Name> --project src/Sofa.Connections --output-dir Persistence/Migrations
SofaGeneratingMigrations=true dotnet ef migrations add <Name> --project src/Sofa.Flow --output-dir Data/Migrations
SofaGeneratingMigrations=true dotnet ef migrations add <Name> --project src/Sofa.Execution --output-dir Data/Migrations
SofaGeneratingMigrations=true dotnet ef migrations add <Name> --project src/Sofa.Intake --output-dir Data/Migrations
SofaGeneratingMigrations=true dotnet ef migrations add <Name> --project src/Sofa.Notices --output-dir Data/Migrations
SofaGeneratingMigrations=true dotnet ef migrations add <Name> --project src/Sofa.Records --output-dir Data/Migrations
```

`SofaGeneratingMigrations` brings in EF's design package only for this, as its version of Roslyn clashes with Wolverine's runtime compilation.

## Tests

```sh
dotnet test --solution Sofa.slnx   # unit and integration tests; the integration tests need Docker
cd web && npm test                  # the web client's tests
```

The integration tests start their own copy of Sofa, so they run fine beside a running app host, with a fake GitHub of their own in place of GitHub's API, and build and run the stub Worker image (`tests/stub-worker`) in place of Claude Code. They hide the resources' own output (Postgres, npm, Vite and the services); set `SOFA_TEST_RESOURCE_LOGS=1` to see it, as CI does.

## The API client

Building `Sofa.AppGateway` writes its OpenAPI description to `web/openapi/app-gateway.json`. After changing the App Gateway's API, rebuild it and run `npm run generate:api` in `web/`, and commit both (ADR 0022).
