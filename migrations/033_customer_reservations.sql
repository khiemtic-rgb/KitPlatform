-- Customer-initiated medicine reservations (P7 — đặt thuốc trước)

CREATE TABLE customer_reservations (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           UUID          NOT NULL REFERENCES tenants(id),
    customer_id         UUID          NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    reservation_number  VARCHAR(50)   NOT NULL,
    status              SMALLINT      NOT NULL DEFAULT 1,
    fulfillment_type    SMALLINT      NOT NULL DEFAULT 1,
    address_id          UUID          REFERENCES customer_addresses(id),
    notes               TEXT,
    staff_notes         TEXT,
    submitted_at        TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    confirmed_at        TIMESTAMPTZ,
    ready_at            TIMESTAMPTZ,
    collected_at        TIMESTAMPTZ,
    cancelled_at        TIMESTAMPTZ,
    rejected_at         TIMESTAMPTZ,
    created_at          TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    updated_at          TIMESTAMPTZ   NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_customer_reservations_number UNIQUE (tenant_id, reservation_number)
);

COMMENT ON COLUMN customer_reservations.status IS '1=Pending 2=Confirmed 3=Ready 4=Collected 5=Cancelled 6=Rejected';
COMMENT ON COLUMN customer_reservations.fulfillment_type IS '1=Pickup 2=Delivery';

CREATE INDEX ix_customer_reservations_customer
    ON customer_reservations (tenant_id, customer_id, status, submitted_at DESC);

CREATE TABLE customer_reservation_items (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    reservation_id      UUID          NOT NULL REFERENCES customer_reservations(id) ON DELETE CASCADE,
    line_number         INT           NOT NULL,
    product_id          UUID          NOT NULL REFERENCES products(id),
    product_unit_id     UUID          NOT NULL REFERENCES product_units(id),
    product_code        VARCHAR(50)   NOT NULL,
    product_name        VARCHAR(255)  NOT NULL,
    unit_name           VARCHAR(50)   NOT NULL,
    quantity            NUMERIC(18,4) NOT NULL,
    customer_note       VARCHAR(255),
    created_at          TIMESTAMPTZ   NOT NULL DEFAULT NOW()
);

CREATE INDEX ix_customer_reservation_items_reservation
    ON customer_reservation_items (reservation_id, line_number);

CREATE TRIGGER trg_customer_reservations_updated
    BEFORE UPDATE ON customer_reservations
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();
