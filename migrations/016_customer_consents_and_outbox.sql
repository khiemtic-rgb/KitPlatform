-- PharmaCore 016: CDP foundation — customer consent + integration outbox
-- Code/API: P1 CDP (consent UI + outbox writer). Schema-only at this migration.

-- customer_consents.channel: 1 SMS, 2 Zalo, 3 Email, 4 AppPush
-- customer_consents.purpose: 1 Marketing, 2 CareReminder, 3 Research, 4 AiAssist
-- customer_consents.source:   1 POS, 2 Admin, 3 App, 4 Import

CREATE TABLE customer_consents (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES tenants(id),
    customer_id     UUID         NOT NULL REFERENCES customers(id) ON DELETE CASCADE,
    channel         SMALLINT     NOT NULL,
    purpose         SMALLINT     NOT NULL,
    granted         BOOLEAN      NOT NULL DEFAULT FALSE,
    granted_at      TIMESTAMPTZ,
    revoked_at      TIMESTAMPTZ,
    source          SMALLINT     NOT NULL DEFAULT 1,
    captured_by     UUID         REFERENCES users(id),
    notes           TEXT,
    created_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    updated_at      TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    CONSTRAINT uq_customer_consents UNIQUE (tenant_id, customer_id, channel, purpose),
    CONSTRAINT ck_customer_consents_granted_at CHECK (
        (granted = FALSE AND granted_at IS NULL)
        OR (granted = TRUE AND granted_at IS NOT NULL)
    )
);

CREATE INDEX ix_customer_consents_customer ON customer_consents (tenant_id, customer_id);
CREATE INDEX ix_customer_consents_granted ON customer_consents (tenant_id, customer_id, purpose)
    WHERE granted = TRUE;

CREATE TRIGGER trg_customer_consents_updated
    BEFORE UPDATE ON customer_consents
    FOR EACH ROW EXECUTE FUNCTION set_updated_at();

COMMENT ON TABLE customer_consents IS 'Opt-in/opt-out theo kenh + muc dich. Can co truoc campaign / AI assist / care reminder.';
COMMENT ON COLUMN customer_consents.purpose IS '4 AiAssist = dong y ho tro AI duoc si (tuong lai).';

-- integration_outbox.event_type examples:
--   order.completed, order.cancelled, sales_return.completed, customer.consent.updated
-- integration_outbox.aggregate_type: sales_order | sales_return | customer_consent | customer

CREATE TABLE integration_outbox (
    id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       UUID         NOT NULL REFERENCES tenants(id),
    event_type      VARCHAR(80)  NOT NULL,
    event_version   SMALLINT     NOT NULL DEFAULT 1,
    aggregate_type  VARCHAR(50)  NOT NULL,
    aggregate_id    UUID         NOT NULL,
    payload         JSONB        NOT NULL,
    occurred_at     TIMESTAMPTZ  NOT NULL DEFAULT NOW(),
    published_at    TIMESTAMPTZ,
    publish_attempts INT         NOT NULL DEFAULT 0,
    last_error      TEXT,
    CONSTRAINT ck_integration_outbox_payload_object CHECK (jsonb_typeof(payload) = 'object')
);

CREATE INDEX ix_integration_outbox_unpublished
    ON integration_outbox (tenant_id, occurred_at)
    WHERE published_at IS NULL;

CREATE INDEX ix_integration_outbox_aggregate
    ON integration_outbox (tenant_id, aggregate_type, aggregate_id, occurred_at DESC);

COMMENT ON TABLE integration_outbox IS 'Transactional outbox: ERP ghi cung transaction nghiep vu; worker publish sang CDP/analytics.';
COMMENT ON COLUMN integration_outbox.payload IS 'Envelope JSON: eventId, eventType, tenantId, occurredAt, actor, data (API DTO shape).';
