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

Each service owns its own database exclusively; there is no shared schema or cross-service database access. Services never call each other's databases directly, and the frontend only ever talks to the API Gateway.

## Technology Stack

| Layer | Technology |
|---|---|
| Backend | C# / .NET 10 |
| Data access | Entity Framework Core, PostgreSQL |
| Messaging | RabbitMQ via MassTransit (8.x) |
| Frontend | Angular, NgRx |
| Real-time updates | SignalR |
| Containers | Docker, Docker Compose |
| Testing | xUnit, Testcontainers, WebApplicationFactory |

## Project Status

| Phase | Scope | Status |
|---|---|---|
| 1 | Database schemas (all services) + Order Service | Complete |
| 2 | Payment Service + Inventory Service | Complete |
| 3 | RabbitMQ event contracts + Saga pattern | Complete |
| 4 | Frontend (Angular) | Pending |
| 5 | Docker Compose + production readiness | Pending |
| 6 | Full test suite + documentation | Pending |

All three core services (Order, Payment, Inventory) are implemented end-to-end and verified together against real RabbitMQ and PostgreSQL. Creating an order automatically triggers payment processing and, on success, inventory reservation - and the saga now closes the loop back to Order Service, which reacts to the payment/inventory outcome and drives the order all the way to `Completed` (or a `PaymentFailed`/`InventoryFailed` terminal state), with no manual steps anywhere in the flow. Every consumer across all three services retries 3 times with exponential backoff before a failed message is dead-lettered. 153 automated tests passing across the three services. The API Gateway, the frontend, and the Docker setup do not exist yet.

## Getting Started

### Prerequisites

- .NET 10 SDK
- Docker Desktop
- (Phase 4 onward) Node.js and Angular CLI

### 1. Start the infrastructure

```bash
docker run --name orders-postgres -e POSTGRES_PASSWORD=devpassword -p 5432:5432 -d postgres:16
docker run --name orders-rabbitmq -p 5672:5672 -p 15672:15672 -d rabbitmq:3-management
```

RabbitMQ management UI: `http://localhost:15672` (`guest` / `guest`).

### 2. Provision the databases

Run against the `orders-postgres` container, in order:

```bash
docker exec -i orders-postgres psql -U postgres < scripts/setup-databases.sql
docker exec -i orders-postgres psql -U postgres -d paymentdb < scripts/payment-service-schema.sql
docker exec -i orders-postgres psql -U postgres -d inventorydb < scripts/inventory-service-schema.sql
```

This creates three logical databases (`orderdb`, `paymentdb`, `inventorydb`), each with its own dedicated role. Order Service's own schema is applied via its EF Core migration instead (next step).

### 3. Run Order Service

```bash
cd src/OrderService
dotnet ef database update --project OrderService.Infrastructure --startup-project OrderService.API
cd OrderService.API
dotnet run --launch-profile http
```

Listens on `http://localhost:5290`, Swagger at `http://localhost:5290/swagger`. Order Service is the only one with an EF Core migration to apply — Payment and Inventory Service map onto the schema already created in step 2.

### 4. Run Payment Service

```bash
cd src/PaymentService/PaymentService.API
dotnet run --launch-profile http
```

Listens on `http://localhost:5033`, Swagger at `http://localhost:5033/swagger`.

### 5. Run Inventory Service

```bash
cd src/InventoryService/InventoryService.API
dotnet run --launch-profile http
```

Listens on `http://localhost:5225`, Swagger at `http://localhost:5225/swagger`.

### 6. Try the full system

Import the three collections in `docs/api/` into Postman (one per service — see [`docs/api/README.md`](docs/api/README.md)), or use each service's own `*.http` file from VS Code or Visual Studio.

With all three services running:
1. Create a product with Inventory Service's `Create Product` request.
2. Create an order with Order Service's `Create Order` request, using that product's id.
3. Poll `GET /api/orders/{id}` on Order Service over the next few seconds: the order moves from `PaymentProcessing` to `InventoryProcessing` to `Completed` (or lands in `PaymentFailed`/`InventoryFailed`, per the mock gateway rules and available stock) — entirely automatic, driven by RabbitMQ events, with no further requests needed. `GET /api/payments/{orderId}` on Payment Service and the product's `reservedQuantity` on Inventory Service reflect the same outcome along the way.

## Running Tests

```bash
cd src/OrderService && dotnet test
cd src/PaymentService && dotnet test
cd src/InventoryService && dotnet test
```

153 tests across the three services (92 Order, 25 Payment, 36 Inventory): domain rules, application use cases, consumer wiring (MassTransit's in-memory test harness), end-to-end in-memory saga flow tests covering every branch (success and both failure paths) plus duplicate-event idempotency, a retry/dead-letter exhaustion test, and HTTP integration tests running against a real, disposable PostgreSQL instance (Testcontainers) — including a concurrency test that races two orders for the last unit of stock.

## Project Structure

```
/
├── src/
│   ├── OrderService/       Implemented (Phase 1)
│   ├── PaymentService/     Implemented (Phase 2)
│   ├── InventoryService/   Implemented (Phase 2)
│   ├── ApiGateway/         Pending (Phase 5)
│   └── Frontend/           Pending (Phase 4, Angular)
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
