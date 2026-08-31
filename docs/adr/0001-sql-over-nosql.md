# ADR 0001: SQL (PostgreSQL) over NoSQL

## Status

Accepted.

## Context

The exercise statement (`docs/technical-exercise.md`, Phase 1) allows SQL Server, PostgreSQL, or MySQL, and asks the ADR set to justify SQL over NoSQL specifically. The domain has three characteristics that matter for this choice:

- Orders, payments, and inventory reservations are inherently relational: an order has line items, a payment references exactly one order, a reservation references exactly one order and one product. These are foreign-key relationships with referential integrity requirements, not documents that vary in shape.
- Money and stock quantities are involved. Overselling stock or double-charging a payment are the two failure modes the exercise's business rules explicitly guard against (`docs/technical-exercise.md`'s Payment/Inventory business rules; `CLAUDE.md`'s "Critical Business Rules"). Both require real ACID transactions and, for stock, row-level locking or optimistic concurrency - guarantees a NoSQL document or key-value store either does not provide at all, or only approximates through eventual consistency.
- Idempotency (order creation, payment processing) is implemented via unique constraints and existence checks scoped to a single transaction (`payments.order_id` unique, `inventory_reservations.(order_id, product_id)` unique) - a pattern that depends on the database enforcing uniqueness and transactional atomicity itself, rather than the application coordinating it across a schemaless store.

## Decision

Use a relational database - PostgreSQL - for all three services. PostgreSQL specifically (over SQL Server/MySQL, both allowed by the exercise) because a single `orders-postgres` container serving three logical databases with per-service roles was already the environment set up before this project's Phase 1 began, and there was no requirement pulling toward a different engine or toward mixing engines across services.

## Consequences

- Entity Framework Core with the Repository/Unit of Work pattern maps naturally onto this choice and is used identically across all three services (see `CLAUDE.md`'s Technology Stack).
- PostgreSQL's `xmin` system column is used as the optimistic concurrency token for stock reservations, avoiding a hand-rolled version column - a PostgreSQL-specific convenience that would need reconsidering if a service ever moved to SQL Server/MySQL.
- The trade-off NoSQL would have offered - horizontal write scaling, schema flexibility - is not needed here: order volume in this exercise's scope is not a scaling problem, and the schema is well-understood and stable (defined once in Phase 1, not iterated on since).
