-- PHARMACY_V2: thời gian làm bài, tỷ lệ làm xong, câu hay bị bỏ ngang.
-- Bỏ các bài hoàn thành dài hơn 2 giờ (để máy mở rồi quay lại).
-- Chạy: psql ... -f scripts/kap-v2-completion-time.sql

\echo '--- Thời gian làm bài (phút): trung vị và p75, bài <= 2 giờ ---'
SELECT
    count(*) AS bai_tinh,
    round(percentile_cont(0.5) WITHIN GROUP (ORDER BY phut)::numeric, 1) AS trung_vi_phut,
    round(percentile_cont(0.75) WITHIN GROUP (ORDER BY phut)::numeric, 1) AS p75_phut
FROM (
    SELECT EXTRACT(EPOCH FROM (s.completed_at - s.started_at)) / 60.0 AS phut
    FROM assessment_submission s
    JOIN assessment_template t ON t.id = s.template_id
    WHERE t.code = 'PHARMACY_V2'
      AND s.completed_at IS NOT NULL
      AND s.completed_at >= s.started_at
      AND s.completed_at - s.started_at <= INTERVAL '2 hours'
) x;

\echo '--- Tỷ lệ làm xong (mọi bài V2 đã bắt đầu) ---'
SELECT
    count(*) AS bat_dau,
    count(*) FILTER (WHERE s.status <> 'draft') AS lam_xong,
    round(
        100.0 * count(*) FILTER (WHERE s.status <> 'draft') / NULLIF(count(*), 0),
        1
    ) AS ty_le_phan_tram
FROM assessment_submission s
JOIN assessment_template t ON t.id = s.template_id
WHERE t.code = 'PHARMACY_V2';

\echo '--- Câu cuối cùng có trả lời của các bài còn draft ---'
SELECT
    q.code,
    q.title,
    count(*) AS so_bai_dung_o_day
FROM assessment_submission s
JOIN assessment_template t ON t.id = s.template_id AND t.code = 'PHARMACY_V2'
JOIN LATERAL (
    SELECT r.question_id
    FROM assessment_response r
    WHERE r.submission_id = s.id
    ORDER BY r.updated_at DESC, r.created_at DESC
    LIMIT 1
) cuoi ON TRUE
JOIN assessment_question q ON q.id = cuoi.question_id
WHERE s.status = 'draft'
GROUP BY q.code, q.title
ORDER BY so_bai_dung_o_day DESC, q.code;

\echo '--- Theo nguồn quảng cáo (utm_campaign / utm_content) ---'
SELECT
    COALESCE(s.metadata #>> '{utm,utm_campaign}', '') AS utm_campaign,
    COALESCE(s.metadata #>> '{utm,utm_content}', '') AS utm_content,
    count(*) AS bat_dau,
    count(*) FILTER (WHERE s.status <> 'draft') AS lam_xong,
    count(*) FILTER (
        WHERE s.respondent_phone IS NOT NULL AND btrim(s.respondent_phone) <> ''
    ) AS de_lai_so
FROM assessment_submission s
JOIN assessment_template t ON t.id = s.template_id
WHERE t.code = 'PHARMACY_V2'
  AND s.metadata ? 'utm'
GROUP BY 1, 2
ORDER BY bat_dau DESC, utm_campaign, utm_content;
