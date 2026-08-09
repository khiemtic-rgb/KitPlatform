-- KitPlatform 185: alias public.prescription_lines → e-Rx lines (POS medication reminders)
-- Root cause: SalesRepository JOIN used bare "prescription_lines"; real table is
-- pack_pharmacy.electronic_prescription_lines (096). View keeps old name safe if any SQL remains.

CREATE OR REPLACE VIEW public.prescription_lines AS
SELECT
    id,
    tenant_id,
    prescription_id,
    product_id,
    product_unit_id,
    line_dispensing_class,
    qty_prescribed,
    qty_dispensed,
    dosage_instruction,
    sort_order,
    created_at
FROM pack_pharmacy.electronic_prescription_lines;

COMMENT ON VIEW public.prescription_lines IS
    'Compatibility alias for pack_pharmacy.electronic_prescription_lines (POS drink reminders).';

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'kitplatform') THEN
        GRANT SELECT ON public.prescription_lines TO kitplatform;
    END IF;
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'pharmacore') THEN
        GRANT SELECT ON public.prescription_lines TO pharmacore;
    END IF;
END $$;
