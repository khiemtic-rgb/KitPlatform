# Parent Success Engine v1 — Payer Retention North Star

**Mã:** KIT-PRD-FO-PSE-01 · **Pack:** FamilyOS · **Version:** 1.0  
**Ngày:** 2026-07-27 · **Owner:** Product + Behavior OS  
**Phụ thuộc:** [behavior-os-north-star-v1.md](./behavior-os-north-star-v1.md) · [family-os-pack-brief-v1.md](./family-os-pack-brief-v1.md) · [family-currency-economics-v1.md](./family-currency-economics-v1.md)  
**Trạng thái:** DESIGN · local only · Family OS park deploy vẫn áp dụng

---

## 1. Verdict (tóm tắt)

Phân tích **đúng và quan trọng hơn** hầu hết feature EdTech/FamilyTech: **daily user ≠ payer**.

| Vai trò | Ai | Job-to-be-done | Nếu thất bại |
|---------|-----|----------------|--------------|
| Daily user | Con | Chơi / tick / sao / thói quen | Churn engagement |
| **Payer** | Phụ huynh | Bớt mệt · bớt nhắc · con trưởng thành · nhà yên | **Hủy gia hạn dù con thích** |

Famixa hiện mạnh ở **Behavior OS / Currency / Twin** (đổi hành vi trẻ) nhưng **mặt tiền phụ huynh** vẫn nghiêng “quản lý việc” (routine, sao, checklist). Tab Value trong family-app có Health Score / Coach / Transformation nhưng phần lớn **client + localStorage**, lệch khỏi `behavior_event` server — nên payer **không cảm nhận được ROI cảm xúc** sau 30 ngày.

**Kết luận sản phẩm:** Behavior OS = engine đổi hành vi trẻ. **Parent Success Engine (PSE)** = engine chăm sóc cảm xúc + chứng minh giá trị cho người trả tiền. Hai engine phải đọc chung `behavior_event`, không invent signal mới.

---

## 2. Đánh giá từng đề xuất (1–10)

| # | Đề xuất | Điểm | Lý do | Sẵn sàng tái sử dụng |
|---|---------|------|-------|----------------------|
| 1 | Dashboard = cảm giác “nhẹ hơn hôm qua” | **9.5** | Đúng ngôn ngữ payer; 10 giây hiểu | Twin Peace/Autonomy/Intervention · CoachInsight · Health Score |
| 2 | Daily Insight (1 câu đáng giá/ngày) | **9** | Chống noise; tăng open rate | `FamilyCoachInsightService` (Admin only!) |
| 3 | AI Parenting Coach (dạy cha mẹ) | **9.5** | Top-3 retention; khác biệt khó copy | CoachInsight + MotivationIntervention + ParentEmotionPush |
| 4 | Family Health Report | **8.5** | Shareable → viral nội bộ | `FamilyInsightService` weekly + Twin |
| 5 | Family Timeline | **8** | Emotional moat dài hạn | `FamilyMemory` (server) vs Journey (client) — cần unify |
| 6 | AI Wins | **8.5** | ROI narrative ngắn | Memory + Coach strength + Intervention delta |
| 7 | Parent Achievements | **7.5** | Đồng cảm payer; dễ gamify sai | Parent Goal (`226`) + badge pattern (C5) |
| 8 | AI Letter (tháng) | **9** | Peak emotion; screenshot-worthy | Cần narrative layer trên event aggregates |
| 9 | ROP (Return on Parenting) | **9.5** | Ngôn ngữ bán hàng + renew | `transformation-report.ts` (client) → phải serverize |
| 10 | Parent Success Engine (3 câu/ngày) | **10** | Meta-loop đúng bài toán payer | Chưa có orchestration |

**Rủi ro chung cần tránh**
- LLM tự bịa “gia đình tốt lên” khi signal yếu → mất trust.
- % đẹp nhưng không giải thích được (cấm “magic %” không có mẫu số).
- Parent Achievements trở thành checklist áp lực mới cho bố mẹ.
- Dashboard cảm xúc nhưng không có deep-link vào hành động cụ thể (coach tip).

---

## 3. Ba câu hỏi Parent Success (SoT)

Mỗi ngày PSE chỉ cần giúp phụ huynh tiến gần “Có”:

1. **Hôm nay bạn đã phải nhắc con ít hơn chưa?** ← `parent_nudge` + `reminder_fired` WoW  
2. **Hôm nay gia đình có bớt căng thẳng hơn chưa?** ← Peace Index / skip+conflict proxy / Observe-only days  
3. **Hôm nay bạn có thêm thời gian chất lượng với con chưa?** ← challenge unlock, gratitude, movie night, memory moments  

Nếu 3 chỉ số này **cải thiện theo tháng** → gia hạn. Đó là định nghĩa thành công của PSE — không phải DAU trẻ.

---

## 4. Top 3 ưu tiên (đúng như đề xuất — chốt)

### P0 — AI Parenting Coach (daily 1–2 tips)
- **Không** dạy con; dạy cách tương tác.
- Sáng: 1 câu hỏi thay vì nhắc điểm.
- Tối: 1 ranh giới (đừng ép khi Twin risk = high).
- Nguồn: `FamilyCoachInsight` + Intervention + Twin evening band.
- Kênh: Parent home first viewport + 1 push/ngày tối đa.

### P0 — Family Growth Report (không phải task report)
- Tuần / 30 ngày: Autonomy ↑, Reminder ↓, Peace ↑, Self-start count, Habit graduation.
- Copy kiểu “Gia đình đang tốt lên?” — không “8/12 routine”.
- Server `FamilyInsightService` + Twin — bỏ phụ thuộc localStorage.

### P0 — Return on Parenting (ROP)
- 30 / 90 ngày: giờ nhắc tiết kiệm · % tranh cãi proxy · % tự học · giờ chất lượng (proxy).
- Artifact renew: màn hình “Chứng cứ 90 ngày” + share card.
- Serverize từ `behavior_event` (không localStorage).

**P1 tiếp theo:** Daily Insight unify · AI Wins strip · Family Timeline = Memory · AI Letter tháng · Parent Achievements (nhẹ, opt-in).

---

## 5. Kiến trúc đề xuất

```
behavior_event (SoT)
    ├─ Behavior OS (child autonomy, habit, currency)
    └─ Parent Success Engine
           ├─ Daily Parent Pulse (3 Q answers + Family Score narrative)
           ├─ Parenting Coach (1–2 tips/day)
           ├─ Growth Report + ROP cards
           ├─ Wins / Letter / Timeline (Memory)
           └─ Parent Achievements (opt-in)
```

**Nguyên tắc**
1. Mọi số liệu PSE đọc `behavior_event` / Twin / Intervention — không invent KPI không có schema.  
2. family-app Parent **home** = PSE pulse (10 giây), không phải task list. Tasks lùi tab.  
3. Sao / Currency = công cụ trẻ; **không** là hero của payer UI.  
4. 1 insight / ngày · 1 coach tip sáng hoặc tối · không spam.  
5. Không % khi n &lt; ngưỡng tin cậy (vd. &lt; 5 ngày dữ liệu → qualitative only).

---

## 6. Wave triển khai gợi ý (local)

| Wave | Scope | Outcome payer |
|------|-------|---------------|
| **P0a** | Parent home rewrite: “Hôm nay nhẹ hơn?” + Family Score narrative từ server Twin/Intervention | 10 giây hiểu |
| **P0b** | Wire family-app → `GET coach-insight` (bỏ Foxy local-only làm SoT) | Daily Coach thật |
| **P0c** | Server ROP 30/90 + Growth Report card (thay transformation localStorage) | Renew artifact |
| **P1** | Memory = Timeline duy nhất · AI Wins digest · AI Letter monthly template | Emotion moat |
| **P2** | Parent Achievements (3 badge nhẹ) + Parent Success 3Q check-in tối | Self-recognition |
| **P3** | `parent_coach_acted` · nút Đã thử trên Famixa tip | Trust Flywheel đo được |

### Home UX P0 — Brief (2026-07-27)

Parent Home không còn dashboard stack (ROP/Wins/Letter/Twin).  
Layout: **AI Brief → Need Attention (nếu có) → Progress strip → Explore chips**.  
SoT: `buildHomeBrief` + `buildParentPulse` + coach tip; ROP/Letter/3Q trên tab Value.  
**P0.5:** Brief ưu tiên Attention khi có việc; CTA/deep-link `#fv-3q` / `#fv-rop` / `#fv-ai-letter`.  
**P1/P2:** Family Feed (ẩn nếu trống) + Memory win vào Brief bullets.  
Chi tiết: `parent-home-brief-p0-plan.md`.

Không deploy pilot hàng loạt khi Family OS vẫn park.

---

## 7. KPI PSE (đo được)

| KPI | Định nghĩa | Nguồn |
|-----|------------|-------|
| **Parent Open Rate (home)** | % ngày parent mở app / 7 ngày | analytics later |
| **Coach Act Rate** | % tip được “đã thử” hoặc phản hồi | new event `parent_coach_acted` |
| **Intervention Downtrend** | `parent_nudge`/tuần giảm sau 30 ngày | `behavior_event` |
| **ROP View @ renew** | % payer xem ROP card trong 7 ngày trước hết hạn | commercial later |
| **Renew Intent proxy** | Survey 1 câu “Bạn thấy nhẹ hơn?” / tháng | soft |

Không lấy số sao / số routine done của con làm KPI PSE.

---

## 8. Non-goals v1

- LLM free-form chat thay coach tip có cấu trúc  
- Đo “tranh cãi” bằng mic / NLP tin nhắn  
- MDM / screen time OS-level làm ROP chính  
- Thay Behavior OS / Currency — PSE **bổ sung**, không thay

---

## 9. Acceptance (khi implement P0)

- [x] Parent home first viewport: narrative “nhắc ↓ / chủ động ↑ / ngày tích cực?” — không dẫn bằng ⭐/routine count  
- [x] Daily Coach tip lấy từ server insight + Twin risk, ≤ 2 tip/ngày  
- [x] ROP 30 ngày tính từ `behavior_event`, hiển thị được trên 1 màn hình shareable  
- [x] family-app Value tab không còn là SoT localStorage cho score/report chính (ROP server; local chỉ fallback)

### P0a ship note (2026-07-27)
Parent home = `ph-pulse-hero` (Family Score từ Twin + nudge trend + autonomy/peace lines + coach-insight). Movie Night & Behavior OS detail xuống dưới.

### P0b ship note (2026-07-27)
`resolveParentCoach`: SoT = `coach-insight` + Twin evening + `behavior/coach` parentAdvice; local Foxy chỉ fallback. Sheet hiển thị tối đa 2 tip/ngày.

### P0c ship note (2026-07-27)
`GET …/parent-success/rop?days=30|90` · Growth Report từ behavior_event · home ROP card · Value tab `FamilyValuePanel` dùng server ROP.

### P1 acceptance
- [x] Family Timeline = `family_memory` SoT (`buildFamilyJourneyFromMemories`); glance chỉ fallback empty
- [x] `GET …/ai/wins-digest` · home AI Wins strip · Value tab wins list
- [x] `GET …/ai/letter?month=` · monthly templated Letter (Memory + ROP) · shareable · thin-data guard

### P1 ship note (2026-07-27)
`IFamilyAiDigestService` · `FamilyOsAiDigestController` · Timeline unify · parent home Wins/Letter cards · Value tab Wins + Letter + Memory timeline.

### P2 acceptance
- [x] Evening 3Q check-in (`parent_success_checkin` · mig 246) · home card · soft reflection
- [x] 3 light parent achievements (Nhẹ tay hơn / Con tự bắt đầu / Có kỷ niệm) — computed, no star/child badge
- [x] Parent UX one voice **Famixa** (Coach/Wins/Letter/pulse); Foxy remains kid surface

### P2 ship note (2026-07-27)
`GET/POST …/parent-success/evening-checkin` · `GET …/achievements` · Famixa label pass on parent home + Value.

### P3 acceptance
- [x] `behavior_event.parent_coach_acted` (mig 247) · tipId dedupe / ngày
- [x] `POST/GET …/parent-success/coach-acted` · nút **Đã thử** trên Famixa tip sheet
- [x] KPI Coach Act Rate có nguồn đo được

### P3 ship note (2026-07-27)
Trust Flywheel bước 1: quan sát tip → bố mẹ “Đã thử” → event SoT. Outcome `parent_nudge`↓ đối chiếu là bước sau.

### Family Replay chữ (sau P3)
- [x] `GET …/ai/replay?month=` — scenes từ Memory + ROP growth beats · shareTextVi
- [x] Value tab full Replay · home teaser khi không thin-data
- Non-goal: video 365 ngày (later)
