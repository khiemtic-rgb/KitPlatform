# Relationship Engine v1 — Family Relationship OS (kernel)

**Mã:** KIT-PRD-FO-RE-01 · **Pack:** FamilyOS · **Version:** 1.0  
**Ngày:** 2026-07-31 · **Owner:** Product + Family OS  
**Phụ thuộc:** [family-os-pack-brief-v1.md](./family-os-pack-brief-v1.md) · [behavior-os-north-star-v1.md](./behavior-os-north-star-v1.md) · [parent-success-engine-v1.md](./parent-success-engine-v1.md) · [family-team-play-v1.md](./family-team-play-v1.md)  
**Trạng thái:** DESIGN · brief + Member Home P0 spec · **chưa code** · local only · Family OS park deploy vẫn áp dụng

---

## 1. Verdict

Famixa đang có nguy cơ được nhớ như **Task Manager** (nhắc việc → tick → thưởng). Định vị dài hạn đúng hơn:

> **AI Human Growth Operating System for Families**  
> với kernel mới: **Relationship Engine** — tìm thời điểm tốt nhất để tạo tương tác **người ↔ người**.

AI **không** thay cha mẹ / anh chị. AI **tạo cơ hội** để họ tương tác.

```
Sai:  AI → Con
Đúng: AI → Thành viên đang cầm máy → Người kia → Gia đình gần hơn → Memory → AI học
```

**Family OS** = lớp trải nghiệm (app, flow, billing).  
**Behavior OS** = Growth (đổi hành vi, sensor).  
**Relationship Engine** = tạo khoảnh khắc quan hệ.  
**Memory Engine** = lịch sử nhà (đã có `family_memory`; mở rộng lens theo member).

Định vị nội bộ có thể gọi *Family Relationship Operating System*. **Chưa** đổi brand ngoài trời cho đến khi P0 loop chạy và đo được.

---

## 2. Thesis

| Cũ | Mới |
|----|-----|
| Task → Done | Task → Emotion → Interaction → Memory → Habit → Relationship |
| AI nhắc / AI khen | AI đề xuất; **người** nói; app chỉ chuyển lời |
| Một “màn Con” / một “màn Bố mẹ” | **Màn hình của An, Bin, Mẹ, Bố** — cùng sân khấu, khác đạo diễn |
| DAU / % việc xong | Parent→Child interaction · Trigger accept · Child felt |

Task / commitment **không bỏ** — chúng là **sensor** (biết khi nào tiến bộ, khi nào nản). Sản phẩm người dùng cảm nhận là quan hệ, không phải danh sách.

---

## 3. Ba engine phối hợp

```
behavior_event + day flow + ritual + team nudge
        │
        ▼
┌─────────────────┐
│  Growth Engine  │  (Behavior OS) — tiến bộ / dip / autonomy
└────────┬────────┘
         │ signals
         ▼
┌─────────────────┐
│ Relationship    │  TriggerResolver — ai nên nói với ai, hôm nay
│ Engine          │  Member messages (parent_voice, sibling nudge, gratitude…)
└────────┬────────┘
         │ khi người gửi lời
         ▼
┌─────────────────┐
│ Memory Engine   │  family_memory + per-member lens
└─────────────────┘
```

| Engine | Làm | Không làm |
|--------|-----|-----------|
| Growth | Đo streak, skip, self-start, habit stage | Không spam “con chưa xong” lên hero nhà |
| Relationship | Max 2 Growth Trigger / ngày / viewer; draft lời; CTA gửi | Không chat AI thay người; không checklist cha mẹ |
| Memory | Lưu khoảnh khắc người→người | Không KPI xấu hổ công khai |

---

## 4. Nguyên tắc bắt buộc (DNA)

1. **AI đề xuất. Người nói. App chuyển lời.** — Con không thấy “AI khen”; thấy “Mẹ / Bố / Chị”.
2. **Mọi chiều quan hệ** — không chỉ mẹ→con: con→anh chị, con→bố mẹ, (P1+) bố↔mẹ.
3. **Member Graph** — mỗi cạnh (An↔Mẹ, An↔Bin…) có trạng thái “nóng / lạnh”; ưu tiên cạnh lạnh + khoảnh khắc vàng.
4. **Max 2 trigger / ngày / người cầm máy** — luôn có “Để sau / Không lần này”.
5. **Chỉ signal đo được** — cấm bịa “mẹ có vẻ mệt” nếu không có event / proxy đã schema.
6. **Không xấu hổ công khai** — khớp Team Play: không “An còn 70%” trên hero cả nhà; dip chỉ trong card 1:1 với người được mời hành động.
7. **Coach tip phải có CTA quan hệ** — hết tip bằng “Gửi cho con” / “Hẹn tối nay”; cấm AI↔người thay người↔người.
8. **Không đo screen time thiết bị** (P0–P1) — giữ Screen Boundary A+B; không MDM.
9. **Local trước** — mig qua `migration-files.family-os.txt`; không gộp Pharmacy auth; không deploy VPS trừ khi user bảo rõ.

---

## 5. Growth Trigger (primitive)

Mỗi trigger là object (không phải notification lung tung):

| Field | Ý |
|-------|---|
| `code` | `praise_streak` · `encourage_dip` · `cheer_sibling` · `thank_parent` · (P1: `birthday_wish`, `ritual_invite`, `caregiver_care`, …) |
| `viewerMemberId` | Người đang cầm máy |
| `toMemberId` | Người nhận lời (nếu có) |
| `whyNow` | Signal ngắn, rule-id (để audit; không hiện xấu hổ) |
| `titleVi` / `bodyVi` | Copy gợi ý cho viewer |
| `ctaLabelVi` | VD: “Gửi lời khen” |
| `draftBodyVi` | Draft 1 câu — viewer sửa được rồi gửi |
| `templateCode` | `praise` · `encourage` · `cheer_up` · … |
| `expiresAt` / `flowDate` | Hết hạn trong ngày |

**Resolver:** rule-based C# (P0). LLM sau này chỉ được **viết lời**, không invent luật / không bịa signal.

---

## 6. Message rails (người → người)

| Rail | From → To | Trạng thái |
|------|-----------|------------|
| Team nudge | Child → Child | **Đã có** (templates cố định; role matrix) |
| Gratitude | Child → Parent | **Đã có** (ít tùy chọn `to`) |
| **Parent voice** | Parent → Child | **P0 mới** — team-nudge **cấm** parent as from/to |
| (P1) Member voice mở rộng | Bố↔Mẹ, Con→Con ngoài nudge | Sau khi P0 ổn |

**Parent voice (P0 schema đề xuất)**

`pack_family.parent_voice_message`:

- `from_member_id` (parent/guardian) · `to_member_id` (child)  
- `template_code` (`praise` | `encourage` | `custom`) · `body_vi`  
- `status` (`sent` | `read` | `thanks`) · `flow_date`  
- Khi send → `family_memory` (kind `parent_voice` hoặc payload rõ trên kind hiện có)

Con nhận trong **inbox riêng** (“Lời từ mẹ/bố”), không trộn copy Foxy như lời AI.

---

## 7. Spec — Member Home personalization (P0)

### 7.1 Mục tiêu P0 (acceptance)

1. **Hai con** login / focus khác nhau → **không** cùng hero line quan hệ + **không** cùng CTA primary (trừ khi graph/signal trùng hợp hiếm).  
2. **Bố và mẹ** (`parentMembershipId` khác) → Morning Brief / trigger card **khác góc** (ưu tiên cạnh quan hệ với *parent đang login* + `effectiveChildFocus`).  
3. Bố/mẹ **gửi được** lời khen/động viên → con **thấy** trong inbox; con bấm đã đọc / cảm ơn.  
4. Max 2 trigger/viewer/ngày từ API; dismiss được.

### 7.2 Không còn “màn vai trò thuần”

| Lớp | Ví dụ |
|-----|--------|
| Vai trò (khung) | Kid: missions + Foxy shell; Parent: brief + house progress |
| Cá nhân (P0 bắt buộc) | CTA + copy + inbox theo `viewerMemberId` + Member Graph proxies |

**Cấm P0:** một pool câu “Hôm nay cố lên nhé” giống hệt mọi con; Parent Home chỉ đổi tên con focus mà không đổi trigger theo phụ huynh login.

### 7.3 Kid Home — bề mặt P0

**Viewer:** `childMemberId`  
**Nguồn:** `GET relationship/triggers?forMemberId=` + parent_voice inbox + team-nudge + gratitude state

| Slot | Hành vi cá nhân hóa |
|------|---------------------|
| Foxy / home line | Ưu tiên trigger #1 của *con này* (cheer / thank / “mẹ vừa gửi lời”) — không `stablePick` chung nhà |
| Inbox | (1) Parent voice chưa đọc (2) Sibling nudge — thứ tự theo thời gian |
| CTA strip | `cheer_sibling` nếu `canInvite` + còn mission đội — copy có **tên target**, khác em/chị |
| Thanks | `thank_parent` nếu chưa gửi gratitude hôm nay + có done |

**Hai con A/B — ví dụ chấp nhận được**

| | An (xong việc, canInvite) | Bin (còn việc, có lời mẹ) |
|--|---------------------------|---------------------------|
| Line | “Bin còn việc — gửi cổ vũ?” | “Mẹ vừa gửi lời cho bạn” |
| Primary CTA | Gửi lời cổ vũ → Bin | Đọc lời mẹ / Cảm ơn mẹ |
| Inbox | Trống hoặc cũ | Parent voice mới |

### 7.4 Parent Home — bề mặt P0

**Viewer:** `parentMembershipId` (đã có trên `ParentBoardView`)  
**Focus:** `effectiveChildFocus` (`all` | một con)

| Slot | Hành vi |
|------|---------|
| Card đầu (trong/sau Morning Brief) | 1–2 triggers từ resolver cho **parent này** |
| Focus một con | Ưu tiên streak/dip của con đó |
| Focus cả nhà | Chọn 1 “khoảnh khắc vàng” hoặc 1 cạnh lạnh nhất với *parent này* (proxy: chưa gửi parent_voice N ngày) |
| Sheet gửi lời | Style `ph-nudge-*`; draft sửa được; Gửi → parent_voice |
| Sibling nudge | **Giữ** — orchestrate anh chị; không thay parent_voice |

**Hai phụ huynh — ví dụ chấp nhận được**

| | Mẹ (login) | Bố (login) |
|--|------------|------------|
| Signal | An streak 7 ngày; mẹ chưa khen hôm nay | Bin dip; bố 3 ngày chưa gửi lời |
| Card | “An 7 ngày liên tiếp — một lời khen của mẹ…” | “Bin có vẻ mất nhịp — một lời động viên của bố…” |
| Draft | Xưng “Mẹ” | Xưng “Bố” (từ displayName / role label member) |

### 7.5 Trigger matrix P0 (chốt ship)

| Code | Viewer | Điều kiện tối thiểu (rule) | Rail |
|------|--------|----------------------------|------|
| `praise_streak` | Parent | Con có chuỗi done ≥ 7 **hoặc** milestone memory gần; chưa có parent_voice `praise` từ *viewer này* → con trong `flowDate` | parent_voice |
| `encourage_dip` | Parent | Con pending cao / skip proxy 2+ ngày (day-flow + behavior nếu sẵn); chưa encourage hôm nay từ viewer | parent_voice |
| `cheer_sibling` | Kid | `FamilyTeamRoleMatrix.canInvite` + team remaining ≥ 1 + có target | team-nudge (đã có) |
| `thank_parent` | Kid | Có ≥1 done hôm nay; chưa `gratitude` từ con trong `flowDate` | gratitude (đã có) |

Ưu tiên xếp hạng (cao → thấp): parent_voice chưa đọc (kid) → `praise_streak` → `encourage_dip` → `cheer_sibling` → `thank_parent`. Cắt còn ≤2.

### 7.6 API shape P0 (đề xuất)

Base: `/api/family-os/families/{familyId}`

| Method | Path | Ý |
|--------|------|---|
| GET | `/relationship/triggers?forMemberId=&flowDate=` | Max 2 triggers cho viewer |
| POST | `/parent-voice` | Body: from, to, templateCode, bodyVi → status `sent` |
| GET | `/parent-voice?forMemberId=&flowDate=` | Inbox của con (hoặc outbox parent) |
| POST | `/parent-voice/{id}/ack` | `read` \| `thanks` |

Auth / tenant: giữ pattern FamilyOS hiện có. Grant DB: `pharmacore` **và** `kitplatform` (học từ mig 254).

### 7.7 UI files (khi code)

| File | Việc |
|------|------|
| `ParentBoardView.tsx` | Trigger card + sheet parent_voice theo `parentMembershipId` |
| `KidFocusView.tsx` | Inbox parent_voice; cá nhân hóa `foxyHomeLine` / CTA theo triggers |
| `memberPersonalize.ts` | Map DTO → props UI (mỏng) |
| `family-os.api.ts` | Client types + fetch |
| `app.css` | Tái sử dụng `ph-nudge-*` / `kv2-*`; không skin mới nặng |
| Mig `255_…parent_voice…` + `migration-files.family-os.txt` | Schema |

### 7.8 Ngoài P0

Chi tiết backlog thông minh / cuốn hút → **§8 P1 backlog**. Không chặn ship P0.

---

## 8. P1 backlog — thông minh hơn · cuốn hút hơn

**Điều kiện vào P1:** P0 parent_voice + Member Home cá nhân hóa đã smoke local (2 con khác CTA, 2 phụ huynh khác card, gửi→inbox).

**Công thức giữ nguyên**

```
Signal thật → 1 cơ hội quan hệ → Người gửi 1 chạm
  → Người nhận cảm thấy → Memory thành câu chuyện
```

**Anti-smart (vẫn cấm ở P1):** chat AI dài thay người; badge áp lực bố mẹ; đo phút điện thoại để chấm điểm; >2 trigger/ngày/viewer; 5 push/ngày.

### 8.1 Thứ tự đề xuất (đòn bẩy)

| # | Item | Outcome | Phụ thuộc |
|---|------|---------|-----------|
| P1.1 | **Golden moment realtime** | Card ≤2 phút sau signal (streak 7, first self-start, nhà xong sớm) trên đúng viewer | TriggerResolver + behavior/day-flow events |
| P1.2 | **Lời chưa gửi** | Parent Home: gợi ý đã mở / draft chưa `sent` → 1 chạm gửi lại | parent_voice draft hoặc trigger dismiss≠sent |
| P1.3 | **Evening Circle** | Ritual tối opt-in: 1 câu cả nhà → mỗi người 1 câu ngắn → Memory | Ritual + `family_memory` |
| P1.4 | **Weekly Story** | Thẻ cuối tuần narrative (khen / đọc / streak / xin lỗi…) — không % task | Memory aggregate + PSE report |
| P1.5 | **Calibration 1 câu / tuần** | Soft profile (vd thích khen riêng) → DNA / Blueprint — không form CRM | Blueprint hydrate + Brief CTA |
| P1.6 | **Sibling thanks + combo đôi** | Cảm ơn sau nudge; An+Bin cùng xong → thưởng đội nhỏ | Team nudge ack + Team Unlock |
| P1.7 | **Foxy chỉ đạo diễn** | Copy kid: “Mẹ đang muốn nói…” → mở inbox người thật | parent_voice inbox UX |
| P1.8 | **Family Mode / mùa** | Thi · nghỉ hè · ốm · ông bà về — đổi nhịp trigger (ít task, nhiều ôm) | AFE Family Mode |
| P1.9 | **Bố ↔ mẹ + caregiver care** | Micro cảm ơn / phụ việc; ngôn ngữ mời, không “% việc do mẹ” | Member voice mở rộng |
| P1.10 | **Birthday / milestone reward picker** | AI hỏi bố mẹ muốn chúc / thưởng gì — nút sẵn | parent_voice + catalog |
| P1.11 | **Memory lens “của tôi”** | Timeline theo member, không dump cả nhà | `family_memory` filter |
| P1.12 | **Ông bà / adult thứ 3 (opt-in)** | Nhận Weekly Story / lời chúc — không cần full kid/parent app | Invite + share card |
| P1.13 | **LLM draft only** | Chỉ viết `draftBodyVi`; không invent luật / signal | Sau khi rule-based ổn |
| P1.14 | **Quiet hours + in-app first** | Push tối đa 1/ngày; ưu tiên khi mở app | Parent push infra |
| P1.15 | **Context chip progressive** | Cài đặt “Nhà mình”: vài chip bối cảnh / cách động viên từng con — **không** hỏi thu nhập | Blueprint + settings; xem trao đổi product 2026-07-31 |

### 8.2 Spec mỏng từng cụm ưu tiên

#### P1.1 — Golden moment realtime

- Fire khi event vừa ghi (không chỉ nightly job).  
- Gắn `code` mới hoặc boost rank: `praise_streak` / `first_autonomous` / `team_early_finish`.  
- Surface: viewer đúng vai (mẹ/bố/con); hết hạn trong vài giờ.  
- KPI: time-to-send sau milestone; missed golden recovery ↑.

#### P1.2 — Lời chưa gửi

- Trạng thái trigger: `suggested` → `opened` → `sent` | `dismissed`.  
- Card “Còn 1 chạm để An nhận lời của mẹ”.  
- Không spam lại nếu `dismissed` cùng `flowDate`.  
- KPI: convert `opened`→`sent`.

#### P1.3 — Evening Circle

- Opt-in ritual (đã có ritual card — mở rộng prompt xoay vòng).  
- 1 prompt/đêm cho cả nhà; câu trả lời = memory kind riêng hoặc `manual` + payload `evening_circle`.  
- Không chấm điểm; không bắt buộc đủ thành viên.  
- KPI: nights with ≥2 members answered / tuần.

#### P1.4 — Weekly Story

- Artifact 1 thẻ: đếm lời khen (parent_voice+nudge) · gratitude · ritual đọc · streak · (P1.9) xin lỗi nếu có.  
- Copy narrative, cấm “8/12 việc”.  
- Deep-link từng dòng → memory / inbox cũ.  
- KPI: open rate cuối tuần; renew narrative (PSE).

#### P1.5 — Calibration (thay form nặng)

- Max 1 câu/tuần trên Brief: chip có/không hoặc 2 lựa chọn.  
- Ghi Blueprint layer (values / child preference).  
- Mọi draft sau đó có `because` “vì nhà bạn…”.  
- **Cấm** form thu nhập; hoàn cảnh chỉ chip optional (P1.15).

#### P1.6 — Sibling chemistry

- Sau `ack` nudge = thanks: gợi ý em gửi cảm ơn anh/chị (template ngắn).  
- Combo đôi: 2 child cùng `done` 1 commitment cặp → proposal Team Unlock nhẹ.  
- Giữ Team Play: không leaderboard cá nhân trên hero.

#### P1.7–P1.8 — Đạo diễn + mùa

- Foxy/Famixa không bao giờ đứng tên lời khen chính.  
- Family Mode đổi trọng số resolver (mùa thi: ưu tiên `encourage_dip` + ôm; nghỉ hè: ritual chơi chung).

#### P1.9–P1.12 — Graph mở rộng

- Bố↔mẹ / caregiver: cùng primitive message, template riêng, opt-in.  
- Birthday / reward picker: AI hỏi, người chọn, người gửi.  
- Memory lens + ông bà: moat chia sẻ nội bộ, không phình task engine.

#### P1.13–P1.15 — Thông minh mềm

- LLM sau rule; Quiet hours; progressive chips trong Cài đặt “Nhà mình” / DNA — khớp AFE & Blueprint-first.

### 8.3 P1 explicit non-goals

| Không làm ở P1 | Lý do |
|----------------|-------|
| MDM / đo screen time thiết bị | Screen Boundary A+B đủ; lệch DNA |
| Form CRM thu nhập + tiểu sử | Mất trust; AFE cấm settings nặng |
| Free-form AI chat nuôi dạy | Phá “người nói” |
| Đổi brand ngoài trời sang Relationship OS | Đợi P0+P1.1–4 đo được |

### 8.4 Gợi ý sprint sau P0

1. **Sprint A:** P1.1 + P1.2 (+ cứng P1.7 copy)  
2. **Sprint B:** P1.3 + P1.4  
3. **Sprint C:** P1.5 + P1.15 chips  
4. **Sprint D:** P1.6 → rồi P1.8–P1.12 theo nhu cầu pilot  

---

## 9. KPI (đo Relationship, không đo task)

| KPI | Định nghĩa |
|-----|------------|
| **Trigger accept rate** | Gửi / (gợi ý − dismiss) theo tuần |
| **Parent→Child voice / tuần** | Số parent_voice `sent` |
| **Child felt rate** | `thanks` hoặc `read` / sent |
| **Unique viewer coverage** | % member có ≥1 trigger hợp lệ / tuần |
| **Missed golden recovery** | Streak ≥7 mà có praise trong 24h |

Không lấy DAU tick routine làm north-star Relationship.

Khớp PSE: trigger accept + voice tăng → câu hỏi #3 Parent Success (“thời gian chất lượng”) có signal cứng.

---

## 10. Anti-patterns (cấm)

| Cấm | Vì sao |
|-----|--------|
| AI tự gửi lời khen mang danh mẹ | Mất attribution cảm xúc |
| Cha mẹ chat AI về con thay vì gửi con | Regress chatbot nuôi dạy |
| Một template home cho mọi con | Phá personalization |
| Chấm điểm / streak cha mẹ bắt buộc | Biến bố mẹ thành task mới |
| “95% việc nhà do mẹ” kiểu đổ lỗi | Caregiver care chỉ P1+, ngôn ngữ mời không phán |
| Magic % không mẫu số | Mất trust (PSE) |

---

## 11. Liên hệ tài liệu / code hiện có

| Thành phần | Vai trò với RE |
|------------|----------------|
| Behavior OS | Sensor Growth |
| Parent Success Engine | Payer loop; RE cung cấp CTA “gửi lời” cho tip |
| Team Play sibling nudge | Rail child→child; RE không thay |
| `parentMembershipId` trên Parent Board | Key personalization bố/mẹ |
| `childMemberId` trên KidFocusView | Key personalization từng con |
| Screen Boundary | Không mở đo device time trong RE |

---

## 12. Lộ trình

| Phase | Ship | Trạng thái |
|-------|------|------------|
| **Brief** | Doc này (RE + Member Home P0 spec) | **Done** |
| **P0 code (local)** | parent_voice + TriggerResolver + Parent/Kid cards | Sau khi chốt brief |
| **P0 verify** | 2 con khác CTA; 2 parent khác card; gửi→inbox | Smoke local |
| **P1 Sprint B (local)** | P1.3 Evening Circle · P1.4 Weekly Story | **Now** |
| **P1 Sprint C+** | Calibration · sibling chemistry · … (§8) | Sau Sprint B |
| **Pilot VPS** | Chỉ khi user yêu cầu rõ | Park mặc định |

---

## 13. Open questions (không chặn P0)

1. Xưng hô draft: lấy từ `displayName` hay role label cố định (Mẹ/Bố)? — **P0 default:** role label nếu có, else displayName ngắn.  
2. Kind `family_memory`: thêm `parent_voice` vs tái dùng `gratitude` + payload — **P0 default:** kind mới nếu mig cho phép enum/check; không thì payload trên `gratitude`.  
3. Push notification parent_voice — **P0:** in-app only.

---

## 14. Checklist trước khi code P0

- [x] Brief RE + Member Home P0 (doc này)  
- [x] P1 backlog gắn vào §8  
- [x] Mig 257 + API TriggerResolver + parent_voice (local)  
- [x] ParentBoardView card + sheet  
- [x] KidFocusView inbox + personalize line/CTA  
- [ ] Build + smoke 2 con / 2 phụ huynh  
- [ ] Không deploy VPS除非 được yêu cầu  

---

**Một câu DNA**

> Famixa không có “màn hình Con” và “màn hình Bố mẹ”.  
> Famixa có màn hình của từng thành viên — AI chỉ đạo diễn để họ nói với nhau đúng lúc.
