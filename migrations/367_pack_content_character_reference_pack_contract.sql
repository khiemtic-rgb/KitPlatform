-- KitPlatform 367: CHARACTER_REFERENCE_PACK_V1 contract alignment
-- Reuses pack_content.video_character_reference_pack. No duplicate table.
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Does not generate. Does not modify Master / DNA / PRP / Golden.

ALTER TABLE pack_content.video_character_reference_pack
    DROP CONSTRAINT IF EXISTS ck_video_crp_status;

ALTER TABLE pack_content.video_character_reference_pack
    ADD CONSTRAINT ck_video_crp_status CHECK (status IN (
        'DRAFT', 'REVIEW', 'VALIDATED', 'APPROVED', 'DIRECTOR_APPROVED',
        'LOCKED', 'REJECTED', 'SUPERSEDED'
    ));

CREATE OR REPLACE FUNCTION pack_content.fn_video_crp_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    RAISE EXCEPTION 'CRP_LOCKED: cannot delete a Character Reference Pack';
  END IF;
  IF OLD.status = 'LOCKED' THEN
    IF NEW.status = 'SUPERSEDED'
       AND NEW.pack_sha256 IS NOT DISTINCT FROM OLD.pack_sha256
       AND NEW.master_sha256 IS NOT DISTINCT FROM OLD.master_sha256
       AND NEW.dna_sha256 IS NOT DISTINCT FROM OLD.dna_sha256
       AND NEW.extra_json IS NOT DISTINCT FROM OLD.extra_json
       AND NEW.notes IS NOT DISTINCT FROM OLD.notes
       AND NEW.character_id IS NOT DISTINCT FROM OLD.character_id
       AND NEW.pack_version IS NOT DISTINCT FROM OLD.pack_version
    THEN
      RETURN NEW;
    END IF;
    RAISE EXCEPTION 'CRP_LOCKED: V1 không overwrite. Tạo REFERENCE PACK V2.';
  END IF;
  RETURN NEW;
END;
$$;

COMMENT ON TABLE pack_content.video_character_reference_pack IS
    'CHARACTER_REFERENCE_PACK_V1. MASTER > DNA > REFERENCE PACK > PRODUCTION. Spec in extra_json. SUPERSEDED is event+status; payload of LOCKED V1 stays immutable.';
