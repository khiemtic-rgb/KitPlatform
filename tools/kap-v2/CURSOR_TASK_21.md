# CURSOR TASK 21 — Thứ tự làm các việc còn lại (giao lúc 2026-11-19 23:11 +07)

File này chỉ xếp thứ tự. Chi tiết từng việc nằm trong các file đã có, **không sửa các file đó**.
Mỗi bước xong thì ghi giờ (+07) vào `tools/git-safety/LOG_2026-11-19.md` rồi mới sang bước sau.

Quy định chung:
- Không ép push.
- Push chỉ khi người dùng gõ "push"; deploy chỉ khi người dùng gõ "deploy".
- Không commit tk.txt, .env, appsettings*.json.
- Không chạy `reset --hard`, `clean`, `checkout --`, `restore`, không xóa stash, không xóa nhánh.

## Bước 1 — Sao lưu (làm ngay)

Làm **PHẦN 1 của CURSOR_TASK_20.md**: bundle toàn repo, chụp thay đổi chưa commit vào `refs/backup/<giờ>/wip`, liệt kê file nhạy cảm và file lớn.
Không cần chờ người dùng. Xong thì sang bước 2.

## Bước 2 — Xác nhận DLL Survey trên VPS đúng bằng code trên nhánh `kap-v2-backend` (1e68a1a7)

1. Đọc ProductVersion của 2 DLL Survey trên VPS và của 2 DLL vừa build:
   `[System.Diagnostics.FileVersionInfo]::GetVersionInfo(<dll>).ProductVersion`
2. Lấy mã commit sau dấu `+` của bản VPS. Build lại Survey pack ở Release trên `kap-v2-backend`, thêm `-p:SourceRevisionId=<mã đó>`, cùng đường dẫn `E:\KitPlatform`.
3. So mã băm với VPS.
   - **Khớp:** báo "code giống hệt VPS". Chờ người dùng gõ "push", rồi fast-forward `main` lên 1e68a1a7 (nếu origin/main đã đổi thì rebase trước, build lại, báo lại). **Không deploy DLL.**
   - **Không khớp:** so bằng ILSpy hoặc ildasm (danh sách kiểu, phương thức, chuỗi), báo chỗ khác. Không push. Dừng bước 2 và làm tiếp bước 4.

## Bước 3 — Đồng bộ 3 nơi

Làm **CURSOR_TASK_19.md** từ mục 2 trở đi. Mục 1 của Task 19 chính là bước 2 ở trên.
- Lập bảng VPS / GIT / LOCAL.
- Ghi `/var/www/kit-platform/api/SURVEY_VERSION.txt` nếu DLL khớp. `assessment-web/VERSION.txt` đã ghi lúc 23:02.
- Tạo `scripts/check-survey-sync.ps1`.
- Ghi quy tắc deploy vào `tools/kap-v2/README.md`.
- Commit script và README vào cùng nhánh khảo sát. Push khi người dùng gõ "push".

## Bước 4 — Chia các phần ngoài khảo sát

Làm **PHẦN 2 và 3 của CURSOR_TASK_20.md**.
- Phần 2, đẩy các nhánh `backup/*`: chờ người dùng gõ "push".
- Phần 3: lập bảng chia việc rồi **DỪNG**. Người dùng sẽ gửi bảng cho Claude xem trước khi duyệt.
- Dòng `381_pack_test_onboarding_canary.sql` trong `scripts/run-migrations.ps1` (local) thuộc phần này. Xếp vào việc phù hợp.

## Không làm trong task này

- Mục E của CURSOR_TASK_17 (báo cáo cũ phía máy chủ cho người quản trị): để sau.
- Phần 4, 5 của CURSOR_TASK_20: chờ người dùng duyệt bảng.

## Báo lại cho người dùng sau mỗi bước

| Bước | Giờ xong (+07) | Kết quả | Đang chờ gì |
|---|---|---|---|
| 1 Sao lưu | | đường dẫn bundle, hash snapshot, số file nhạy cảm | — |
| 2 DLL | | khớp / không khớp, ProductVersion 4 DLL | "push" |
| 3 Đồng bộ | | bảng VPS / GIT / LOCAL | "push" |
| 4 Chia việc | | bảng chia việc | người dùng duyệt |
