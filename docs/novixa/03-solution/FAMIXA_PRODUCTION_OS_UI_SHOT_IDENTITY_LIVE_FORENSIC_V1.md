# FAMIXA PRODUCTION OS — LIVE BÀN SHOT IDENTITY FORENSIC V1

Date: 2026-09-22  
Mode: READ-ONLY. Login used only to GET. No POST to Production OS. No job. No code change.

This pass tried to resolve the two remaining UNKNOWN/UNCONFIRMED findings from `FAMIXA_PRODUCTION_OS_UI_SHOT_IDENTITY_HANDOFF_V1.md`.

---

## A. CURRENT BÀN SHOT IDENTITY

**Is the currently open BÀN SHOT official `EP01-SC01-SH02`?** **UNKNOWN**

### What was actually read

| Probe | Result | Verdict |
|---|---|---|
| Cursor-controlled tabs (63) | URLs are `/login` except one static image GET on `:5290`. No `/content/videos`. No `data-director-bench`. | **CONFIRMED** — Cursor browser is **not** on BÀN SHOT |
| Visible Chrome/Edge window titles | No Novixa / Famixa / BÀN SHOT title | **CONFIRMED** — cannot attach to a desk window |
| `studioShotId` React state | `useState<string>()` — not in URL, not in `kit.famixaSeries.v4`, not in query | **CONFIRMED** in source. Live value: **UNKNOWN** |
| User Chrome DOM / `localShotId` | Not readable this pass | **UNKNOWN** |

Therefore these live values are **not read** from an open desk:

`selected/current index` · `current.id` · `current.shot` · `studioShotId` · hydrate `buildId` · scene/episode on screen

Do not infer them from the label `Shot 02`.

### Proven mapping (code + official graph GET) — not the open row

`deskShots` when `prodMode==='director'`:

```
[0] TEST-SC01-SH01   shot='01'  scene/sceneId=TEST-SC01   buildId=bbbbbbbb-0000-4000-8000-000000000371
[1] TEST-SC01-SH02   shot='02'  scene/sceneId=TEST-SC01   buildId=bbbbbbbb-0000-4000-8000-000000000371
[2] EP01-SC01-SH01   shot=SH01  scene/sceneId=SC01        buildId=4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f
[3] EP01-SC01-SH02   shot=SH02  scene/sceneId=SC01        buildId=4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f
[4] EP01-SC01-SH03   shot=SH03  scene/sceneId=SC01        buildId=4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f
[5] EP01-SC01-SH04   shot=SH04  scene/sceneId=SC01        buildId=4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f
[6] EP01-SC01-SH05   shot=SH05  scene/sceneId=SC01        buildId=4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f
[7] EP01-SC01-SH06   shot=SH06  scene/sceneId=SC01        buildId=4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f
```

Official shot ids from live `GET /api/content/series/builds/4ef164d6-…` `graph.episode.shots` — **CONFIRMED**.  
TEST stubs from `productionOsTestShotStubs()` — **CONFIRMED**.

Selector path:

```
studioShotId (useState, set by onSelectShot / onOpenShot / focusStudioShot)
  → ShotProductionWorkspace selectedShotId
  → currentId = resolveLaneAShotId(deskShots, selectedShotId || localShotId)
  → current = deskShots.find(id === currentId)
  → hydrate buildId = productionOsBuildIdForShot(filmBuildId, studioShotId)
```

If `studioShotId` is empty: `resolveLaneAShotId` returns `deskShots[0]` = `TEST-SC01-SH01`, but hydrate `if (!shotId) return` and BÀN SHOT pickers receive `undefined` → labels `Chưa xác định` / `Chưa quyết định`. That default is **not** Runway + 5s. **CONFIRMED** in source. Whether the user’s desk is in that empty state: **UNKNOWN**.

Heading (do not use as identity):

| `current.id` | Heading |
|---|---|
| `TEST-SC01-SH02` | `Shot 02-02` (scene nav 02/02, note `TEST-SC01`) |
| `EP01-SC01-SH01` | `Shot 01-SH01` (scene nav 01/06, note `SC01`) |
| `EP01-SC01-SH02` | `Shot 02-SH02` (scene nav 02/06, note `SC01`) |

---

## B. RUNWAY + 5s SOURCE IDENTITY

The pair `Runway · Gen-4 Turbo` + `5 giây` is **not** the live GET of official `EP01-SC01-SH02`.

Live GET this pass (KIT_MKT, GET only):

| Shot | buildId | GET `/ai-selections` | GET `create-video-input-preview` |
|---|---|---|---|
| `EP01-SC01-SH01` | `4ef164d6-…` | `runway` / `gen4_turbo` / `9b0dceee-…` ACTIVE | `5` AUTHORITATIVE snap `7dbaa094-…` |
| `EP01-SC01-SH02` | `4ef164d6-…` | `google` / `veo-3.1` / `c52b646f-…` ACTIVE | `10` AUTHORITATIVE snap `6505b8a2-…` |
| `TEST-SC01-SH02` | `bbbbbbbb-…0371` | `runway` / `gen4_turbo` / `cb04684f-…` ACTIVE | `5` AUTHORITATIVE snap empty |

**Matching live state for Runway + 5s:** `TEST-SC01-SH02` **or** official `EP01-SC01-SH01`.  
**Official `EP01-SC01-SH02`:** excluded as GET source.  
**Which of the two matching rows is on the current desk:** **UNKNOWN** (desk not read).  
This is **not** claimed as the user’s screenshot.

Graph `episode.shots[].seconds` for official SH02 is `5` (editorial). `FamixaDirectorTimingPicker` does not read it. **CONFIRMED**.

---

## C. EXACT EVIDENCE / PATH

### Model video `Runway · Gen-4 Turbo`

```
osVideoModelByShot[studioShotId].selection
  ← hydrate GET /ai-selections?buildId=productionOsBuildIdForShot(film, studioShotId)&shotCode=studioShotId&stage=VIDEO
  ← FamixaMotionEnginePicker(selectionProviderId, selectionModelId)
  ← formatDirectorVideoModelLabel('runway','gen4_turbo')
```

`engine` / `localStorage['kit.famixaSeries.engine']`: `void _engine` — not this label. **CONFIRMED**.

BÀN SHOT prop: `osVideoModel={studioShotId ? osVideoModelByShot[studioShotId] : undefined}` — no `active.id` fallback. **CONFIRMED**.

### Production duration `5 giây`

```
osDirectorTimingByShot[studioShotId]
  ← hydrate GET …/create-video-input-preview
  ← directorTimingFromPreview({ durationSec, timingStatus })
  ← FamixaDirectorTimingPicker
  ← formatDirectorProductionDuration → '5 giây' only if AUTHORITATIVE (or DIRECTOR+EXPLICIT) and durationSec===5
```

`shot.seconds` is not this status text. **CONFIRMED**.

No source-read path paints official SH02 GET (`google`/`veo-3.1`/`10`) as Runway + 5s. GET 404 hydrates to `undefined` → `Chưa xác định`, not Runway. **CONFIRMED**.

---

## D. ARE THE TWO FINDINGS RESOLVED?

| Prior finding | Was | Now |
|---|---|---|
| User’s current `studioShotId` is official SH02 | **UNKNOWN** | Still **UNKNOWN** — desk not read |
| Screenshot / UI Runway+5s is TEST SH02 or official SH01 | **UNCONFIRMED** | **Partially resolved:** official SH02 **CONFIRMED excluded** as GET source of that pair. Remaining identity is `{TEST-SC01-SH02, EP01-SC01-SH01}`. Which of those two is current: still **UNKNOWN** |

STOP. No implementation. No writes.
