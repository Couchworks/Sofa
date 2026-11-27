# Each service has one queue, named after it, for commands and replies

Every service listens on one durable RabbitMQ queue named after it, such as `execution`, and marks it as where replies to its own messages go. A command is sent to the queue of the service that owns what it changes; the receiver answers with Wolverine's reply to sender, without knowing who sent it. Every listener uses Wolverine's durable inbox and every sender its durable outbox, both in the service's own schema, named after the service with underscores (`app_gateway`). The names live in `Sofa.Contracts.SofaServices`, so the app host, the services and their senders agree on them.

## Considered Options

- **Wolverine's conventional routing**, a queue or exchange per message type: no routing to write, but one queue shared by every service handling the same type, which competes for events meant for all of them, and many queues to watch.
- **Wolverine's system reply queue per node**: no configuration, but replies go to a short-lived queue and are lost if the sender restarts, which the outbox is there to prevent.

## Consequences

- Events that several services react to will need an exchange per event, with each subscribing service binding its own queue; that is settled when the first such event is built.
