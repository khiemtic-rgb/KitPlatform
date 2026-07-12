-- KitPlatform 120: CL3-A — encounter modality (remote consult) + encounter_session skeleton for video (B)
-- Depends on: 119_connect_referral_clinic_customer.sql, 078_pack_clinic_crm.sql, 110_novixa_connect_bookings.sql
--
-- Ownership note: on some local DBs pack_clinic tables are owned by pharmacore while pack_connect
-- is owned by kitplatform. This script is idempotent and skips privileged steps with NOTICE when
-- the current role is not the table owner — re-run the pack_clinic block as the owner if needed.

-- ---------------------------------------------------------------------------
-- Modality columns (pack_clinic — may require table owner)
-- ---------------------------------------------------------------------------

DO $$
BEGIN
    ALTER TABLE pack_clinic.clinic_appointment
        ADD COLUMN IF NOT EXISTS encounter_modality VARCHAR(30) NOT NULL DEFAULT 'in_person';
    ALTER TABLE pack_clinic.clinic_visit
        ADD COLUMN IF NOT EXISTS encounter_modality VARCHAR(30) NOT NULL DEFAULT 'in_person';

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_clinic_appointment_modality'
          AND conrelid = 'pack_clinic.clinic_appointment'::regclass
    ) THEN
        ALTER TABLE pack_clinic.clinic_appointment
            ADD CONSTRAINT ck_clinic_appointment_modality CHECK (
                encounter_modality IN ('in_person', 'remote_async', 'remote_video')
            );
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_clinic_visit_modality'
          AND conrelid = 'pack_clinic.clinic_visit'::regclass
    ) THEN
        ALTER TABLE pack_clinic.clinic_visit
            ADD CONSTRAINT ck_clinic_visit_modality CHECK (
                encounter_modality IN ('in_person', 'remote_async', 'remote_video')
            );
    END IF;

    COMMENT ON COLUMN pack_clinic.clinic_appointment.encounter_modality IS
        'CL3: in_person | remote_async (A — call outside Novixa) | remote_video (B — reserved)';
    COMMENT ON COLUMN pack_clinic.clinic_visit.encounter_modality IS
        'CL3: copied from appointment or set on walk-in; remote_async = BS goi ngoai he thong';

    CREATE INDEX IF NOT EXISTS ix_clinic_appointment_modality_day
        ON pack_clinic.clinic_appointment (tenant_id, encounter_modality, appointment_at)
        WHERE deleted_at IS NULL;

    CREATE INDEX IF NOT EXISTS ix_clinic_visit_modality
        ON pack_clinic.clinic_visit (tenant_id, encounter_modality, started_at DESC)
        WHERE deleted_at IS NULL;
EXCEPTION
    WHEN insufficient_privilege THEN
        RAISE NOTICE '120: pack_clinic modality DDL skipped (run as table owner): %', SQLERRM;
    WHEN OTHERS THEN
        RAISE NOTICE '120: pack_clinic modality DDL skipped: %', SQLERRM;
END $$;

-- pack_connect (usually app-role owned)
ALTER TABLE pack_connect.bookings
    ADD COLUMN IF NOT EXISTS encounter_modality VARCHAR(30) NOT NULL DEFAULT 'in_person';

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
        WHERE conname = 'ck_connect_bookings_modality'
          AND conrelid = 'pack_connect.bookings'::regclass
    ) THEN
        ALTER TABLE pack_connect.bookings
            ADD CONSTRAINT ck_connect_bookings_modality CHECK (
                encounter_modality IN ('in_person', 'remote_async', 'remote_video')
            );
    END IF;
END $$;

COMMENT ON COLUMN pack_connect.bookings.encounter_modality IS
    'CL3: bridged to clinic_appointment on confirm';

-- ---------------------------------------------------------------------------
-- Encounter session (B skeleton; A creates status=none, media_provider=null)
-- ---------------------------------------------------------------------------

DO $$
BEGIN
    CREATE TABLE IF NOT EXISTS pack_clinic.encounter_session (
        id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
        tenant_id               UUID NOT NULL REFERENCES public.tenants(id),
        workspace_id            UUID,
        visit_id                UUID NOT NULL REFERENCES pack_clinic.clinic_visit(id),
        appointment_id          UUID REFERENCES pack_clinic.clinic_appointment(id),
        session_status          VARCHAR(30) NOT NULL DEFAULT 'none',
        media_provider          VARCHAR(50),
        provider_session_id     VARCHAR(200),
        join_url_patient        TEXT,
        join_url_clinician      TEXT,
        started_at              TIMESTAMPTZ,
        ended_at                TIMESTAMPTZ,
        metadata                JSONB NOT NULL DEFAULT '{}'::jsonb,
        created_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        updated_at              TIMESTAMPTZ NOT NULL DEFAULT NOW(),
        deleted_at              TIMESTAMPTZ,
        CONSTRAINT ck_encounter_session_status CHECK (
            session_status IN ('none', 'waiting', 'live', 'ended', 'failed')
        ),
        CONSTRAINT uq_encounter_session_visit UNIQUE (visit_id)
    );

    COMMENT ON TABLE pack_clinic.encounter_session IS
        'CL3: media/session stub. A = none/null provider; B = WebRTC waiting/live + join URLs.';

    CREATE INDEX IF NOT EXISTS ix_encounter_session_tenant_status
        ON pack_clinic.encounter_session (tenant_id, session_status)
        WHERE deleted_at IS NULL;

    GRANT SELECT, INSERT, UPDATE, DELETE ON pack_clinic.encounter_session TO kitplatform;
EXCEPTION
    WHEN insufficient_privilege THEN
        RAISE NOTICE '120: encounter_session DDL skipped (run as pack_clinic owner): %', SQLERRM;
    WHEN undefined_object THEN
        RAISE NOTICE '120: encounter_session GRANT skipped: %', SQLERRM;
    WHEN OTHERS THEN
        RAISE NOTICE '120: encounter_session DDL skipped: %', SQLERRM;
END $$;

-- ---------------------------------------------------------------------------
-- Module registry + enable remote on DEMO_CLINIC (video stays off)
-- ---------------------------------------------------------------------------

INSERT INTO platform_module_registry (module_code, module_name, description, verticals, sort_order)
SELECT v.code, v.name, v.description, v.verticals, v.sort_order
FROM (
    VALUES
        ('clinic_telemed_remote', 'Kham tu xa (nhe)', 'CL3-A — remote_async modality, no embedded video', ARRAY['clinic','hybrid'], 103),
        ('clinic_telemed_video', 'Kham online video', 'CL3-B — WebRTC waiting room (disabled until B)', ARRAY['clinic','hybrid'], 104)
) AS v(code, name, description, verticals, sort_order)
WHERE NOT EXISTS (
    SELECT 1 FROM platform_module_registry m WHERE m.module_code = v.code
);

UPDATE public.tenants t
SET
    settings = jsonb_set(
        COALESCE(t.settings, '{}'::jsonb),
        '{platform,enabled_modules}',
        (
            SELECT COALESCE(jsonb_agg(to_jsonb(m)), '[]'::jsonb)
            FROM (
                SELECT DISTINCT m
                FROM jsonb_array_elements_text(
                    COALESCE(t.settings->'platform'->'enabled_modules', '[]'::jsonb)
                    || '["clinic_telemed_remote"]'::jsonb
                ) AS m
            ) d
        ),
        true
    ),
    updated_at = NOW()
WHERE t.tenant_code = 'DEMO_CLINIC'
  AND t.deleted_at IS NULL;
