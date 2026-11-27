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

## Tests

```sh
dotnet test --solution Sofa.slnx   # unit and integration tests; the integration tests need Docker
cd web && npm test                  # the web client's tests
```

The integration tests start their own copy of Sofa, so they run fine beside a running app host. They hide the resources' own output (Postgres, npm, Vite and the services); set `SOFA_TEST_RESOURCE_LOGS=1` to see it, as CI does.

## The API client

Building `Sofa.AppGateway` writes its OpenAPI description to `web/openapi/app-gateway.json`. After changing the App Gateway's API, rebuild it and run `npm run generate:api` in `web/`, and commit both (ADR 0022).
