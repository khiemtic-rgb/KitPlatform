-- KitPlatform 360: SUPERSEDED Contract/Prompt rows stay immutable (payload/text/SHA).
-- Manifest: deploy/ubuntu/migration-files.content.txt only.
-- Does not modify Master/DNA/PRP V1. Does not touch Golden SH01-01. No pixels.

CREATE OR REPLACE FUNCTION pack_content.fn_video_prod_shot_contract_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    IF OLD.contract_status IN ('DIRECTOR_APPROVED', 'SUPERSEDED') THEN
      RAISE EXCEPTION 'SHOT_CONTRACT_LOCKED: V1 không overwrite. Dùng V2.';
    END IF;
    RETURN OLD;
  END IF;
  IF OLD.contract_status = 'DIRECTOR_APPROVED'
     AND NEW.contract_status = 'SUPERSEDED'
     AND NEW.payload_json = OLD.payload_json
     AND NEW.contract_sha256 = OLD.contract_sha256
     AND NEW.canonical_json = OLD.canonical_json THEN
    RETURN NEW;
  END IF;
  IF OLD.contract_status IN ('DIRECTOR_APPROVED', 'SUPERSEDED') THEN
    RAISE EXCEPTION 'SHOT_CONTRACT_LOCKED: V1 không overwrite. Dùng V2.';
  END IF;
  RETURN NEW;
END;
$$;

CREATE OR REPLACE FUNCTION pack_content.fn_video_prod_prompt_immutable()
RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
  IF TG_OP = 'DELETE' THEN
    IF OLD.prompt_status IN ('COMPILED', 'SUPERSEDED') THEN
      RAISE EXCEPTION 'PROMPT_COMPILER_LOCKED: Prompt V1 không overwrite. Dùng V2.';
    END IF;
    RETURN OLD;
  END IF;
  IF OLD.prompt_status = 'COMPILED'
     AND NEW.prompt_status = 'SUPERSEDED'
     AND NEW.prompt_text = OLD.prompt_text
     AND NEW.prompt_sha256 = OLD.prompt_sha256
     AND NEW.contract_sha256 = OLD.contract_sha256 THEN
    RETURN NEW;
  END IF;
  IF OLD.prompt_status IN ('COMPILED', 'SUPERSEDED') THEN
    RAISE EXCEPTION 'PROMPT_COMPILER_LOCKED: Prompt V1 không overwrite. Dùng V2.';
  END IF;
  RETURN NEW;
END;
$$;
