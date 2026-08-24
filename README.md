# Real-time Order Processing System

Microservices-based order processing platform with event-driven communication and real-time status updates, built with C#/.NET, PostgreSQL, RabbitMQ, and Angular.

This repository implements the technical exercise described in [`docs/technical-exercise.md`](docs/technical-exercise.md). This README documents the system as it stands today; the linked document is the original specification the project is built against.

## Overview

Customers place orders through the API. Each order goes through a saga that coordinates three independent services — Order, Payment, and Inventory — communicating asynchronously over RabbitMQ, each with its own database. The frontend reflects order status changes in real time via SignalR.

## Architecture

```
┌─────────────────────────────────────────────────────────────┐
│                    Frontend (Angular)                       │
│                   - Order Management Dashboard              │
│                   - Real-time Status Updates                │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    API Gateway (C#)                         │
│                 - Authentication/Authorization              │
│                 - Request Routing                           │
│                 - Rate Limiting                             │
└─────────────────────────────────────────────────────────────┘
                              │
        ┌─────────────────────┼─────────────────────┐
        ▼                     ▼                     ▼
┌───────────────┐   ┌───────────────┐   ┌───────────────┐
│ Order Service │   │Payment Service│   │Inventory Svc  │
│ (C#/Postgres) │   │ (C#/Postgres) │   │ (C#/Postgres) │
└───────────────┘   └───────────────┘   └───────────────┘
        │                     │                     │
        └─────────────────────┼─────────────────────┘
                              │
                        ┌─────────────┐
                        │  RabbitMQ   │
                        │  (Events)   │
                        └─────────────┘
```

Each service owns its own database exclusively; there is no shared schema or cross-service database access. Services never call each other's databases directly. The frontend now calls only the API Gateway, which routes to Order/Payment/Inventory Service and forwards the SignalR hub's WebSocket traffic.

## Technology Stack

| Layer | Technology |
|---|---|
| Backend | C# / .NET 10 |
| API Gateway | YARP reverse proxy, JWT bearer auth, rate limiting |
| Data access | Entity Framework Core, PostgreSQL |
| Messaging | RabbitMQ via MassTransit (8.x) |
| Frontend | Angular (standalone components), NgRx, Angular Material |
| Real-time updates | SignalR |
| Logging | Serilog (Console, File, Seq) |
| Observability | OpenTelemetry tracing (Jaeger), Prometheus metrics |
| Containers | Docker, Docker Compose |
| Testing | xUnit, Testcontainers, WebApplicationFactory, Vitest |

## Project Status

| Phase | Scope | Status |
|---|---|---|
| 1 | Database schemas (all services) + Order Service | Complete |
| 2 | Payment Service + Inventory Service | Complete |
| 3 | RabbitMQ event contracts + Saga pattern | Complete |
| 4 | Frontend (Angular) | Complete |
| 5 | API Gateway, Docker Compose + production readiness | Complete |
| 6 | Full test suite + documentation | Pending |

All three core services (Order, Payment, Inventory) are implemented end-to-end and verified together against real RabbitMQ and PostgreSQL. Creating an order automatically triggers payment processing and, on success, inventory reservation - and the saga closes the loop back to Order Service, which reacts to the payment/inventory outcome and drives the order all the way to `Completed` (or a `PaymentFailed`/`InventoryFailed` terminal state), with no manual steps anywhere in the flow. Every consumer across all three services retries 3 times with exponential backoff before a failed message is dead-lettered. The Angular frontend (`src/Frontend/`) provides order creation, a live-updating order list, and an order detail view with a status timeline, driven in real time by a SignalR hub in Order Service - no polling, no page refresh.

Phase 5 added the API Gateway (`src/ApiGateway/`, YARP), the single URL the frontend now talks to; a minimal demo JWT login gating only Inventory Service's product-creation endpoint; API versioning (`/api/v1/...` everywhere); Serilog structured logging; `/health`/`/ready`/`/live` health checks; OpenTelemetry tracing to Jaeger and Prometheus metrics; and full containerization via Docker Compose. 173 backend tests (99 Order, 25 Payment, 38 Inventory, 11 Gateway) and 54 frontend tests, all passing.

## Getting Started

### Prerequisites

- Docker Desktop (both options below use it; Option B also runs the .NET/Angular processes on the host)
- .NET 10 SDK — Option B only
- Node.js 22+ and Angular CLI (`npm install -g @angular/cli`) — Option B only

### Option A: Docker Compose (recommended)

Brings up the entire system — Postgres, RabbitMQ, Seq, Jaeger, Prometheus, all four backend services, and the frontend — with a single command, in a clean environment:

```bash
cd docker
docker compose up --build
```

| Endpoint | URL |
|---|---|
| Frontend | `http://localhost:4200` |
| API Gateway (Swagger) | `http://localhost:5013/swagger` |
| RabbitMQ management UI | `http://localhost:15672` (`guest` / `guest`) |
| Seq (structured logs) | `http://localhost:8081` |
| Jaeger (distributed traces) | `http://localhost:16686` |
| Prometheus (metrics) | `http://localhost:9090` |

`docker-compose.override.yml` is merged automatically and runs every service via `dotnet watch run` / `ng serve` with the local source bind-mounted, so edits hot-reload the same way they would running locally. Run `docker compose -f docker-compose.yml up --build` explicitly to skip the override and run the production images instead.

Demo JWT credentials (see [`CLAUDE.md`](CLAUDE.md)'s Security section for the reasoning): `admin` / `Admin123!` (role `Admin`, the only role that can create products) and `customer` / `Customer123!` (role `Customer`). Obtain a token with `POST /api/v1/auth/login` against the Gateway.

### Option B: Manual (run each process yourself)

Useful for debugging a single service without the rest of the stack.

**1. Start the infrastructure**

```bash
docker run --name orders-postgres -e POSTGRES_PASSWORD=devpassword -p 5432:5432 -d postgres:16
docker run --name orders-rabbitmq -p 5672:5672 -p 15672:15672 -d rabbitmq:3-management
```

**2. Provision the databases**

Run against the `orders-postgres` container, in order:

```bash
docker exec -i orders-postgres psql -U postgres < scripts/setup-databases.sql
docker exec -i orders-postgres psql -U postgres -d paymentdb < scripts/payment-service-schema.sql
docker exec -i orders-postgres psql -U postgres -d inventorydb < scripts/inventory-service-schema.sql
```

This creates three logical databases (`orderdb`, `paymentdb`, `inventorydb`), each with its own dedicated role. Order Service's own schema is applied automatically on startup instead (`Database.Migrate()`, next step) — no manual `dotnet ef database update` needed.

**3. Run Order Service**

```bash
cd src/OrderService/OrderService.API
dotnet run --launch-profile http
```

Listens on `http://localhost:5290`, Swagger at `http://localhost:5290/swagger/index.html` (Swagger UI is served from a versioned path now — see the `.http` file or Swagger's own version dropdown).

**4. Run Payment Service**

```bash
cd src/PaymentService/PaymentService.API
dotnet run --launch-profile http
```

Listens on `http://localhost:5033`.

**5. Run Inventory Service**

```bash
cd src/InventoryService/InventoryService.API
dotnet run --launch-profile http
```

Listens on `http://localhost:5225`.

**6. Run the API Gateway**

```bash
cd src/ApiGateway/ApiGateway.API
dotnet run --launch-profile http
```

Listens on `http://localhost:5013`, Swagger at `http://localhost:5013/swagger`. Requires Order/Payment/Inventory Service already running (steps 3-5) - it proxies every request to them and issues/validates the demo JWTs.

**7. Run the Frontend**

```bash
cd src/Frontend
npm install
npm start
```

Listens on `http://localhost:4200`. Talks only to the Gateway (`http://localhost:5013`, step 6) and connects to Order Service's SignalR hub through it at `/hubs/orders`.

### Try the full system

Use the Angular app at `http://localhost:4200`: create a product first via the Gateway's Swagger (`http://localhost:5013/swagger`, `POST /api/v1/products` requires an Admin bearer token — no product-management UI exists in the frontend yet, that is admin-dashboard bonus scope), then create an order from the frontend's "New Order" form. Watch the order list and detail view update live, with no page refresh, as the order moves through `PaymentProcessing` → `InventoryProcessing` → `Completed` (or a failure branch).

Alternatively, drive it purely over HTTP: import the collections in `docs/api/` into Postman (one per service, plus the Gateway — see [`docs/api/README.md`](docs/api/README.md)), or use each service's own `*.http` file from VS Code or Visual Studio.

Through the Gateway (`http://localhost:5013`):
1. `POST /api/v1/auth/login` with the demo admin credentials to obtain a bearer token.
2. Create a product with `POST /api/v1/products`, using that token.
3. Create an order with `POST /api/v1/orders`, using that product's id.
4. Poll `GET /api/v1/orders/{id}` over the next few seconds: the order moves from `PaymentProcessing` to `InventoryProcessing` to `Completed` (or lands in `PaymentFailed`/`InventoryFailed`, per the mock gateway rules and available stock) — entirely automatic, driven by RabbitMQ events, with no further requests needed. `GET /api/v1/payments/{orderId}` and the product's `reservedQuantity` reflect the same outcome along the way.

## Running Tests

```bash
cd src/OrderService && dotnet test
cd src/PaymentService && dotnet test
cd src/InventoryService && dotnet test
cd src/ApiGateway && dotnet test
cd src/Frontend && npm test
```

173 backend tests across the four services (99 Order, 25 Payment, 38 Inventory, 11 Gateway): domain rules, application use cases, consumer wiring (MassTransit's in-memory test harness), end-to-end in-memory saga flow tests covering every branch (success and both failure paths) plus duplicate-event idempotency, a retry/dead-letter exhaustion test, HTTP integration tests running against a real, disposable PostgreSQL instance (Testcontainers) — including a concurrency test that races two orders for the last unit of stock — and JWT issuance/validation/role-claim tests for the Gateway.

54 frontend tests (Vitest): NgRx reducer/effects/selectors, the SignalR-to-store bridge, the HTTP error and auth interceptors, and component specs for each page and the shared status-badge/loading-skeleton components.

Run the four backend commands one at a time, not in parallel: the integration tests connect to the real `orders-rabbitmq` broker (only Postgres is containerized per-test via Testcontainers), so two services' suites running at once can cross-deliver real messages mid-test.

## Project Structure

```
/
├── src/
│   ├── OrderService/       Implemented (Phase 1)
│   ├── PaymentService/     Implemented (Phase 2)
│   ├── InventoryService/   Implemented (Phase 2)
│   ├── ApiGateway/         Implemented (Phase 5, YARP + JWT)
│   └── Frontend/           Implemented (Phase 4, Angular)
├── docker/                 Implemented (Phase 5): docker-compose.yml, docker-compose.override.yml, prometheus.yml
├── docs/
│   ├── api/                Postman collection per service
│   └── technical-exercise.md
├── scripts/                Database setup and schema scripts
├── CLAUDE.md                Project rules and conventions
└── README.md
```

## Further Reading

- [`docs/technical-exercise.md`](docs/technical-exercise.md) — full original requirements, business rules, and evaluation criteria.
- [`CLAUDE.md`](CLAUDE.md) — architecture rules, established patterns, and non-negotiable business rules for anyone contributing to this repository.
- [`docs/api/`](docs/api/README.md) — Postman collections, one per service.
