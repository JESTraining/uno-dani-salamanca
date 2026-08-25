# Architecture

This document expands on the summary diagram in the root [`README.md`](../README.md) with the detail that belongs in a dedicated reference: component responsibilities, the two communication paths the system uses, the order saga's state machine, and the cross-cutting concerns added in Phase 5. It describes the system as implemented; the reasoning behind each major decision lives in [`docs/adr/`](adr/README.md), and the authoritative rules and event contracts live in [`CLAUDE.md`](../CLAUDE.md) - this document does not restate those, it points to them.

## Components

**Frontend** (`src/Frontend/`) - Angular, standalone components, NgRx for the cross-cutting `orders` feature slice. Talks to exactly one backend URL: the API Gateway. Receives live order status updates over a SignalR connection to Order Service's hub, proxied through the Gateway's WebSocket support.

**API Gateway** (`src/ApiGateway/`) - YARP reverse proxy. The single entry point the frontend uses; the only service that issues JWTs (`POST /api/v1/auth/login`, against a minimal demo user store); the only place CORS is configured. Routes every `/api/v1/...` request and the `/hubs/orders` WebSocket traffic to the matching downstream service. Has no database of its own and no business logic beyond authentication - see [ADR 0004](adr/0004-key-technology-choices.md) for why YARP was chosen over Ocelot.

**Order Service** (`src/OrderService/`) - owns order creation, the order status state machine (below), and the saga's orchestrating side: it is the only service that both publishes events to kick off the saga and consumes events to react to its outcome. Exposes `POST/GET/PUT/DELETE /api/v1/orders`. Makes one direct, documented synchronous call of its own (not through the Gateway) - to Inventory Service, to check stock availability before creating an order.

**Payment Service** (`src/PaymentService/`) - owns payment processing against a simulated gateway (mock fraud/failure rules, 2-5 second delay). Reacts to `OrderCreatedEvent`; has no endpoint that Order Service or the frontend calls synchronously to trigger a charge - payment is entirely event-driven.

**Inventory Service** (`src/InventoryService/`) - owns product catalog and stock reservations. Reacts to `PaymentProcessedEvent` to reserve stock, and runs a background service that releases any reservation not confirmed within 5 minutes. Exposes `GET /api/v1/products/{id}` for Order Service's synchronous stock check and `POST /api/v1/products` (Admin-only) for catalog management.

**RabbitMQ** - the only channel for asynchronous communication between services (see "Asynchronous Flow" below). Each event has its own direct exchange; MassTransit retries a failed consumer 3 times with exponential backoff before dead-lettering to an auto-created `<queue>_error` queue.

**PostgreSQL** - one logical database per service (`orderdb`, `paymentdb`, `inventorydb`), each with its own role and no cross-service access. Order Service's schema is managed by an EF Core migration; Payment and Inventory Service apply their tracked `scripts/*.sql` files directly (see [ADR 0001](adr/0001-sql-over-nosql.md) and [ADR 0003](adr/0003-database-per-service.md)).

**Observability stack** (Phase 5, Docker Compose only) - Seq for structured logs (every service writes via Serilog), Jaeger for distributed traces (OpenTelemetry, OTLP), Prometheus for metrics (`prometheus-net.AspNetCore`'s `/metrics` on every backend service).

## Synchronous Flow

```
Frontend ──HTTPS/WSS──▶ API Gateway ──┬──▶ Order Service
                                       ├──▶ Payment Service
                                       └──▶ Inventory Service

Order Service ──HTTP (documented exception)──▶ Inventory Service  (stock check only)
```

The frontend never calls a microservice directly - every request and the SignalR connection go through the Gateway (`environment.ts`'s single `gatewayUrl`). The Gateway adds authentication (JWT bearer, validated independently by whichever downstream service is JWT-protected - today, only Inventory Service's `POST /api/v1/products`), CORS, and rate limiting, then proxies as-is; it does not transform request or response bodies. Order Service's call to Inventory Service is the one standing exception to "frontend goes through the Gateway" because it is not frontend traffic - it is Order Service checking stock before accepting an order, synchronously, before the saga's asynchronous phase even starts.

## Asynchronous Flow

All inter-service communication that is not the stock check above happens through RabbitMQ, using the event contracts defined in [`CLAUDE.md`](../CLAUDE.md#architecture-non-negotiable-rules) (reproduced there, not duplicated here, since that table is the single source of truth for field lists). At a glance, the events and their direction:

```
Order Service ──OrderCreatedEvent────────▶ Payment Service, Inventory Service

Payment Service ──PaymentProcessedEvent──▶ Order Service, Inventory Service
Payment Service ──PaymentFailedEvent─────▶ Order Service

Inventory Service ──InventoryReservedEvent─▶ Order Service
Inventory Service ──InventoryFailedEvent───▶ Order Service

Order Service ──OrderCompletedEvent───────▶ (published on success; no current consumer)
Order Service ──OrderStatusChangedEvent───▶ (published on every transition; no current consumer)
```

Order Service is both a publisher (`OrderCreatedEvent`, `OrderCompletedEvent`, `OrderStatusChangedEvent`) and the consumer that closes the loop (`PaymentProcessedEvent`, `PaymentFailedEvent`, `InventoryReservedEvent`, `InventoryFailedEvent`) - it is the saga's orchestrator even though the pattern itself is choreographed (each service reacts to events with no central coordinator process; see [ADR 0002](adr/0002-saga-pattern-for-order-processing.md)). `OrderCompletedEvent` and `OrderStatusChangedEvent` currently have no consumer in this repository; they exist so a future service (e.g. a notifications service) can subscribe without any change to Order Service.

## Order Status State Machine

Implemented as guarded transition methods on the `Order` domain entity (`OrderService.Domain/Order.cs`), each a no-op if the order already reached or passed the target status - the mechanism that makes every consumer idempotent against redelivery.

```
                    ┌──────────────────┐
                    │     Pending      │
                    └────────┬─────────┘
                             │ StartPaymentProcessing()
                             │ (synchronous, on order creation)
                             ▼
                    ┌──────────────────┐
        ┌───────────┤ PaymentProcessing├───────────┐
        │           └────────┬─────────┘           │
        │ MarkPaymentFailed()│ MarkPaymentProcessed()│ Cancel()
        │ (PaymentFailedEvent)                      │ (Pending/PaymentProcessing only)
        ▼                    ▼                      ▼
┌───────────────┐   ┌──────────────────┐    ┌──────────────┐
│ PaymentFailed │   │InventoryProcessing│    │  Cancelled   │
│  (terminal)   │   └────────┬─────────┘    │  (terminal)  │
└───────────────┘            │                └──────────────┘
                    ┌─────────┴─────────┐
        MarkInventoryFailed()      Complete()
        (InventoryFailedEvent)  (InventoryReservedEvent)
                    ▼                    ▼
          ┌──────────────────┐  ┌──────────────┐
          │ InventoryFailed  │  │  Completed   │
          │   (terminal)     │  │  (terminal)  │
          └──────────────────┘  └──────────────┘
```

`Shipped` and `Delivered` also exist on the `OrderStatus` enum for the immutability business rule ("orders in Shipped or Delivered status are immutable") but have no producer in the current saga - there is no shipping/fulfillment service in this exercise's scope, so nothing ever transitions an order into them today. `OrderStatusChangedEvent` is published on every transition above, which is how the frontend's SignalR-driven live status updates work without polling.

## Cross-Cutting Concerns

**Security** - JWT bearer auth, issued only by the Gateway against a minimal demo user store, validated independently by every service via a shared signing key. Only `POST /api/v1/products` is actually gated. CORS is configured with an explicit origin list, only at the Gateway. All SQL access goes through EF Core's parameterized queries. Full reasoning: `CLAUDE.md`'s Security section.

**Observability** - Serilog (Console/File/Seq) for logs, OpenTelemetry/OTLP/Jaeger for traces, `prometheus-net.AspNetCore` for metrics (see [ADR 0004](adr/0004-key-technology-choices.md) for why not OpenTelemetry's own Prometheus exporter). `/health`, `/ready`, `/live` on every backend service.

**Resilience** - every RabbitMQ consumer retries 3 times with exponential backoff before MassTransit dead-letters the message; Inventory Service's stock reservations are protected against overselling via PostgreSQL's `xmin` optimistic concurrency token; a background service releases reservations not confirmed within 5 minutes.
