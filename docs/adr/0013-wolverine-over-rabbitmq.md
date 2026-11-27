# Services message each other with Wolverine over RabbitMQ

Commands, events, sagas and the outbox that ADR 0012 asks for are built on Wolverine, with its outbox kept in each service's Postgres schema, over RabbitMQ on one box and in Kubernetes. A managed broker in the cloud later is a change of Wolverine transport, not of the services. Wolverine is MIT-licensed and maintained; MassTransit, the usual .NET choice, became commercial with v9, and the open-source v8's official maintenance ends after 2026.

## Considered Options

- **MassTransit v9**: the most widely used, but a paid licence for every installation, SaaS included.
- **MassTransit v8 or a fork of it**: free, but no longer officially maintained.
- **NServiceBus**: mature, and commercial too.
- **Kafka or NATS JetStream** as the broker: built for streams and replay, more than command and event traffic between a dozen services needs.
