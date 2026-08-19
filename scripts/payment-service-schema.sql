-- Esquema del Payment Service (Fase 1, Task 1.1). Ejecutar contra paymentdb.
-- El servicio en si (API, EF Core) se construye en la Fase 2; este script
-- deja el esquema listo con anticipacion, como pide el enunciado original.

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

-- Rastro de auditoria: un registro por cada intento de procesamiento,
-- incluso si el pago original se reintenta.
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
