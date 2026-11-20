# NOVIXA — Cảnh 4 — Style proof

Trạng thái: **STYLE PROOF — CHỜ XEM**. Đây là một cảnh mẫu để đánh giá ngôn ngữ chuyển động. Chưa đủ căn cứ để dựng các cảnh còn lại hay hai bản 9:16 và 4:5 của video 53 giây.

Các khung dưới đây là khung nguồn đã ghi vào file. Chưa xem liền một mạch 12 giây và chưa nghe lại giọng.

## 1. File và thời lượng

| | |
| --- | --- |
| File | `proof/novixa-intro-style-proof-001/final/scene4-motion-proof.mp4` |
| Khung hình | 1080×1920, 30 fps, H.264 + AAC |
| Thời lượng | 12.000 giây |
| Dung lượng | 810.244 byte |
| SHA-256 | `062132E067101CE94EFA2DBDAA2CD6AC70744CEC9AC55584551D3A6DD5476EE5` |

12 giây là độ dài của cảnh mẫu, không phải slot 11 giây của timeline đã khóa. Giọng là file đã duyệt `proof/novixa-intro-v1/voice/s4.mp3`, giữ nguyên câu và cách đọc, nối thêm im lặng cho đủ 12 giây. Không nhạc.

Bốn file V1 và V1r giữ nguyên dung lượng: 2.134.593, 1.909.800, 2.082.058, 1.809.251 byte.

## 2. Cách chuyển động được thực hiện

Một mặt phẳng duy nhất, vẽ lại từng khung bằng Pillow rồi đưa sang FFmpeg. Các vật thể giữ nguyên và đổi vị trí. Không cắt sang một ảnh khác.

| Thời điểm | Việc xảy ra trên cùng một bố cục |
| --- | --- |
| 0,1–1,6 giây | Chữ NOVIXA phóng từ một điểm ở giữa rồi đi lên vị trí tiêu đề. |
| 1,4–2,6 giây | Màn bán hàng lớn dần ngay dưới chữ đó. |
| 2,6–4,7 giây | Màn bán hàng đứng yên để đọc. |
| 4,7–6,4 giây | Màn bán hàng thu về ô bên trái. Bảng tồn kho nằm sẵn bên phải và hiện ra khi màn bán hàng nhường chỗ, đồng thời trượt nốt một đoạn ngắn vào ô của nó. |
| 6,2–7,4 giây | Thẻ INV-02 đi xuống từ mép dưới của bảng tồn kho. |
| 7,2–8,2 giây | Thẻ INV-01 đi xuống từ chính mép đó, vào khoảng giữa bảng tồn kho và INV-02. |
| 8,3–8,8 giây | Một mặt bảng trắng hiện dưới cả nhóm. |
| 8,8–12,0 giây | Bố cục đứng yên. |

“Bán hàng” rơi vào lúc màn bán hàng đang đứng yên. “Tồn kho” rơi vào lúc bảng tồn kho đang hiện ra. “Theo dõi” rơi vào lúc thẻ báo cáo bắt đầu rời khỏi bảng tồn kho.

## 3. Tài sản UI đã dùng

- Bán hàng: `docs/novixa/launch/DOC-008/assets/screenshots/10-staff-pos.png`, cả khung.
- Tồn kho: `04-inventory-stock.png`, crop `(540, 580, 1500, 1320)`.
- Báo cáo: `08-reports-home.png`, hai crop `(548, 888, 1292, 1088)` và `(1296, 888, 2048, 1088)`. Cạnh trên thấp hơn crop gốc trong hồ sơ khóa để bỏ mép thẻ phía trên và bỏ mục “Bán hàng”. Hai thẻ còn INV-01 “Tồn kho & giá trị” và INV-02 “Sắp hết hạn sử dụng”, kèm câu mô tả.

Mỗi ảnh UI mang nhãn “Dữ liệu minh họa” trong suốt thời gian ảnh đó có mặt. Không dùng dashboard doanh thu, không dùng POS quản trị, không sửa logo, không sửa ảnh nguồn.

## 4. Chuyển động thực và ảnh tĩnh

Chuyển động thực là sự đổi vị trí và kích thước của từng vật thể qua 360 khung: chữ đi lên, màn bán hàng lớn lên rồi thu về ô trái, bảng tồn kho hiện ra từ sau màn bán hàng, hai thẻ báo cáo đi ra từ mép dưới của bảng tồn kho.

Phần bên trong vẫn là ảnh chụp giao diện. Không có thao tác bấm, không có dòng hàng mới, không có số liệu mới. 0 đ và giỏ trống là nội dung sẵn có của ảnh POS.

## 5. Giới hạn

- Từ khoảng 2,6 đến 4,7 giây, màn bán hàng là một ảnh đứng yên. Đó là khoảng để đọc, và nó vẫn là một ảnh tĩnh.
- Khoảng 5,5 giây, màn bán hàng còn che cột mã sản phẩm. Cột này chỉ đọc đủ ở khoảng 6,7 giây.
- Góc dưới bên trái của mặt bảng cuối còn trống, vì điện thoại thấp hơn cột tồn kho và hai thẻ báo cáo.
- Chưa có bản 4:5. Chưa dựng năm cảnh còn lại. Chưa xem liền mạch và chưa nghe.

## 6. Khung hình

Trong `proof/novixa-intro-style-proof-001/qa/`:

- `frame-0.7.png` — điểm tập trung.
- `frame-3.6.png` — màn bán hàng đang là tâm.
- `frame-5.5.png` — màn bán hàng đang nhường chỗ cho tồn kho.
- `frame-6.7.png` — bán hàng và tồn kho đã tách, thẻ INV-02 đang xuống.
- `frame-7.7.png` — thẻ INV-01 đang ra từ bảng tồn kho.
- `frame-8.4.png` — bố cục đã vào chỗ.
- `frame-11.2.png` — mặt bảng cuối, sẵn để nối cảnh sau.

Quyết định dựng tiếp chỉ có sau khi xem file này. Nếu nhịp giữa vẫn đọc như một chuỗi ảnh, dừng ở cảnh mẫu.
