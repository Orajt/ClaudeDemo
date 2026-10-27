DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'spike_datahub') THEN
        CREATE SCHEMA spike_datahub;
    END IF;
END $EF$;
CREATE TABLE IF NOT EXISTS spike_datahub."__EFMigrationsHistory" (
    "MigrationId" character varying(150) NOT NULL,
    "ProductVersion" character varying(32) NOT NULL,
    CONSTRAINT "PK___EFMigrationsHistory" PRIMARY KEY ("MigrationId")
);

DO $EF$
BEGIN
    IF NOT EXISTS(SELECT 1 FROM pg_namespace WHERE nspname = 'spike_datahub') THEN
        CREATE SCHEMA spike_datahub;
    END IF;
END $EF$;

CREATE TABLE spike_datahub.order_payments (
    order_id uuid NOT NULL,
    customer_name text,
    total_amount numeric(18,2),
    order_status text,
    orders_version bigint NOT NULL,
    settlement_status text,
    settled_net_amount numeric(18,2),
    settlement_version bigint NOT NULL,
    CONSTRAINT "PK_order_payments" PRIMARY KEY (order_id)
);

INSERT INTO spike_datahub."__EFMigrationsHistory" ("MigrationId", "ProductVersion")
VALUES ('20261027202734_Initial', '10.0.14');

