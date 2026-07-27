# Behavior OS — North Star v1 (Wave 0)

**Mã:** KIT-PRD-FO-BOS-01 · **Pack:** FamilyOS · **Version:** 1.0  
**Ngày:** 2026-07-27 · **Owner:** Product + Architecture  
**Phụ thuộc:** [family-os-pack-brief-v1.md](./family-os-pack-brief-v1.md) · [adaptive-family-engine-v1.md](./adaptive-family-engine-v1.md)  
**Trạng thái:** ACTIVE local (Wave 0–5) · Family OS epic feature-park vẫn áp dụng cho deploy pilot hàng loạt

---

## 1. Positioning

**Famixa** = *AI Human Growth Operating System for Families*

| Không | Có |
|-------|-----|
| Family App (task / routine / reward stack) | Behavior OS (đổi hành vi) |
| Completed Routine | **Behavior Changed** |
| AI nhắc việc | AI giảm vai trò nhắc theo thời gian |
| Kiểm soát con | Chuyển giao trách nhiệm cho con |

**Family OS** = lớp trải nghiệm (family-app, day flow, inbox).  
**Behavior OS** = kernel (event bus, habit lifecycle, policy nhắc, reflection, KPI autonomy).

---

## 2. North-star KPIs (đo được)

| KPI | Định nghĩa vận hành | Nguồn event |
|-----|---------------------|-------------|
| **Child Autonomy Score** | % lần tự bắt đầu / hoàn thành không cần parent nudge trong 7 ngày | `commitment_done`, `parent_nudge`, `self_start` |
| **Parental Intervention Index** | Số lần nhắc (app + parent push) / commitment mở / tuần | `reminder_fired`, `parent_nudge` |
| **Habit Graduation Rate** | % template đạt `autonomous` \| `maintained` | `habit_stage_changed` |
| **Reflection Rate** | % `commitment_done` có reflection ≤ 15 phút | `reflection_submitted` |
| **Family Peace Index** (lite) | Proxy: ít skip+conflict suggestion / tuần | `commitment_skipped`, consequence suggest |

Không lấy DAU / số tick routine làm KPI chính.

---

## 3. Event schema (Wave 0)

Bảng `pack_family.behavior_event`:

| Field | Ý |
|-------|---|
| `event_type` | `commitment_done` · `commitment_skipped` · `reflection_submitted` · `habit_stage_changed` · `reminder_suppressed` · `reminder_fired` · `parent_nudge` |
| `member_id` / `commitment_id` / `template_id` | Liên kết |
| `payload_json` | Chi tiết (stage from→to, prompt_code, …) |
| `occurred_at` | UTC |

Mọi quyết định AI sau này (Motivation / Intervention / Twin) **đọc** event này — không invent signal mới không có schema.

---

## 4. Habit lifecycle (Wave 1)

Trên `commitment_template`:

```
new → guided → assisted → habit_forming → autonomous → maintained
```

| Stage | Ngưỡng (mặc định) | Reminder |
|-------|-------------------|----------|
| `new` | 0–2 ngày done liên tiếp | Full |
| `guided` | 3–6 | Full |
| `assisted` | 7–13 | Full |
| `habit_forming` | 14–20 | Soft (upcoming only) |
| `autonomous` | ≥21 + ổn định | **Suppressed** (AI “tốt nghiệp nhắc”) |
| `maintained` | autonomous ≥14 ngày thêm | Suppressed |

Một lần skip liên tiếp có thể hạ 1 bậc (không về `new` đột ngột trừ chuỗi dài).

---

## 5. Reflection (Wave 1)

Sau `commitment_done`, hỏi **một** câu (15 giây):

| `prompt_code` | Câu (VI) |
|---------------|----------|
| `hardest` | Điều khó nhất hôm nay là gì? |
| `learned` | Con học được gì? |
| `improve_tomorrow` | Mai con muốn cải thiện điều gì? |

Lưu `pack_family.commitment_reflection` + event `reflection_submitted`.

---

## 6. Non-goals (Wave 0–1)

- Digital Twin đầy đủ / Prediction % / Negotiation game / Photo evidence bắt buộc  
- LLM free-form chat  
- MDM / đo screen time thiết bị  
- Deploy VPS Family pilot hàng loạt (vẫn theo park deploy — chỉ local)

---

## 7. Ship checklist Wave 1 (local)

- [x] PRD này  
- [x] Migration `240_pack_family_behavior_os_wave1.sql`  
- [x] API habit stage trên commitment DTO + suppress reminder  
- [x] API POST reflection  
- [x] family-app: badge stage + sheet reflection sau done  
- [x] `behavior_event` ghi khi done / stage change / suppress  

---

## 8. Wave 2 — Evidence Ladder + Confidence + Illusion check

**Mục tiêu:** “done” không còn là boolean thuần — có **bậc bằng chứng** và **điểm tin cậy hoàn thành** (cần thêm dữ liệu, không cáo buộc con).

### Evidence ladder

| Level | Tín hiệu |
|-------|----------|
| 0 | Self tick |
| 1 | + Reflection |
| 2 | + Retrieval check (nhiệm vụ kiểu học) |
| 3 | + Photo (`evidence_url`, tùy chọn) |

### Confidence (0–100)

Công thức thuần trong `FamilyEvidenceConfidence` — nhãn VI: Ít tín hiệu / Cần thêm dữ liệu / Khá chắc / Tin cậy cao.

### Illusion-of-learning

Sau nhiệm vụ học (heuristic title), 2 câu MCQ meta:

1. Cách làm: lướt / thực hành / nhớ lại  
2. Mức nhớ: giải thích được / đại khái / cần xem lại  

`skim + can_explain` → `illusion_risk` (boost thấp hơn).

### Ship checklist Wave 2 (local)

- [x] Migration `241_pack_family_behavior_os_wave2.sql`  
- [x] API confidence trên commitment DTO  
- [x] GET/POST `retrieval-check`  
- [x] family-app: sheet quiz sau reflection + badge confidence  
- [x] Events: `retrieval_submitted`, `confidence_scored`, `evidence_uploaded`  

Non-goals Wave 2: camera mặc định, Twin, LLM, Motivation engine.

---

## 9. Wave 3 — Motivation + Intervention lite

**Mục tiêu:** *Intervention ≠ Reminder* — can thiệp có playbook; động lực có cue có cấu trúc (không LLM).

### Motivation drivers (lite graph)

`autonomy` · `progress` · `mastery` · `relatedness` · `rest` — chọn theo habit stage / learning / skip.

### Intervention levels

| Level | Ý |
|-------|---|
| `observe_only` | Thói quen tự chủ / hết ngân sách — **không** parent push |
| `self_cue` | Gợi ý cho con tự bắt đầu |
| `soft_nudge` | Nhắc nhẹ, ưu tiên child chime |
| `parent_nudge` | Bố mẹ được phép nhắc (còn budget) |

Ngân sách mặc định: **3 parent nudge / ngày / gia đình** (`parent_nudge_day` + policy).

### Ship checklist Wave 3 (local)

- [x] Migration `242_pack_family_behavior_os_wave3.sql` (event types)  
- [x] `FamilyMotivationIntervention` pure policy  
- [x] Commitment DTO: cue / intervention / allowParentPush  
- [x] GET `behavior/coach` · POST `self-start`  
- [x] Parent push tôn trọng `AllowParentPush`  
- [x] family-app: cue + «Bắt đầu rồi»; parent «NHẮC NHẸ» lọc observe  

Non-goals Wave 3: full Motivation Graph UI, Twin, Negotiation game, LLM.

---

## 10. Wave 4 — Twin + Prediction lite

**Mục tiêu:** Behavior Digital Twin (7 chiều tín hiệu) + dự đoán bỏ cuộc buổi tối theo **band** (thấp/vừa/cao) — không % giả.

### Twin dimensions

`autonomy` · `consistency` · `learning_depth` · `reflection` · `persistence` · `self_start` · `peace`

Disclaimer bắt buộc: *mô hình hóa tín hiệu, không đánh giá tính cách*.

### Evening quit prediction

Rule/signal trên khung giờ ≥17:00 + habit stage + skip tối + learning — output `low|medium|high` + lý do + action gợi ý.

### Ship checklist Wave 4 (local)

- [x] Migration `243_pack_family_behavior_os_wave4.sql`  
- [x] `FamilyBehaviorTwin` pure scorer  
- [x] GET `behavior/twin` + snapshot table  
- [x] Evening risk trên commitment DTO  
- [x] family-app: parent twin panel + kid evening cue  
- [x] Events: `twin_scored`, `prediction_flagged`  

Non-goals Wave 4: Family Twin peer fairness, LLM twin narrative, fake precision %.

---

## 11. Wave 5 — Family Twin + Retirement policy

**Mục tiêu:** Family Twin (Peace / Autonomy / Intervention + công bằng anh chị em) + **Autonomy Gradient** runtime + **Observe-only**.

### Retirement stages

`full_support` → `assisted` → `soft` → `observe` → `retired`

### Observe-only

Policy `behavior_retirement_policy.observe_only` — khi bật: mọi commitment `AllowParentPush=false`; parent push bị chặn.

### Dependence warning

Intervention tuần này tăng trong khi Autonomy / Self-start thấp → cảnh báo *AI đang nuôi phụ thuộc*.

### Sibling fairness

So sánh tỉ lệ done — nhãn đều / lệch nhẹ / lệch rõ — **không xếp hạng con**.

### Ship checklist Wave 5 (local)

- [x] Migration `244_pack_family_behavior_os_wave5.sql`  
- [x] `FamilyTwinRetirement` pure policy  
- [x] GET `behavior/family-twin` · GET/PUT `retirement-policy`  
- [x] Wire Observe-only vào intervention + parent push  
- [x] family-app: AI Retirement panel + toggle Observe-only  
- [x] Events: `retirement_advanced`, `observe_mode_*`, `dependence_warned`  

Non-goals Wave 5: Negotiation game, MDM, deploy pilot (vẫn park).

---

## 12. Unpark deploy

Khi Product chốt: bỏ park deploy, thêm `240`–`244` vào `migration-files.family-os.txt`, smoke `family.kittech.vn`.
