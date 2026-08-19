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
| 2 | Payment Service + Inventory Service | Pending |
| 3 | RabbitMQ event contracts + Saga pattern | Pending |
| 4 | Frontend (Angular) | Pending |
| 5 | Docker Compose + production readiness | Pending |
| 6 | Full test suite + documentation | Pending |

Order Service is implemented end-to-end (REST API, EF Core, RabbitMQ publishing, 38 automated tests). The other services, the API Gateway, the frontend, and the Docker setup do not exist yet.

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

### 3. Run the Order Service

```bash
cd src/OrderService
dotnet ef database update --project OrderService.Infrastructure --startup-project OrderService.API
cd OrderService.API
dotnet run --launch-profile http
```

The API listens on `http://localhost:5290`, with Swagger at `http://localhost:5290/swagger`.

### 4. Try the API

Import [`docs/api/OrderService.postman_collection.json`](docs/api/OrderService.postman_collection.json) into Postman, or use `src/OrderService/OrderService.API/OrderService.API.http` directly from VS Code or Visual Studio.

Note: `POST /api/orders` currently returns `503 Service Unavailable` by design — it validates stock against the Inventory Service before creating an order, and that service does not exist until Phase 2.

## Running Tests

```bash
cd src/OrderService
dotnet test
```

38 tests: domain rules, application use cases, and HTTP integration tests running against a real, disposable PostgreSQL instance (Testcontainers).

## Project Structure

```
/
├── src/
│   ├── OrderService/       Order Service (implemented)
│   ├── PaymentService/     Phase 2
│   ├── InventoryService/   Phase 2
│   ├── ApiGateway/         Phase 5
│   └── Frontend/           Phase 4 (Angular)
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
