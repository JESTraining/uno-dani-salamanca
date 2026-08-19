-- Run as superuser (postgres) against the orders-postgres container.
-- Creates a database and a dedicated role per service, to satisfy the
-- "database per service" rule: no service shares credentials or a
-- database with another.

CREATE DATABASE orderdb;
CREATE DATABASE paymentdb;
CREATE DATABASE inventorydb;

CREATE USER order_service WITH PASSWORD 'order_service_dev_pwd';
CREATE USER payment_service WITH PASSWORD 'payment_service_dev_pwd';
CREATE USER inventory_service WITH PASSWORD 'inventory_service_dev_pwd';

GRANT ALL PRIVILEGES ON DATABASE orderdb TO order_service;
GRANT ALL PRIVILEGES ON DATABASE paymentdb TO payment_service;
GRANT ALL PRIVILEGES ON DATABASE inventorydb TO inventory_service;

\c orderdb
GRANT ALL ON SCHEMA public TO order_service;

\c paymentdb
GRANT ALL ON SCHEMA public TO payment_service;

\c inventorydb
GRANT ALL ON SCHEMA public TO inventory_service;
