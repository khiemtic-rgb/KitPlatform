# Famixa · Motivation Patterns S1–S4

**Mã:** KIT-PRD-FO-MOT-01 · **Pack:** FamilyOS / Famixa  
**Ngày:** 2026-08-02 · **Trạng thái:** Shipped local (heuristic)  
**Phụ thuộc:** [behavior-os-north-star-v1.md](./behavior-os-north-star-v1.md) · [famixa-growth-balance-v1.md](./famixa-growth-balance-v1.md)

---

## North star

Famixa giải quyết **động lực + cách đồng hành**, không chỉ nhắc việc.

Promise: giúp bố mẹ thấy *vì sao chưa làm*, thử chiến thuật khác, kiên trì với tiến bộ nhỏ — **không** hứa “con tự giác nhanh”.

---

## S1 — Pattern catalog (5)

| Code | Tên VI | Ý |
|------|--------|---|
| `evening_fatigue` | Chậm buổi tối | Có thể mệt |
| `subject_avoidance` | Né môn khó | Sợ / chưa thấy làm được |
| `nudge_dependent` | Chỉ làm khi có nhắc | Thiếu tự chủ |
| `social_boost` | Làm tốt hơn khi có người cùng | Động lực xã hội |
| `streak_fragile` | Giữ nhịp yếu | Duy trì thấp, không phải lười |

Mỗi pattern có 2–3 **tactic** (child cue + parent advice). SoT code: `FamilyBehaviorPatterns.cs`.

---

## S2 — Motivation Engine

- Day-flow / coach / parent-push gọi `FamilyMotivationIntervention.Decide` với pattern infer + tactic.
- Pattern `nudge_dependent` / `evening_fatigue` **hạ** parent push — **không** tăng nudge budget.
- Week playbook lưu pattern/tactic đang thử (`behavior_week_playbook`).

---

## S3 — Child Voice (tuần)

API:

- `POST …/behavior/child-voice` — hardest / wantParent / wish → ≤2 tip cho bố mẹ
- `GET …/behavior/week-playbook` — catalog + active + tip + child voice

UI: màn Báo cáo → Insight tuần; kid treasure có lối gửi nhanh (nếu có member).

---

## S4 — Parent strategy mirror + Twin

- Một **parent strategy tip**/tuần từ tỷ lệ nhắc vs self-start (+ pattern).
- Twin dimensions hiện trong weekly / coach surface (không jargon nặng trên free).

---

## Non-goals

- Điểm động lực “7/10” giả khoa học  
- LLM free-form  
- Tăng spam reminder  
- Chấm điểm / đổ lỗi bố mẹ  
