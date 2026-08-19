-- Ejecutar como superusuario (postgres) contra el contenedor orders-postgres.
-- Crea una base de datos y un rol dedicado por servicio, para cumplir la regla
-- "base de datos por servicio": ningun servicio comparte credenciales ni base
-- de datos con otro.

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
