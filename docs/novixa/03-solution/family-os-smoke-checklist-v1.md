# FamilyOS — Smoke Checklist (local)

**Mã:** KIT-QA-FO-SMOKE-01 · **Ngày:** 2026-07-23  
**Tenant:** `DEMO_FAMILY` / `admin` / `Admin@123`  
**Ports:** API `5290` · family-app `5178` · Admin `5173`

Chạy script nhanh: `.\scripts\smoke-family-os-local.ps1`

---

## A. Value persistence (score / nudge / onboarding)

| # | Bước | Kỳ vọng | ☐ |
|---|------|---------|---|
| A1 | Restart API sau khi có migration `200` | `/api/health` = ok | |
| A2 | Login + `GET .../value/state` | 200, maps health/nudge + onboarding nullable | |
| A3 | Parent Home → Nhắc nhẹ 1 lần | `POST .../nudges/increment` → count tăng; reload state vẫn giữ | |
| A4 | Mở tab Giá trị | Health Score hiện; sau F5 vẫn có lịch sử | |
| A5 | Browser 2 / Incognito cùng tenant | Hydrate thấy nudge/score/onboarding từ server | |
| A6 | Chạy lại Onboarding (settings) → hoàn tất | `PUT .../onboarding`; Who-are-you không ép onboard lại | |

## B. Team Unlock (TP2)

| # | Bước | Kỳ vọng | ☐ |
|---|------|---------|---|
| B1 | Migration `201` applied | bảng `pack_family.team_unlock_event` | |
| B2 | `GET .../team-day` | teamPercent / remainingMissions / heroMissionLine | |
| B3 | Đánh dấu hết Mission trẻ trong ngày | `POST .../team-unlocks/ensure` → `pending_confirm` | |
| B4 | Parent tab Rewards → **Mở thưởng nhà** | status `confirmed`; hero hiện đã mở | |
| B5 | (Tuỳ chọn) **Để sau** trên unlock mới | status `deferred`; không tự confirmed | |
| B6 | Child Home khi đội xong | Copy nhắc bố mẹ xác nhận (không tự mở) | |

## C. Regression nhẹ

| # | Bước | Kỳ vọng | ☐ |
|---|------|---------|---|
| C1 | Day flow ensure | vẫn tạo commitments | |
| C2 | Consequence pending vẫn decide được | applied / waived | |
| C3 | Kid “Mình đã làm” | done + celebrate | |

---

## D. Payer path (Brief → Coach “Đã thử” → ROP → checkout Pro)

| # | Bước | Kỳ vọng | ☐ |
|---|------|---------|---|
| D1 | Parent Home Morning Brief hiện | 1 CTA chính (attention / coach / 3Q) | |
| D2 | Coach → **Đã thử** | `POST .../parent-success/coach-acted` 200 | |
| D3 | Báo cáo → ROP | `GET .../parent-success/rop?days=30` 200 hoặc gated Free/Plus | |
| D4 | Gia hạn / checkout Pro | `POST .../billing/checkout` tạo order hoặc URL | |
| D5 | Decision Inbox + Family Mode | duyệt ~vài giây; Mode 1 chạm ≤1 phút | |

Chạy kèm: `.\scripts\smoke-family-os-local.ps1` (đã gồm mục D API).

---

## Không làm trong smoke này

- Deploy VPS / `migration-files.prod.txt`
- Sibling nudge (TP3)
- Cooperation Score API (TP4)
