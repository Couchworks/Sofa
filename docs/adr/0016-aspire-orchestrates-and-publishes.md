# Aspire runs Sofa locally and produces its deployments

Aspire describes the services, Workers, RabbitMQ and Postgres in one app host: in development it starts them with one command and a dashboard of their logs and traces, and it publishes the one-box install as Docker Compose and the cluster install as Helm charts for Kubernetes. One description of the system then serves every place ADR 0005 says it runs.

## Consequences

- Aspire's Kubernetes publishing was still in preview at 13.3; until it is stable, the cluster install may need its charts adjusted by hand.
- Every service reports through OpenTelemetry, which Aspire's dashboard shows locally and a cloud backend later; Usage can grow from the same signals.
