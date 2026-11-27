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

### Identity's database

Identity keeps its records in its schema through EF Core migrations, applied when it starts. After changing its model, add a migration with the local tool:

```sh
dotnet tool restore
SofaGeneratingMigrations=true dotnet ef migrations add <Name> --project src/Sofa.Identity --output-dir Data/Migrations
```

`SofaGeneratingMigrations` brings in EF's design package only for this, as its version of Roslyn clashes with Wolverine's runtime compilation.

## Tests

```sh
dotnet test --solution Sofa.slnx   # unit and integration tests; the integration tests need Docker
cd web && npm test                  # the web client's tests
```

The integration tests start their own copy of Sofa, so they run fine beside a running app host. They hide the resources' own output (Postgres, npm, Vite and the services); set `SOFA_TEST_RESOURCE_LOGS=1` to see it, as CI does.

## The API client

Building `Sofa.AppGateway` writes its OpenAPI description to `web/openapi/app-gateway.json`. After changing the App Gateway's API, rebuild it and run `npm run generate:api` in `web/`, and commit both (ADR 0022).
