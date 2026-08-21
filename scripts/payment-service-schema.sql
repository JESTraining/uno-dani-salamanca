-- Payment Service schema (Phase 1, Task 1.1). Run against paymentdb.
-- The service itself (API, EF Core) is built in Phase 2; this script
-- gets the schema ready ahead of time, as the original exercise asks.

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE payments (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    order_id UUID NOT NULL,
    amount NUMERIC(12,2) NOT NULL CHECK (amount > 0),
    payment_method VARCHAR(30) NOT NULL DEFAULT 'CreditCard',
    status VARCHAR(20) NOT NULL DEFAULT 'Pending',
    transaction_id UUID NULL,
    failure_reason VARCHAR(200) NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_payments_order_id UNIQUE (order_id),
    CONSTRAINT ck_payments_status CHECK (status IN ('Pending', 'Processing', 'Succeeded', 'Failed', 'Refunded'))
);

CREATE INDEX ix_payments_status ON payments (status);

-- Audit trail: one record per processing attempt, even if the original
-- payment is retried.
CREATE TABLE payment_transaction_logs (
    id UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    payment_id UUID NOT NULL REFERENCES payments (id),
    attempt_number INT NOT NULL,
    status VARCHAR(20) NOT NULL,
    amount NUMERIC(12,2) NOT NULL,
    failure_reason VARCHAR(200) NULL,
    processing_started_at TIMESTAMPTZ NOT NULL,
    processing_completed_at TIMESTAMPTZ NULL,
    created_at TIMESTAMPTZ NOT NULL DEFAULT now(),
    CONSTRAINT uq_payment_transaction_logs_attempt UNIQUE (payment_id, attempt_number)
);

CREATE INDEX ix_payment_transaction_logs_payment_id ON payment_transaction_logs (payment_id);

-- This script runs as the postgres superuser, so it owns these tables.
-- setup-databases.sql's "GRANT ALL ON SCHEMA public" only covers schema-level
-- privileges (CREATE/USAGE), not table-level access to objects owned by a
-- different role - without this, payment_service can connect to paymentdb
-- but gets "permission denied for table payments" on every query.
GRANT ALL PRIVILEGES ON ALL TABLES IN SCHEMA public TO payment_service;
GRANT ALL PRIVILEGES ON ALL SEQUENCES IN SCHEMA public TO payment_service;
ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT ALL PRIVILEGES ON TABLES TO payment_service;
