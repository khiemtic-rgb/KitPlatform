# PRODUCTION-CREDENTIAL-REPAIR-003 — Báo cáo nghiệm thu Giai đoạn A

Ngày ghi nhận: 2026-10-15.

Báo cáo này chỉ ghi bằng chứng đã quan sát và các điều kiện còn thiếu. Không chứa giá trị khóa, mật khẩu SSH, hoặc nội dung nhạy cảm khác. Không thay thế checklist `deploy/ubuntu/production-credential-operator-checklist.md`.

## 1. Mục tiêu và phạm vi

Mục tiêu Giai đoạn A là xác nhận khóa production `prod-v1` đã được tạo trên VPS, được sao lưu ngoài máy (kho Windows DPAPI), và được đối chiếu restore thành công, đồng thời dọn file tạm của thao tác đó.

Phạm vi báo cáo dừng tại nghiệm thu bằng chứng khóa và ghi rõ hồ sơ database. Không gồm restart API, deploy, bật HTTP transport, live probe, phê duyệt migration, hoặc áp migration 377, 378, 379.

Checklist vận hành là hồ sơ stop/go cho ba migration đó. Một đường dẫn nằm ngoài thư mục cấu hình không phải bằng chứng ngoài máy. Token checklist chỉ được chấp nhận khi giá trị đúng là `true`.

## 2. Bằng chứng đã PASS

| Hạng mục | Kết quả quan sát |
|---|---|
| Định danh khóa | `prod-v1` |
| Vị trí khóa nguồn | Đã tạo trong `/etc/kit-platform/api.env` |
| Quyền `api.env` | `600` `root:root` |
| Kiểm tra khóa nguồn | Script kiểm tra in `PRODUCTION_CREDENTIAL_KEY_OK` |
| Kho Windows DPAPI | Có file `production-credential-key.dpapi` |
| Manifest kho | `keyId=prod-v1`, `materialBytes=32`, `scope=CurrentUser`, `user=ADMIN-PC\Admin` |
| ACL thư mục vault và các file | Chỉ `ADMIN-PC\Admin` có `FullControl` |
| Đối chiếu restore | Script khôi phục in `PRODUCTION_CREDENTIAL_KEY_RESTORE_OK` |
| File export tạm trên Windows | Đã xóa (`EXPORT_EXISTS=False`) |
| File restore tạm trên VPS | Đã xóa (`RESTORE_FILE_ABSENT`) |
| Staging pull | Không còn file khớp mẫu kiểm tra trong `/run/kit-production-credential-pull` |
| `evidence.env` | Đã tạo, quyền `600` `root:root` |
| Cài đặt ứng dụng | `ProductionCredentials__OffMachineBackupConfirmed=true` đã ghi trong `api.env` |

`ProductionCredentials__OffMachineBackupConfirmed=true` là cài đặt ứng dụng riêng. Checklist không gán mặc định cho cài đặt này. Giá trị đó không thay các token `PRODUCTION_CREDENTIAL_DB_BACKUP_VERIFIED`, `PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED`, `OFFMACHINE_CONFIRMED`, `PRODUCTION_CREDENTIAL_KEY_BACKUP_VERIFIED`, `PRODUCTION_CREDENTIAL_KEY_RESTORE_VERIFIED`, hoặc `PRODUCTION_CREDENTIAL_MIGRATION_APPROVED`.

## 3. Bằng chứng còn thiếu và các điều kiện chưa đạt

Checklist yêu cầu database backup được restore và kiểm tra trên một máy khác trước khi sao chép khóa tiếp theo và trước migration. Chỉ sau kiểm tra đó mới được đặt `PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED=true`.

Bằng chứng hiện có cho database:

- Archive database đã qua kiểm tra gzip và hash.
- Restore probe vào PostgreSQL cục bộ đã thành công.

Restore probe cục bộ không phải restore trên máy khác. Chưa có bằng chứng đủ để khẳng định yêu cầu restore trên máy khác đã hoàn tất.

Vì vậy:

- Database restore ngoài máy: **chưa PASS**.
- `PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED`: **chưa đạt** điều kiện checklist.
- Toàn bộ điều kiện migration: **chưa đạt**.
- `PRODUCTION_CREDENTIAL_MIGRATION_APPROVED`: **không được đặt** trong Giai đoạn A.

`evidence.env` chỉ được ghi nhận là đã tồn tại với quyền `600` `root:root`. Báo cáo này không sửa và không xóa file đó. Sự tồn tại và quyền của file không chứng minh restore database ngoài máy, không biến token checklist thành `true`, và không phải phê duyệt migration.

Trước khi xem xét phê duyệt migration cần bổ sung: restore đúng archive đã kiểm tra lên một máy khác, kèm bằng chứng xác minh restore đó.

## 4. Tình trạng API và các giới hạn đã tuân thủ

Tại thời điểm kiểm tra, API PID `3125097` vẫn tồn tại. Quan sát này chỉ cho biết tiến trình đó có mặt lúc kiểm tra. Không dùng PID này để khẳng định toàn bộ hệ thống chưa từng restart trong mọi khoảng thời gian.

Các giới hạn đã giữ trong Giai đoạn A:

- Không restart API và không deploy.
- Không chạy migration 377, 378, 379.
- Không bật HTTP transport và không chạy live probe.
- Không đặt `PRODUCTION_CREDENTIAL_MIGRATION_APPROVED`.
- Không đổi và không tạo lại production key.
- Không sửa và không xóa bản sao development-key.
- Không sửa và không xóa database backup.
- Không sửa và không xóa `evidence.env`.
- Không commit và không push.

## 5. Quyết định cuối

**Giai đoạn A — khóa đã được backup và restore kiểm tra thành công; hồ sơ database restore ngoài máy chưa đủ; migration 377–379 vẫn BLOCKED.**

## 6. Các bước tiếp theo

Thứ tự dưới đây là việc cần làm sau. Báo cáo này không thực hiện chúng.

1. Restore archive database đã qua kiểm tra gzip/hash lên một máy khác và kiểm tra restore đó.
2. Chỉ sau khi có bằng chứng xác minh restore trên máy khác, operator mới xem xét đặt `PRODUCTION_CREDENTIAL_DB_RESTORE_VERIFIED` đúng giá trị `true`. Đồng thời đối chiếu `PRODUCTION_CREDENTIAL_DB_BACKUP_VERIFIED` với checklist: token này chỉ đứng khi backup đã được lấy và archive đã được kiểm tra, và chỉ chấp nhận đúng `true`.
3. Giữ nguyên `evidence.env` cho đến khi có quyết định vận hành riêng. Không dùng file này hoặc `ProductionCredentials__OffMachineBackupConfirmed=true` để thay token còn thiếu.
4. Phê duyệt migration là một quyết định operator riêng, sau khi các token checklist bắt buộc đều đúng `true`. Chỉ khi đó mới đặt `PRODUCTION_CREDENTIAL_MIGRATION_APPROVED=true`.
5. Chỉ sau phê duyệt đó mới chạy `deploy/ubuntu/run-kit-migrations-production-credentials.sh` cho 377, 378, và 379.
6. Live probe, gồm `ProductionCredentialRotationLiveProbe`, vẫn không gắn vào lệnh mới. Không chạy cho đến khi một phê duyệt riêng nêu rõ database và tenant.
