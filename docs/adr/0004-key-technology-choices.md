# ADR 0004: Key Technology Choices

## Status

Accepted.

## Context

The exercise statement leaves several implementation-level technology choices open, or names alternatives without mandating one (`docs/technical-exercise.md`'s Tech Stack section lists options like "C# (.NET) or Node.js/Java", ".NET or React", "SQL Server, PostgreSQL, or MySQL", and describes an API Gateway without naming a library). Each choice below was made once and is treated as settled unless explicitly revisited with the user (`CLAUDE.md`'s Technology Stack section is the enforced record of all of them); this ADR consolidates the reasoning that would otherwise only live scattered across `CLAUDE.md` comments and commit messages, into the single artifact the exercise's Phase 6 asks for.

## Decisions

**API Gateway: YARP over Ocelot.** YARP reuses ASP.NET Core's native JWT bearer authentication and `Microsoft.AspNetCore.RateLimiting` middleware directly - the same primitives already used to secure the three domain services - instead of adopting a third-party, gateway-specific configuration format (`ocelot.json`) and its own auth/rate-limiting abstractions. One less thing to learn and keep consistent across the codebase.

**Messaging library: MassTransit, pinned to the 8.x line.** MassTransit 9 and later require a commercial license (`SetLicense`/`MT_LICENSE`) and refuse to start without one - a hard blocker for an exercise with no such license. Every `.csproj` pins an exact 8.x version rather than a floating range, so an unattended `dotnet restore` on a later date cannot silently pull in a 9.x release and break every service at once.

**Metrics: `prometheus-net.AspNetCore` over OpenTelemetry's own Prometheus exporter.** `OpenTelemetry.Exporter.Prometheus.AspNetCore` has never had a stable release (confirmed via the NuGet API: only alpha/beta/rc tags across every version up to the one current at the time of Phase 5). OpenTelemetry stays scoped to what it does have a stable, production-ready exporter for - tracing, via OTLP to Jaeger - while metrics use the mature, widely-used `prometheus-net.AspNetCore` package instead. Two libraries covering the two observability signals cleanly, rather than one library used partly stable and partly beta.

**Data access: Entity Framework Core over Dapper.** A single ORM across all three services, rather than mixing EF Core and a micro-ORM, keeps one data-access pattern to learn and review (`CLAUDE.md`'s explicit rationale: "to keep a single data access pattern across services"). EF Core's migrations also cover Order Service's schema management need (the only service whose schema wasn't already fixed by a hand-written script before Phase 1 began).

**Concurrency control: PostgreSQL's `xmin` system column over a manual version column.** PostgreSQL already maintains `xmin` (the transaction id that last wrote a row) on every table for its own MVCC implementation; mapping it as a shadow concurrency-token property gives optimistic concurrency (used to prevent overselling stock in Inventory Service) with no extra column, no extra write, and no application code responsible for incrementing it. This is PostgreSQL-specific - a future move to SQL Server/MySQL would need a real version column instead (see ADR 0001 for why PostgreSQL was chosen at all).

**Frontend: Angular over React.** Chosen in Phase 4 as one of the exercise's explicitly allowed options. State management follows accordingly: NgRx for the cross-cutting `orders` feature slice (the one piece of state genuinely shared across routes/components - the order list, the currently-selected order, in-flight loading flags), plain component signals for anything purely local to one component. The exercise's Phase 6 testing task names "Jest + React Testing Library" - read as the framework-appropriate testing stack for whichever frontend framework is chosen, not a literal requirement to use React's own tools with Angular; the frontend uses Angular's own current testing stack (Vitest, via the `@angular/build:unit-test` builder) instead, and this document is where that reading of the requirement is recorded.

## Consequences

Each of these is a single, local decision rather than a system-wide architectural commitment (unlike ADR 0001-0003) - reversing any one of them (e.g. swapping YARP for Ocelot, or `prometheus-net` for a future stable OpenTelemetry Prometheus exporter) would touch one service's `Program.cs`/`.csproj` and not ripple into the other services' code or the event contracts, saga logic, or database boundaries.
