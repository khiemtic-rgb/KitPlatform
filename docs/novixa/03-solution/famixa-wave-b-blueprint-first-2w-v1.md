# Famixa · Wave B — Blueprint-first (2 tuần)

**Mã:** KIT-PRD-FO-BP-WB2W-01 · **Pack:** FamilyOS / Famixa  
**Ngày:** 2026-07-30 · **Trạng thái:** Active scope  
**Phụ thuộc:** [`famixa-dna-blueprint-roadmap-v1.md`](./famixa-dna-blueprint-roadmap-v1.md) · [`famixa-blueprint-domain-gap-matrix-v1.md`](./famixa-blueprint-domain-gap-matrix-v1.md)

> **Mục tiêu 2 tuần:** AI Coach / Parent Brief / Coach Insight **bắt buộc đọc Blueprint (DNA)** trước khi nói — mỗi lời khuyên có `because` “vì nhà bạn…”.  
> **Không làm trong 2 tuần:** Growth Zone UI đầy đủ · Stage Timeline · Screen Wallet · Playbook CMS 50+ · đổi công thức Health Score.

---

## 1. Outcome cảm nhận (acceptance)

Phụ huynh Pro (hoặc trial=Pro) thấy:

1. **Brief / Coach** có ít nhất một câu gắn Stage / Values / Focus / Goals từ DNA khi `hasBlueprint`.  
2. **Coach Insight API** (server) prepend / gắn `blueprintBecauseVi` khi có Blueprint.  
3. Khi **chưa hydrate** Blueprint: không bịa DNA — giữ copy hiện tại + CTA hydrate / calibration nếu cần.  
4. Gap matrix §3 được cập nhật cột R = ✓ cho Brief · Coach client · Coach Insight.

Smoke (local `DEMO_FAMILY`):

- [ ] Onboarding xong → DNA card có Values/Focus  
- [ ] Home Brief `reasonVi` hoặc bullet chứa “Vì nhà bạn…” / values / focus  
- [ ] Coach tip `basedOn` chứa Blueprint because  
- [ ] `GET …/coach-insight` có `blueprintBecauseVi` (hoặc proposal đã gắn because)  
- [ ] Free teaser: không lộ Focus/Next đầy đủ; không 500 khi thiếu Blueprint

---

## 2. Scope 10 ngày làm việc

### Tuần 1 — Bắt buộc đọc (B1 core)

| Ngày | Việc | Owner path |
|------|------|------------|
| D1 | Gap matrix + scope (doc) | `docs/novixa/03-solution/*` |
| D1–D2 | Client `blueprint-context.ts` — `becauseFromDna` · apply coach/brief | `client/family-app/src/shared/value/` |
| D2–D3 | Wire `ParentBoardView` + `FamilyValuePanel` truyền `dnaCard` | family-app modules |
| D3–D4 | Server: `FamilyCoachInsightService` inject Blueprint → `blueprintBecauseVi` | Packs/FamilyOS |
| D5 | Map API client + smoke Brief/Coach | `family-os.api.ts` |

### Tuần 2 — Siết + Playbook stub + HS copy

| Ngày | Việc | Owner path |
|------|------|------------|
| D6 | Health Score / Báo cáo: 1 dòng “theo DNA” (không đổi điểm) | `FamilyValuePanel` / health UI |
| D7 | Playbook **starter index** 20 ID (doc + const) — chưa CMS | doc + `family-playbook-ids.ts` hoặc C# static |
| D8 | Calibration / sparse: nếu thiếu values → 1 câu hỏi thay vì generic coach | DNA / Brief |
| D9 | Capability gate: parenting_coach vẫn Pro; Free không lộ deep because | commercial |
| D10 | Cập nhật gap matrix · checklist smoke · ghi Wave B progress vào roadmap §7 | docs |

---

## 3. Hợp đồng kỹ thuật `because`

### 3.1 Nguồn ưu tiên (DNA card)

```text
1. valuesLabelsVi (≤2)
2. focusLabelsVi (≤2)
3. stageLabelVi
4. nextStepVi / coachTipVi (nếu không trùng)
5. growthBalanceLabelVi / calibrationLabelVi (phụ)
```

### 3.2 Form câu (VI)

- Có values + focus: `Vì nhà bạn chọn {values} và đang tập trung {focus}.`  
- Chỉ stage: `Vì nhà bạn đang ở giai đoạn {stage}.`  
- Sparse: `null` → không gắn câu giả.

### 3.3 Chèn vào đâu

| Surface | Field |
|---------|--------|
| Coach tip | `basedOn` += ` · {because}` |
| Home Brief | `primaryAction.reasonVi` prefix hoặc bullet[0] |
| Coach Insight DTO | `BlueprintBecauseVi` (+ optional mirror vào Proposal) |
| Health / Báo cáo | 1 dòng caption dưới score (không đổi số) |

---

## 4. Playbook starter (20 ID) — catalog stub

Chưa phải engine. Mục tiêu: **có ID ổn định** để Wave B+ gắn proposalCode.

| ID | Trigger (tóm tắt) | Domain |
|----|-------------------|--------|
| PB0001 | Con quên đánh răng | Habit / brush |
| PB0002 | Quên chuẩn bị cặp sáng | Habit / pack |
| PB0003 | Ngủ muộn lặp | Sleep |
| PB0004 | Bỏ qua đọc sách | Learning / values |
| PB0005 | Quá nhiều lần nhắc phụ huynh | Peace / goals |
| PB0006 | Việc quá giờ buổi tối | Twin evening |
| PB0007 | Con báo xong — chờ duyệt | Awaiting |
| PB0008 | Pattern quên ≥3/7 ngày | CoachInsight |
| PB0009 | Stage teen — tránh giọng trẻ nhỏ | Stage |
| PB0010 | Values Reading → ưu tiên đọc | Values |
| PB0011 | Focus tự lập / ít nhắc | Goals |
| PB0012 | School bubble competitive | Context L4 |
| PB0013 | Self-view lệch peer shock | Calibration |
| PB0014 | Growth Balance — tránh dễ hư | Growth Balance |
| PB0015 | Growth Balance — thiếu phấn đấu | Growth Balance |
| PB0016 | Movie Night / team incomplete | Team |
| PB0017 | Skip lý do forgot | Skip |
| PB0018 | Optional task done đúng giờ | Strength |
| PB0019 | Illusion risk hit | Behavior → Blueprint |
| PB0020 | Chưa hydrate Blueprint | Sparse ask |

ProposalCode hiện tại (`suggest_move_*`, …) giữ nguyên; map sang PB* dần ở tuần 2.

---

## 5. Non-goals (cấm scope creep)

- Form Settings 8 tầng Blueprint  
- Free-form LLM chat  
- Growth Zone dashboard 4–6 trục (chỉ stub copy nếu cần)  
- Merge Blueprint vào Pharmacy `migration-files.prod.txt`  
- Deploy VPS Family OS khi Pharmacy audit freeze còn hiệu lực  

---

## 6. Definition of Done Wave B-2w

- [x] Gap matrix published  
- [x] Scope 2w published  
- [x] Client because helper + Brief/Coach wired  
- [x] Server CoachInsight đọc Blueprint  
- [x] API field / mapping  
- [x] Health caption (D6 early)  
- [x] Playbook 20-ID stub  
- [x] Gap matrix R cột cập nhật  
- [x] Sparse path PB0020 (Brief `dna_setup` + Coach tip)  
- [x] Map proposalCode → PB0001 / PB0002 / PB0008 (+ server `PlaybookId`)  
- [x] Pure smoke script `scripts/smoke-blueprint-wave-b.mjs`  
- [x] Smoke DEMO_FAMILY UI pass (browser: Brief shows `Vì nhà bạn…` + DNA Values/Focus)  

---

## 7. Liên kết roadmap

Cập nhật mục B1 trong `famixa-dna-blueprint-roadmap-v1.md` §3:  
**B1 = Wave B-2w này** (Coach/Brief/Insight đọc Blueprint). B2–B4 giữ nguyên lịch dài hơn.
