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
| 6 | Full test suite + documentation | Complete |

All three core services (Order, Payment, Inventory) are implemented end-to-end and verified together against real RabbitMQ and PostgreSQL. Creating an order automatically triggers payment processing and, on success, inventory reservation - and the saga closes the loop back to Order Service, which reacts to the payment/inventory outcome and drives the order all the way to `Completed` (or a `PaymentFailed`/`InventoryFailed` terminal state), with no manual steps anywhere in the flow. Every consumer across all three services retries 3 times with exponential backoff before a failed message is dead-lettered. The Angular frontend (`src/Frontend/`) provides order creation, a live-updating order list, and an order detail view with a status timeline, driven in real time by a SignalR hub in Order Service - no polling, no page refresh.

Phase 5 added the API Gateway (`src/ApiGateway/`, YARP), the single URL the frontend now talks to; a minimal demo JWT login gating only Inventory Service's product-creation endpoint; API versioning (`/api/v1/...` everywhere); Serilog structured logging; `/health`/`/ready`/`/live` health checks; OpenTelemetry tracing to Jaeger and Prometheus metrics; and full containerization via Docker Compose. Phase 6 closed out the exercise with measured test coverage (all four backend services and the frontend well above the 70% minimum on service logic/domain/event handlers - see "Running Tests" below) and the final documentation set: [`docs/architecture.md`](docs/architecture.md) and [`docs/adr/`](docs/adr/README.md). 176 backend tests (99 Order, 25 Payment, 38 Inventory, 14 Gateway) and 68 frontend tests, all passing.

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

## Configuration Reference

Every setting below is read from each service's `appsettings.json` via ASP.NET Core's configuration binding, which means any of them can be overridden with an environment variable using the `Section__Key` naming convention (double underscore for nesting) without touching a file - this is how `docker/docker-compose.yml` sets `ASPNETCORE_ENVIRONMENT=Docker` to select each service's `appsettings.Docker.json` overrides, and how a real deployment would inject secrets instead of using the dev-only literals checked into the repo.

| Variable | Services | Purpose | Local dev default |
|---|---|---|---|
| `ConnectionStrings__OrderDb` / `PaymentDb` / `InventoryDb` | Order / Payment / Inventory | PostgreSQL connection string for that service's own database | `Host=localhost;Port=5432;Database=<db>;Username=<role>;Password=<role>_dev_pwd` |
| `RabbitMq__Host`, `RabbitMq__Username`, `RabbitMq__Password` | Order, Payment, Inventory | RabbitMQ broker connection | `localhost` / `guest` / `guest` |
| `Services__InventoryService__BaseUrl` | Order | Base URL for the one documented direct service-to-service call (stock check before creating an order) | `http://localhost:5225` |
| `Cors__AllowedOrigins__0` | All four | Allowed browser origin(s) for CORS (array; only the Gateway's is exercised by a real caller today - see `CLAUDE.md`) | `http://localhost:4200` |
| `Jwt__SigningKey`, `Jwt__Issuer`, `Jwt__Audience` | Gateway (issues), Inventory (validates) | Shared HMAC signing key/issuer/audience for JWT bearer tokens - **must** be overridden with a real secret outside local dev | dev-only literal in `appsettings.json` |
| `Jwt__ExpirationMinutes` | Gateway | Issued token lifetime | `60` |
| `Otlp__Endpoint` | All four | OTLP endpoint OpenTelemetry traces are exported to (Jaeger) | `http://localhost:4317` |
| `Serilog__WriteTo__2__Args__serverUrl` | All four | Seq server URL (the third `WriteTo` sink, after Console/File) | `http://localhost:5341` |
| `ASPNETCORE_ENVIRONMENT` | All four | Selects the `appsettings.{Environment}.json` overlay - `Docker` in Compose, unset (`Development`) for local `dotnet run` | `Development` |
| `ASPNETCORE_URLS` | All four | Kestrel listen URL(s) - Compose sets this to `http://+:8080` inside each container | launch profile-specific (`http://localhost:5290` etc.) |
| `ReverseProxy__Clusters__*__Destinations__destination1__Address` | Gateway | YARP's proxy target for each downstream service - Compose overrides these to the container service names (`http://order-service:8080/` etc.) | `http://localhost:5290/` etc. |
| `POSTGRES_PASSWORD` | `postgres` container only | Superuser password for the shared Postgres container the three service databases live in | `devpassword` |

## Running Tests

```bash
cd src/OrderService && dotnet test
cd src/PaymentService && dotnet test
cd src/InventoryService && dotnet test
cd src/ApiGateway && dotnet test
cd src/Frontend && npm test
```

176 backend tests across the four services (99 Order, 25 Payment, 38 Inventory, 14 Gateway): domain rules, application use cases, consumer wiring (MassTransit's in-memory test harness), end-to-end in-memory saga flow tests covering every branch (success and both failure paths) plus duplicate-event idempotency, a retry/dead-letter exhaustion test, HTTP integration tests running against a real, disposable PostgreSQL instance (Testcontainers) — including a concurrency test that races two orders for the last unit of stock — and JWT issuance/validation/role-claim tests for the Gateway (including an integration test that boots the Gateway's real ASP.NET Core pipeline end-to-end, not just mocked services).

68 frontend tests (Vitest): NgRx reducer/effects/selectors (including every action handler, not only the ones exercised by component specs), the SignalR-to-store bridge, the HTTP error and auth interceptors, and component specs for each page and the shared status-badge/loading-skeleton/confirm-dialog components.

Run the four backend commands one at a time, not in parallel: the integration tests connect to the real `orders-rabbitmq` broker (only Postgres is containerized per-test via Testcontainers), so two services' suites running at once can cross-deliver real messages mid-test.

### Test Coverage

The exercise requires a minimum of 70% unit test coverage over service logic, domain models, and event handlers. Measured with `coverlet.collector` (already referenced by every `*.Tests.csproj`) plus the [ReportGenerator](https://github.com/danielpalme/ReportGenerator) local tool (`.config/dotnet-tools.json` - run `dotnet tool restore` once, then `dotnet test --collect:"XPlat Code Coverage"` followed by `dotnet reportgenerator -reports:<service>/**/coverage.cobertura.xml -targetdir:coverage-reports/<service> -reporttypes:TextSummary`) and, for the frontend, `@vitest/coverage-v8` (`npm test -- --code-coverage`). Generated code (ASP.NET Core's OpenAPI source generator output, compiler-emitted attribute classes) is excluded from the backend numbers below, since it isn't code this project owns:

| Service | Line coverage | Branch coverage |
|---|---|---|
| Order Service | 93% | 83.5% |
| Payment Service | 90.5% | 84.6% |
| Inventory Service | 90.6% | 69.3% |
| API Gateway | 98% | 85% |
| Frontend (statements) | 78.5% | 70.6% |

All four backend services exceed the 70% minimum by a wide margin on `*.Application`/`*.Domain`/`*.Infrastructure` (the layers the requirement targets); the raw, unfiltered number for each `*.API` project is lower only because it includes generated code and thin `Program.cs` startup wiring, not because business logic is undertested. The frontend has no exercise-mandated percentage (Task 6.1's coverage requirement is stated for the xUnit/NUnit backend suites) but is included for completeness.

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
│   ├── adr/                Implemented (Phase 6): Architecture Decision Records
│   ├── architecture.md     Implemented (Phase 6)
│   └── technical-exercise.md
├── scripts/                Database setup and schema scripts
├── CLAUDE.md                Project rules and conventions
└── README.md
```

## Further Reading

- [`docs/technical-exercise.md`](docs/technical-exercise.md) — full original requirements, business rules, and evaluation criteria.
- [`CLAUDE.md`](CLAUDE.md) — architecture rules, established patterns, and non-negotiable business rules for anyone contributing to this repository.
- [`docs/architecture.md`](docs/architecture.md) — components, synchronous/asynchronous flow diagrams, and the order status state machine.
- [`docs/adr/`](docs/adr/README.md) — Architecture Decision Records (SQL vs NoSQL, Saga pattern, database per service, key technology choices).
- [`docs/api/`](docs/api/README.md) — Postman collections, one per service.
