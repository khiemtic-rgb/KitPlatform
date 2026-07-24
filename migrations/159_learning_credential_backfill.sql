-- KitPlatform 159: Backfill learning credentials from already-passed modules

INSERT INTO pack_learning.credential (
    tenant_id, employee_id, competency_code, level_code,
    source_module_id, score_pct, earned_at
)
SELECT
    e.tenant_id,
    e.employee_id,
    c.code,
    m.level_code,
    m.id,
    mp.score_pct,
    COALESCE(mp.completed_at, mp.updated_at, NOW())
FROM pack_learning.enrollment e
INNER JOIN pack_learning.module_progress mp
    ON mp.enrollment_id = e.id AND mp.status = 'passed'
INNER JOIN pack_learning.module m ON m.id = mp.module_id
CROSS JOIN LATERAL UNNEST(m.competency_codes) AS c(code)
WHERE c.code IS NOT NULL AND btrim(c.code) <> ''
ON CONFLICT (tenant_id, employee_id, competency_code) DO NOTHING;
