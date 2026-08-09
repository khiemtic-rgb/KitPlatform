-- KitPlatform 140: SPA_HEALTH_V1 maturity levels (4 tiers)
-- Depends on: 089, 139_spa_health_v1_seed.sql
-- Additive only — does not touch pharmacy maturity rows

INSERT INTO assessment_maturity_level (
    id, template_id, vertical_code, level, code, name, description, score_min, score_max, sort_order
)
VALUES
    (
        'a0000002-0000-4000-8000-000000000801',
        'a0000002-0000-4000-8000-000000000001',
        'spa_health',
        1, 'INIT', 'Khởi tạo',
        'Vận hành chủ yếu thủ công, thiếu dữ liệu tập trung và quy trình chưa ổn định.',
        1.0000, 1.5999, 1
    ),
    (
        'a0000002-0000-4000-8000-000000000802',
        'a0000002-0000-4000-8000-000000000001',
        'spa_health',
        2, 'GROW', 'Phát triển',
        'Đã có một số quy trình nhưng còn phụ thuộc kinh nghiệm cá nhân.',
        1.6000, 2.3999, 2
    ),
    (
        'a0000002-0000-4000-8000-000000000803',
        'a0000002-0000-4000-8000-000000000001',
        'spa_health',
        3, 'PRO', 'Chuyên nghiệp',
        'Quy trình và dữ liệu bắt đầu đồng bộ; có thể mở rộng với giám sát cơ bản.',
        2.4000, 3.1999, 3
    ),
    (
        'a0000002-0000-4000-8000-000000000804',
        'a0000002-0000-4000-8000-000000000001',
        'spa_health',
        4, 'LEAD', 'Dẫn đầu',
        'Dữ liệu liên thông, cảnh báo chủ động và cải tiến liên tục.',
        3.2000, 4.0000, 4
    )
ON CONFLICT (template_id, vertical_code, level) DO UPDATE SET
    name = EXCLUDED.name,
    description = EXCLUDED.description,
    score_min = EXCLUDED.score_min,
    score_max = EXCLUDED.score_max;
