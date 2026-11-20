# Nút "Tạm dừng đăng" theo thương hiệu

09/11/2026. Claude đã sửa code ở local `E:\KitPlatform`, chỉ kiểm tra bằng mắt; chưa build, chưa test.
Việc của Cursor: build, chạy test, sửa lỗi biên dịch nhỏ nếu có, rồi triển khai lên máy chủ B.

## Mục đích
Chủ muốn tự dừng việc Agent đăng bài cho từng thương hiệu (trước mắt: Novixa và Người giữ vốn), không cần vào máy chủ.

## Đã đổi
- `src/Packs/Content/KitPlatform.Packs.Content.Application/ContentPublishPlan.cs`
  - `ContentBrandScheduleSettingsDto` thêm `bool PublishPaused = false` (tham số cuối, có mặc định).
  - `ContentBrandScheduleSettingsRequest` thêm `bool? PublishPaused`.
  - `ContentPublishPlanRules.Normalize`: `request.PublishPaused ?? current.PublishPaused`.
- `.../Infrastructure/ContentPublishPlanStore.cs`
  - `SaveSettingsAsync` ghi thêm `publishPaused` vào `settings_json`.
  - `ListAutopilotBrandsAsync` bỏ qua thương hiệu đang tạm dừng.
- `.../Infrastructure/ContentWorkRepository.cs`
  - `ListDuePublishJobIdsAsync`: không lấy job `Queued` của thương hiệu đang tạm dừng (`brand_id = ANY(@Paused)`).
  - Hàm mới `ListPublishPausedBrandIdsAsync`: đọc `brand_schedule_setting` có `publishPaused = true`;
    nếu bảng chưa có thì trả mảng rỗng, không chặn việc đăng.
- `.../Infrastructure/ContentAutopilotWorker.cs`
  - `RunBrandAsync`: thương hiệu đang tạm dừng thì không xếp lịch, không viết nháp.
- `client/admin/src/shared/api/content-agent.api.ts`
  - Kiểu `AgentScheduleSettings` thêm `publishPaused`.
  - Hàm mới `setAgentPublishPaused(brandId, paused)`: gửi PUT chỉ có `{ publishPaused }`, các cài đặt khác giữ nguyên.
- `client/admin/src/modules/content/AgentScheduleSettingsCard.tsx`
  - Nút "Tạm dừng đăng" / "Chạy lại đăng bài" ở đầu thẻ, bấm là lưu ngay.
  - Thẻ đỏ "Đang tạm dừng đăng" và cảnh báo:
    - bài đã có lịch vẫn nằm nguyên;
    - khi chạy lại, bài quá giờ sẽ đăng ngay.

## Hành vi
- **Khi tạm dừng:**
  - Không bài nào của thương hiệu được Agent đăng tự động, kể cả bài đã duyệt lịch hoặc đã quá giờ.
  - Job vẫn giữ trạng thái `Queued`, không bị huỷ.
  - Không xếp lịch, không viết thêm.
- **Khi người dùng tự bấm "Đăng"/"Đăng lại"** (`/content/jobs/{id}/run`): vẫn chạy, vì đó là thao tác tay.
- **Khi bấm "Chạy lại":** job quá giờ sẽ đăng ngay ở vòng quét kế tiếp (15 giây).

## Việc của Cursor
1. `dotnet build` và các test Content (`ContentScheduleApprovalRulesTests` và các test khác có `ContentBrandScheduleSettingsDto`). Sửa lỗi biên dịch nhỏ nếu có, giữ nguyên hành vi.
2. Admin: `npm run build` (tsc). Lỗi tsc có sẵn ở `PharmacyLocalRegimenPage.tsx` không thuộc việc này.
3. Triển khai lên B (API + admin) như các lần trước. **Hỏi chủ trước khi restart `kit-famixa-api`.** Không đụng nginx/systemd.
4. Không cần migration: dùng cột `settings_json` có sẵn của `pack_content.brand_schedule_setting`.
5. Sau khi chạy:
   - vào admin B → Lịch đăng & Agent → Cài đặt lịch Agent, chọn Người giữ vốn → bấm "Tạm dừng đăng";
   - làm tương tự với Novixa;
   - kiểm tra 4 job Facebook `Queued` của Người giữ vốn (19:30 ngày 10–13/11) không chạy.
6. Báo lại. Không commit nếu chủ chưa bảo.

## Ghi chú cho chủ
- Trên TikTok @novixavn có 6 video gắn #NguoiGiuVonAgent (từ 3 đến 8/11).
- Nơi đăng TikTok của Người giữ vốn trên B đang "Đã kết nối". Nhiều khả năng nó trỏ vào chính tài khoản @novixavn.
- Cần kiểm tra và kết nối lại đúng tài khoản trước khi chạy lại đăng cho Người giữ vốn.
