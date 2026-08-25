# ADR 0002: Saga Pattern for Order Processing

## Status

Accepted.

## Context

An order's completion spans three services and three databases (`CLAUDE.md`'s "Database per service" rule rules out a shared database from the start): Order Service creates the order, Payment Service charges it, Inventory Service reserves stock. A single ACID transaction across all three is not possible without a distributed transaction coordinator (two-phase commit), which does not fit RabbitMQ-based, loosely-coupled services and is explicitly what the Saga pattern exists to avoid. The exercise statement mandates the Saga pattern directly (Phase 3, "Distributed Transaction Handling"; `CLAUDE.md`'s "Non-Negotiable Rules": "The Saga pattern governs the entire order flow").

Two Saga variants exist: **orchestration** (a dedicated coordinator service tells each participant what to do) and **choreography** (each service reacts to events from the others, with no central coordinator).

## Decision

Choreography, not orchestration. Order Service publishes `OrderCreatedEvent`; Payment Service and Inventory Service react independently and publish their own outcome events (`PaymentProcessedEvent`/`PaymentFailedEvent`, `InventoryReservedEvent`/`InventoryFailedEvent`); Order Service consumes those outcome events and drives its own state machine to `Completed` or one of two compensating terminal states, `PaymentFailed`/`InventoryFailed` (see `docs/architecture.md`'s state machine diagram). There is no separate "saga orchestrator" service or process - Order Service plays that role implicitly, since it is the only participant that both starts the flow and reacts to every outcome, but it does so the same way any other consumer does: by handling events, not by issuing commands to the other services.

Compensation, not rollback: if payment fails, the order moves to the terminal `PaymentFailed` status - nothing is "undone" in Payment Service, because nothing succeeded there to undo. If inventory reservation fails after a successful payment, the order moves to `InventoryFailed`; a real production system would likely also trigger a refund event here, which is out of this exercise's scope but is the natural next compensating step this pattern anticipates.

Idempotency is what makes choreography safe under at-least-once delivery: every domain transition method on `Order` (`MarkPaymentProcessed`, `MarkPaymentFailed`, `Complete`, `MarkInventoryFailed`) is a no-op if the order already reached or passed the target status, so redelivery of the same event - which RabbitMQ/MassTransit's retry-then-dead-letter behavior can cause - never double-applies an effect.

## Consequences

- No single point of failure or bottleneck coordinating the flow - each service scales and fails independently, consistent with the microservices boundary the rest of the architecture already commits to.
- The trade-off is that the overall flow is harder to see in one place than an orchestrator's explicit workflow definition would be - this is why `docs/architecture.md` includes an explicit state diagram and event-flow diagram: choreography's implicit coupling (which events cause which reactions) benefits from being made explicit in documentation, since it is not visible from any single service's code.
- Every consumer needed a retry-then-dead-letter policy (`cfg.UseMessageRetry`, Phase 3) and every transition needed to be independently idempotent, both direct consequences of choreography's reliance on asynchronous, at-least-once event delivery rather than a coordinator that can retry a command synchronously and know definitively whether it succeeded.
