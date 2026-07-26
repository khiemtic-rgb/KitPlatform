# FamilyOS — Pack Brief v1.1 (Starter)

**Mã:** KIT-PRD-FO-01 · **Tier:** T1 · **Trạng thái:** F2 Agreement · **Version:** 1.1  
**Ngày:** 2026-07-22 · **Owner:** Product + Architecture  
**Phụ thuộc:** [platform-kernel-and-solution-packs-v1.md](./platform-kernel-and-solution-packs-v1.md)

---

## 1. Mục tiêu

**FamilyOS Starter** giúp gia đình hạt nhân có con **4–17 tuổi** cùng tổ chức, phối hợp và **thực thi những gì đã thống nhất** — không phải quản lý con bằng checklist.

Tagline: **One Family. One Plan. One Daily Flow.**  
Bổ sung (Team Play): **Not “I finished.” — “We finished.”** — xem [family-team-play-v1.md](./family-team-play-v1.md).  
Bổ sung (AFE): **Gia đình không học Famixa — Famixa học từng gia đình.** — xem [adaptive-family-engine-v1.md](./adaptive-family-engine-v1.md). Cha mẹ duyệt vài giây; con đề xuất có trách nhiệm; không cấu hình Rule theo ngày.

Vấn đề không phải “quên việc” (Todo/Calendar đã làm được). Vấn đề là **không có cơ chế thực thi cam kết chung của cả nhà**.

Chu trình giá trị:

```
Thống nhất (Agreement)
  → Làm (Commitment / Daily Flow)
  → Nhắc (Reminder)
  → Kiểm (Check / Progress)
  → Nhìn lại (Review)
  → Điều chỉnh (Routine / Agreement)
```

Accountability (trách nhiệm với cam kết) là **khác biệt dài hạn** so với Todo — nhưng Starter **không** ship engine phạt đầy đủ ngày 1.

---

## 2. Bảy capability (tầm nhìn sản phẩm)

| # | Capability | Vai trò | Starter |
|---|------------|---------|---------|
| 1 | **Routine** | Nhịp sống / mẫu ngày | ✅ |
| 2 | **Daily Timeline / Flow** | Một ngày chung của nhà | ✅ |
| 3 | **Commitment** | Cam kết (không gọi Task) | ✅ |
| 4 | **Reminder** | Nhắc theo ngữ cảnh / khung giờ | ✅ L1 (in-app + nhắc nhanh copy/share) |
| 5 | **Family Agreement** | Đồng thuận trước khi đổi luật nhà | ✅ **F2** (nền Accountability) |
| 6 | **Accountability Engine** | Reward + Consequence + Review | 🔜 Tầm nhìn; **Lite sau F2** |
| 7 | **Family Coach** | Quan sát → nhớ → đề xuất quyết định (không chat LLM) | ✅ C1+C2 Daily Insight + Memory 7 ngày; 🔜 C3 đồng hành cả ngày |
| 8 | **Family Team Play** | Điểm đội · Team Streak · Unlock · Sibling nudge · Cooperation Score | 🟡 Brief + prototype UI TP0–TP1 — [family-team-play-v1.md](./family-team-play-v1.md) |
| 9 | **Screen Boundary (A+B)** | PIN + soft-lock trong app · Screen Agreement deep-link OS (không firewall) | 🟡 Brief + prototype — [family-screen-boundary-v1.md](./family-screen-boundary-v1.md) |
| 10 | **Adaptive Family Engine (AFE)** | Setup Wizard · Decision Inbox · Child Proposal · Family Mode · Screen Wallet thỏa thuận · Adaptive đề xuất | 🟡 Epic — [adaptive-family-engine-v1.md](./adaptive-family-engine-v1.md) |

### 2.0 Family Coach (thesis)

FamilyOS **không** khen chung (“Giỏi quá!”). Coach **quan sát hành vi → nhớ pattern → một đề xuất cụ thể** (đổi neo Routine, hỗ trợ đúng việc). Không free-form LLM chat. AI hỗ trợ quyết định phụ huynh — không thay thế cha mẹ, không đánh giá con.

### 2.1 Accountability — định nghĩa & mức phản hồi (tầm nhìn)

Không gọi *Punishment* / *Control* / **phạt**. Gọi **Accountability** (cam kết trách nhiệm) — kết quả của thỏa thuận đã đồng ý.

| Level | Tên | Ý | Starter |
|-------|-----|---|---------|
| L1 | Gentle Reminder | Nhắc nhẹ, không hậu quả | ✅ (bảng nhà + nhắc nhanh) |
| L2 | Reflection | Hỏi vì sao chưa làm (lý do có cấu trúc) | ✅ F2.5 |
| L3 | Family Consequence | Hậu quả **đã thống nhất trong Agreement** — không do AI bịa | ✅ F2.5 (parent confirm) |
| L4 | Family Review | Báo cáo tuần / đề xuất chỉnh Routine | 🟡 Lite: streak + ngày đẹp |

**Reward** đi cùng Consequence (streak / “ngày đẹp” đơn giản trước; badge/unlock sau).

### 2.1b Family Agreement taxonomy (A0)

Agreement lưu **điều gia đình đã thống nhất** — không phải kho luật. Chu trình: Đồng thuận → Cam kết → Thực hiện → Nhìn lại → Điều chỉnh.

| Category | Ý | Starter |
|----------|---|---------|
| `foundation` | Văn hóa nền (tôn trọng, lắng nghe, cùng thực hiện, được đề xuất đổi) | ✅ seed F001–F004 |
| `routine` | Thỏa thuận nhịp sống (vd giờ ngủ) | 🟡 schema |
| `commitment` | Neo cam kết hằng ngày | 🟡 schema |
| `reward` | Quyền lợi khi hoàn thành | ✅ Wizard |
| `accountability` | Thỏa thuận khi chưa hoàn thành (ngôn ngữ tích cực, không “phạt”) | ✅ Wizard + L3 |
| `grace` | Gia hạn có đồng ý | 🟡 schema (chưa engine) |
| `exception` | Ngoại lệ (ốm, sinh nhật, du lịch) | 🟡 schema (chưa engine) |
| `change` | Đổi Routine/Commitment có hiệu lực ngày | 🟡 schema |
| `value` | Giá trị nhà (tự giác, trách nhiệm…) | 🟡 schema |

**Cấu trúc chuẩn:** title · category · purpose · who · effective · conditions · exceptions · result · review · status.

**Agreement Wizard (7 bước):** chủ đề → mục tiêu/value → khi nào → thưởng → accountability (catalog an toàn) → ngoại lệ → ai đồng ý + review. Không bắt cha mẹ “nghĩ luật từ đầu”.

### 2.2 Ranh giới Accountability (bắt buộc)

FamilyOS **không** hỗ trợ / không gợi ý:

- Cấm ăn cơm · đánh đập · xúc phạm · phạt tiền · hình thức gây hại thể chất/tinh thần  

**Consequence Library** (catalog giáo dục, gia đình chọn / tùy chỉnh trong khung an toàn), ví dụ nhóm:

- Screen · Entertainment · Responsibility · Learning · Community  

AI/engine **chỉ thi hành thỏa thuận đã `accepted`** — không tự tăng phạt. Ưu tiên tìm nguyên nhân (Routine chưa hợp) hơn là siết hậu quả.

---

## 3. Ranh giới Phase 1 (chung)

FamilyOS **không** làm:

- Tài chính · sức khỏe · thuốc · khám bệnh  
- GPS liên tục · trường học LMS · smart home  
- Giám sát / kiểm soát trẻ · thay thế cha mẹ  
- AI “hiểu môi trường” (TV / xe / họp) — để sau PPF  
- **Deploy production** cho đến khi Product **chốt** (local-only cho đến lúc đó)

**Không** mở rộng `CustomerFamilyService` / `public.family_members` thành FamilyOS graph.

---

## 4. Định vị trong hệ sinh thái

```
KitPlatform kernel (IAM, org, events, notify, audit, AI hooks)
├── Pack:Pharmacy     → Novixa Pharmacy (+ family_members care wallet — riêng)
├── Pack:Connect      → Novixa Connect
├── Pack:Clinic       → Clinic
├── Pack:Survey       → KAP
└── Pack:FamilyOS     → FamilyOS Starter   ← brief này
```

| Item | Value |
|------|--------|
| API prefix | `/api/family-os/*` |
| Schema | `pack_family.*` |
| Tenant package | `family_os` |
| Module gate | `family_os` |
| Vertical | `family` (cũng cho phép `hybrid`) |
| Pilot host (planned) | `family.kittech.vn` — **chưa chốt deploy** |

---

## 5. Mô hình dữ liệu

```
family 1──* membership
family 1──* routine 1──* commitment_template
family 1──* day_flow 1──* commitment   (Progress)
family 1──* agreement                  (F2 — nền Accountability)
```

Luồng ngày (F1):

1. Chọn routine theo weekday  
2. `POST .../day-flows/ensure` materialize commitments  
3. `PATCH .../commitments/{id}` → Progress  

Luồng Agreement (F2):

1. Đề xuất (`proposed`) — đổi routine / cam kết / **luật accountability**  
2. Thảo luận (`discussing`)  
3. `accepted` | `rejected` | `withdrawn`  
4. Chỉ agreement **`accepted`** mới được Accountability Lite (sau) đọc để thi hành  

---

## 6. Phase

| Phase | Phạm vi | Status |
|-------|---------|--------|
| **F0** | Pack + module + overview | ✅ |
| **F1** | Graph · Routine · Day Flow · Progress | ✅ |
| **F2** | Family Agreement workflow (nền Accountability) | ✅ |
| **F2.5** | Accountability Lite (L2 reflection + L3 từ Agreement + library) | ✅ |
| **F3** | Context reminder rule-based | ✅ |
| **F4** | Consumer channel + `family.kittech.vn` | Planned — **chưa chốt** |
| **Later** | AI Context · L4 Review · Caregiver/Senior → Novixa Care | Out of Starter core |

---

## 7. Acceptance

### Đã có (F0–F1–F3 + mobile)

- [x] Pack `src/Packs/FamilyOS/` + `AddFamilyOsPack`  
- [x] Migration `192` (+ `192a` owner optional)  
- [x] APIs families / routines / day-flows / progress  
- [x] Admin FamilyOS + sửa routine/template  
- [x] Mobile: kid focus · parent board · soft lock · nhắc nhanh (local)  
- [x] F3 `reminderState` theo timezone nhà  

### F2 Agreement

- [x] Brief v1.1 — 7 capabilities + ranh giới Accountability  
- [x] Migration mở rộng `agreement` (target accountability / terms)  
- [x] API list / create / decide agreement  
- [x] Admin UI **Thỏa thuận**  
- [x] Seed demo Agreement trên `DEMO_FAMILY`  
- [x] Admin **Thành viên** (add / edit / archive)  
- [x] Catalog thưởng/hậu quả theo nhà (`accountability_option` + cấu hình Admin)  
- [ ] Deploy production — **chưa chốt**  

### F2.5 Accountability Lite

- [x] L2 Reflection — `skipped` + `skipReason` (forgot/busy/need_help/not_ready/sick/other)  
- [x] Admin Daily Flow: **Chưa làm** + lý do  
- [x] Mobile Parent Board + Kid Focus: ghi lý do  
- [x] L3 — `consequence_event` từ Agreement `accepted` (gợi ý, parent **Áp dụng / Bỏ qua**)  
- [x] Admin + mobile: panel **Hậu quả hôm nay**  
- [x] L4 lite — streak + **ngày đẹp** (`accountability-glance`, tuần ISO)  
- [x] Late-done (sau `window_end`, family TZ) **không** tính ngày đẹp / streak; tag `isLateDone`  
- [x] Local browser reminder theo khung giờ (`due_now` / `overdue`) trên family-app  
- [x] **Push phụ huynh** Web Push khi `due_now` / `overdue` (`196` + worker; đăng ký trên board)  
- [x] Soft-lock guide sau Áp dụng hậu quả `screen_*` (Screen Time / Family Link deep-link)  
- [x] Parent digest tối (push 1 lần + card/Zalo trên board từ 20:00 TZ nhà)  
- [x] Admin **Tổng quan** → Family Dashboard (health / hôm nay / thành viên / gợi ý; scope → Developer collapse)  
- [x] Daily Flow **DF-1+DF-2** (Admin + family-app): header hôm nay, progress có ý nghĩa, status VI, commitment cards theo thành viên, guardian CTA trên thẻ  
- [x] **R0** Routine Operating Model lite (`197`): `priority` / `expected_duration_minutes` / `context_anchor` / `depends_on_template_ids` trên template + snapshot commitment; copy khi generate Day Flow; seed chuỗi sáng demo  
- [x] **R1** Admin Routine editor: sửa ưu tiên, thời lượng, “Sau việc…”, neo ngữ cảnh (khung giờ vẫn là gợi ý)  
- [x] **Family Coach C1** — Daily Insight có cấu trúc (`GET …/coach-insight`): headline / điểm mạnh / cần chú ý / đề xuất; Admin Dashboard + Day Flow banner; **cấm** khen chung  
- [x] **Family Coach C2** — Memory lite 7 ngày: pattern `forgot` cùng template ≥3 → đề xuất đổi neo (sau ăn tối / sau học); không persist profile  
- [ ] **Family Coach C3** — đồng hành cả ngày (context moments / push theo giờ) — chưa ship  
- [x] **Agreement A0** — taxonomy 9 nhóm + thesis đồng thuận (không phạt / không kho luật)  
- [x] **Agreement A1** — migration `198`: category codes + purpose/effective/review/applies_to + terms schema v2; Foundation seed F001–F004  
- [x] **Agreement A2** — Admin Agreement Wizard 7 bước (Accountability/Reward); list theo category  
- [ ] Grace / Exception / Change **engine** — chưa ship (chỉ lưu cấu trúc)  

**Local login:** `DEMO_FAMILY` / `admin` / `Admin@123`  
**Không** thêm migration FamilyOS vào `migration-files.prod.txt` cho đến khi chốt deploy off-hours.  
**Email digest:** chưa gửi (notify email stub) — dùng push + Zalo share.  
**R2 (chưa làm):** AI scheduler / auto-shift window từ giờ dậy.  
**Non-goal:** free-form LLM chat / “Giỏi quá {tên}!” / chữ **phạt** / bắt cha mẹ tự nghĩ luật trống.

---

## 8. Tiêu chí thành công (pilot)

Sau 30–60 ngày với ~20 gia đình:

- Cha mẹ nhắc tay ít hơn  
- Con chủ động hoàn thành nhiều hơn  
- Cả nhà biết kế hoạch trong ngày  
- Giảm tranh cãi việc lặp lại  
- (F2+) Có ít nhất vài thỏa thuận được `accepted` và được nhắc lại khi đổi luật nhà  

→ Product–Problem Fit cho Starter — **trước** khi mở rộng Accountability Engine / AI.
