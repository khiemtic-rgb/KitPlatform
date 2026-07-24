-- KitPlatform 166: People value loop — feedback 2 chiều + career labels rõ hơn

ALTER TABLE pack_learning.evaluation
    ADD COLUMN IF NOT EXISTS employee_feedback TEXT,
    ADD COLUMN IF NOT EXISTS next_month_goal TEXT,
    ADD COLUMN IF NOT EXISTS employee_responded_at TIMESTAMPTZ;

-- Lộ trình nghề gần thực tế nhà thuốc (kiến thức / kỹ năng / KPI ngắn trong summary)
UPDATE pack_learning.career_level AS cl
SET
    title = v.title,
    summary = v.summary
FROM (VALUES
    ('newbie', 'Nhân viên mới',
     'Kiến thức: L0–L1 · Kỹ năng: đăng nhập, mở ca, bán cơ bản · KPI: hoàn thành học L0–L1'),
    ('staff', 'Nhân viên quầy',
     'Kiến thức: POS/FEFO + CSKH · Kỹ năng: phục vụ khách, gắn điểm · KPI: đơn/ca ổn định, eval ≥70'),
    ('senior', 'Nhân viên chính',
     'Kiến thức: kho + ca + biên tư vấn · Kỹ năng: GRN/cận date, escalate · KPI: eval ≥75, thâm niên ≥6 tháng'),
    ('lead', 'Ca trưởng',
     'Kiến thức: đóng ca / lệch quỹ · Kỹ năng: điều phối ca, mentor NV mới · KPI: eval ≥80, thâm niên ≥12 tháng')
) AS v(code, title, summary)
WHERE cl.code = v.code
  AND cl.is_active
  AND (cl.tenant_id IS NULL OR cl.tenant_id IN (
      SELECT id FROM public.tenants
      WHERE tenant_code IN ('NT_XUANHOA', 'DEMO_PHARMACY') AND deleted_at IS NULL
  ));

-- Bậc Quản lý chi nhánh (nếu chưa có)
INSERT INTO pack_learning.career_level (
    tenant_id, code, title, summary, sort_order,
    min_months_tenure, min_avg_evaluate, required_competency_codes
)
SELECT t.id, 'branch_mgr', 'Quản lý chi nhánh',
       'Kiến thức: toàn bộ trụ Learn–Evaluate–Recognize–Grow · Kỹ năng: chấm/ghi nhận/duyệt bậc · KPI: đội hoàn thành học + gắn bó',
       50, 24, 85, ARRAY['pos_basic', 'shift_close', 'grn_receive']::text[]
FROM public.tenants t
WHERE t.deleted_at IS NULL
  AND t.tenant_code IN ('NT_XUANHOA', 'DEMO_PHARMACY')
  AND NOT EXISTS (
      SELECT 1 FROM pack_learning.career_level cl
      WHERE cl.tenant_id = t.id AND cl.code = 'branch_mgr'
  );
