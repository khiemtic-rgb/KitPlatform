# Famixa · Family Growth Blueprint™ — Gap Matrix Domain × Feature

**Mã:** KIT-PRD-FO-BP-GAP-01 · **Pack:** FamilyOS / Famixa  
**Ngày:** 2026-07-30 · **Trạng thái:** SoT audit (Wave A shipped · Wave B bắt đầu)  
**Phụ thuộc:** [`famixa-dna-blueprint-roadmap-v1.md`](./famixa-dna-blueprint-roadmap-v1.md) · [`famixa-wave-b-blueprint-first-2w-v1.md`](./famixa-wave-b-blueprint-first-2w-v1.md)

> **Câu hỏi bắt buộc trước mọi màn / Coach mới:**  
> *Thông tin này thuộc Domain nào trong Blueprint? Ai đọc? Ai ghi?*  
> Nếu không trả lời được → không ship như “AI của nhà”, chỉ là feature rời.

---

## 1. Chú giải cột

| Ký hiệu | Nghĩa |
|---------|--------|
| **R** | Feature **đọc** Blueprint (layers / DNA) khi quyết định copy hoặc đề xuất |
| **W** | Feature **ghi** Blueprint (hydrate / calibration / illusion / upsert) |
| **—** | Không đụng Blueprint |
| **EXISTS / PARTIAL / FRAGMENTED / MISSING** | Độ chín domain (nhìn tổng thể, không phải từng file) |

**Spine kỹ thuật (Wave A):**

| Artifact | Path |
|----------|------|
| Schema | `migrations/249_pack_family_blueprint.sql` → `pack_family.family_blueprint` |
| Contracts | `FamilyBlueprintContracts.cs` |
| Service / Hydrator | `FamilyBlueprintService.cs` · `FamilyBlueprintRepository.cs` (`FamilyBlueprintHydrator`) |
| API | `FamilyOsBlueprintController.cs` — GET/PUT blueprint · GET dna · POST hydrate · POST calibration |
| Client DNA | `FamilyDnaCard.tsx` · `fetchFamilyDnaCard` trong `family-os.api.ts` |

---

## 2. Scoreboard 10 Domain (đối chiếu phân tích Digital Twin)

| # | Domain (Blueprint™) | Status | Lớp nội bộ (8-layer) | Ghi chú 1 dòng |
|---|---------------------|--------|----------------------|----------------|
| 1 | Family Identity | PARTIAL | L1 Profile | Tên nhà / TZ / childCount; chưa cấu trúc nhà đầy đủ |
| 2 | Member Blueprint | FRAGMENTED | L3 Child (+ membership) | DOB/role + selfCalibration sparse; chưa trait/learning/personality |
| 3 | Development Stage | PARTIAL | L2 Stage | Age-band → label; chưa Stage Timeline / anticipation |
| 4 | Family Context | PARTIAL | L4 Context | School bubble lite; calendar/wallet/struggle chưa merge |
| 5 | Family Values | FRAGMENTED | L6 Values | Tag từ onboarding trên DNA; Coach chưa enforce |
| 6 | Family Goals | FRAGMENTED | L8 Goals | Blueprint goals ≠ Parent Goals ≠ routine outcomes |
| 7 | Growth Model | FRAGMENTED | L7 + Twin song song | Behavior Twin / Peace Twin / Growth Balance; Growth Zone chưa UI |
| 8 | AI Observation | PARTIAL | events / insight | `behavior_event`, CoachInsight — module-local |
| 9 | Growth Engine loop | PARTIAL | AFE + PSE + client | Có từng khâu; chưa 1 pipeline cite Blueprint |
| 10 | Playbook Library | MISSING | — | Không catalog; string template trong C# / client |

---

## 3. Ma trận Domain × Feature × R/W

### 3.1 Bề mặt sản phẩm (family-app + API)

| Feature | Domain chính | R Blueprint | W Blueprint | Evidence (file) | Gap |
|---------|--------------|:-----------:|:-----------:|-----------------|-----|
| DNA card | 1·3·5·6·9 | ✓ | — | `FamilyDnaCard.tsx` · `ParentBoardView.tsx` | Chỉ hiển thị; Free teaser cắt Focus/Next |
| Onboarding hydrate | 1·3·5·6 | — | ✓ | `FamilyValueService` · `POST …/hydrate` · `OnboardingPage` | W L1/L6/L8; không W L4 đầy đủ |
| Calibration capture | 2·4·7 | — | ✓ | `CalibrationCaptureSheet.tsx` · `POST …/calibration` · `FamilySelfCalibration` | W L3/L4 lite + Growth Balance |
| Illusion risk bump | 2·8 | — | ✓ | `FamilyBehaviorService` → `NoteIllusionRiskAsync` | Chỉ counter; không đóng loop Coach |
| Parent Home Brief | 8·9 | ✓ Wave B | — | `home-brief.ts` · `blueprint-context.ts` · `ParentBoardView` | R DNA → reason/bullets |
| Parenting Coach (client) | 3·5·6·9 | ✓ Wave B | — | `resolve-parenting-coach.ts` · `blueprint-context.ts` | R DNA → basedOn / doThis |
| Coach Daily Insight (server) | 8·9 | ✓ Wave B | — | `FamilyCoachInsightService.cs` | R DNA → `BlueprintBecauseVi` + proposal |
| Behavior Coach / Twin | 7·8 | — | partial W | Behavior services · `behavior_twin_snapshot` | Twin ≠ Blueprint Growth Model |
| Health Score | 7·9 | ✓ caption | — | `FamilyValuePanel` · `dnaCaptionForHealth` | Không đổi điểm; caption “vì DNA” |
| Family Value / ROP / Letter | 6·9 | — | — | `FamilyValuePanel.tsx` · `FamilyParentSuccessService.cs` | Chưa cite values/goals Blueprint |
| Routine / Day Flow | 4·6 | — | — | DayFlow services · admin Routine | Engine riêng; không đọc goals/stage |
| Family Timeline / Memory | 2·8 | — | — | `family_memory` · memories UI | Không gắn Growth History Blueprint |
| AFE Inbox / proposals | 8·9·10 | — | — | `ai_proposal` · adaptive engine | Proposal không index Playbook ID |
| Parent Goals (PSE) | 6 | — | — | `parent_goal` · Parent Success | Song song với L8 Blueprint |
| Screen Wallet | 4 | — | — | commercial + negotiate | Wave B roadmap item B2 — chưa Blueprint |
| Growth Zone UI | 7 | — | — | doc only | Wave B B4 — chưa code |
| Playbook CMS / catalog | 10 | — | — | — | MISSING; starter index trong Wave B doc |

\* Health Score: Wave B **không** đổi công thức điểm; chỉ bắt buộc copy “because” khi có DNA (tránh KPI giả từ Blueprint).

### 3.2 Domain → ai được phép ghi (policy)

| Domain | Writer được phép (Wave B) | Không được |
|--------|---------------------------|------------|
| Identity / Values / Goals | Onboarding hydrate · admin upsert có chủ đích | Coach tự mutate |
| Stage | Hydrate từ age band · (Wave C Timeline) | Health Score tự đổi stage |
| Member / Context | Calibration · illusion bump | Free-form LLM |
| Observation | Behavior events · day_flow (ngoài Blueprint JSON) | Ghi đè layers bằng prompt |
| Playbook | Catalog versioned (chưa có) | Hardcode phân tán không ID |

---

## 4. Đối chiếu 10 Domain phân tích ↔ 8 lớp Famixa

| Phân tích (10) | Famixa 8-layer / artifact | Khớp? |
|----------------|---------------------------|-------|
| Family Identity | L1 + `pack_family.family` | Một phần |
| Member Blueprint | membership + L3 | Yếu / phân mảnh |
| Development Stage | L2 | Label only |
| Family Context | L4 | Lite |
| Family Values | L6 | Tag |
| Family Goals | L8 (+ parent_goal riêng) | Phân mảnh |
| Growth Model | Twin + Growth Balance + (Growth Zone planned) | Phân mảnh |
| Observation Engine | behavior_event + CoachInsight | Có, chưa unify |
| Growth Engine | AFE + PSE + client brief | Có mảnh, chưa spine |
| Playbook Library | — | Thiếu |

---

## 5. Maturity vs lộ trình V1–V4 (phân tích Digital Twin)

| Bar | Đặt Famixa |
|-----|------------|
| V1 profile+stage+goals+values+routine+50 PB | ~45% — thiếu Playbook Library |
| V2 context+growth model+HS+Brief+200 PB | ~20% — HS/Brief có UI nhưng chưa Blueprint-native; Growth Zone/PB thiếu |
| V3 knowledge graph / cá nhân hóa lịch sử | Aspirational |
| V4 AI sinh playbook / dự đoán | Aspirational |

**Famixa waves:** A ≈ shipped · B = đang làm (doc này + 2w scope) · C = chưa.

---

## 6. Definition of Done cho “đã thống nhất”

Một feature được coi là **cắm Blueprint** khi thỏa **cả 3**:

1. **Domain rõ** trong bảng §3 (cột Domain chính không trống).  
2. **R hoặc W** có evidence trong code (không chỉ marketing).  
3. Copy / đề xuất có **`because`** trích được từ DNA hoặc layers (values · stage · focus · goals · calibration) — trừ khi sparse → hỏi 1 câu (Hiến pháp §7).

Checklist ship PR:

- [ ] PR mô tả Domain + R/W  
- [ ] Không thêm store song song cho cùng tín hiệu Blueprint  
- [ ] Free/Plus teaser không silent-fail capability  

---

## 7. Việc tiếp theo

1. Thực thi [`famixa-wave-b-blueprint-first-2w-v1.md`](./famixa-wave-b-blueprint-first-2w-v1.md).  
2. Mỗi PR Wave B cập nhật cột R/W trong §3 (không để matrix lệch code).  
3. Khi có Playbook catalog v0 → thêm hàng Domain 10 + index stage×values.
