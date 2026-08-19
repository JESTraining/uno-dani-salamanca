# CLAUDE.md

This file is the source of mandatory rules for any agent (Claude Code or otherwise) working on this repository. Any agent must read this document in full before writing or modifying code.

## Repository Purpose

This repository implements the technical exercise "Full-Stack Microservices Exercise: Real-time Order Processing System": an order processing system based on microservices (C#/.NET, SQL, RabbitMQ) with an Angular frontend and real-time status updates via SignalR.

## Current Repository State

Phase 1 is complete: the database schemas for all three services exist (`orderdb` via EF Core migrations, `paymentdb` and `inventorydb` via the scripts in `scripts/`) and Order Service is implemented end-to-end in `src/OrderService/` (REST API, EF Core, RabbitMQ, 38 automated tests passing). Payment Service, Inventory Service, API Gateway, Frontend, and Docker infrastructure do not exist yet.

This summary is updated at the close of each phase, but it may fall out of date between sessions. Any agent must verify the real state using the repository's own search tools before assuming something is or is not implemented.

## Source of Truth

[`docs/technical-exercise.md`](docs/technical-exercise.md) contains the full technical exercise statement, in English, organized into six phases (Phase 1 through Phase 6) with their tasks, business rules, evaluation criteria, and bonus challenges. It is the project's source specification; in case of any ambiguity, it takes precedence over any other interpretation. `README.md`, at the repository root, is the project's public presentation (for whoever evaluates or clones it), not the specification — it must not be used as a source of requirements.

Before starting work on a phase, an agent must read the corresponding section of `docs/technical-exercise.md` in full. Do not move on to the next phase without having covered its requirements (or having explicitly agreed on an exception with the user).

This file (`CLAUDE.md`) acts as the project's status board: the "Current Repository State" section is updated at the close of each phase.

## Architecture: Non-Negotiable Rules

- **Database per service.** Order Service, Payment Service, and Inventory Service each have their own database, without exception. No service accesses another service's database directly, whether through a direct connection or a shared view.
- **Synchronous communication only via API Gateway or explicitly documented REST calls.** The frontend never calls a microservice directly; it always goes through the API Gateway.
- **Asynchronous communication only via RabbitMQ**, using exactly these event contracts (JSON format):

  | Event | Fields |
  |--------|--------|
  | `OrderCreatedEvent` | OrderId, CustomerId, TotalAmount, Items, Timestamp |
  | `PaymentProcessedEvent` | OrderId, TransactionId, Amount, Timestamp |
  | `PaymentFailedEvent` | OrderId, Reason, Timestamp |
  | `InventoryReservedEvent` | OrderId, Items, Timestamp |
  | `InventoryFailedEvent` | OrderId, Reason, Timestamp |
  | `OrderCompletedEvent` | OrderId, Status, Timestamp |
  | `OrderStatusChangedEvent` | OrderId, PreviousStatus, NewStatus, Timestamp |

  `OrderStatusChangedEvent` is not in the original exercise statement: it was added in Phase 1 because Order Service publishes an event on every status change (not only on creation), and the original contract only covered creation and completion. Implemented in `OrderService.Application.IntegrationEvents`. If an agent needs to add a field or a new event, it must update this table in the same change — do not let code and documentation drift apart.

- **The Saga pattern governs the entire order flow** (creation, payment, inventory reservation, completion or compensation). No implementation may introduce an alternate path that completes an order without going through the described Saga flow.
- **Idempotency is mandatory** for order creation and payment processing. Every event handler must be idempotent (reprocessing the same event twice must not duplicate effects).

## Technology Stack

Do not change these choices without explicit confirmation from the user:

- **Backend:** C# on .NET 10 (current LTS; the original exercise statement mentions .NET 8, but the project adopts the current LTS installed in the development environment).
- **Data access:** Entity Framework Core (decision made in Phase 1; do not use Dapper, to keep a single data access pattern across services), with the Repository and Unit of Work pattern. Column naming in snake_case via the `EFCore.NamingConventions` package (`UseSnakeCaseNamingConvention()` in each `DbContext`), to match the hand-written SQL scripts.
- **Databases:** PostgreSQL (decision made in Phase 1). The original exercise statement allows SQL Server or MySQL, but a single `orders-postgres` container already exists, with one logical database and one dedicated role per service (`orderdb`/`order_service`, `paymentdb`/`payment_service`, `inventorydb`/`inventory_service` — see `scripts/setup-databases.sql`). Keep Postgres for Payment and Inventory Service unless the user explicitly asks to switch engines.
- **Optimistic concurrency:** PostgreSQL's `xmin` system column as the concurrency token (shadow property `uint xmin` with `IsConcurrencyToken()` + `HasColumnType("xid")`), without adding manual version columns unless the engine is not Postgres. See `OrderConfiguration` in `OrderService.Infrastructure` as a reference.
- **Messaging:** RabbitMQ via MassTransit, **pinned to version 8.x** (`MassTransit.RabbitMQ` 8.5.10 or any later 8.x version; exact version, not floating). **Never upgrade to MassTransit 9 or higher**: those versions require a commercial license (`SetLicense`/`SetLicenseLocation` or the `MT_LICENSE`/`MT_LICENSE_PATH` environment variables) and the application fails to start without one (`MassTransit.ConfigurationException` at host startup). Topic or direct exchanges, dead letter queues, and exponential backoff retry (3 attempts), to be defined in Phase 3.
- **Frontend:** Angular (decision made). Do not use React in this project. UI with Angular Material, PrimeNG, or ng-bootstrap; state with NgRx.
- **Real-time:** SignalR (hub in Order Service, client in the frontend).
- **Containers:** Docker with multi-stage Dockerfiles, orchestrated with Docker Compose.
- **Logging:** Serilog, with configurable sinks (Console, File, Seq).
- **Observability:** OpenTelemetry (tracing) and Prometheus (metrics) starting in Phase 5.
- **Authentication:** JWT with role-based authorization, starting in Phase 5.

## Code Conventions

- Follow SOLID and DRY principles. Do not create abstractions or extra layers that the current requirement does not need.
- Use async/await for every I/O operation (database, HTTP, messaging).
- Use .NET's native dependency injection container; avoid the service locator pattern.
- Naming: PascalCase for classes, methods, and public properties in C#; camelCase for local variables and parameters; camelCase for variables and functions in JavaScript/TypeScript, PascalCase for React components.
- **All code comments must be written in English**, regardless of the language of the surrounding project documentation.
- Code identifiers, variable names, class names, and commit messages are written in English, following standard technical convention.
- All project documentation that ships in the repository (this file, READMEs, ADRs once they exist) is written in English. The `plan/` folder is local-only planning material, excluded from the repository via `.gitignore`, and stays in Spanish for the user's own tracking — it is never a source of requirements for anyone outside this project.
- Do not add comments that describe what the code does when the function or variable name already makes it clear. Only comment when there is a non-obvious reason (an external constraint, a specific workaround, a decision that would surprise someone reading the code later).
- Do not leave implementations half-finished, nor functionality behind "just in case" flags. If a task is marked complete, it must be finished and tested.
- Commit messages must be descriptive and in English, preferably in Conventional Commits format (`feat:`, `fix:`, `docs:`, `test:`, `chore:`).
- In DTOs defined as `record` with a primary constructor, validation attributes (`[Required]`, `[MaxLength]`, `[Range]`, etc.) go directly on the parameter, without the `[property: ...]` prefix. With `property:`, ASP.NET Core throws an `InvalidOperationException` at runtime ("validation metadata defined on property... must be associated with the constructor parameter") instead of returning 400. See `OrderService.Application/Contracts/OrderDtos.cs`.

## Established Architecture Pattern (replicate in Payment and Inventory Service)

Order Service (Phase 1, in `src/OrderService/`) sets the pattern that Payment Service and Inventory Service must follow in Phase 2, so that all three services stay consistent:

- Five projects per service: `<Service>.Domain` (entities and business rules as methods, no public setters), `<Service>.Application` (DTOs in `Contracts/`, interfaces in `Abstractions/`, use cases in `Services/`, integration events in `IntegrationEvents/`, exceptions in `Exceptions/`), `<Service>.Infrastructure` (`DbContext` and EF Core configurations in `Persistence/`, event publisher in `Messaging/`, outbound HTTP clients in `ExternalServices/`), `<Service>.API` (`Controllers/`, `Middleware/`, `Program.cs`), `<Service>.Tests` (subfolders `Domain/`, `Application/`, `Unit/`, `Integration/`).
- Centralized error handling in an `ExceptionHandlingMiddleware` per service, translating domain/application exceptions into HTTP status codes (404 not found, 409 state or concurrency conflict, 422 business rule violation, 503 external dependency unavailable). Do not repeat `try/catch` in every controller.
- Idempotency via the `Idempotency-Key` header, with a dedicated table (`idempotency_keys` in Order Service) whose commit happens in the same transaction as the main operation (same `SaveChangesAsync`), not in a separate step.
- Integration tests with `WebApplicationFactory` + `Testcontainers.PostgreSql` (a real, ephemeral database), replacing in `CustomWebApplicationFactory` only the dependencies on external services that do not exist yet or should not be spun up in the test (see `AlwaysAvailableInventoryChecker` in `OrderService.Tests`).

## Critical Business Rules (must never be violated)

These rules come from the original exercise statement and must be reflected in real code validations, not only in documentation:

**Order Service**
- An order can only be cancelled if it is in `Pending` or `PaymentProcessing` status.
- Orders in `Shipped` or `Delivered` status are immutable.
- The total amount must be greater than 0.
- Every order requires at least one item.

**Payment Service (simulated gateway)**
- Payments over $10,000: always fail (fraud detection).
- Payments whose amount ends in `.99`: 20% random failure probability.
- All other payments: succeed.
- Simulate a 2-to-5-second processing delay with `Task.Delay`.

**Inventory Service**
- Stock is reserved the moment a payment is processed successfully (`PaymentProcessed` event).
- If the reservation is not confirmed within 5 minutes, it is automatically released and a failure event is triggered.
- Every stock operation must be atomic (SQL transaction) and protect against overselling via row-level locking or optimistic concurrency.

See the full exercise statement in [`docs/technical-exercise.md`](docs/technical-exercise.md) before implementing the corresponding logic.

## Security

- All SQL queries must be parameterized. Concatenating user input directly into a query or command is prohibited.
- Validate every input at the API boundary (request payloads); do not rely on frontend validation as the only barrier.
- Never commit secrets, connection strings with real credentials, or API keys. Use environment variables and `appsettings.json` only for non-sensitive configuration.
- JWT is mandatory on endpoints that are not explicitly public; role-based authorization where the exercise statement requires it (for example, product creation restricted to administrators).
- CORS must be configured with explicit origins; do not use a wildcard (`*`) in configurations intended for production.

## Testing

- Minimum unit test coverage: 70%, measured over service logic, domain models, and event handlers.
- Unit tests with xUnit or NUnit on the backend; Jest and React Testing Library on the frontend (if React is chosen).
- Database integration tests with TestContainers or an in-memory database.
- Endpoint integration tests with `WebApplicationFactory`.
- Every critical business rule listed above must have at least one test that covers it explicitly, including its edge cases (for example, a payment of exactly $10,000, an amount ending in `.99`).
- No task is considered done if the code implementing it has no associated tests.

## Docker and DevOps

- Each microservice and the frontend have their own Dockerfile with a multi-stage build.
- Each container exposes a working health check.
- Configuration depends on environment variables; do not hardcode hosts, ports, or credentials in code or in images.
- `docker-compose.yml` must be able to bring up the entire system (all services, RabbitMQ, databases) with a single command, in a clean environment.

## Workflow for Agents

1. Read this file in full before touching any code.
2. Identify which phase the project is in by checking the "Current Repository State" section of this file.
3. Read the corresponding section of `docs/technical-exercise.md` in full before starting to write code.
4. Implement only what that phase asks for; do not get ahead of later phases or bonus challenges unless the user explicitly asks for it.
5. When a phase is completed, update the "Current Repository State" section of this file.
6. If an ambiguity is found in the exercise statement, resolve it using whatever criteria best fits the rest of this file's rules, and record the interpretation taken in the code or in the corresponding commit message.
7. Run the relevant tests before considering a task finished.

## Documentation and Communication Style

- Emojis are prohibited anywhere in the repository: code, comments, commits, documentation, or output to the user. The tone must be serious and professional at all times.
- No additional documentation files are created outside the structure already defined (`docs/`, this `CLAUDE.md`) unless the user requests it.

## Explicit Prohibitions

- Do not couple services by sharing a database or by calling another service's database directly.
- Do not introduce a path that completes or modifies an order without going through the described business rules and Saga flow.
- Do not skip idempotency in order creation or payment processing.
- Do not commit secrets, real connection strings, or `.env` files with sensitive values.
- Do not implement bonus challenges (see the "Bonus Challenges" section of `docs/technical-exercise.md`) before completing the six main phases, unless the user explicitly asks for it.
- Do not use `git push --force`, `git reset --hard`, or similar destructive commands without the user's explicit authorization for that specific action.

## Target Folder Structure

Same tree as the "Suggested Folder Structure" section of `docs/technical-exercise.md`, plus the files that already exist outside that original list:

```
/
├── src/
│   ├── OrderService/       (Phase 1 - implemented: Domain/Application/Infrastructure/API/Tests)
│   ├── PaymentService/     (Phase 2 - pending)
│   ├── InventoryService/   (Phase 2 - pending)
│   ├── ApiGateway/         (Phase 5 - pending)
│   └── Frontend/           (Phase 4 - pending, Angular)
├── docker/                 (Phase 5 - pending)
├── docs/
│   ├── api/                (implemented: Postman collection per service)
│   ├── technical-exercise.md  (implemented: original statement, moved from README.md)
│   ├── architecture.md     (Phase 6 - pending)
│   └── adr/                (Phase 6 - pending)
├── scripts/                (implemented: setup-databases.sql, payment-service-schema.sql, inventory-service-schema.sql)
├── CLAUDE.md
├── .gitignore
└── README.md               (public presentation of the project, not the specification)
```

`CLAUDE.md`, `.gitignore`, `scripts/`, `docs/api/`, `docs/technical-exercise.md`, and `src/OrderService/` already exist. The rest of the structure is built incrementally, phase by phase.
