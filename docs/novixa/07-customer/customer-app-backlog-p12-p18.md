# Novixa Customer App — Backlog P12–P18 (thực hiện sau P11b)

**Mã:** NVX-PRD-10 · **Tier:** T2 · **Trạng thái:** Backlog · **Version:** 1.0  
**Ngày:** 2026-07-09 · **Owner:** Product

**Điều kiện bắt đầu:** [Gate G1 pass](./customer-app-phase-gates-v1.md) (P11b Done, **L1**)

**Liên quan:**
- [customer-app-roadmap-p12-p18.md](./customer-app-roadmap-p12-p18.md) — framework & trình tự
- [customer-app-phase-gates-v1.md](./customer-app-phase-gates-v1.md) — cổng G2–G5
- Handoff active: `.cursor/handoff/customer-app-p11b.md`

> ⛔ **Không bắt đầu code** các phase dưới đây cho đến khi gate tương ứng pass.  
> Trình tự chuẩn: **P15 → P12 → P13 → P14 → P16 → P17 → P18**

---

## Tổng quan

| Phase | Tên | Gate vào | Gate ra | Maturity | Sprints |
|---|---|---|---|---|---|
| **P15** | Family 2.0 + Analytics | G1 | G2 | L1→L2 | 4 |
| **P12** | Commerce + Rx upload | G2* | G3a | L2→L3a | 4 |
| **P13** | Digital Rx + timeline | G3a | G3 | L3a→L3 | 4 |
| **P14** | AI LLM + interactions | G3 | G4 | L3→L4 | 4 |
| **P16** | Engagement + Knowledge | G4 | G5a | L4→L5a | 4 |
| **P17** | Native + payments | G5a + P12 | G5b | L5a→L5b | 5 |
| **P18** | Community + ecosystem | G5a | G5c | L5b→L5c | 4 |

\* Track A: P12 có thể sau G1 — cần Sales sign-off ([phase gates](./customer-app-phase-gates-v1.md#decision-track-a-exception)).

---

## P15 — Family 2.0 + Personal analytics

**Handoff file (tạo khi bắt đầu):** `.cursor/handoff/customer-app-p15.md`

### Deliverables

| ID | Hạng mục | Acceptance criteria |
|---|---|---|
| P15-01 | Invite caregiver OTP | SĐT mời → accept → `linked_customer_id` set |
| P15-02 | ACL caregiver | `view_reminders` / `confirm_dose` / `place_order` |
| P15-03 | Push caregiver | Khi thành viên đến giờ uống + `notifyCaregiver` |
| P15-04 | Route `/insights` | Chi tiêu 6T, adherence %, streak, top thuốc |
| P15-05 | Health PDF | Vitals + adherence; filter `familyMemberId` |

### API / schema (dự kiến)

- `POST /api/customer-app/family/{id}/invite`
- `POST /api/customer-app/family/invites/{token}/accept`
- `GET /api/customer-app/insights/summary`
- Migration: `family_invites`, `family_member_permissions`

### Sprint breakdown

| Sprint | Focus |
|---|---|
| S1 | Invite + accept flow |
| S2 | Permissions + caregiver dose confirm |
| S3 | Insights API + UI |
| S4 | PDF export |

### Gate G2

Xem [customer-app-phase-gates-v1.md § G2](./customer-app-phase-gates-v1.md#gate-g2--p15-done-l1--l2).

---

## P12 — Commerce + Prescription upload (capture)

**Handoff:** `.cursor/handoff/customer-app-p12.md`

### Deliverables

| ID | Hạng mục | Acceptance criteria |
|---|---|---|
| P12-01 | Cart | `customer_carts` + add/update/remove |
| P12-02 | Shop UI | `/shop` browse, search, category RX/OTC/TPCN/device |
| P12-03 | Checkout | Pickup/delivery; address; order status |
| P12-04 | Staff queue | Admin/staff thấy đơn app pending |
| P12-05 | Rx upload | Ảnh đơn → `prescription_uploads` pending |
| P12-06 | DS approve | Admin approve/reject + lý do |
| P12-07 | Payment stub | COD + dynamic QR; webhook confirm |
| P12-08 | POS convert | Online order → `sales_order` |

### API / schema (dự kiến)

- `/api/customer-app/cart/*`
- `/api/customer-app/checkout/*`
- `/api/customer-app/prescription-uploads/*`
- Migration: `customer_carts`, `customer_checkout_orders`, `prescription_uploads`

### Không làm trong P12

- OCR (→ P13)
- MoMo/VNPay (→ P17)
- Redeem điểm checkout (→ P16-S4)

### Gate G3a

10 đơn OTC E2E; 5 Rx upload duyệt — [§ G3](./customer-app-phase-gates-v1.md#gate-g3--p12--p13-done-l2--l3).

---

## P13 — Digital Rx (structure + extract)

**Handoff:** `.cursor/handoff/customer-app-p13.md`

### Deliverables

| ID | Hạng mục | Acceptance criteria |
|---|---|---|
| P13-01 | e-Rx schema | `electronic_prescriptions` + lines |
| P13-02 | Import POS | Từ draft order / sales order |
| P13-03 | OCR pipeline | Ảnh → draft e-Rx → **DS confirm** |
| P13-04 | Auto reminders | Từ e-Rx approved |
| P13-05 | Repurchase 1-tap | Từ e-Rx line |
| P13-06 | Timeline | `/medications` hoặc `/prescriptions` timeline |
| P13-07 | Share | Read-only link hoặc PDF |

### Pattern

`Capture (P12)` → `Structure (P13-S1)` → `Extract (P13-S2)` → `Automate (P13-S3)`

### Gate G3b

3 OCR+confirm; 3 auto reminder — [§ G3](./customer-app-phase-gates-v1.md#gate-g3--p12--p13-done-l2--l3).

---

## P14 — AI LLM + drug interactions

**Handoff:** `.cursor/handoff/customer-app-p14.md`

### Deliverables

| ID | Hạng mục | Acceptance criteria |
|---|---|---|
| P14-01 | LLM integration | `AiOrchestrator` + RAG knowledge pack |
| P14-02 | Guardrails | BR-AI-001; disclaimer 100% |
| P14-03 | Explain e-Rx | Context từ `active-medications` + e-Rx |
| P14-04 | Interaction engine | Hoạt chất; alert; `suggestChat` |
| P14-05 | Proactive | Adherence/repurchase notifications (không chẩn đoán) |

### Phụ thuộc

- P13-S1 bắt buộc
- Drug knowledge pack pharmacy

### Gate G4

[§ G4](./customer-app-phase-gates-v1.md#gate-g4--p14-done-l3--l4)

---

## P16 — Engagement & Knowledge

**Handoff:** `.cursor/handoff/customer-app-p16.md`

### Deliverables

| ID | Hạng mục | Acceptance criteria |
|---|---|---|
| P16-01 | CSAT | 1–5 sao sau đơn hoàn tất |
| P16-02 | Complaint ticket | → admin queue |
| P16-03 | Survey | In-app form hoặc deep link |
| P16-04 | Knowledge CMS | Admin publish → app `/knowledge` |
| P16-05 | Campaign banner | Home feed; consent marketing |
| P16-06 | Redeem checkout | Điểm tại checkout (max % đơn) |
| P16-07 | Home med expiry | *(optional)* HSD thuốc tại nhà |
| P16-08 | Consultation slot | *(optional)* Đặt lịch tư vấn |

### Gate G5a

CSAT ≥15%; 5 bài knowledge — [§ G5](./customer-app-phase-gates-v1.md#gate-g5--p16p18-l4--l5).

---

## P17 — Native app + payments deep

**Handoff:** `.cursor/handoff/customer-app-p17.md`

### Deliverables

| ID | Hạng mục | Acceptance criteria |
|---|---|---|
| P17-01 | Capacitor/RN shell | iOS + Android build |
| P17-02 | FCM/APNs | Push native |
| P17-03 | Payment gateway | MoMo / VNPay / ZaloPay (≥1) |
| P17-04 | Widget | Nhắc thuốc home screen |
| P17-05 | Deep link | Tenant QR → app |
| P17-06 | Store release | TestFlight / Play internal → production |
| P17-07 | Biometric login | Face/Touch optional |

### Gate G5b

Store pass; 1 gateway live.

---

## P18 — Community & Connected platform

**Handoff:** `.cursor/handoff/customer-app-p18.md`

### Deliverables

| ID | Hạng mục | Acceptance criteria |
|---|---|---|
| P18-01 | Product reviews | Post-purchase; moderation |
| P18-02 | Community Q&A | DS moderated |
| P18-03 | Assessment link | Public screening → customer app |
| P18-04 | BLE device POC | BP monitor demo |
| P18-05 | Insurance profile | *(backlog thấp)* — chưa V1 |

### Gate G5c

Moderation workflow; 1 assessment template live.

---

## P11b-S2 backlog — ✅ Done (2026-07-09)

| ID | Hạng mục | Trạng thái |
|---|---|---|
| P11b-02 | Notification i18n service | ✅ |
| P11b-02a | Migration 091 | ✅ |
| P11b-02b | Push + in-app localized at enqueue | ✅ |

---

## Ritual khi bắt đầu mỗi phase

1. Copy template handoff → `.cursor/handoff/customer-app-p{N}.md`
2. Tạo migration `06X_p{N}_*.sql`
3. Cập nhật [demo-customer-app-checklist.md](../../client/customer-app/demo-customer-app-checklist.md)
4. Mở rộng [pilot-smoke-test-checklist-v1.md](./pilot-smoke-test-checklist-v1.md)
5. Phase end: tick gate → cập nhật [founding commitment](./customer-app-founding-commitment-v1.md) nếu public

---

## Out of scope toàn V1 (không lên kế hoạch P12–P18)

- Xác thực VNeID / giấy tờ gia đình (mô hình Long Châu)
- Hồ sơ bảo hiểm đầy đủ
- Marketplace đa nhà thuốc
- Thay thế chẩn đoán / kê đơn tự động không DS
- Portal BN phòng khám đầy đủ / EMR trên app KH (Option 2–3 — sau khi PK bật channel riêng)

### Đã ship sớm (song song P11b) — Connect-aware Option 1

| ID | Việc | Ghi chú |
|---|---|---|
| CA-CONNECT-01 | `GET /api/customer-app/connect/inbox` | Referral / booking / rx_ready theo KH NT |
| CA-CONNECT-02 | Home card điều phối | CTA lấy thuốc → hub nhà thuốc |
| CA-CONNECT-03 | Gói `connectInbox` trong home-summary | 1 round-trip |

---

**Changelog**

| Version | Ngày | Thay đổi |
|---|---|---|
| 1.1 | 2026-07-12 | Connect-aware Option 1 (inbox + Home) — không mở Clinic portal |
| 1.0 | 2026-07-09 | Backlog P11b-S2 + P15–P18 với acceptance criteria |
