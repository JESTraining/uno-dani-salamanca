-- Inventory Service schema (Phase 1, Task 1.1). Run against inventorydb.
-- The service itself (API, EF Core) is built in Phase 2; this script
-- gets the schema ready ahead of time, as the original exercise asks.

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE products (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    sku VARCHAR(50) NOT NULL UNIQUE,
    name VARCHAR(200) NOT NULL,
    description VARCHAR(1000) NULL,
    unit_price NUMERIC(12,2) NOT NULL CHECK (unit_price > 0),
    stock_quantity INT NOT NULL CHECK (stock_quantity >= 0),
    reserved_quantity INT NOT NULL DEFAULT 0 CHECK (reserved_quantity >= 0),
    version INT NOT NULL DEFAULT 1,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT ck_products_reserved_not_exceeds_stock CHECK (reserved_quantity <= stock_quantity)
);

-- The version column supports optimistic concurrency on stock updates:
-- every write must include "AND version = @expectedVersion" and verify
-- that exactly one row was affected.

CREATE TABLE inventory_reservations (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL,
    product_id UUID NOT NULL REFERENCES products (id),
    quantity INT NOT NULL CHECK (quantity > 0),
    status VARCHAR(20) NOT NULL DEFAULT 'Reserved',
    reserved_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    expires_at TIMESTAMPTZ NOT NULL,
    confirmed_at TIMESTAMPTZ NULL,
    released_at TIMESTAMPTZ NULL,
    CONSTRAINT uq_inventory_reservations_order_product UNIQUE (order_id, product_id),
    CONSTRAINT ck_inventory_reservations_status CHECK (status IN ('Reserved', 'Confirmed', 'Released', 'Expired'))
);

-- The composite index supports the cleanup job that releases expired
-- reservations (status = 'Reserved' AND expires_at < now()).
CREATE INDEX ix_inventory_reservations_status_expires ON inventory_reservations (status, expires_at);
CREATE INDEX ix_inventory_reservations_order_id ON inventory_reservations (order_id);

-- Added in Phase 2 (not part of the original Phase 1 design): PaymentProcessedEvent
-- (fixed contract in CLAUDE.md) carries only OrderId/TransactionId/Amount/Timestamp,
-- not line items. Inventory Service separately consumes OrderCreatedEvent to record
-- what each order contains here, then looks it up by order_id when PaymentProcessedEvent
-- arrives and it is time to actually reserve stock.
CREATE TABLE order_item_snapshots (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL,
    product_id UUID NOT NULL,
    quantity INT NOT NULL CHECK (quantity > 0),
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_order_item_snapshots_order_product UNIQUE (order_id, product_id)
);

CREATE INDEX ix_order_item_snapshots_order_id ON order_item_snapshots (order_id);

-- This script runs as the postgres superuser, so it owns these tables.
-- setup-databases.sql's "GRANT ALL ON SCHEMA public" only covers schema-level
-- privileges (CREATE/USAGE), not table-level access to objects owned by a
-- different role - without this, inventory_service can connect to
-- inventorydb but gets "permission denied for table products" on every query.
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO inventory_service;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO inventory_service;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL PRIVILEGES ON TABLES TO inventory_service;
