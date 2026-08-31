# ADR 0003: Database per Service

## Status

Accepted.

## Context

The three core services each own a distinct part of the domain - orders, payments, inventory - with different read/write patterns, different consistency requirements (inventory needs row-level locking against overselling; payments need a strict uniqueness guarantee per order; orders need the richest query surface for the frontend's list/filter views), and different lifecycles. The exercise statement requires this explicitly (Phase 1: "each service should have its own database"; `CLAUDE.md`'s "Non-Negotiable Rules": "Database per service... without exception").

A shared database (or a shared schema with per-service views) was the main alternative considered and rejected: it would let one service's migration or query pattern affect another's performance or availability, and it would make "services never call each other's databases directly" unenforceable at the infrastructure level rather than guaranteed by construction.

## Decision

Each service gets its own logical PostgreSQL database (`orderdb`, `paymentdb`, `inventorydb`) inside the shared `orders-postgres` container, each with its own dedicated role that has no grants on the other two databases (`scripts/setup-databases.sql`). No service's `DbContext` ever connects to another service's database; the only cross-service data access is the documented synchronous HTTP call (Order Service checking Inventory Service's stock via its REST API) and the RabbitMQ event contracts - never a direct query.

Using one PostgreSQL container with three logical databases, rather than three separate database server processes, is an infrastructure simplification appropriate to this exercise's scope (local development and a Docker Compose demo, not a production multi-tenant deployment) - it does not weaken the isolation guarantee, since PostgreSQL roles and per-database permissions enforce the same boundary a separate server would.

## Consequences

- Each service's schema evolves independently: Order Service uses an EF Core migration; Payment and Inventory Service apply a tracked `.sql` script directly (see `CLAUDE.md`'s "Key Design Decisions" for why the split exists). Neither can break the other by changing its own schema.
- Any data an order-related view needs from another service (e.g. current stock) must come through that service's API or through an event - there is no shortcut via a SQL join across databases. This is the direct cost of the isolation: some reads that a single shared database could answer in one query now require a network call or a locally cached copy of eventually-consistent data (e.g. Order Service does not know Inventory Service's exact stock level except through the synchronous check at order-creation time).
- Integration tests reflect the same boundary: each service's test suite spins up its own disposable Postgres instance (Testcontainers) and never shares state with another service's tests.
