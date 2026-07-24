# Family Team Play — Solution Brief v1.0

**Mã:** KIT-PRD-FO-TP-01 · **Pack:** FamilyOS · **Version:** 1.0  
**Ngày:** 2026-07-23 · **Owner:** Product + Architecture  
**Phụ thuộc:** [family-os-pack-brief-v1.md](./family-os-pack-brief-v1.md)

**Trạng thái:** Brief chốt hướng · Prototype UI TP0–TP1 (local) · **TP2 Team Unlock API + parent confirm (local)**

---

## 1. Thesis

Đa số ứng dụng quản lý trẻ theo mô hình:

> Cha mẹ → giao việc → con làm → cha mẹ kiểm tra.

Điều này vô tình biến anh chị em thành đối thủ.

**Family Team Play** đảo mô hình:

> Không phải “Tôi hoàn thành.” — mà là “Gia đình mình hoàn thành.”

FamilyOS không cạnh tranh bằng checklist đẹp hơn, mà bằng **văn hóa đồng đội trong nhà**: giúp nhau, nhắc nhau, cùng mở thưởng, cùng giữ chuỗi.

Tagline bổ sung: **Not “I finished.” — “We finished.”**

North star metric: **Family Cooperation Score** — đo phối hợp, không đo ai giỏi nhất.

---

## 2. Đối chiếu định vị

| Ứng dụng quản lý trẻ | FamilyOS Team Play |
|----------------------|--------------------|
| “Con còn 1 việc” | “Cả đội còn 1 Mission” |
| Thưởng cá nhân | **Team Unlock** (cả nhà) |
| Streak từng con trên hero | **Team Streak** nhà trên hero |
| AI nhắc / báo cáo thiếu sót | AI **Rescue / Sibling nudge / Celebration** |
| Leaderboard anh chị em | **Cooperation Score** nhà |

Khớp thesis Starter: *One Family. One Plan. One Daily Flow* — Accountability là trách nhiệm với cam kết đã thống nhất, không giám sát trẻ.

---

## 3. Nguyên tắc bắt buộc

1. **Hero luôn là nhà** — điểm cá nhân chỉ ở chi tiết / drawer / chip phụ.
2. **Ngôn ngữ đội** trên bề mặt chung — không gắn tên khi còn thiếu, trừ ngữ cảnh *mời giúp* (có chọn).
3. **Anh chị giúp ≠ quản đốc** — chỉ mời nhắc; không duyệt done / không phạt em.
4. **Không xấu hổ công khai** — Rescue không đưa “X còn 70%” lên hero cả nhà.
5. **Thưởng đội từ Agreement** — Team Unlock chỉ từ reward `accepted` / catalog an toàn.
6. **Coach rule-based trước** — mã đề xuất cố định; LLM (nếu có) chỉ viết lời, không bịa luật.
7. **Ranh giới cũ giữ nguyên** — không harmful punishment, không thay thế cha mẹ, không free-form LLM chat.

---

## 4. Vòng lặp giá trị

```
Agreement đội (reward / boss tuần)
  → Day Flow cả nhà
  → Team Progress (điểm nhà trên hero)
  → còn Mission? → Rescue / Sibling nudge
  → đủ điều kiện đội → Team Unlock + Celebration
  → Team Streak + Badges
  → Cooperation Score
  → Coach đề xuất chỉnh nhịp / văn hóa nhà
```

---

## 5. Mười cơ chế (product map)

| # | Cơ chế | Ý | Phase |
|---|--------|---|-------|
| 1 | **Điểm cá nhân + điểm đội** | Hero = % nhà; chi tiết = từng con | TP0–TP1 |
| 2 | **Team Unlock** | Thưởng cả đội (Movie / Pizza / Công viên) | TP2 |
| 3 | **Team Streak** | Chuỗi ngày đẹp **nhà** là bề mặt chính | TP1 |
| 4 | **Anh chị giúp em** | Mời nhắc → tin từ chị/anh, không từ AI | TP3 |
| 5 | **Family Combo** | Morning / Night combo cả đội | TP5 |
| 6 | **Family Boss** | Boss tuần đa-con đa-việc | TP5 |
| 7 | **Rescue** | Copy khoảng trống đội + CTA giúp | TP1 copy · TP3 nudge |
| 8 | **Family Celebration** | Mission Complete + confetti / mascot | TP1 lite · TP6 rich |
| 9 | **Huy hiệu gia đình** | Family Champion, Helping Family… | TP6 |
| 10 | **AI Coach (team)** | `invite_sibling_nudge`, `rescue_streak`… | TP3+ |

---

## 6. Family Cooperation Score

Chỉ số **0–100**, mặc định theo **tuần** (sparkline 30 ngày sau).

| Trụ | Ký hiệu | Trọng số | Đo |
|-----|---------|----------|-----|
| Team Completion | 🤝 | 35% | % ngày cả đội cùng hoàn thành / ngày có Mission |
| Family Streak | 🌟 | 25% | độ dài + ổn định streak nhà |
| Help Each Other | 💙 | 20% | nudge gửi + (tuỳ chọn) em xong trong cửa sổ sau nudge |
| Team Unlock | 🎉 | 10% | số unlock đội trong kỳ |
| Family Harmony | 🏡 | 10% | tín hiệu đội (ít open/overdue tổng) — **không** gắn “ai kém” trên hero |

**Không** công bố bảng xếp hạng điểm cá nhân cạnh Cooperation Score.

---

## 7. Mô hình miền (target)

Tận dụng: `day_flow` · `commitment` · `accountability-glance` · `accountability_option` · `agreement` · Coach insight.

Net-new (theo phase):

| Khái niệm | Mô tả |
|-----------|--------|
| **TeamDay** | Snapshot derive: `teamDone`, `teamTotal`, `teamPercent`, `remainingMissions`, `teamComplete` |
| **TeamUnlock** | Reward `unlock_scope = family` + parent confirm |
| **HelpOffer / TeamNudge** | Lời mời anh chị nhắc + tin tới em |
| **FamilyCombo** | Rule cửa sổ giờ + tags `morning` / `night` |
| **FamilyBoss** | Mục tiêu tuần đa commitment |
| **TeamBadge** | Catalog huy hiệu nhà + earn events |
| **CooperationScore** | Aggregate + cache ngày/tuần |

### Điều kiện ngày đẹp đội (Team Streak)

Mọi trẻ **có Mission trong ngày** đều `done` đúng điều kiện ngày đẹp hiện tại (late-done không tính), hoặc có grace/exception `accepted` → `team_beautiful_day`.

Copy hero khi còn thiếu:

> 🎯 Cả đội còn **N** Mission nữa để hoàn thành ngày hôm nay.

Không:

> Đức Huy còn 1 việc.

---

## 8. Sibling Nudge — guardrails

- Chỉ thành viên cùng `family_id`, role `child`.
- Rate-limit theo ngày; có “Để sau” / “Để bố mẹ nhắc”.
- Template tin an toàn (“Cố lên em”) — nhà có thể tắt hiện tên Mission.
- Không giao quyền duyệt progress cho anh chị.
- Không tính Help nếu spam / em báo khó chịu (sau này).
- Ưu tiên mời con đã 100% / lớn hơn (cấu hình được).

---

## 9. API / capability (target)

Capabilities (gate pack, chưa bật prod):

- `family_team_play`
- `sibling_nudge`
- `team_unlock`
- `family_cooperation_score`

Endpoints tối thiểu:

| Method | Path | Phase |
|--------|------|-------|
| GET | `/families/{id}/team-day` | TP1 · **shipped local** |
| GET | `/families/{id}/cooperation-score?period=week` | TP4 |
| POST | `/families/{id}/team-nudges` | TP3 |
| POST | `/families/{id}/team-nudges/{nudgeId}/send` | TP3 |
| GET | `/families/{id}/team-unlocks?ensure=` | TP2 · **shipped local** |
| POST | `/families/{id}/team-unlocks/ensure` | TP2 · **shipped local** |
| POST | `/families/{id}/team-unlocks/{id}/confirm` | TP2 · **shipped local** |

Coach proposal codes mới: `invite_sibling_nudge` · `rescue_streak` · `celebrate_team_day` · `suggest_team_unlock_confirm`.

---

## 10. Lộ trình

| Phase | Phạm vi | Status |
|-------|---------|--------|
| **TP0** | Language: hero nhà, copy đội, ẩn “X còn N việc” trên hero | Prototype UI |
| **TP1** | Team % (client/API), Team Streak surface, Celebration lite, Rescue copy | Prototype UI (derive client) |
| **TP2** | Team Unlock + parent confirm | **Local shipped** (migration 201) |
| **TP3** | Sibling Nudge + Help signal | Planned (UI tease trên prototype) |
| **TP4** | Cooperation Score API + Overview | Planned (UI stub trên prototype) |
| **TP5** | Combo + Boss tuần | Later |
| **TP6** | Badges + Celebration rich (nhạc / ảnh) | Later |

**Demo Differentiation sớm:** TP0 → TP1 → TP3 → TP2.

---

## 11. Acceptance — Prototype UI (local)

- [x] Brief này (`family-team-play-v1.md`)
- [x] Admin Overview: hero **Gia đình hôm nay** (% đội + thanh + copy Mission đội)
- [x] Admin Overview: chi tiết từng con vẫn xem được (chip / card phụ) — không leaderboard hero
- [x] Admin Overview: Team Streak + Celebration lite khi đội 100%
- [x] Admin Overview: Cooperation Score stub (derive client)
- [x] Kid home: dải **Nhà mình** / Mission đội cạnh mục tiêu cá nhân
- [x] `GET team-day` — shipped local (TP2 bundle)
- [ ] Sibling Nudge gửi thật — chưa (chỉ CTA / copy tease)
- [x] Team Unlock engine — shipped local (ensure + parent confirm)
- [ ] Không thêm vào `migration-files.prod.txt` cho đến khi chốt deploy

**Local:** `DEMO_FAMILY` / `admin` / `Admin@123` · Admin `:5173` · family-app `:5178`

---

## 12. Anti-patterns (cấm)

- Leaderboard anh chị em trên màn chính  
- Push “X chưa làm” tới cả nhà / ngoài nhà  
- Thưởng cá nhân lấn át thưởng đội trên cùng một ngày đẹp đội  
- AI tự bịa thưởng / hậu quả  
- Biến anh chị thành quản đốc duyệt việc  

---

## 13. Liên kết

- Pack brief: [family-os-pack-brief-v1.md](./family-os-pack-brief-v1.md)  
- Pack code: `src/Packs/FamilyOS/`  
- Prototype UI: `client/admin/.../FamilyOsOverviewPage.tsx` · `client/family-app/.../KidFocusView.tsx`
