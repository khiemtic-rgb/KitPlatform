# Famixa Product Design Manifesto V2.0

**Family Operating System — Design Philosophy**  
**Ngày:** 2026-08-05 · **Đối tượng:** AI agent · Engineer · UI/UX · Product  
**Kim chỉ nam:** đọc xong trước khi thiết kế bất kỳ màn hình / tính năng / copy nào.

---

## Tuyên ngôn sản phẩm (Product Vision)

> **Famixa không được tạo ra để khiến gia đình dành nhiều thời gian cho điện thoại. Famixa được tạo ra để giúp gia đình dành nhiều thời gian hơn cho nhau.**

Câu này đặt ở đầu mọi tài liệu thiết kế, mọi phiên họp sản phẩm và mọi quyết định phát triển.  
Mỗi tính năng phải **kéo người dùng gần nhau hơn**, không kéo họ **ở lại trong ứng dụng lâu hơn**.

---

## 1. Mục tiêu của Famixa

Famixa **KHÔNG phải** là:

- Ứng dụng quản lý công việc  
- Ứng dụng checklist  
- Ứng dụng nhắc việc  
- Chatbot AI  
- Dashboard gia đình  

Famixa **là**:

**Family Operating System™** — hệ điều hành giúp gia đình sống hạnh phúc hơn mỗi ngày thông qua AI.

Mục tiêu cuối cùng **không** phải làm người dùng mở app nhiều hơn, mà giúp:

1. Con tự giác hơn  
2. Bố mẹ ít phải nhắc hơn  
3. Các thành viên hiểu nhau hơn  
4. Gia đình có nhiều khoảnh khắc đẹp hơn  
5. Lưu giữ hành trình trưởng thành của cả gia đình  

**Nếu một tính năng không giúp đạt một trong các mục tiêu trên thì không nên phát triển.**

---

## 2. KPI thật sự của Famixa

**Không phải:** Daily Active User · Session Time · Screen Time trong app  

**Mà là (Family Growth):**

- Gia đình giảm số lần phải nhắc nhau  
- Con tăng tính tự giác  
- Số lời khen giữa các thành viên tăng lên  
- Số khoảnh khắc tích cực tăng lên  
- Gia đình dành nhiều thời gian chất lượng cùng nhau hơn  

Famixa đo **Family Growth**, không đo **Screen Time** (trong app).

---

## 3. Triết lý UI/UX

Gia đình rất bận.

- Cha mẹ thường chỉ có **30–60 giây**  
- Con chỉ có **1–3 phút**  

Vì vậy:

- Không được tạo cảm giác đang dùng phần mềm quản trị  
- Mỗi màn hình phải trả lời **đúng một câu hỏi**  
- Không nhồi Dashboard · không bắt đọc nhiều · không menu phức tạp  
- UI: nhẹ · ấm · thân thiện · có cảm xúc  

---

## 4. Triết lý AI

AI **không phải** ChatGPT. AI không tồn tại để trả lời câu hỏi.

AI tồn tại để: quan sát · học · hiểu · đồng hành · kết nối · gợi ý đúng lúc · tạo khoảnh khắc đẹp.

**AI càng ít nói nhưng đúng lúc càng tốt.**

---

## 5. Family Growth Blueprint™

Blueprint **không phải một màn hình**. Đây là bộ não của hệ thống.

Blueprint liên tục học: thói quen · hành vi · cảm xúc · phản ứng · mối quan hệ · tiến bộ — rồi phục vụ mọi bề mặt.

Người dùng **không cần** mở màn “xem Blueprint”. Nó âm thầm làm mọi thứ thông minh hơn.

Chi tiết kỹ thuật / gap: xem [famixa-blueprint-domain-gap-matrix-v1.md](./famixa-blueprint-domain-gap-matrix-v1.md), [famixa-dna-blueprint-roadmap-v1.md](./famixa-dna-blueprint-roadmap-v1.md).

---

## 6. Triết lý thiết kế các màn hình

| Bề mặt | Chỉ trả lời | Không được thành |
|--------|-------------|------------------|
| **Trang chủ** | Hôm nay gia đình mình thế nào? | Dashboard / báo cáo / nhiều card |
| **Kế hoạch / Nhiệm vụ** | Việc tiếp theo là gì? | Danh sách dài gây áp lực |
| **Nhật ký** | Ký ức của gia đình | Log / timeline công việc thuần |
| **Kho báu** | Thế giới động lực | Cửa hàng đổi thưởng khô |
| **Gia đình / Giá trị** | Family Growth Center | Report bảng biểu vô cảm |
| **Fami** | Một người bạn | Chat LLM generic |

Trang chủ chỉ nên hiện: AI chào · điều quan trọng nhất hôm nay · một hành động · một lời động viên.

---

## 7. Cảm xúc quan trọng hơn dữ liệu

Một lời khen > 10 biểu đồ.  
Một cái ôm > 100 KPI.  
Một bữa cơm > 1 Dashboard.

Famixa phải **tạo cảm xúc**, không chỉ báo cáo.

---

## 8. Ít thao tác hơn

Ưu tiên: **AI tự làm** — Replay tự tạo · Letter tự viết · Insight tự hiện · Timeline tự tổng hợp.  
Người dùng không phải thao tác nhiều.

---

## 9. Mỗi ngày chỉ cần tạo 1 khoảnh khắc

Không cần 20 thông báo. Chỉ cần **một** khoảnh khắc đúng lúc, ví dụ:

- *“Mẹ, con vừa hoàn thành 3 việc liên tiếp — con đang rất cần một lời khen.”*  
- *“Bố, hôm nay đã 4 ngày rồi hai bố con chưa chơi cùng nhau.”*

---

## 10. Quy tắc phát triển tính năng

Trước khi thêm bất kỳ tính năng nào, trả lời **3 câu**:

1. Nó có giúp **con tự giác hơn** không? → Không thì không làm.  
2. Nó có giúp các thành viên **hiểu nhau hơn** không? → Không thì không làm.  
3. Sau **10 năm**, gia đình có muốn xem lại không? → Không thì không đầu tư nhiều.

---

## 11. Triết lý cuối cùng

Famixa không cạnh tranh bằng số lượng tính năng.

Famixa cạnh tranh bằng **những thay đổi nhỏ mỗi ngày**:

- Con tự giác thêm một chút  
- Mẹ bớt nhắc một lần  
- Bố khen con thêm một câu  
- Cả nhà ăn cùng nhau thêm một bữa  
- Có thêm một ký ức đẹp  

Sau nhiều năm, những thay đổi nhỏ ấy tạo nên một gia đình hạnh phúc hơn.

---

## Liên kết nội bộ

- Pack brief: [family-os-pack-brief-v1.md](./family-os-pack-brief-v1.md)  
- Screen Boundary (không firewall): [family-screen-boundary-v1.md](./family-screen-boundary-v1.md)  
- Team Play: [family-team-play-v1.md](./family-team-play-v1.md)  
- Parent Success / Growth Report: [parent-success-engine-v1.md](./parent-success-engine-v1.md)  
- Daily Digital Mirror (định hướng mirror buổi tối): [daily-digital-mirror-v1.md](./daily-digital-mirror-v1.md)  
