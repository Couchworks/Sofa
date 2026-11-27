# The web client's API client is generated types over openapi-fetch

The App Gateway writes its OpenAPI description to `web/openapi/` on every build, and it is committed. The web client generates TypeScript types from it with openapi-typescript, and calls the API through openapi-fetch, with openapi-react-query giving TanStack Query hooks over the same types. Only types are generated, so a changed API shows up as type errors where it's called, and CI fails when the committed description or the generated types are out of date.

## Considered Options

- **Generating a whole SDK** (Hey API, Orval, NSwag): functions and hooks per operation, but much more generated code to review and keep in step with its generator.
- **Fetching the description from a running gateway**: always fresh, but generating the client would need the backend, Postgres and RabbitMQ running.

## Consequences

- openapi-typescript needs TypeScript 5 at the moment, so the web client stays on TypeScript 5.9 until it supports 7.
- A change to the App Gateway's API is one commit with the description, the regenerated types and the web client's use of them.
