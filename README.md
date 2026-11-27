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

A fresh installation has no Users. While nobody has set it up, each start of Identity logs a new setup code: in the dashboard, open `identity`'s console logs and find `Sofa is not set up yet. Open it and enter this setup code: sofa_setup_…`. Open the web client, which shows the setup page, and enter the code with the first User's name, email and password and the names of their Workspace and Team. The code works once; the first User is then signed in, and signs in again later with their email and password. Postgres keeps no volume yet, so every start of the app host is a fresh installation.

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

Sofa creates its own GitHub App through GitHub's manifest flow, and GitHub must reach Sofa twice: in the browser, back to `/github-app/created` and `/github-app/installed`, and for webhooks at `/webhooks/github`. Both go to the App Gateway, so put it behind a public address with a tunnel and tell Sofa that address before creating the App:

```sh
cloudflared tunnel --url http://localhost:5176           # or: ngrok http 5176
dotnet user-secrets --project src/Sofa.AppHost set Sofa:PublicAddress https://<your-tunnel-address>/
dotnet run --project src/Sofa.AppHost
```

Port 5176 is the App Gateway's. Without a public address Sofa uses the App Gateway's local one, and the App is created with its webhooks off. A quick tunnel's address changes every run, while the App keeps the addresses it was created with; use a named tunnel or a fixed ngrok domain, or change the App's webhook URL on GitHub when the address changes.

1. Open the web client through the public address and choose **Create on GitHub**, naming the organisation to own the App, or none for your own account. The App is private, so it can only be installed on its owner.
2. GitHub shows the App to create; create it, and GitHub sends you back with the App made.
3. Choose **Install on GitHub** and pick the account. Back in Sofa, the installation shows as a Connection.
4. Label an issue in one of its Repositories. In Aspire's dashboard, Intake's structured logs show the `issues.labeled` event, with its trace through the App Gateway and Connections.

Connections seals the App's private key, webhook secret and client secret with envelope encryption under a key encryption key from configuration (`Credentials:KeyEncryptionKey`). Locally Aspire generates it once and keeps it in the app host's user secrets as `Parameters:connections-key-encryption-key`; lose it and the App's credentials can't be opened, so the App must be created again. To rotate it, set a new key and list the old one under `Credentials:RetiredKeyEncryptionKeys`.

The pages for the GitHub App and Connections are for a Workspace's Admins. They show once signed in, but the App Gateway doesn't yet check that the User is an Admin: everyone is the Admin of one placeholder Workspace. The App Gateway's `AdminsOnly` group and `IFindSignedInAdmin` are where that check goes, and the web client's admin views live in `web/src/admin`.

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
