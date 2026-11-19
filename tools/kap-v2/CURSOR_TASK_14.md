# Việc 14: Đăng trang health-check (file riêng)

File này tách riêng, vì `CURSOR_TASKS_2.md` đang mở trong editor và bị lưu đè bằng bản cũ. **Đóng tab `CURSOR_TASKS_2.md` và `CURSOR_HANDOFF.md` trong Cursor**, rồi chỉ ghi Nhật ký ở cuối file này.

Kết quả Việc 10 và 13 (đã xong, API và survey đã deploy lúc 18:22 ngày 19/11) cũng ghi tóm tắt vào Nhật ký dưới đây, vì bản ghi trong `CURSOR_TASKS_2.md` đã mất khi file bị lưu đè.

`novixa.vn` là Cloudflare Pages, tự build khi có push lên nhánh production trên GitHub. `dist` đang có trên máy được build từ cây làm việc có cả thay đổi khác (nút YouTube, video trang chủ), nên **không** dùng `wrangler pages deploy dist`.

1. **Xác định nhánh production và trạng thái nhánh trên remote.**
   - Xem nhánh production của project Cloudflare Pages, thường là `main`: trong cài đặt Pages hoặc theo README.
   - Chạy `git fetch`.
   - Ghi commit mới nhất của `origin/<nhánh đó>` vào Nhật ký.
2. **Tạo worktree.** `git worktree add E:\novixa-site-release origin/<nhánh> -b health-check-v2`.
3. **Đưa vào chỉ hai file:**
   - `novixa-site/src/pages/vi/health-check.astro`
   - `novixa-site/src/pages/vi/danh-gia/index.astro`

   Với mỗi file, so với bản trên `origin/<nhánh>`. Nếu ngoài phần sửa của Việc 3 còn thay đổi khác thì chỉ chuyển phần Việc 3, rồi ghi lại.
4. **Build và kiểm tra trong worktree.**
   - Chạy `npm ci` và `npm run build` trong `novixa-site` của worktree.
   - Mở `dist/vi/health-check/index.html`, kiểm tra:
     - ghi 6 nhóm;
     - ghi 31 câu, khoảng 7–10 phút;
     - không còn bốn câu hứa đã bỏ;
     - hình 62/100 có nhãn "Khá tốt" và ghi là minh họa.
   - Trang chủ **không** có nút YouTube hay video mới.
5. **Commit** đúng hai file trong worktree.
6. **Dừng chờ người phụ trách trả lời "push" trong chat.** Trước khi dừng, ghi vào Nhật ký: diff hai file và lệnh push sẽ chạy, ví dụ `git push origin health-check-v2:main`.
7. **Khi được duyệt:**
   - Push.
   - Chờ Cloudflare build xong.
   - Mở `https://novixa.vn/vi/health-check/` kiểm tra lại các điểm ở bước 4.
   - Ghi **"ĐÃ ĐĂNG"**.

   Nếu push bị từ chối vì nhánh remote đã có commit mới thì dừng và báo, không ép push.

## Nhật ký

| Việc | Trạng thái | Ghi chú |
|---|---|---|
| 10 | Xong | 109 luật. 447 chạy 2 lần trên dev. `run_qa.py` đúng bảng: Vai 1 hết hạn rồi I1; Vai 2 C3 rồi C2; Vai 3 hết hạn rồi O1; Vai 4 B2 rồi B4; Vai 5 chỉ O5. GROWTH vai 5 là "Chưa đủ dữ liệu". TECH vai 5 vẫn 0/100 vì chỉ 1/4 câu null, dưới ngưỡng 50%. |
| 13 | ĐÃ DEPLOY API + survey. Chưa đăng novixa.vn | Backup `/var/backups/KitPlatform/novixa_prod_kap447_20261119_182233.sql.gz` và `/var/www/kit-platform/api-backup-20261119_182233/`. 447 COMMIT: V1 và V2 đều active. Chỉ hai DLL Survey. Ngưỡng so sánh trên VPS từ 5 thành 30. Health 200. `survey.novixa.vn` bản `index-BJIRDTIc.js`. `DEMO_PHARMACY` đơn bán 200. Không đụng dữ liệu NT_XUANHOA. |
| 14 | ĐÃ ĐĂNG | Push `65f36859..df9606c1` `health-check-v2` → `main`, không ép. `https://novixa.vn/vi/health-check/` đã có 6 nhóm, "31 câu, khoảng 7–10 phút", "Hình minh họa, không phải kết quả thật", "Khá tốt". Bốn câu hứa đã hết. `https://novixa.vn/vi/danh-gia/` đã có câu 31 câu. |
