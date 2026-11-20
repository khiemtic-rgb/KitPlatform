# FAMIXA SH02 — UI DISPLAY vs CANONICAL STATE FORENSIC V1

Read-only. No writes. Cursor-controlled tabs were on `/login` or `/content/ops` — not the user’s BÀN SHOT. Runtime identity below is from live GETs + the exact render/hydrate path. The pair **Runway + 5s** is a live canonical of two other shots, not official SH02.

---

## A. SHOT ID

Cannot read `studioShotId` from the user’s Chrome (no desk tab in Cursor).

Director desk `deskShots` = `[TEST-SC01-SH01, TEST-SC01-SH02, …official shots]`. First scene nav is TEST only (`Shot 01 / 02`). Heading uses `current.id` / `current.shot`, not the string `EP01-SC01-SH02`.

| If desk shows | Runtime `current.id` | Scope |
|---|---|---|
| `TEST-SC01` + “TEST fixture story” + Shot 01/02 | `TEST-SC01-SH02` when index 02 | TEST `bbbbbbbb-0000-4000-8000-000000000371` |
| `SC01` + Shot 01-SH01 + Linh story | `EP01-SC01-SH01` | official `4ef164d6-…` |
| `SC01` + Shot 02-SH02 + Minh / bài kiểm tra | `EP01-SC01-SH02` | official `4ef164d6-…` |

Do not trust “Shot 02” alone.

---

## B. CANONICAL (GET now, unchanged)

**Official `EP01-SC01-SH02` / `4ef164d6-…`**

| | |
|---|---|
| Model | `google` / `veo-3.1` |
| SelectionId | `c52b646f-86b9-4a43-834d-d94ab9f013b8` ACTIVE |
| Timing | `10` / `AUTHORITATIVE` |
| SnapshotId | `6505b8a2-8a71-4d74-8d93-f1fae8319b6b` |
| CREATE_VIDEO | `available=true`, no block |

**Official `EP01-SC01-SH01`** — `runway` / `gen4_turbo` / `9b0dceee-…` / **5s** / snap `7dbaa094-…` / `OFFICIAL_FILM_FORBIDDEN`

**TEST `TEST-SC01-SH02` / `bbbbbbbb-…`** — `runway` / `gen4_turbo` / `cb04684f-…` / **5s** `AUTHORITATIVE` (preview; snap id empty) / CREATE_VIDEO `MISSING_PERFORMANCE_SNAPSHOT`

---

## C. UI MODEL SOURCE

- Component: `FamixaMotionEnginePicker` `data-current-video-model`
- Helper: `formatDirectorVideoModelLabel(selectionProviderId, selectionModelId)`
- Prop: `osVideoModel.selection` from `osVideoModelByShot[studioShotId]`
- Hydrate: `GET /ai-selections?buildId=productionOsBuildIdForShot(film, studioShotId)&shotCode=studioShotId`
- `engine` / `localStorage['kit.famixaSeries.engine']`: **voided** in picker — does not paint this label
- Graph `aiSelection`: not in this picker
- Fallback: GET 404 → `undefined` → “Chưa xác định” (not Runway)

**Runway · Gen-4 Turbo** ⇒ `osVideoModel.selection` is `runway`/`gen4_turbo` ⇒ hydrate shot was **SH01 or TEST-SC01-SH02**, not official SH02.

---

## D. UI TIMING SOURCE

- Component: `FamixaDirectorTimingPicker` `data-director-timing-status`
- Helper: `formatDirectorProductionDuration` if `timingStatus===AUTHORITATIVE` or DIRECTOR+EXPLICIT
- State: `osDirectorTimingByShot[studioShotId]` from `GET create-video-input-preview` (`directorTimingFromPreview`)
- `shot.seconds` / `episode.shots[].seconds` / `engine`: **not** this status text
- Graph 5s/10s (`setShortSeconds`) is a different control

**5 giây** ⇒ preview `durationSec=5` + AUTHORITATIVE ⇒ SH01 or TEST-SC01-SH02. Official SH02 preview is **10**.

---

## E. ROOT CAUSE

Canonical official SH02 is still **Veo / 10s / c52b646f / 6505b8a2**. The pixels **Runway + 5s** are the live canonical of **`TEST-SC01-SH02`** (desk prefix) or **`EP01-SC01-SH01`**. There is no hydrate overwrite that turns official SH02 GET Veo/10s into Runway/5s. Global `engine` does not write Model video or Production duration.

Most likely: BÀN SHOT is on the TEST (or SH01) row, not `studioShotId=EP01-SC01-SH02`.

---

## F. EXACT NEXT FIX (no impl)

`ContentFamixaSeriesTab.tsx` — `deskShots` (`productionOsTestShotStubs()` prepend).  
`ShotProductionWorkspace.tsx` — heading/nav must surface `current.id` + OS `buildId`.  
Do not “fix” by copying official SH02 Veo/10s onto the open shot.

STOP.
