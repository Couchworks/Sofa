# Sofa

A software factory where AI agents take a Request through a Flow of Phases. The vocabulary is in `CONTEXT.md`, decisions in `docs/adr/`, plans in `docs/roadmap.md` and `docs/specs/`.

## Layout

- `src/` the services of ADR 0012, `Sofa.Contracts`, `Sofa.ServiceDefaults` and the Aspire app host `Sofa.AppHost`
- `tests/` unit tests and integration tests through Aspire's test host
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

Execution asks Identity for a token valid for one Run of one Agent, and the Agent Gateway accepts only such tokens (ADR 0025). Until the services that start Runs exist, diagnostic endpoints show it, on each service's own address from the dashboard:

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

The pages for the GitHub App, Connections and adding Users are for a Workspace's Admins. Their API lives under `/api/workspaces/{workspaceId}/…`, and the App Gateway asks Identity whether the signed-in User is an Admin of that Workspace: 401 without a token, 403 for anyone else. The web client shows them for the Workspace the User administers; they live in `web/src/admin`.

### Databases

Identity and Connections keep their records in their own schemas through EF Core migrations, applied when they start. After changing a model, add a migration with the local tool:

```sh
dotnet tool restore
SofaGeneratingMigrations=true dotnet ef migrations add <Name> --project src/Sofa.Identity --output-dir Data/Migrations
SofaGeneratingMigrations=true dotnet ef migrations add <Name> --project src/Sofa.Connections --output-dir Persistence/Migrations
```

`SofaGeneratingMigrations` brings in EF's design package only for this, as its version of Roslyn clashes with Wolverine's runtime compilation.

## Tests

```sh
dotnet test --solution Sofa.slnx   # unit and integration tests; the integration tests need Docker
cd web && npm test                  # the web client's tests
```

The integration tests start their own copy of Sofa, so they run fine beside a running app host, with a fake GitHub of their own in place of GitHub's API. They hide the resources' own output (Postgres, npm, Vite and the services); set `SOFA_TEST_RESOURCE_LOGS=1` to see it, as CI does.

## The API client

Building `Sofa.AppGateway` writes its OpenAPI description to `web/openapi/app-gateway.json`. After changing the App Gateway's API, rebuild it and run `npm run generate:api` in `web/`, and commit both (ADR 0022).
