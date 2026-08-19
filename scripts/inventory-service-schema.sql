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
