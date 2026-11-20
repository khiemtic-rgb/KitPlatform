# FAMIXA PRODUCTION OS — READ-ONLY HANDOFF (UI SHOT IDENTITY)

Date: 2026-09-22  
Mode: READ-ONLY. No code change. No DB write. No job. No POST.

This document is the state before any further UI shot-identity fix.

Verdict vocabulary: **CONFIRMED** (live GET or file read this pass) · **UNCONFIRMED** (code exists, not proven on the user’s open desk) · **UNKNOWN** (not observed).

---

## 1. WHAT HAS BEEN IMPLEMENTED

| Change | File / function | Purpose | Status |
|---|---|---|---|
| Motion Engine Picker | `FamixaMotionEnginePicker.tsx` | Label **Model video** from `selectionProviderId`/`selectionModelId` only. `engine` is `void`. `Thay đổi` toggles Select. Optional `busy`. | **CONFIRMED** in source. Display on user’s current desk: **UNKNOWN**. |
| Label helper | `formatDirectorVideoModelLabel` in `famixa-director-video-model-display.ts` | `runway`/`gen4_turbo` → `Runway · Gen-4 Turbo`; `google`/`veo-3.1` → `Google Veo 3`; empty → `Chưa xác định`. | **CONFIRMED** |
| Fail-closed persist | `ContentFamixaSeriesTab.tsx` `persistEngine` | POST `/ai-selections` first; commit `osVideoModelByShot` only from 2xx + `selectionId`. Fail → revert + toast. | **CONFIRMED** in source. Live POST of official SH02 Veo was done in a prior mission (`c52b646f-…`). User-desk click path: **UNCONFIRMED**. |
| Fail-closed hydrate | same file, `useEffect([studioShotId, state.buildId])` | GET 200 → server row. GET 404 → `undefined`. Other errors → prior row only if `selectionId` present. | **CONFIRMED** in source. Hydrate on user’s desk after reload: **UNCONFIRMED**. |
| Director Timing Picker | `FamixaDirectorTimingPicker.tsx` | **Production duration** from `osDirectorTiming`. 5/10 buttons POST `director-timing`. | **CONFIRMED** in source. |
| Timing hydrate | `directorTimingFromPreview` + `setOsDirectorTimingByShot` | Preview `durationSec` + `timingStatus===AUTHORITATIVE` → `5 giây` / `10 giây`. | **CONFIRMED** in source |
| Timing persist | `persistDirectorTiming` | POST `/director-timing`; no graph seconds; no CREATE_VIDEO. | **CONFIRMED** in source. Official SH02 10s snap `6505b8a2-…` exists (prior mission). |
| Desk wiring | `ShotProductionWorkspace.tsx` + `ContentFamixaStudioView.tsx` | Pass `osVideoModel` / `osDirectorTiming` / busy flags into both pickers. | **CONFIRMED** |
| Shot/build resolution | `productionOsBuildIdForShot` / `isProductionOsTestShot` | TEST shot codes → TEST build `bbbbbbbb-0000-4000-8000-000000000371`. Else film `state.buildId`. | **CONFIRMED** |
| TEST prepend | `deskShots` = `[...productionOsTestShotStubs(), ...official shots]` when `prodMode==='director'` | Puts TEST-SC01-SH01/SH02 first on BÀN SHOT. | **CONFIRMED** |
| create-video-input-preview | client GET in hydrate; server `GetCreateVideoInputPreview` | Timing + selection fields for the **resolved** `buildId`+`shotCode`. | **CONFIRMED** (live GET this pass) |

Not claimed as “fixed”: user’s open BÀN SHOT matching official SH02. That is **UNCONFIRMED**.

---

## 2. CURRENT CANONICAL STATE

Live GET this pass (login KIT_MKT, no writes).

### Official `EP01-SC01-SH01`

| Field | Value | Verdict |
|---|---|---|
| studioShotId (if this shot is open) | `EP01-SC01-SH01` | CONFIRMED as OS key |
| buildId | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` | CONFIRMED |
| provider / model | `runway` / `gen4_turbo` | CONFIRMED |
| selectionId | `9b0dceee-ba53-4cc5-8139-ee9ba58a2011` ACTIVE | CONFIRMED |
| timing | `5` AUTHORITATIVE | CONFIRMED |
| snapshotId | `7dbaa094-55ff-4eea-8413-8e8ce2cbf254` | CONFIRMED |
| CREATE_VIDEO | `available=false` `OFFICIAL_FILM_FORBIDDEN` | CONFIRMED |

### Official `EP01-SC01-SH02`

| Field | Value | Verdict |
|---|---|---|
| studioShotId (if this shot is open) | `EP01-SC01-SH02` | CONFIRMED as OS key |
| buildId | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` | CONFIRMED |
| provider / model | `google` / `veo-3.1` | CONFIRMED |
| selectionId | `c52b646f-86b9-4a43-834d-d94ab9f013b8` ACTIVE | CONFIRMED |
| timing | `10` AUTHORITATIVE | CONFIRMED |
| snapshotId | `6505b8a2-8a71-4d74-8d93-f1fae8319b6b` | CONFIRMED |
| CREATE_VIDEO | `available=true` (no block) | CONFIRMED |

### TEST `TEST-SC01-SH02`

| Field | Value | Verdict |
|---|---|---|
| studioShotId (if this shot is open) | `TEST-SC01-SH02` | CONFIRMED as OS key |
| buildId | `bbbbbbbb-0000-4000-8000-000000000371` | CONFIRMED |
| provider / model | `runway` / `gen4_turbo` | CONFIRMED |
| selectionId | `cb04684f-e6bc-42fb-86d1-d2f850d2c0cc` ACTIVE | CONFIRMED |
| timing | `5` AUTHORITATIVE (preview; `performanceSnapshotId` empty) | CONFIRMED |
| snapshotId | empty on preview | CONFIRMED |
| CREATE_VIDEO | `available=false` `MISSING_PERFORMANCE_SNAPSHOT` | CONFIRMED |

Which of these three is `studioShotId` on the user’s screen right now: **UNKNOWN**.

---

## 3. CURRENT UI RESOLUTION PATH

```
prodMode==='director'
  deskShots = [TEST-SC01-SH01, TEST-SC01-SH02, …episode shots]
       ↓ selectedShotId={studioShotId}
ShotProductionWorkspace
  currentId = resolveLaneAShotId(deskShots, studioShotId)
  current   = shot with current.id
       ↓
ContentFamixaSeriesTab hydrate (studioShotId, state.buildId)
  buildId = productionOsBuildIdForShot(filmBuildId, studioShotId)
  GET /ai-selections?buildId&shotCode=studioShotId&stage=VIDEO
  GET …/create-video-input-preview?episodeCode&sceneCode
       ↓
  osVideoModelByShot[studioShotId]
  osDirectorTimingByShot[studioShotId]
       ↓ BÀN SHOT props (no active.id fallback)
  FamixaMotionEnginePicker(selectionProviderId/ModelId)
  FamixaDirectorTimingPicker(timing)
```

BÀN SHOT (`ShotProductionWorkspace`):  
`osVideoModel={studioShotId ? osVideoModelByShot[studioShotId] : undefined}` — **CONFIRMED**.

TẠO VIDEO (`ContentFamixaStudioView`):  
`(studioShotId && osVideoModelByShot[studioShotId]) || osVideoModelByShot[active.id]` — **CONFIRMED**. Second clause can paint **active** shot (often SH01) if `studioShotId` is empty.

`engine` / `loadEngine` / `localStorage['kit.famixaSeries.engine']`: discarded in picker (`void _engine`). Does not set Model video. **CONFIRMED**. May still label CTA `Tạo video · Google Veo 3`. That CTA is not Model video SoT.

`shot.seconds` is not `FamixaDirectorTimingPicker` status. **CONFIRMED**.

---

## 4. WHY UI CAN SHOW RUNWAY + 5s

Rows that **are** `runway` / `gen4_turbo` / `5s` on live GET:

1. Official `EP01-SC01-SH01` — **CONFIRMED**
2. TEST `TEST-SC01-SH02` — **CONFIRMED**

Official `EP01-SC01-SH02` is **not** that pair — **CONFIRMED**.

Path that paints it:

1. `studioShotId` is one of those two ids.
2. Hydrate GET returns runway + preview `durationSec=5` AUTHORITATIVE.
3. `formatDirectorVideoModelLabel('runway','gen4_turbo')` → `Runway · Gen-4 Turbo`.
4. `formatDirectorProductionDuration({ durationSec: 5, timingStatus: 'AUTHORITATIVE' })` → `5 giây`.

No source-read this pass shows official SH02 GET being overwritten to Runway/5s. **CONFIRMED** (code + live GET).  
Whether the user’s desk `studioShotId` is SH01 or TEST-SC01-SH02: **UNKNOWN**.

Default desk open: `resolveLaneAShotId(deskShots, undefined)` → `deskShots[0]` = `TEST-SC01-SH01` (Runway-less until that shot’s GET). “Shot 02” in that first scene = `TEST-SC01-SH02` = Runway + 5s. **CONFIRMED** in source.

---

## 5. SHOT IDENTITY RISK

| Distinguisher | In OS / GET | On BÀN SHOT heading today |
|---|---|---|
| Full shotCode | `TEST-SC01-SH02` / `EP01-SC01-SH01` / `EP01-SC01-SH02` | **Not rendered** |
| OS buildId | official vs `bbbbbbbb-…` | **Not rendered** |
| Heading | — | `Shot {index}-{current.shot}` e.g. TEST stub `shot:'02'` → `Shot 02-02`; official often `Shot 02-SH02` |
| Scene note | — | `TEST-SC01` vs `SC01` |
| Story | — | `TEST fixture story` vs Linh vs Minh |

Can a Director tell the three shots apart from **code+OS**? Yes. **CONFIRMED**.  
Can they tell from **heading “Shot 02” alone**? No. **CONFIRMED**.

`deskShots` prepends TEST fixtures in director mode. First scene is TEST (2 shots). Index 02 ≠ official SH02. **CONFIRMED**.

User-desk `studioShotId` at last screenshot: **UNKNOWN**.

---

## 6. UNTOUCHED / DO NOT TOUCH

Do not write, POST, or job against:

| Artifact | Id / value |
|---|---|
| Official film build | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` |
| Official SH01 VIDEO selection | `9b0dceee-…` runway/gen4_turbo |
| Official SH01 timing snap | `7dbaa094-…` 5s DIRECTOR |
| Official SH02 VIDEO selection | `c52b646f-…` google/veo-3.1 |
| Official SH02 timing snap | `6505b8a2-…` 10s DIRECTOR |
| Official SH02 KEYFRAME | `91daaf4b-…` |
| TEST VIDEO selection | `cb04684f-…` (TEST scope only) |
| SH01 CREATE_VIDEO | remains `OFFICIAL_FILM_FORBIDDEN` — do not issue permit / CREATE_VIDEO |
| Official SH02 CREATE_VIDEO | evaluate `available=true` — **do not execute** |
| Graph `shot.seconds` / EP01 graph | not SoT; do not rewrite |
| NT_XUANHOA / Pharmacy / Family auth | out of pack |

Prior forensic/impl reports under `docs/novixa/03-solution/FAMIXA_*` are read artifacts, not licenses to mutate OS.

---

## 7. NEXT FIX (proposal only — not implemented)

**`ContentFamixaSeriesTab.tsx`**

- Stop treating TEST stubs as the default official desk list, or isolate them so `deskShots[0]` is not `TEST-SC01-SH01` when the Director opens official EP01.
- Keep `productionOsBuildIdForShot` + fail-closed hydrate.
- Do not use `active.id` as a silent fallback for official Model/Timing on the desk path.

**`ShotProductionWorkspace.tsx`**

- Render **`current.id`** (full shotCode) and OS **buildId** (or TEST vs FILM) next to “Shot 02-…”.
- Nav labels must not be index-only (`02`) when TEST and official both exist.

Do not “correct” the open row by copying official SH02 Veo/10s onto TEST or SH01.

---

## VERDICT ROLLUP

| Finding | Verdict |
|---|---|
| Official SH02 canonical = Veo / 10s / `c52b646f` / `6505b8a2` / CREATE_VIDEO available | **CONFIRMED** |
| Official SH01 canonical = Runway / 5s / `9b0dceee` / `7dbaa094` | **CONFIRMED** |
| TEST-SC01-SH02 canonical = Runway / 5s / `cb04684f` | **CONFIRMED** |
| Picker Model video reads `osVideoModel.selection`, not `engine` | **CONFIRMED** |
| Timing picker reads preview AUTHORITATIVE duration, not `shot.seconds` | **CONFIRMED** |
| `deskShots` prepends TEST; “Shot 02” in first scene = TEST-SC01-SH02 | **CONFIRMED** |
| User’s current `studioShotId` is official SH02 | **UNKNOWN** |
| User screenshot Runway+5s is TEST SH02 or official SH01 | **UNCONFIRMED** (only matching live rows; desk not read) |
| Fail-closed hydrate makes official SH02 desk show Veo/10s after reload | **UNCONFIRMED** (code path only) |
| Official SH02 CREATE_VIDEO executed | **CONFIRMED** it was **not** executed this handoff (read-only) |

STOP.
