# Dựng phim ở máy local, máy chủ B chỉ đăng: "Đồng bộ lên máy chủ" + dọn file đã đăng + dọn phần dựng phim trên B

09/11/2026, 22:10. Claude đã sửa code ở local `E:\KitPlatform`. Mới soát bằng mắt, chưa build, chưa test.
Chủ đã duyệt các việc sau: build, test, commit, push, deploy lên B, restart `kit-famixa-api`, và dọn phần dựng phim trên B (mục C).
Làm sau handoff `content-publish-pause.md`. Nếu commit đó chưa đẩy thì gộp cả hai vào hai commit liên tiếp; tệp chung đã chứa cả thay đổi tạm dừng.

## A. Đã đổi

### API (dùng chung local + B)
- `src/Packs/Content/KitPlatform.Packs.Content.Application/ContentContracts.cs` → `ContentOptions` thêm:
  - `RemotePublishBaseUrl`: chỉ đặt ở máy local; B để trống.
  - `MediaCleanupEnabled`: mặc định `true`.
- `.../Application/ContentPublishPlan.cs`
  - Cài đặt lịch theo thương hiệu thêm `MediaKeepDaysPosted` (mặc định 3, giới hạn 1–60) và `MediaKeepDaysCancelled` (mặc định 7).
  - Thêm `IContentMediaCleanupService`, `ContentMediaStorageDto`, `ContentMediaCleanupReportDto`.
- `.../Infrastructure/ContentPublishPlanStore.cs`: lưu hai trường số ngày giữ file vào `settings_json`.
- `.../Infrastructure/ContentMediaCleanup.cs` (mới)
  - Service dọn file video và worker chạy mỗi 6 giờ (bắt đầu sau 2 phút).
  - Chỉ xử lý asset `kind = 'video'` chưa có `meta_json.purgedAt`. Chỉ xoá khi:
    - không còn `publish_job` nào ở trạng thái khác Succeeded/Cancelled;
    - không còn lượt đăng tay (`schedule_proposal` không có `publish_job_id`) ở trạng thái khác Posted/Cancelled/Rejected;
    - đã quá số ngày giữ, tính từ lần cập nhật cuối của job/lượt.
  - Bài chưa từng lên lịch thì không đụng.
  - Xoá file nằm trong `Content:AssetRoot` (chặn `../`) và thư mục rỗng. Đánh dấu `purgedAt` / `purgedBytes` / `purgedBy` vào `meta_json`. Không xoá dòng DB.
- `.../Infrastructure/ContentPackDependencyInjection.cs`:
  - đăng ký service và worker dọn file;
  - đăng ký HttpClient `content-remote` (timeout 20 phút).
- `src/KitPlatform.Api/Controllers/Content/ContentAgentController.cs`:
  - `GET api/content/agent/media-storage`;
  - `POST api/content/agent/media-cleanup`.
- `src/KitPlatform.Api/Controllers/Content/ContentRemoteRelayController.cs` (mới, `api/content-remote`)
  - Cầu nối local → B.
  - `GET status`.
  - `POST auth/{login|select-workspace|refresh}`.
  - `GET|POST api/{**path}`, chỉ cho các đường trong danh sách cố định:
    - `content/brands`, `content/brands/{id}/channels`;
    - `content/topics` (POST), `content/topics/{id}` (GET);
    - `content/topics/{id}/variants/import`, `content/topics/{id}/video`;
    - `content/agent/schedule-topics`, `content/agent/schedule-settings`;
    - `auth/me`.
  - Token B lấy từ header `X-Remote-Authorization`. Thân yêu cầu (multipart video) đi thẳng sang B, tối đa 600 MB.
  - B không đặt `RemotePublishBaseUrl` nên trên B mọi lệnh ở đây trả 404.
- `src/KitPlatform.Api/appsettings.Development.json`: thêm `"RemotePublishBaseUrl": "https://famixa.157.10.53.182.sslip.io"`.
  - **Chỉ là cấu hình máy local. KHÔNG commit tệp này** (có thể chứa khoá); nếu đang được git theo dõi thì bỏ ra khỏi commit.

### Admin
- `client/admin/src/shared/api/content-remote.api.ts` (mới)
  - Axios qua `/api/content-remote`. Phiên đăng nhập B lưu ở `localStorage` (`kitplatform-remote-publish`), không lưu mật khẩu.
  - Tự refresh khi gặp 401.
  - Các hàm gọi B: brands, channels, createTopic, importVariants, uploadVideo (có %), scheduleTopics.
- `client/admin/src/modules/content/RemotePublishServerCard.tsx` (mới): thẻ kết nối máy chủ đăng bài. Người dùng tự gõ tài khoản; có bước chọn workspace.
- `client/admin/src/modules/content/NovixaExportStep.tsx` (bước 5):
  - Khi `status.enabled`: hiện thẻ kết nối. Tìm thương hiệu trên B theo `code` (dự phòng theo id).
  - Nút đổi thành "Duyệt và đồng bộ lên máy chủ". Cùng các bước như cũ, nhưng chạy trên B: tạo bài → nhập bản viết → gửi video → B xếp lịch và duyệt.
  - Ghi tiến độ vào `exports[key].remote`; bấm lại thì làm tiếp từ chỗ dừng, không tạo trùng.
  - Câu miễn trừ đầu tư chỉ bắt buộc với thương hiệu tài chính (`lintProfileOf(brandCode) === 'trading'`), không áp cho Novixa.
- `client/admin/src/modules/content/film-production-orchestrator.ts`: `FilmExportRecord.remote`.
- `client/admin/src/shared/api/content-agent.api.ts`: thêm `mediaKeepDays*`, `fetchMediaStorage`, `runMediaCleanup`.
- `client/admin/src/modules/content/AgentScheduleSettingsCard.tsx`: mục "Dọn file video đã đăng" gồm số ngày giữ, dung lượng hiện tại và nút "Dọn ngay".

## B. Việc của Cursor
1. `dotnet build`, test Content, admin `npm run build` (tsc).
   - Sửa lỗi biên dịch nhỏ nếu có, giữ nguyên hành vi.
   - Lỗi tsc có sẵn ở `PharmacyLocalRegimenPage.tsx` không thuộc việc này.
   - Test đỏ vì thay đổi này thì dừng và báo lại.
2. Commit đúng các tệp ở mục A, trừ `appsettings.Development.json`.
   - Message: `content: dựng phim ở máy local, đồng bộ thành phẩm lên máy chủ đăng; tự dọn video đã đăng`.
   - Push.
3. Deploy API + admin lên B và restart `kit-famixa-api`. Không đụng nginx/systemd. Không cần migration.
   - B **không** đặt `RemotePublishBaseUrl`.
   - Kiểm tra trong log B có dòng của worker dọn file (đang bật hay đang tắt).
4. Chạy lại API local (`run-dev.bat`) để nhận `RemotePublishBaseUrl`.
   - Gọi `GET http://localhost:5173/api/content-remote/status` (đã đăng nhập admin local), phải ra `enabled: true`.
5. Thử đầu cuối bằng một video ngắn thử, KHÔNG đăng công khai:
   - Tạo trên B một thương hiệu thử, hoặc bật "Tạm dừng đăng" cho thương hiệu dùng để thử.
   - Studio local → bước 5 → kết nối máy chủ (chủ tự gõ tài khoản) → Duyệt và đồng bộ.
   - Thấy bài, video và lịch trên B. Sau đó huỷ lượt thử trên B.
   - Báo kết quả.
6. Gọi `GET /api/content/agent/media-storage` trên B và ghi lại số file / MB trước khi dọn.

## C. Dọn phần dựng phim trên B (sau khi mục B chạy ổn)
Chủ đã đồng ý xoá để giải phóng bộ nhớ. Làm theo thứ tự:
1. **Kiểm kê trước, chưa xoá gì.** Chạy `du -sh` từng mục và báo lại danh sách kèm dung lượng:
   - `/var/lib/kit-famixa/video-repo`: bộ dựng Python/ffmpeg, do `scripts/sync-video-kit-to-server.ps1` đẩy lên. Gồm cả các thư mục `outputs/novixa-studio/*` và proof render.
   - Thư mục dựng phim trong ContentRoot của API B, theo `Content:*Root`:
     - `VideoAssetRoot` (`App_Data/content-video`), `WorkAssetRoot` (`App_Data/content-work`);
     - `KitVideoKeyframeRoot`, `KitVideoMasterRoot`, `KitVideoIdentityRoot`, `KitVideoStressRoot`, `KitVideoVideoRoot`;
     - `VisualIntelligenceRoot`;
     - thư mục render tạm của Novixa (preview, motion, shorts).
   - Venv / cache Python và các thư mục tạm của ffmpeg nếu có.
   - Cache gói (`~/.cache/pip`, `~/.nuget/packages` cũ) chỉ liệt kê, không xoá.
2. **KHÔNG xoá:**
   - `Content:AssetRoot` (`App_Data/content-assets`): chứa video đính bài đang chờ đăng; việc dọn file video đã có worker lo.
   - Cơ sở dữ liệu.
   - `/etc/kit-famixa/*`.
   - Mã nguồn đang chạy và log hệ thống.
3. Sau khi chủ xác nhận danh sách:
   - xoá các mục trên;
   - bỏ (comment) dòng `Content__VideoRepoRoot` trong `/etc/kit-famixa/api.env`, chỉ dòng đó; không in giá trị khoá khác;
   - restart `kit-famixa-api`.
4. Báo lại:
   - dung lượng ổ trước / sau (`df -h`) và RAM lúc rảnh (`free -m`);
   - API, admin B và trang Lịch đăng & Agent vẫn chạy;
   - các lượt đã lên lịch vẫn còn.
5. Từ nay không chạy `scripts/sync-video-kit-to-server.ps1` nữa; ghi chú điều đó vào đầu script.

Không commit gì ngoài mục B.2 nếu chủ chưa bảo. Không in giá trị khoá hay secret.
