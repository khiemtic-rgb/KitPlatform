-- KitPlatform 290: Video MVP V1 — pipeline statuses + Creatomate template seed
-- Manifest: deploy/ubuntu/migration-files.content.txt
-- Depends on: 288 (video factory)

ALTER TABLE pack_content.video_job
    DROP CONSTRAINT IF EXISTS ck_video_job_status;

ALTER TABLE pack_content.video_job
    ADD CONSTRAINT ck_video_job_status CHECK (status IN (
        'Draft',
        'GeneratingScript',
        'GeneratingAssets',
        'GeneratingVoice',
        'PreparingRender',
        'Queued',
        'Rendering',
        'Ready',
        'Failed',
        'Approved'
    ));

COMMENT ON CONSTRAINT ck_video_job_status ON pack_content.video_job IS
    'MVP V1 pipeline: script/assets/voice → Creatomate render → Ready/Approved.';

-- Creatomate-capable template (external_template_id filled in org settings / admin later)
INSERT INTO pack_content.video_template (
    code, name, provider, external_template_id, aspect_ratio, duration_sec, description, config_json, sort_order
)
SELECT
    'creatomate_9x16_mvp',
    'Creatomate 9:16 MVP (text + scene images)',
    'creatomate',
    NULL,
    '9:16',
    45,
    'MVP V1: map HOOK/PROBLEM/… text + image URLs vào Creatomate. Điền external_template_id = Creatomate template UUID.',
    '{
      "beats": ["HOOK", "PROBLEM", "INSIGHT", "SOLUTION", "CTA"],
      "platform": "tiktok",
      "modifications": {
        "Title.text": "{{title}}",
        "Script.text": "{{script}}"
      },
      "sceneTextKeys": {
        "HOOK": "Hook.text",
        "PROBLEM": "Problem.text",
        "INSIGHT": "Insight.text",
        "SOLUTION": "Solution.text",
        "CTA": "Cta.text"
      },
      "sceneImageKeys": {
        "HOOK": "Hook.image",
        "PROBLEM": "Problem.image",
        "INSIGHT": "Insight.image",
        "SOLUTION": "Solution.image",
        "CTA": "Cta.image"
      },
      "voiceKey": "Voice.source"
    }'::jsonb,
    5
WHERE NOT EXISTS (
    SELECT 1 FROM pack_content.video_template t WHERE t.code = 'creatomate_9x16_mvp'
);
