# Daily Digital Mirror — Brief v1.0

**Mã ý tưởng:** KIT-PRD-FO-DDM-01 · **Pack:** FamilyOS / Famixa  
**Ngày:** 2026-08-05 · **Trạng thái:** M1 local (API + Agent PS1 + UI) — chưa MSI / chưa schedule 22:30 job  
**Phụ thuộc triết lý:** [famixa-product-design-manifesto-v2.md](./famixa-product-design-manifesto-v2.md)  
**Phụ thuộc enforcement:** [family-screen-boundary-v1.md](./family-screen-boundary-v1.md) (A+B — không tự firewall)

---

## 1. Ý tưởng một câu

Mỗi tối (~22:30), gia đình nhận **một tổng kết ấm** về thế giới số của con (app / web đã mở trong ngày).  
Bố mẹ gửi **một lời nhắc hoặc lời khen** dựa trên đó. Con **cũng thấy cùng bản mirror** — dù **không khóa máy**.

Không phải giám sát live. Là **gương soi cuối ngày** + **một khoảnh khắc đối thoại**.

---

## 2. Đối chiếu Manifesto V2 — có được làm không?

| Câu hỏi Manifesto §10 | Digital Mirror |
|----------------------|----------------|
| Giúp con tự giác hơn? | **Có** — nhìn thói quen → điều chỉnh, không chỉ sợ khóa |
| Giúp hiểu nhau hơn? | **Có** — mẹ nhắc bằng dữ liệu + giọng thương; con thấy mình được hiểu |
| Sau 10 năm muốn xem lại? | **Có** nếu lưu thành **ký ức / insight ngày** trong Nhật ký — không nếu chỉ là bảng phút app khô |

| Nguy cơ lệch Manifesto | Cách giữ thẳng |
|------------------------|----------------|
| Thành dashboard screen-time | Chỉ **1 card tối** + 1 CTA — không biểu đồ nhiều series |
| Tăng “mở app để soi con” | Push tối đa **1 lần/ngày**; mẹ xử lý trong 30 giây |
| Giám sát / xâm phạm | Con thấy **cùng số liệu**; không live feed; không nội dung chat/ảnh |
| Đo Screen Time thay Family Growth | KPI thành công = **lời khen / xung đột giảm / giữ lời hứa giờ ngủ** — không phải phút chặn |

**Kết luận alignment:** **ĐƯỢC LÀM** — khớp mục tiêu 1–5 của Manifesto **nếu** đóng gói như *khoảnh khắc buổi tối*, không như *control panel*.

---

## 3. So với những gì Famixa / FamilyOS đã có

| Đã có | Quan hệ với Mirror |
|-------|-------------------|
| **Day Flow / Nhiệm vụ / Team Play** | Hành vi *cam kết nhà*; Mirror bổ sung hành vi *thế giới số* ngoài checklist |
| **Health Score / Coach / Value panel** | Mirror là **tín hiệu đầu vào** mới cho Coach (“sau 21h Roblox nhiều”) — không thay Health Score |
| **Nhật ký** | Mirror tối → có thể **rơi thành 1 trang ký ức** (“Tối 05/08 — nhà nhìn lại ngày số”) |
| **Kho báu / sao / unlock** | Có thể gắn *badge tự giác số* sau nhiều ngày giữ lời — không biến Mirror thành shop |
| **Screen Boundary A+B** | Vẫn là lớp **thỏa thuận + soft-lock + deep-link OS**; Mirror **không thay** chặn cứng, **cũng không mâu thuẫn** |
| **Foxy / Fami messaging** | Kênh giao **1 câu** tối — đúng “ít nói đúng lúc” |
| **Parent Success / Growth Report** | Mirror nuôi câu chuyện “mẹ bớt xung đột giờ ngủ” — thuộc mặt **payer ROI cảm xúc** |

**Khoảng trống hiện tại:** chưa có agent thiết bị → chưa có nguồn app/web usage. Mọi UI mẹ hiện tại đều dựa Day Flow / glance / local value — **không biết** con mở gì trên PC.

---

## 4. Triển khai thế nào (khớp Manifesto)

### 4.1 Nguyên tắc ship

1. **Không khóa** ở tầng Mirror — enforcement vẫn A+B.  
2. **Minh bạch:** bật Agent = giải thích rõ cho con + bố mẹ.  
3. **Một khoảnh khắc/ngày** (~22:30 giờ nhà).  
4. AI viết **insight + gợi ý lời nói**, không dump log.  
5. Lưu bản rút gọn vào **Nhật ký** (10 năm còn đọc được).

### 4.2 Phụ thuộc thiết bị

| Nền | Agent | Phạm vi v1 |
|-----|-------|------------|
| **Windows** | **Bắt buộc** cài Famixa Agent (một lần, admin/PIN phụ huynh) | App foreground + thời lượng; web qua extension hoặc proxy nhẹ (P1) |
| **Android** | P2 | Usage stats |
| **iOS** | Không hứa usage | Chỉ thỏa thuận + Screen Time OS + checklist |

Heartbeat agent → nếu tắt, mẹ nhận *“Agent im lặng”* (không kết tội “con phá”).

### 4.3 Luồng sản phẩm

```
[Agent Windows] ghi usage trong ngày
        ↓
[~22:30 local] server/job tổng hợp top app/web + so với thỏa thuận số (nếu có)
        ↓
AI viết 1 dòng cho mẹ + 1 dòng cho con (+ 2–3 nút nhắc/khen sẵn)
        ↓
Push / inbox mẹ · card trên Nhật ký / Home (không mở Dashboard mới)
        ↓
Mẹ chọn: Khen | Nhắc nhẹ | Hẹn lại thỏa thuận
        ↓
Con thấy cùng mirror + lời mẹ → có thể đáp 1 câu → ghi nhật ký
```

### 4.4 UI (một câu hỏi / bề mặt)

| Ai | Câu hỏi màn hình | Nội dung tối đa |
|----|------------------|-----------------|
| Mẹ | “Tối nay mình nói gì với con?” | Top 3 app/web · 1 insight · 3 nút |
| Con | “Hôm nay mình đã sống với màn hình thế nào?” | Cùng top 3 · lời mẹ · 1 chỗ đáp |
| Nhật ký | “Ký ức tối nay” | Card đã rút gọn, không bảng raw |

**Cấm v1:** live “đang mở app X”, heatmap 24h, xếp hạng anh chị em, keyword nội dung trang.

### 4.5 Wave đề xuất

| Wave | Scope | Độ khó |
|------|--------|--------|
| **M0** | Spec + copy + empty state “Chưa có Agent” + checklist cài | ✅ Ship family-app — `DailyMirrorEmptyPanel` (home + nhật ký) |
| **M1** | Agent Windows: app duration + heartbeat + UI mẹ/con (ingest + GET day + notes) | ✅ Local — migration `273` · API `…/mirror/*` · `tools/famixa-mirror-agent` · parent/kid UI |
| **M2** | Web (extension) + so khớp Screen Agreement giờ ngủ + job/push ~22:30 | Trung bình |
| **M3** | Android / Blueprint học pattern “đêm số” cho Coach | Cao hơn |

### M1 — surface đã có (local)

| Layer | Path / endpoint |
|-------|-----------------|
| DB | `migrations/273_pack_family_digital_mirror.sql` |
| API | `POST …/mirror/heartbeat` · `POST …/mirror/usage` · `GET …/mirror/day` · `POST …/mirror/notes` |
| Agent | `tools/famixa-mirror-agent` · nút **Tải & cài Agent Windows** (`.cmd` 1 lần, điền sẵn token) · assets `/mirror-agent/` |
| Parent UI | Home (compact) + Nhật ký — top apps · Khen / Nhắc nhẹ / Hẹn lại |
| Kid UI | Hub — cùng top apps + lời bố mẹ (không checklist Agent) |

---

## 5. Metric thành công (Family Growth — không phải Screen Time app)

- Số tối mẹ gửi **khen** / **nhắc nhẹ** (tỷ lệ khen tăng theo tuần)  
- Giảm xung đột giờ ngủ (self-report hoặc ít “xin thêm giờ” ngoài thỏa thuận)  
- Agent uptime % (không phải mục tiêu vanity)  
- Con mở card mirror và **đáp lời** ≥ 1 lần/tuần  

**Không** tối ưu: thời gian mẹ ở lại màn báo cáo.

---

## 6. Quyết định sản phẩm

| Quyết định | Giá trị |
|------------|---------|
| Làm Mirror? | **Có** — sau / song song M0 với Screen Boundary A+B |
| Tự xây chặn cứng? | **Không** (giữ [family-screen-boundary-v1.md](./family-screen-boundary-v1.md)) |
| Bắt buộc Agent? | **Có** cho usage thật; không Agent thì chỉ soft moment từ Day Flow |
| Platform v1 | **Windows-first**; iOS nói thật giới hạn |

---

## 7. Một câu pitch nội bộ

> Mirror biến dữ liệu số thành **một lời nói tối nay** — để con tự giác hơn và mẹ bớt nóng, không để mẹ theo dõi nhiều hơn.
