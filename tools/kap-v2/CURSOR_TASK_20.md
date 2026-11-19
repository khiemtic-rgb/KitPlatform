# CURSOR TASK 20 — Đưa an toàn mọi phần chưa vào main lên git: không lẫn, không mất

Phạm vi: repo `E:\KitPlatform`. Thêm `E:\novixa-site-release` nếu có thay đổi chưa commit.

Mục tiêu: mọi code đang nằm ngoài main được cất an toàn và chia gọn theo từng việc. Gồm:
- 39 commit trên nhánh `work/luu-2026-11-16`;
- các thay đổi chưa commit trên máy (YouTube, Content, Pharmacy sales, Loyalty, Sales agent, Famixa M1, Sàn gạch đỏ, Second Brain…).

**Thời gian:** mọi tên nhánh, tên file sao lưu, message commit đều ghi giờ Việt Nam (UTC+07:00), dạng `2026-11-19_2305`.
Ghi nhật ký từng bước vào `tools/git-safety/LOG_<ngày>.md`: giờ bắt đầu, giờ xong, lệnh đã chạy, kết quả.

## Cấm tuyệt đối trong task này

- Không chạy `git reset --hard`, `git clean`, `git checkout -- <file>`, `git restore`, `git stash drop` hoặc `git stash clear`.
- Không ép push. Không xóa nhánh nào, kể cả nhánh local.
- Không merge gì vào `main`. Main chỉ nhận phần khảo sát (TASK 18, 19).
- Không đẩy lên git: tk.txt, .env*, appsettings.Production.json, file khóa, mật khẩu, token, file trên 50 MB.
- Cây làm việc trên máy giữ nguyên từ đầu tới cuối. Mọi việc làm trên worktree riêng hoặc qua index tạm.

## PHẦN 1 — Sao lưu trước (làm NGAY, trước cả TASK 18)

TASK 18 có bước `stash` và `stash pop` trên cây làm việc, nên phải có bản sao lưu trước.

1. Ghi giờ bắt đầu.
2. Sao lưu toàn bộ lịch sử git ra file:
   `git bundle create E:\Backup\git\KitPlatform_all_<giờ>.bundle --all`
   Chạy `git bundle verify` để kiểm tra file.
3. Chụp nguyên trạng thái chưa commit thành một commit, **không đụng cây làm việc và index thật**:
   ```
   $env:GIT_INDEX_FILE = "$env:TEMP\kp-snap-<giờ>.idx"
   git read-tree HEAD
   git add -A
   $tree = git write-tree
   $snap = git commit-tree $tree -p HEAD -m "snapshot chưa commit lúc <giờ> (+07)"
   Remove-Item Env:GIT_INDEX_FILE
   git update-ref refs/backup/<giờ>/wip $snap
   ```
   Lệnh `add -A` tôn trọng `.gitignore`, nên `node_modules`, `bin` và `obj` không vào.
4. Kiểm tra commit chụp ở bước 3:
   - Liệt kê file có tên khớp `.env`, `tk.txt`, `appsettings.Production`, `*.pfx`, `*.pem`, `*.key`, `id_rsa`, hoặc có chuỗi giống mật khẩu/token. Liệt kê cả file trên 50 MB.
   - Những file này **chỉ nằm trong bundle ở máy**, không bao giờ đẩy lên. Ghi danh sách vào LOG.
5. Kiểm tra commit `94780487 chore snapshot production — 16070 file` theo cùng cách. Nếu có file nhạy cảm thì đánh dấu commit này "không đẩy lên".
6. Ghi vào LOG:
   - đường dẫn bundle;
   - hash commit chụp;
   - số file đã thay đổi;
   - số file chưa từng được git theo dõi;
   - danh sách file nhạy cảm.
7. **DỪNG.** Báo người dùng: "Đã sao lưu lúc <giờ>, có thể làm TASK 18."

## PHẦN 2 — Đẩy bản sao lưu lên git (chờ người dùng gõ "push")

Đẩy dưới dạng nhánh mới có ghi giờ, không đụng nhánh nào đang có:
- `backup/2026-11-19_<giờ>/work-luu`: trỏ vào đầu nhánh `work/luu-2026-11-16`.
- `backup/2026-11-19_<giờ>/wip`: trỏ vào commit chụp ở Phần 1.
  - Trước khi đẩy, tạo bản đã bỏ các file nhạy cảm (index tạm, `git rm --cached` trên index tạm đó) rồi đẩy bản này.
  - Nếu commit 94780487 nằm trong lịch sử nhánh và bị đánh dấu "không đẩy lên" thì DỪNG, báo người dùng, chưa đẩy.

Sau khi đẩy:
1. Kiểm tra `git ls-remote origin "backup/*"` thấy đủ các nhánh.
2. Ghi giờ và hash vào LOG.

## PHẦN 3 — Chia theo từng việc (chỉ lập đề xuất, chưa làm)

Lập bảng gửi người dùng, gom mọi thay đổi ngoài main theo từng việc. Thay đổi gồm 39 commit cộng các file chưa commit trong commit chụp. Mỗi việc một dòng:

| Việc | Thư mục chính | Commit liên quan | File chưa commit | Build/kiểm tra gì | Nhánh đề xuất |
|---|---|---|---|---|---|
| ví dụ: Content agent, YouTube, Fanpage | … | 9fad62f9, … | … | … | `topic/content-agent_2026-11-19` |

Gợi ý các việc (Cursor xác định lại theo thư mục thật):
- Content agent, YouTube, Fanpage, giọng đọc, dựng phim
- Video engine
- Famixa M1
- Sàn gạch đỏ
- Nội dung Xuân Hòa
- Pharmacy (low-stock, workflow, sales, loyalty)
- Second Brain, nghiên cứu
- Tài liệu kiến trúc
- Novixa-site: trang chủ, video
- Deploy, migrations khác
- Snapshot production

Ghi rõ:
- file nào thuộc **nhiều việc cùng lúc** (dễ lẫn): liệt kê riêng, chờ người dùng quyết;
- file nào trông như rác hoặc file tạm: chỉ liệt kê, không xóa;
- việc nào build hỏng ở trạng thái hiện tại.

**DỪNG**, chờ người dùng duyệt bảng.

## PHẦN 4 — Tạo nhánh theo từng việc (sau khi người dùng duyệt)

Mỗi việc làm riêng:
1. Tạo worktree mới từ `origin/main`: `git worktree add E:\wt\<tên-việc> -b topic/<tên-việc>_<ngày>`.
2. Đưa đúng file của việc đó vào worktree, lấy từ commit chụp hoặc từ các commit liên quan (`git checkout <snap> -- <đường dẫn>` **trong worktree**, không phải cây chính).
3. Build hoặc kiểm tra theo bảng. Ghi kết quả.
4. Commit, message có giờ:
   `<loại>(<việc>): <mô tả> [lưu lúc 2026-11-19 23:xx +07]`
5. Ghi vào LOG: tên nhánh, hash, số file, build pass hay hỏng.

Sau khi làm hết:
- Đối chiếu tổng: mọi file trong commit chụp và trong 39 commit đều thuộc đúng 1 nhánh topic, hoặc nằm trong danh sách "không đẩy lên" hay "chờ quyết". Không file nào bị bỏ sót. In bảng đối chiếu.
- Chờ người dùng gõ "push" rồi đẩy các nhánh `topic/*`. **Không merge vào main.** Việc nào vào main thì người dùng quyết từng việc sau.

## PHẦN 5 — Khi nào được dọn cây làm việc trên máy

Chưa dọn trong task này. Chỉ báo người dùng điều kiện để dọn sau:
- mọi nhánh `topic/*` và `backup/*` đã có trên origin;
- bảng đối chiếu không còn file bỏ sót;
- người dùng xác nhận bằng chữ.

## Báo lại

- Giờ bắt đầu và giờ xong từng phần.
- Đường dẫn bundle và hash các nhánh `backup/*`.
- Bảng chia việc (Phần 3).
- Danh sách file nhạy cảm và file lớn không đẩy lên.
- Đường dẫn file LOG.
