# Family Screen Boundary — A+B Brief v1.0

**Mã:** KIT-PRD-FO-SB-01 · **Pack:** FamilyOS · **Version:** 1.0  
**Ngày:** 2026-07-23 · **Owner:** Product + Architecture  
**Phụ thuộc:** [family-os-pack-brief-v1.md](./family-os-pack-brief-v1.md) · [family-team-play-v1.md](./family-team-play-v1.md)

**Trạng thái:** Brief chốt · Prototype UI (local) · Không build firewall / chặn URL

---

## 1. Quyết định sản phẩm

Chốt **A + B** cùng lúc:

| Nhánh | Tên | Phạm vi |
|-------|-----|---------|
| **A** | PIN + Soft-lock trong FamilyOS | Chỉ trong family-app (chế độ con) |
| **B** | Screen Agreement | Deep-link + checklist để bố mẹ cấu hình **OS** (Screen Time / Family Link) |

**Không** làm: driver mạng, chặn web toàn máy, VPN filter, bắt buộc mật khẩu để Force Stop / gỡ app (cần native/MDM).

Tagline: **App giữ lời hứa trong nhà · OS giữ ranh giới trên máy.**

---

## 2. Thesis

Sau khi nhà **Áp dụng** thỏa thuận màn hình (`screen_*` / `entertain_no_youtube`):

1. **Trong app (A):** chế độ con bị soft-lock — đổi người / thoát kid mode cần **mã bố mẹ**.  
2. **Ngoài app (B):** FamilyOS không khóa máy giúp — hiện checklist + mở Screen Time / Family Link để bố mẹ giới hạn game / YouTube / thời lượng thật.

Khớp Accountability: chỉ thi hành thỏa thuận đã `accepted` / sự kiện đã `applied` — không giám sát thiết bị, không thay thế cha mẹ.

---

## 3. Nhánh A — PIN + Soft-lock

### Khi nào bật soft-lock

- Có `consequence_event` **hôm nay** với `status = applied` và `consequenceCode` thuộc nhóm màn hình:
  - `screen_no_game_today`
  - `screen_reduce_15` / `screen_reduce_30` / `screen_reduce_30_weekend`
  - `entertain_no_youtube`
- (Sau này) Team Unlock chưa mở + Agreement nhà chọn “khóa nhẹ trong app” — **chưa** trong prototype này.

### Hành vi kid mode khi soft-lock

| Hành động | Không soft-lock | Có soft-lock |
|-----------|-----------------|--------------|
| Làm Mission / ghi tiến độ | OK | OK (vẫn được hoàn thành) |
| Đổi người (giữ nút / mã bố mẹ) | PIN hoặc giữ | **Bắt buộc PIN** |
| Tab Cài đặt → Đổi người | Giữ để đổi | PIN trước |
| Đóng tab trình duyệt | Vẫn được (giới hạn web) | Vẫn được — nhắc B |

### PIN

- 4 số, lưu session parent (đã có `ParentPinSheet` + `verifyParentPin`).
- Đúng PIN → tạm mở soft-lock **phiên hiện tại** (đổi người / vào parent board) hoặc gỡ overlay cho đến khi load lại ngày / còn event `applied`.
- Prototype: PIN đúng → `softLockBypassed` trong session cho đến khi clear / hết ngày.

### Ranh giới trung thực (copy bắt buộc)

> FamilyOS khóa nhẹ **trong app**. Để giới hạn game/YouTube trên máy, bố mẹ bật Screen Time hoặc Family Link (checklist bên dưới).

---

## 4. Nhánh B — Screen Agreement

### Mục tiêu

Sau Áp dụng (hoặc từ Thỏa thuận nhà), bố mẹ có **một checklist cấu hình OS** — không tự firewall.

### Checklist chuẩn (prototype)

1. Mở Screen Time (iPhone) hoặc Family Link (Android)  
2. Đặt giới hạn app / thời lượng đúng thỏa thuận nhà (game, YouTube, …)  
3. Bật Ask to Buy / yêu cầu đồng ý nếu nhà đã thống nhất  
4. Copy hướng dẫn gửi ông bà / máy thứ hai (nếu cần)  
5. Đánh dấu “Đã cấu hình trên máy con” (local checklist — chưa API)

### Deep-link

- iOS: hỗ trợ Apple Screen Time (URL support hiện có)  
- Android: Google Family Link  

### Vị trí UI

- family-app **Parent Board**: sheet sau Áp dụng + card Screen Boundary thường trực khi còn soft-lock  
- Admin **Thỏa thuận**: panel “Ranh giới màn hình (A+B)” — checklist + link  

---

## 5. Luồng

```
Agreement / Consequence catalog (screen_*)
  → Event pending_confirm
  → Parent Áp dụng
  → A: soft-lock kid app (cần PIN để đổi người)
  → B: checklist + deep-link OS
  → Con vẫn làm Mission trong app
  → Hết hiệu lực / Parent gỡ (waive hoặc hết ngày) → tắt soft-lock
```

---

## 6. Lộ trình

| Phase | Phạm vi | Status |
|-------|---------|--------|
| **SB0** | Brief này | ✅ |
| **SB1** | A: soft-lock overlay + PIN để đổi người khi `applied` screen_* | Prototype |
| **SB2** | B: checklist + deep-link trên Parent Board + Admin Agreements | Prototype |
| **SB3** | Persist “đã cấu hình OS” + nhắc lại nếu chưa tick | Later |
| **SB4** | Native companion / MDM (nếu bao giờ) | Out of Starter |
| **SB5** | **Screen Time Wallet (thỏa thuận)** + Child Proposal xin phút — không đo máy; bố mẹ duyệt qua Decision Inbox | AFE sóng 1–2 — [adaptive-family-engine-v1.md](./adaptive-family-engine-v1.md) |

**Đường chính Screen Time:** không bắt bố mẹ cài Rule phút theo ngày. Con đề xuất (+ lý do) → AI tóm tắt → bố mẹ 👍/👎. Wallet = ngân sách tuần thỏa thuận trong nhà; vẫn dùng A+B để giữ ranh giới trên máy.

---

## 7. Acceptance — Prototype

- [x] Brief `family-screen-boundary-v1.md`
- [x] Kid: banner soft-lock khi có screen consequence `applied`
- [x] Kid: đổi người yêu cầu PIN khi soft-lock
- [x] Parent: sheet/checklist Screen Agreement + deep-link
- [x] Admin Agreements: panel Screen Boundary
- [ ] API dedicated soft-lock state — chưa (derive từ consequence events)
- [ ] Chặn Force Stop / chặn web máy — **không làm**

**Local:** family-app `:5178` · Admin `:5173` · PIN mặc định demo `1234`

---

## 8. Anti-patterns

- Nhận Soft-lock là “đã khóa máy con”  
- Tự tăng phạt / tự chặn URL không qua Agreement  
- PIN dễ lộ trên UI kid (không hiện mã)  
- Biến FamilyOS thành antivirus  

---

## 9. Liên kết

- SoftLockGuide hiện có: `FamilySoftLockGuides` (Pack FamilyOS)  
- UI: `ParentPinSheet` · `ParentBoardView` · `KidFocusView` · `FamilyOsAgreementsPage`  
- AFE / Wallet / Child Proposal: [adaptive-family-engine-v1.md](./adaptive-family-engine-v1.md)
