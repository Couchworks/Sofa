# Each event has a fanout exchange, and each subscribing service binds its own queue to it

An event that other services react to is published to a RabbitMQ fanout exchange named after the event, such as `github-event-received`. Every service that reacts to it binds its own queue (ADR 0023) to that exchange, so each gets its own copy through its durable inbox, and the publisher never knows who listens. The exchanges' names live in `Sofa.Contracts.SofaEvents`. This settles what ADR 0023 left for the first such event: GitHub's webhooks, which Intake and Code will both read.

## Considered Options

- **Sending the event to each subscriber's queue**: no exchanges, but the publisher would name every service that cares, and change whenever one starts caring.
- **One topic exchange for every event, bound by routing key**: one exchange to declare, but every binding needs the right key, and a mistyped key loses events silently instead of failing to declare.

## Consequences

- A subscribing service handles every message the exchange carries, so an event is published to its own exchange, never to another event's.
- Events reach subscribers at least once: a subscriber that must not act twice keeps the event's own id, such as a GitHub delivery's id.
