# Việc 15: Sửa giao diện phễu (Health Check + trang làm bài + kết quả sơ bộ)

File riêng, không mở hay sửa các file `CURSOR_*.md` khác. Ghi Nhật ký ở cuối file này. Nếu editor báo file đã đổi trên đĩa thì tải lại trước khi ghi.

**Phạm vi:** chỉ giao diện và câu chữ. Không đổi backend, bộ câu hỏi, migration hay luật báo cáo.

Số đo do Claude lấy trên production ngày 19/11:

- Điện thoại 375×812: nút đầu tiên của trang Health Check nằm ở 829px, tiêu đề dài 5 dòng, trang cao 5.347px.
- Trang kết quả sơ bộ trên điện thoại: hai nhận xét nằm ở 1.658px, nút mở báo cáo ở 2.309px.

## A. `novixa-site/src/pages/vi/health-check.astro` (và CSS liên quan)

1. **Tiêu đề.** Thẻ `h1` chỉ còn: "Nhà thuốc vẫn bán tốt — nhưng có đang để lọt tiền?". Câu "Doanh thu tốt không có nghĩa nhà thuốc đang vận hành tốt." chuyển thành dòng phụ, dùng thẻ `p`, cỡ chữ thân bài, ngay dưới `h1`.
2. **Nút đầu tiên phải thấy ngay.** Đưa nút "Nhận Health Check miễn phí ngay" lên ngay dưới đoạn mô tả, trước danh sách 5 lợi ích. Đặt 4 ô nhỏ (31 câu hỏi / khoảng 7–10 phút / Kết quả ngay / Không đăng ký) ngay dưới nút.
   - Đoạn mô tả bỏ cụm "31 câu, khoảng 7–10 phút," ở đầu, vì 4 ô nhỏ đã có.
   - Mô tả còn lại: "Miễn phí, không cần đăng ký. Xem kết quả sơ bộ ngay, để lại số điện thoại để xem báo cáo đầy đủ. Nếu đồng ý thì có cuộc gọi 10–15 phút."
3. **Thanh nút cố định trên điện thoại.**
   - Chỉ hiện khi màn hình rộng ≤ 768px, và khi nút đầu tiên đã cuộn khỏi màn hình. Dùng `IntersectionObserver`.
   - Thanh nằm ở đáy màn hình, chứa nút "Làm Health Check miễn phí", link giống nút chính.
   - Không che nội dung cuối trang: thêm `padding-bottom` cho trang bằng chiều cao thanh.
   - Ẩn thanh khi nút ở cuối trang đang hiển thị.
4. **Bỏ câu chưa có căn cứ.**
   - Xóa dòng "Đã có hơn 100+ nhà thuốc quan tâm trong tháng này".
   - Đổi "Biết chính xác điểm mạnh, điểm yếu trong vận hành." thành "Thấy rõ điểm mạnh, điểm yếu trong vận hành."
5. **Chuyển tiếp mã theo dõi.** Nếu URL trang có `utm_source`, `utm_medium`, `utm_campaign`, `utm_content` hoặc `ref`, thì mọi link tới `survey.novixa.vn` (nút chính, nút cuối trang, thanh cố định) mang theo đúng các tham số đó. Làm bằng một đoạn script nhỏ chạy lúc tải trang. Chỉ nhận chữ, số, `_` và `-`, tối đa 100 ký tự mỗi giá trị.

**Kiểm tra** (`npm run build`, mở `dist/vi/health-check/index.html`, dùng DevTools giả lập 375×812):

- Nút đầu tiên có `getBoundingClientRect().bottom < 812` khi chưa cuộn.
- `h1` không quá 3 dòng ở 375px.
- Thanh cố định hiện khi đã cuộn qua nút đầu, ẩn khi tới cuối trang.
- Không tràn ngang: `scrollWidth == clientWidth`.
- Mở trang có `?utm_source=test&utm_content=can_han`: link nút trỏ tới `https://survey.novixa.vn/start?utm_source=test&utm_content=can_han`.
- Ở 1366×768: nút đầu nằm trong 600px đầu.
- Trang chủ không có nút YouTube hay video mới. Điều này đúng khi build trong worktree, xem mục D.

## B. `client/assessment-web`: trang làm bài

1. **Nhóm "Bắt đầu" (PROFILE).** Không hiện nhãn "Không tính điểm · Hỗ trợ tư vấn". Không hiện dòng hướng dẫn mặc định "Chọn phương án mô tả đúng nhất với cách nhà thuốc đang vận hành." Nếu câu hỏi có `helpText` riêng thì vẫn hiện `helpText`.
2. **Bộ đếm câu.** Tổng số câu trên bộ đếm và thanh tiến độ tính như thể câu P3 đã được trả lời, tức luôn cộng một câu rẽ nhánh. Bài V2 luôn hiện "/31" ngay từ câu 1.
   - Viết sao cho đúng với mọi bộ câu hỏi: nếu có câu rẽ nhánh mà câu điều kiện chưa trả lời, cộng 1 cho mỗi câu điều kiện.
   - Bài V1 không có rẽ nhánh nên không đổi.

## C. `client/assessment-web`: trang kết quả sơ bộ (`ResultsPage.tsx`)

1. **Thứ tự mới:**
   1. Điểm tổng.
   2. **Hai nhận xét sơ bộ.** Thẻ hiện có, chuyển lên đây.
   3. **Nút chính "Xem ba việc nên làm trước",** dẫn tới trang để lại thông tin, kèm một dòng nhỏ: "Để lại số điện thoại để xem báo cáo đầy đủ và lộ trình 30–60–90 ngày."
   4. Thang điểm.
   5. 6 thẻ nhóm.
   6. Khối "Nhận báo cáo chi tiết" cuối trang, giữ như cũ.
2. **Không có nhận xét nào** (danh sách sơ bộ rỗng): bỏ khối nhận xét, giữ nút chính ở đúng vị trí đó.
3. **Nút ở góc trên.** Khi báo cáo còn khóa (`reportLocked`), đổi chữ "Tải báo cáo PDF" thành "Mở báo cáo đầy đủ", dòng phụ "Để lại số điện thoại". Khi báo cáo đã mở thì giữ "Tải báo cáo PDF".

**Kiểm tra** trên dev, ở 375×812, với bài chạy bằng `run_qa.py` vai 3:

- Nhận xét thứ nhất nằm trong 900px đầu.
- Nút chính nằm trong 1.200px đầu.
- Không tràn ngang.
- `tsc` và `npm run build` đều qua.
- Thêm một bài với đáp án toàn mức 4 (tự tạo trong `run_qa.py`, vai 6): trang không có khối nhận xét rỗng.

## D. Deploy

**1. assessment-web (VPS):**

1. Build Release `client/assessment-web` từ cây chính. Thư mục này chỉ có thay đổi KAP; kiểm tra lại bằng `git status client/assessment-web`. Có file ngoài KAP thì dừng và báo.
2. Trên VPS, nén `/var/www/kit-platform/assessment-web/` thành `/var/www/kit-platform/assessment-web-backup-<giờ>.tgz`.
3. Rsync `dist` lên.
4. Không đụng API, DLL hay database.
5. Kiểm tra trên production ở 375×812: trang làm bài hiện "/31" ngay câu 1, và trang kết quả của bài `c321edfc-5ff5-4adf-9b0a-0bc45c28eb9c` đúng thứ tự mới.

**2. novixa-site (git):**

1. Làm như Việc 14: tạo worktree mới từ `origin/main` mới nhất, nhánh `health-check-v3`.
2. Chỉ đưa vào phần sửa của mục A: `health-check.astro`, CSS đi kèm, script chuyển tiếp mã theo dõi. File có lẫn thay đổi khác (nút YouTube, video trang chủ) thì chỉ lấy phần của mục A.
3. Build, kiểm tra như mục A, commit.
4. **Dừng chờ người phụ trách trả lời "push"** trong chat. Ghi diff và lệnh push vào Nhật ký trước khi dừng.
5. Khi được duyệt: push không ép, chờ Cloudflare build xong, kiểm tra lại các điểm ở mục A trên `https://novixa.vn/vi/health-check/`, rồi ghi **"ĐÃ ĐĂNG"**.

**Quay lại nếu lỗi:**

- assessment-web: giải nén file backup đè lại.
- novixa-site: `git revert` commit đó rồi push. Cách này cũng cần người phụ trách duyệt.

## Nhật ký

| Phần | Trạng thái | Ghi chú |
|---|---|---|
| A | | |
| B | | |
| C | | |
| D1 assessment-web | | |
| D2 novixa-site | | |
