-- KitPlatform 182: Mở lại enrollment «completed» khi còn bài chưa đạt (vd. thêm L6)

UPDATE pack_learning.enrollment e
SET
    status = 'in_progress',
    completed_at = NULL
WHERE e.status = 'completed'
  AND (
      SELECT COUNT(*)::int
      FROM pack_learning.module m
      WHERE m.program_id = e.program_id
  ) > (
      SELECT COUNT(*)::int
      FROM pack_learning.module_progress mp
      WHERE mp.enrollment_id = e.id
        AND mp.status = 'passed'
  );

-- Việt hóa tiêu đề ghi nhận cũ (curriculum L5 / chương trình)
UPDATE pack_learning.recognition
SET title = replace(
    title,
    'L5 — Tăng doanh thu bền vững & xử lý tình huống',
    'L5 — Tư vấn chuyên nghiệp & Phát triển doanh thu'
)
WHERE title LIKE '%Tăng doanh thu bền vững%';

UPDATE pack_learning.recognition
SET
    title = replace(title, 'Onboarding quầy nhà thuốc — L0 đến L5', 'Onboarding quầy nhà thuốc — L0 đến L6'),
    body = replace(COALESCE(body, ''), 'Onboarding quầy nhà thuốc — L0 đến L5', 'Onboarding quầy nhà thuốc — L0 đến L6')
WHERE title LIKE '%L0 đến L5%'
   OR COALESCE(body, '') LIKE '%L0 đến L5%';
