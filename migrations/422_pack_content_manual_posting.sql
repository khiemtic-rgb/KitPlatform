-- KitPlatform 422: Content Park — đăng tay theo lịch (Fanpage chưa có API Meta).
-- Manifest: deploy/ubuntu/migration-files.content.txt only. Do not merge into Pharmacy prod.
-- Precondition: 410 (schedule_proposal).
-- Postcondition: schedule_proposal.status nhận thêm 'Posted' (người dùng đã đăng tay lượt đã duyệt không có publish_job).
-- API cũng tự áp ràng buộc này khi chạy nếu chưa có. Idempotent.

BEGIN;

DO $$
BEGIN
  IF EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ck_content_schedule_proposal_status'
             AND pg_get_constraintdef(oid) NOT LIKE '%Posted%') THEN
    ALTER TABLE pack_content.schedule_proposal DROP CONSTRAINT ck_content_schedule_proposal_status;
    ALTER TABLE pack_content.schedule_proposal ADD CONSTRAINT ck_content_schedule_proposal_status
      CHECK (status IN ('Proposed', 'Approved', 'Posted', 'Rejected', 'Cancelled', 'Failed'));
  END IF;
END $$;

COMMIT;
