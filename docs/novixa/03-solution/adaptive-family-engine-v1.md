# Adaptive Family Engine (AFE) — Epic Brief v1.0

**Mã:** KIT-PRD-FO-AFE-01 · **Pack:** FamilyOS · **Version:** 1.0  
**Ngày:** 2026-07-26 · **Owner:** Product + Architecture  
**Phụ thuộc:** [family-os-pack-brief-v1.md](./family-os-pack-brief-v1.md) · [family-screen-boundary-v1.md](./family-screen-boundary-v1.md)

**Trạng thái:** Epic chốt · Ship theo 3 sóng

---

## 1. Thesis

> Gia đình không phải học cách sử dụng Famixa. Famixa phải học cách vận hành theo từng gia đình.

Đổi từ **Configuration Driven / Parent Driven** sang **AI Assisted Decision / Child Proposal**:

| Không | Có |
|-------|-----|
| Bố mẹ cấu hình Rule theo ngày | Con đề xuất · AI tổng hợp · bố mẹ duyệt |
| Cài phút cố định mỗi ngày | Ngân sách tuần (Wallet thỏa thuận) |
| Settings phức tạp lần đầu | AI Setup Wizard vài câu hỏi |
| AI tự sửa ngầm | AI đề xuất → Inbox 👍/👎 |

**Nguyên tắc UX:** AI chuẩn bị ~95% · cha mẹ quyết định ~5% · mỗi quyết định 1–2 chạm.

---

## 2. Năm module

| Module | Vai trò |
|--------|---------|
| **AI Setup Wizard** | Setup 3–5 phút; sinh Routine (+ đề xuất Reward / Challenge / Wallet tuần đầu) |
| **Adaptive Configuration** | Đề xuất chỉnh Routine / Screen budget theo period + hành vi — **không tự apply** |
| **Smart Proposal Engine** | Con xin (vd +30’) có lý do; AI tóm tắt + khuyến nghị |
| **Decision Inbox** | Gom mọi việc cần bố mẹ: sao, hậu quả, unlock, đổi quà, child request, AI proposal |
| **Family Mode Engine** | 1 chạm: Bình thường / Nghỉ hè / Thi / Du lịch / Cuối tuần → Calendar Period |

**Screen Time Wallet** (gắn Screen Boundary): ngân sách phút **thỏa thuận trong nhà**, không đo máy / không firewall. Soft-lock + deep-link OS giữ nguyên A+B.

**Family Score (nhẹ):** tổng hợp tuần từ ngày đẹp / streak / % routine / challenge — gate đề xuất thưởng phút (copy tích cực, không “phạt”).

---

## 3. KPI sản phẩm

- Gia đình mới hoàn tất thiết lập ≤ **5 phút** trên điện thoại.
- Mỗi ngày bố mẹ thao tác trong Famixa ≤ **1 phút**.
- **90%** thay đổi do AI đề xuất (không tự cấu hình Settings).
- Mỗi quyết định bố mẹ **1–2 chạm**.
- Hầu hết quản lý trên điện thoại, không cần máy tính.

Đo: `onboarding.completedAt − startedAt` · số quyết định Inbox/ngày · `proposal_accepted / config_changes`.

---

## 4. Sóng ship

| Sóng | Phạm vi | Outcome |
|------|---------|---------|
| **1** | Decision Inbox · Child request (+AI summary) · Family Mode 1 chạm | Cảm giác “AI OS” / duyệt 3 giây |
| **2** | Screen Time Wallet tuần · AI đề xuất budget · Setup Wizard mở rộng | Ngân sách + setup không Settings |
| **3** | Adaptive proposals · Family Score gate | Thích nghi theo hành vi / kỳ |

---

## 5. Non-goals

- Đo Screen Time thiết bị · MDM · VPN · chặn URL.
- LLM free-form chat (engine rule-based + tín hiệu day_flow / glance / period).
- AI tự mutate Routine / Wallet / Mode không qua duyệt.
- Child tự grant phút.

---

## 6. API / schema (tóm tắt)

| Surface | Path / table |
|---------|----------------|
| Child request | `pack_family.child_request` · `/requests` (`screen_minutes` + `day_mission`) |
| Ad-hoc mission | `POST …/commitments/ad-hoc` · survives routine rebuild |
| Family admin (phone) | family-app `/family-admin` — members, today’s mission, Family Mode |
| AI proposal | `pack_family.ai_proposal` · trong Decision Inbox |
| Decision Inbox | `GET .../decision-inbox` |
| Family Mode | `POST .../family-modes/activate` (Calendar Period) |
| Wallet | `pack_family.screen_time_wallet` + ledger · `/screen-wallet` |
| Family Score | derived / cached nhẹ trên glance signals |

---

## 7. Acceptance

- [x] Brief epic này
- [x] Sóng 1 API + family-app Inbox / Kid xin phút / Mode sheet
- [x] Sóng 2 Wallet + Wizard (đề xuất ví tuần + Setup mở rộng)
- [x] Sóng 3 Adaptive + Score gate (`ScanAdaptive` + Family Score)
- [x] Copy trung thực: Wallet ≠ khóa máy

---

## 8. Liên kết

- Calendar Period: mig `221` · `FamilyCalendarPeriodService`
- Screen Boundary: [family-screen-boundary-v1.md](./family-screen-boundary-v1.md)
- Parent digest push: mig `230` · `FamilyOsParentPushService`
- Onboarding: `client/family-app/src/shared/onboarding/`
