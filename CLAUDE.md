# CLAUDE.md

This file is the source of mandatory rules for any agent (Claude Code or otherwise) working on this repository. Any agent must read this document in full before writing or modifying code.

## Repository Purpose

This repository implements the technical exercise "Full-Stack Microservices Exercise: Real-time Order Processing System": an order processing system based on microservices (C#/.NET, SQL, RabbitMQ) with an Angular frontend and real-time status updates via SignalR.

## Current Repository State

Phases 1 through 6 are complete - the exercise's full six-phase scope is implemented, tested, and documented (bonus challenges, see `docs/technical-exercise.md`, remain out of scope unless explicitly requested). All three core services (Order, Payment, Inventory) are implemented end-to-end in `src/` (REST APIs, EF Core against their pre-existing schemas, RabbitMQ publishers and consumers) and verified together against real RabbitMQ and PostgreSQL. The choreographed saga closes the loop: Order Service consumes `PaymentProcessedEvent`, `PaymentFailedEvent`, `InventoryReservedEvent`, and `InventoryFailedEvent` (four consumers, `OrderService.Infrastructure/Messaging`) and drives the order through `Pending -> PaymentProcessing -> InventoryProcessing -> Completed` (publishing `OrderCompletedEvent`) or one of the two terminal failure branches, `PaymentFailed`/`InventoryFailed` - all event handlers idempotent via a status-check guard on the `Order` domain methods that implement each transition. All three services retry a failed consumer 3 times with exponential backoff before MassTransit's RabbitMQ transport dead-letters the message to an automatically-created `<queue>_error` queue (`cfg.UseMessageRetry` in every `Program.cs`). Order Service also runs a SignalR hub (`OrderHub`, `/hubs/orders`) broadcasting every status change live; the Angular frontend (`src/Frontend/`, standalone components, NgRx, Angular Material) consumes it and the three REST APIs through the API Gateway.

Phase 5 added `src/ApiGateway/` (YARP reverse proxy, JWT issuance for a minimal demo user store, `[Authorize(Roles = "Admin")]` on Inventory Service's `POST /api/v1/products`), API versioning (every endpoint now under `/api/v{version:apiVersion}/...`), Serilog structured logging (Console/File/Seq) replacing the default `Logging` section, `/health`+`/ready`+`/live` health checks (Postgres/RabbitMQ-backed where applicable), OpenTelemetry tracing exported to Jaeger, Prometheus metrics via `prometheus-net.AspNetCore` (see the "Established Architecture Pattern" note on why this replaced the OpenTelemetry Prometheus exporter), and full containerization (`docker/docker-compose.yml` + `docker-compose.override.yml` for hot-reload dev, one Dockerfile per service). The frontend now targets a single Gateway URL (`environment.ts`'s `gatewayUrl`), closing the temporary Phase 4 exception below.

Phase 6 is complete: this closes the exercise's six main phases. Real, measured code coverage (`coverlet.collector` + the `dotnet-reportgenerator-globaltool` local tool, `.config/dotnet-tools.json`) replaced the earlier untested assumption that the 70% minimum was being met - every backend service exceeds it by a wide margin on `*.Application`/`*.Domain`/`*.Infrastructure` (Order 93%, Payment 90.5%, Inventory 90.6%, Gateway 98% line coverage, generated code excluded; see `README.md`'s "Test Coverage" section for the full table and the exact commands). Measuring the Gateway's coverage surfaced a real, previously-uncovered gap - `ApiGateway.Tests` had only unit tests with mocked services, so `Program.cs`'s actual ASP.NET Core pipeline wiring (JWT bearer config, YARP routes, rate limiter, middleware order) had zero coverage; a `WebApplicationFactory<Program>`-based integration test now exercises `POST /api/v1/auth/login` and `/health` end-to-end, the same pattern the other three services already used. On the frontend, measuring coverage similarly surfaced two real gaps that are now closed: `shared/components/confirm-dialog` had no spec file at all, and `orders.reducer.ts` was missing tests for half its action handlers (`loadOrderDetail*`, `createOrder*`, `cancelOrderFailure`, `setPage`, `clearSelectedOrder`). 176 backend tests (99 Order, 25 Payment, 38 Inventory, 14 Gateway) and 68 frontend tests, all passing. Documentation added: `docs/architecture.md` (components, sync/async flow diagrams, the order status state machine) and `docs/adr/` (four Architecture Decision Records: SQL vs NoSQL, the choreographed Saga pattern, database per service, and the key technology choices already scattered across this file, now consolidated into the artifact format the exercise's Phase 6 asks for).

This summary is updated at the close of each phase, but it may fall out of date between sessions. Any agent must verify the real state using the repository's own search tools before assuming something is or is not implemented.

## Source of Truth

[`docs/technical-exercise.md`](docs/technical-exercise.md) contains the full technical exercise statement, in English, organized into six phases (Phase 1 through Phase 6) with their tasks, business rules, evaluation criteria, and bonus challenges. It is the project's source specification; in case of any ambiguity, it takes precedence over any other interpretation. `README.md`, at the repository root, is the project's public presentation (for whoever evaluates or clones it), not the specification — it must not be used as a source of requirements.

Before starting work on a phase, an agent must read the corresponding section of `docs/technical-exercise.md` in full. Do not move on to the next phase without having covered its requirements (or having explicitly agreed on an exception with the user).

This file (`CLAUDE.md`) acts as the project's status board: the "Current Repository State" section is updated at the close of each phase.

## Architecture: Non-Negotiable Rules

- **Database per service.** Order Service, Payment Service, and Inventory Service each have their own database, without exception. No service accesses another service's database directly, whether through a direct connection or a shared view.
- **Synchronous communication only via API Gateway or explicitly documented REST calls.** The frontend never calls a microservice directly; it always goes through the API Gateway (`src/ApiGateway/`, YARP, implemented Phase 5). `src/Frontend/src/environments/environment.ts` exposes a single `gatewayUrl`; the three `*-api.service.ts` files and the SignalR client all route through it. The one exception remains Order Service's own server-to-server call to Inventory Service (`InventoryHttpClient`), which is internal service-to-service traffic, not frontend traffic, and was already explicitly documented as allowed.
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

  **Two separate MassTransit rules are mandatory for every event in this table, in every service that publishes or consumes it** (both were found the hard way in Phase 2 - the first alone was not enough):

  1. **Exchange naming:** `cfg.Message<TEvent>(m => m.SetEntityName("EventTypeName"))` in the `UsingRabbitMq` block, using the bare event name (no namespace) as the string, identical across services. MassTransit's default exchange name is the CLR type's full name including namespace, so without this, each service's local copy of a shared event would silently get its *own* exchange, and a publisher in one service would never reach a consumer in another.
  2. **Namespace of the local contract copy:** every service's local copy of a shared event (see "Established Architecture Pattern") must live in a bare `namespace IntegrationEvents;` — not `<Service>.Application.IntegrationEvents`. MassTransit's JSON envelope tags each message with a type URN built from CLR namespace + type name (`urn:message:{Namespace}:{TypeName}`), independent of the exchange name, and a consumer only binds to a message whose URN matches a *locally known* type. With per-service namespaces, rule 1 alone still routes the message to the right queue, but the consumer then rejects it as an unrecognized type and RabbitMQ moves it to a `<queue>_skipped` dead-letter queue - no exception, no log on the publishing side, the message just silently vanishes from the consumer's point of view.

  Symptom when either rule is missed: the publish call succeeds and the integration/consumer tests (which use MassTransit's in-memory `ITestHarness`, a single fake bus with no cross-service exchange or namespace mismatch) still pass, but nothing happens end-to-end against real RabbitMQ. Verify both with `docker exec orders-rabbitmq rabbitmqctl list_exchanges name type` (exactly one exchange per event name) and `docker exec orders-rabbitmq rabbitmqctl list_queues name messages consumers` (watch for unexpected `_skipped` queues accumulating messages).

- **The Saga pattern governs the entire order flow** (creation, payment, inventory reservation, completion or compensation). No implementation may introduce an alternate path that completes an order without going through the described Saga flow.
- **Idempotency is mandatory** for order creation and payment processing. Every event handler must be idempotent (reprocessing the same event twice must not duplicate effects).

## Technology Stack

Do not change these choices without explicit confirmation from the user:

- **Backend:** C# on .NET 10 (current LTS; the original exercise statement mentions .NET 8, but the project adopts the current LTS installed in the development environment).
- **Data access:** Entity Framework Core (decision made in Phase 1; do not use Dapper, to keep a single data access pattern across services), with the Repository and Unit of Work pattern. Column naming in snake_case via the `EFCore.NamingConventions` package (`UseSnakeCaseNamingConvention()` in each `DbContext`), to match the hand-written SQL scripts.
- **Databases:** PostgreSQL (decision made in Phase 1). The original exercise statement allows SQL Server or MySQL, but a single `orders-postgres` container already exists, with one logical database and one dedicated role per service (`orderdb`/`order_service`, `paymentdb`/`payment_service`, `inventorydb`/`inventory_service` — see `scripts/setup-databases.sql`). Keep Postgres for Payment and Inventory Service unless the user explicitly asks to switch engines.
- **Optimistic concurrency:** PostgreSQL's `xmin` system column as the concurrency token (shadow property `uint xmin` with `IsConcurrencyToken()` + `HasColumnType("xid")`), without adding manual version columns unless the engine is not Postgres. See `OrderConfiguration` in `OrderService.Infrastructure` as a reference.
- **Messaging:** RabbitMQ via MassTransit, **pinned to version 8.x** (`MassTransit.RabbitMQ` 8.5.10 or any later 8.x version; exact version, not floating). **Never upgrade to MassTransit 9 or higher**: those versions require a commercial license (`SetLicense`/`SetLicenseLocation` or the `MT_LICENSE`/`MT_LICENSE_PATH` environment variables) and the application fails to start without one (`MassTransit.ConfigurationException` at host startup). Direct exchanges (one per event, via `SetEntityName`) with exponential backoff retry (3 attempts, `cfg.UseMessageRetry(r => r.Exponential(...))` in every service's `Program.cs`) implemented in Phase 3; retries exhausted move the message to the receive endpoint's automatically-created `<queue>_error` dead letter queue - no manual DLQ exchange/queue wiring needed.
- **Frontend:** Angular (decision made, implemented Phase 4 in `src/Frontend/`, standalone components, no NgModules). UI with **Angular Material** (chosen in Phase 4 over PrimeNG/ng-bootstrap); state with NgRx for the cross-cutting `orders` feature slice, plain component signals for anything purely local. `@ngrx/*` pinned to the `22.0.0-rc.0`/`next` line to match the installed Angular 22 - the `latest`-tagged NgRx release only supports Angular up to 21 as of this writing; re-check for a stable NgRx release matching the installed Angular major before bumping either package.
- **Real-time:** SignalR - `OrderHub` (`OrderService.Infrastructure/Messaging/OrderHub.cs`, mapped at `/hubs/orders`) implemented Phase 4, broadcast via a `CompositeOrderEventPublisher` that fans every `IOrderEventPublisher` call out to both RabbitMQ and SignalR (see "Established Architecture Pattern"); Angular client in `src/Frontend/src/app/core/services/order-signalr.service.ts`.
- **API Gateway:** YARP (`Yarp.ReverseProxy`, decision made and implemented Phase 5 in `src/ApiGateway/`) over Ocelot - reuses ASP.NET Core's native JWT bearer authentication and `Microsoft.AspNetCore.RateLimiting` middleware directly instead of a third-party proprietary config format. Routes/clusters (including the SignalR hub's WebSocket traffic) are declared in `appsettings.json`'s `ReverseProxy` section.
- **Containers:** Docker with multi-stage Dockerfiles (implemented Phase 5, one per service plus the frontend), orchestrated with Docker Compose (`docker/docker-compose.yml` + `docker-compose.override.yml` for local hot-reload dev, targeting each Dockerfile's SDK/Node build stage instead of its published runtime image).
- **Logging:** Serilog (implemented Phase 5 in all four backend services, `appsettings.json`'s `Serilog` section read via `Serilog.Settings.Configuration`), with sinks Console, File, and Seq.
- **Observability:** OpenTelemetry (implemented Phase 5, tracing only - `AddAspNetCoreInstrumentation`/`AddHttpClientInstrumentation`/`AddNpgsql` (from `Npgsql.OpenTelemetry`, note the extension lives in the bare `Npgsql` namespace, not `OpenTelemetry.Trace`) + `AddSource("MassTransit")`, exported via OTLP to Jaeger). **Metrics use `prometheus-net.AspNetCore` 8.x instead of OpenTelemetry's own Prometheus exporter**: `OpenTelemetry.Exporter.Prometheus.AspNetCore` has never had a stable release (only alpha/beta/rc tags as of Phase 5), so metrics are exposed via `app.UseHttpMetrics()` + `app.MapMetrics()` (`/metrics`) while OpenTelemetry stays scoped to tracing. Documented identically in every service's `Program.cs`.
- **Authentication:** JWT with role-based authorization (implemented Phase 5). A minimal demo user store (`ApiGateway.API/Services/DemoUserStore.cs`, two hardcoded users - `admin`/`Admin123!` role `Admin`, `customer`/`Customer123!` role `Customer` - hashed via `Microsoft.AspNetCore.Identity.PasswordHasher<TUser>`), not a real identity system: this project has no user registration, no persisted accounts, and no login screen in the frontend. Tokens are issued only by the Gateway (`POST /api/v1/auth/login`) and validated independently by the Gateway and by every downstream service via a shared symmetric signing key/issuer/audience (config section `"Jwt"`, dev-only key literal in `appsettings.json`, overridable via `Jwt__SigningKey`). Only one endpoint is actually gated - Inventory Service's `POST /api/v1/products` (`[Authorize(Roles = "Admin")]`) - anchored to this file's own Security-section example ("product creation restricted to administrators"); nothing else in the project requires a token.

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

## Established Architecture Pattern (Order, Payment, and Inventory Service)

Order Service (Phase 1) set the pattern; Payment Service and Inventory Service (Phase 2) followed it, so all three services stay consistent.

**API Gateway is the documented exception to the five-project split** (Phase 5, `src/ApiGateway/`): a single `ApiGateway.API` project plus `ApiGateway.Tests`, no `Domain`/`Application`/`Infrastructure` layers. A pure routing/auth/rate-limiting service has no domain logic to layer - the only real logic is JWT issuance and validation (`Services/DemoUserStore.cs`, `Services/JwtTokenService.cs`, `Controllers/AuthController.cs`), which is what `ApiGateway.Tests` covers. Any other future service should still follow the five-project pattern where it applies.

- Five projects per service: `<Service>.Domain` (entities and business rules as methods, no public setters), `<Service>.Application` (DTOs in `Contracts/`, interfaces in `Abstractions/`, use cases in `Services/`, integration events in `IntegrationEvents/`, exceptions in `Exceptions/`), `<Service>.Infrastructure` (`DbContext` and EF Core configurations in `Persistence/`, event publisher and consumers in `Messaging/`, outbound HTTP clients in `ExternalServices/`, background services in `BackgroundServices/`), `<Service>.API` (`Controllers/`, `Middleware/`, `Program.cs`), `<Service>.Tests` (subfolders `Domain/`, `Application/`, `Unit/`, `Integration/`).
- Centralized error handling in an `ExceptionHandlingMiddleware` per service, translating domain/application exceptions into HTTP status codes (404 not found, 409 state or concurrency conflict, 422 business rule violation, 503 external dependency unavailable). Do not repeat `try/catch` in every controller. EF Core's `DbUpdateConcurrencyException` should not leak past Infrastructure - translate it into an Application-level exception there (see `ConcurrencyConflictException` in `InventoryService.Application.Exceptions`) so Application stays free of EF Core-specific types and the same exception works for both HTTP-triggered and internal (event-driven) concurrency retries.
- Idempotency approach depends on how the operation is triggered, not a single copy-pasted mechanism: Order Service's client-initiated `POST /api/orders` uses the `Idempotency-Key` header with a dedicated table (`idempotency_keys`), committed in the same transaction as the main operation. Payment Service and Inventory Service's operations are event-triggered and scoped to one order, so idempotency there is simpler: a unique DB constraint (`payments.order_id`; `inventory_reservations.(order_id, product_id)`) plus an existence check before acting - no separate key table needed. Order Service's own saga-driven consumers (Phase 3) use a third variant fitted to their shape: each domain transition method on `Order` (`MarkPaymentProcessed`, `MarkPaymentFailed`, `Complete`, `MarkInventoryFailed`) is a no-op if the order already reached or passed the target status, so redelivery of the same event is harmless without any extra table or constraint.
- Integration tests with `WebApplicationFactory` + `Testcontainers.PostgreSql` (a real, ephemeral database), replacing in `CustomWebApplicationFactory` only the dependencies on external services that do not exist yet or should not be spun up in the test (see `AlwaysAvailableInventoryChecker` in `OrderService.Tests`). Services with no EF Core migration (Payment, Inventory - see "Key Design Decisions") apply their tracked `scripts/*.sql` file directly against the test container instead of migrating, stripping the `GRANT`/`ALTER DEFAULT PRIVILEGES` lines first since the role they target only exists in the real `orders-postgres` container, not in the disposable test one.
- Consumer wiring (first introduced in Phase 2) gets its own test using MassTransit's in-memory `AddMassTransitTestHarness`, asserting the consumer both fires and calls the right method with the right arguments - `PaymentManager`/`ReservationManager` tests alone would not catch a wiring mistake.
- Every service's `Program.cs` configures `cfg.UseMessageRetry(r => r.Exponential(3, ...))` on the bus (Phase 3), applying to every consumer on that service - 3 retries with exponential backoff before MassTransit's RabbitMQ transport dead-letters the message to an automatically-created `<queue>_error` queue. No manual DLQ exchange/queue declaration is needed.
- `ILogger<T>` is not limited to `BackgroundServices/` - `OrderManager` (Application/Services) also takes one (Phase 3), logging every saga step with the affected `OrderId`. Any `Application/Services/` class may inject `ILogger<T>` the same way; the owning project's `.csproj` needs an explicit `Microsoft.Extensions.Logging.Abstractions` package reference if it has no other path to it (a plain class library like `<Service>.Application` does not get it transitively the way `<Service>.API`/`<Service>.Infrastructure` do).
- **Decorator pattern for fanning a single Application-layer interface out to multiple transports** (Phase 4): `IOrderEventPublisher` has one implementation registered in DI (`CompositeOrderEventPublisher`), which wraps `RabbitMqOrderEventPublisher` and `SignalROrderEventPublisher` and calls both. `OrderManager` needed zero changes to gain SignalR broadcasting - it only ever depended on the interface. Register the wrapped implementations under their own concrete types and construct the composite via a factory lambda, not `IEnumerable<IOrderEventPublisher>` (that would recursively include the composite once it is registered under the same interface). Reuse this pattern before adding a second constructor dependency anywhere a single interface already gates every call site.
- A plain `Microsoft.NET.Sdk` class library (not `Sdk.Web`) needs `<FrameworkReference Include="Microsoft.AspNetCore.App" />` to resolve ASP.NET Core shared-framework types (`Hub`, `IHubContext<T>`, etc.) it wants to use without becoming a web project itself - see `OrderService.Infrastructure.csproj`.
- **Cross-cutting infrastructure concerns are replicated identically across all four backend services** (Order, Payment, Inventory, Gateway), Phase 5: API versioning (`AddApiVersioning(...).AddMvc().AddApiExplorer(...)`, every route under `/api/v{version:apiVersion}/...`), Serilog (`builder.Host.UseSerilog(...)` reading the `appsettings.json` `Serilog` section), health checks (`/health`, `/ready` tagged `"ready"`, `/live` via a predicate that is always healthy), and OpenTelemetry tracing + prometheus-net metrics. Order/Payment/Inventory additionally wire `AddNpgSql`/`AddRabbitMQ` health checks (the Gateway has no direct dependency, so its `AddHealthChecks()` registers zero checks - `/health` still returns 200 with an empty check list). Copy the block from an existing service rather than reinventing it when adding a fifth service.
- **Each service's `Dockerfile` duplicates its `curl` install + `HEALTHCHECK` instruction into the `build` (SDK) stage, not only the `final` (runtime) stage.** `docker/docker-compose.override.yml` retargets every .NET service to the `build` stage for `dotnet watch run` hot-reload, and Compose's `depends_on: condition: service_healthy` (used throughout `docker-compose.yml`, e.g. the Gateway waiting on all three downstream services) requires a healthcheck to exist on whichever stage actually starts - `docker compose config` does not catch a missing one, since it only validates YAML syntax/interpolation, not this runtime-level consistency; it only surfaces at `docker compose up` as an immediate "missing a healthcheck configuration" error.

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
- JWT is mandatory on endpoints that are not explicitly public; role-based authorization where the exercise statement requires it (for example, product creation restricted to administrators). Implemented Phase 5: see the Technology Stack section's "Authentication" entry for the demo-user-store and shared-signing-key design. Only Inventory Service's `POST /api/v1/products` is actually gated - do not add `[Authorize]` elsewhere without an equally concrete anchor in this file or in `docs/technical-exercise.md`, since there is no login screen in the frontend to make broader gating usable.
- CORS must be configured with explicit origins; do not use a wildcard (`*`) in configurations intended for production. Only the API Gateway configures a CORS policy (`Cors:AllowedOrigins` config key, `http://localhost:4200` for local/Compose dev, applied via `AddCors`/`UseCors`) - the frontend never calls Order/Payment/Inventory Service directly, so they have no CORS need of their own. Their Phase 4-era CORS wiring, from before the Gateway existed, was removed during cleanup after Phase 6.

## Testing

- Minimum unit test coverage: 70%, measured over service logic, domain models, and event handlers. Measured with `coverlet.collector` (already referenced by every `*.Tests.csproj`) via `dotnet test --collect:"XPlat Code Coverage"`, summarized with the `dotnet-reportgenerator-globaltool` local tool (`.config/dotnet-tools.json` - run `dotnet tool restore` once). Exclude generated code from the report (`-classfilters:"-Microsoft.AspNetCore.OpenApi.Generated*;-System.Runtime.CompilerServices*"`) - it inflates the denominator and is not code this project owns. See `README.md`'s "Test Coverage" section for the full command and the current numbers per service.
- Unit tests with xUnit or NUnit on the backend; Jest and React Testing Library on the frontend (if React is chosen).
- Database integration tests with TestContainers or an in-memory database.
- Endpoint integration tests with `WebApplicationFactory`.
- Every critical business rule listed above must have at least one test that covers it explicitly, including its edge cases (for example, a payment of exactly $10,000, an amount ending in `.99`).
- No task is considered done if the code implementing it has no associated tests.
- **Run each service's `dotnet test` one at a time, never in parallel with another service's.** Every `WebApplicationFactory`-based integration test hits the real `orders-rabbitmq` broker (Testcontainers only covers Postgres, not RabbitMQ), so running two services' suites concurrently lets one service's live test consumers process real cross-service traffic published by another service's test run mid-test - observed as a one-off failure in `OrderService.Tests` when its suite ran at the same time as Payment/Inventory Service's. Not a product defect (confirmed by rerunning the suite alone: consistently green); purely a test-isolation gap between services, invisible before Phase 3 since Order Service had no consumers to react to that cross traffic.

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
│   ├── PaymentService/     (Phase 2 - implemented, same layered structure)
│   ├── InventoryService/   (Phase 2 - implemented, same layered structure)
│   ├── ApiGateway/         (Phase 5 - implemented: single-project YARP gateway + JWT issuance, see "Established Architecture Pattern")
│   └── Frontend/           (Phase 4 - implemented, Angular standalone + NgRx + Angular Material; Phase 5 - collapsed to a single Gateway URL)
├── docker/                 (Phase 5 - implemented: docker-compose.yml, docker-compose.override.yml, prometheus.yml)
├── docs/
│   ├── api/                (implemented: Postman collection per service)
│   ├── technical-exercise.md  (implemented: original statement, moved from README.md)
│   ├── architecture.md     (Phase 6 - implemented)
│   └── adr/                (Phase 6 - implemented: 4 ADRs + index)
├── scripts/                (implemented: setup-databases.sql, payment-service-schema.sql, inventory-service-schema.sql)
├── CLAUDE.md
├── .gitignore
└── README.md               (public presentation of the project, not the specification)
```

Every entry above exists. This is the exercise's complete target structure - all six phases are implemented.
