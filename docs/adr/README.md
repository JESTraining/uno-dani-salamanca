# Architecture Decision Records

This folder records the significant, hard-to-reverse architecture decisions made in this project, in the lightweight Status/Context/Decision/Consequences format. It exists to satisfy the exercise's own Phase 6 documentation requirement (`docs/technical-exercise.md`) - it does not replace `CLAUDE.md`, which remains the enforced, up-to-date source of the same rules; these records explain *why* those rules exist.

## Records

| ADR | Decision |
|-----|----------|
| [0001](0001-sql-over-nosql.md) | SQL (PostgreSQL) over NoSQL |
| [0002](0002-saga-pattern-for-order-processing.md) | Saga pattern (choreographed) for order processing |
| [0003](0003-database-per-service.md) | Database per service |
| [0004](0004-key-technology-choices.md) | Key technology choices (YARP, MassTransit 8.x, prometheus-net, EF Core, `xmin` concurrency, Angular) |
