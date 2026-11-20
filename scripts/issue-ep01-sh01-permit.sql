-- FAMIXA-EP01-PERMIT-001: Issue Official Execution Permit for EP01-SC01-SH01 CREATE_VIDEO
-- 
-- Target: buildId=4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f, shotCode=EP01-SC01-SH01
-- Action: CREATE_VIDEO, Provider: runway, Model: gen4_turbo
--
-- This script directly inserts an ISSUED permit into the production_execution_permit table
-- following the canonical permit issuance pattern from ProductionExecutionPermitCanon.Create

DO $$
DECLARE
    v_build_id UUID := '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f';
    v_shot_code TEXT := 'EP01-SC01-SH01';
    v_episode_code TEXT := 'EP01';
    v_scene_code TEXT := 'SC01';
    v_action TEXT := 'CREATE_VIDEO';
    v_provider_id TEXT := 'runway';
    v_model_id TEXT := 'gen4_turbo';
    v_issued_by TEXT := 'FAMIXA-EP01-PERMIT-001';
    v_reason TEXT := 'OFFICIAL_EP01_SH01_REAL_EXECUTION_PERMIT';
    v_permit_hash TEXT;
    v_canonical_string TEXT;
    v_tenant_id UUID;
    v_existing_permit UUID;
BEGIN
    -- Get KIT_MKT tenant_id
    SELECT tenant_id INTO v_tenant_id 
    FROM pack_content.tenant 
    WHERE tenant_code = 'KIT_MKT';
    
    IF v_tenant_id IS NULL THEN
        RAISE EXCEPTION 'Tenant KIT_MKT not found';
    END IF;
    
    RAISE NOTICE 'Tenant KIT_MKT: %', v_tenant_id;
    
    -- Build canonical hash following ProductionExecutionPermitCanon.HashOf
    -- Format: buildId|shotCode|action|providerId|modelId (providerId and modelId lowercased)
    v_canonical_string := v_build_id::text || '|' || v_shot_code || '|' || v_action || '|' || 
                          lower(v_provider_id) || '|' || lower(v_model_id);
    
    -- Compute SHA256 hash (PostgreSQL requires pgcrypto extension)
    v_permit_hash := 'PERMIT_HASH_V1:' || encode(digest(v_canonical_string, 'sha256'), 'hex');
    
    RAISE NOTICE 'Canonical: %', v_canonical_string;
    RAISE NOTICE 'Permit Hash: %', v_permit_hash;
    
    -- Check for existing ISSUED permit
    SELECT permit_id INTO v_existing_permit
    FROM pack_content.production_execution_permit
    WHERE build_id = v_build_id
      AND shot_code = v_shot_code
      AND action = v_action
      AND lower(provider_id) = lower(v_provider_id)
      AND lower(model_id) = lower(v_model_id)
      AND status = 'ISSUED'
      AND tenant_id = v_tenant_id;
    
    IF v_existing_permit IS NOT NULL THEN
        RAISE NOTICE 'Permit already exists: %', v_existing_permit;
        RAISE NOTICE 'Status: REUSED';
    ELSE
        -- Insert new permit
        INSERT INTO pack_content.production_execution_permit (
            permit_id,
            build_id,
            shot_code,
            episode_code,
            scene_code,
            action,
            provider_id,
            model_id,
            status,
            issued_by,
            issued_at,
            consumed_at,
            reason,
            permit_hash,
            tenant_id
        ) VALUES (
            gen_random_uuid(),
            v_build_id,
            v_shot_code,
            v_episode_code,
            v_scene_code,
            v_action,
            v_provider_id,
            v_model_id,
            'ISSUED',
            v_issued_by,
            NOW(),
            NULL,
            v_reason,
            v_permit_hash,
            v_tenant_id
        )
        RETURNING permit_id INTO v_existing_permit;
        
        RAISE NOTICE 'Permit ISSUED: %', v_existing_permit;
        RAISE NOTICE 'Status: CREATED';
    END IF;
    
    -- Verify permit
    RAISE NOTICE '';
    RAISE NOTICE '=== PERMIT VERIFICATION ===';
    RAISE NOTICE 'Build ID: %', v_build_id;
    RAISE NOTICE 'Shot Code: %', v_shot_code;
    RAISE NOTICE 'Action: %', v_action;
    RAISE NOTICE 'Provider: % / Model: %', v_provider_id, v_model_id;
    RAISE NOTICE 'Permit ID: %', v_existing_permit;
    RAISE NOTICE 'Status: ISSUED';
    
END $$;

-- Query to verify permit
SELECT 
    permit_id,
    build_id,
    shot_code,
    action,
    provider_id,
    model_id,
    status,
    issued_by,
    issued_at,
    reason
FROM pack_content.production_execution_permit
WHERE build_id = '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f'
  AND shot_code = 'EP01-SC01-SH01'
  AND action = 'CREATE_VIDEO'
  AND lower(provider_id) = 'runway'
  AND lower(model_id) = 'gen4_turbo'
ORDER BY issued_at DESC
LIMIT 1;
