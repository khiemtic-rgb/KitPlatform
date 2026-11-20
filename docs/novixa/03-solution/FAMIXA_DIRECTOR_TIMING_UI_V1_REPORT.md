# FAMIXA — DIRECTOR TIMING UI V1 REPORT

Date: 2026-09-22  
Scope: Admin Director UI → canonical `POST /api/content/production/director-timing`  
Official film: EP01 `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` / SC01 / `EP01-SC01-SH02` (isolation: `EP01-SC01-SH01`)  
Safety: Production OS contract unchanged. Gate unchanged. AI / provider / CREATE_VIDEO / permit = 0. Graph seconds not used as timing authority.

Every claim is classified: FACT / IMPLEMENTED / TESTED / LIVE_VERIFIED.

---

## 1. EXECUTIVE VERDICT

**PASS**

Director can set Production Duration (5 giây / 10 giây) on the existing BÀN SHOT / TẠO VIDEO desk. Confirm calls only:

`POST /api/content/production/director-timing`

After success, UI reloads Production OS actions / state / create-video-input-preview. CREATE_VIDEO does not auto-run.

On official SH02 after Director 10s:

| Item | Value | Class |
|---|---|---|
| Production duration UI | **10 giây** | LIVE_VERIFIED |
| Preview `timingStatus` | `AUTHORITATIVE` | LIVE_VERIFIED |
| Preview `durationSec` | `10` | LIVE_VERIFIED |
| PerformanceSnapshot | `6505b8a2-8a71-4d74-8d93-f1fae8319b6b` | LIVE_VERIFIED |
| CREATE_VIDEO.blockReason | **`MISSING_PRODUCTION_AI_SELECTION`** (not `TIMING_PRODUCTION_DECISION_REQUIRED`) | LIVE_VERIFIED |
| missingInputs | `FrozenProviderModel` | LIVE_VERIFIED |
| CREATE_VIDEO available | false — no auto run | LIVE_VERIFIED |

Authority remains PerformanceSnapshot `timingSource=DIRECTOR` + `decisionType=EXPLICIT_PRODUCTION_DECISION` + `productionDurationSec > 0`. Graph `shot.seconds` is not SoT. FACT

---

## 2. FORENSIC — WHERE THE CONTROL GOES

Existing Director path (no new screen):

Admin `/content/videos` → Series Famixa — EP01 (6 shot, official) → Short → **Sản xuất shot** → open shot → **BÀN SHOT**.

Second surface: **TẠO VIDEO** card in `ContentFamixaStudioView` (same picker, same persist).

Placement: immediately under **Model video** (`FamixaMotionEnginePicker`), above Khớp môi. FACT / IMPLEMENTED

The existing graph **5s / 10s** buttons (`setShortSeconds` / `onSeconds`) were **not** reused. Those remain editorial I2V clock only. FACT

---

## 3. FILES

Created:

| File | Role |
|---|---|
| `client/admin/src/modules/content/famixa-director-timing.ts` | Authority helper: 5/10 options, `Chưa quyết định`, DIRECTOR+EXPLICIT / preview AUTHORITATIVE |
| `client/admin/src/modules/content/FamixaDirectorTimingPicker.tsx` | Production duration label + status + confirm buttons |
| `client/admin/src/modules/content/famixa-director-timing-ui.smoke.ts` | Contract smoke A–G |
| `docs/novixa/03-solution/FAMIXA_DIRECTOR_TIMING_UI_V1_REPORT.md` | This report |

Modified (UI wire only — no OS contract / gate / provider / graph schema):

| File | Change |
|---|---|
| `ContentFamixaShotProduction/ShotProductionWorkspace.tsx` | Picker under Model video when `onDirectorTiming` |
| `ContentFamixaStudioView.tsx` | Same picker on TẠO VIDEO |
| `ContentFamixaSeriesTab.tsx` | `osDirectorTimingByShot`, `directorTimingBusy`, hydrate from preview (no `claim.timing`), `persistDirectorTiming` |

Not touched (confirmed by scope / grep of persist):

Provider resolver, provider registry, CREATE_VIDEO application, ProductionTask, Snapshot contract, ShotExecutionState, Picture / Motion / LipSync / Assembly pipelines, SH01 timing row, existing AI selections, graph schema.

---

## 4. API USED

Canonical only:

`POST /api/content/production/director-timing`

Client: `materializeProductionDirectorTiming` in `client/admin/src/shared/api/content.api.ts`  
Server: `ContentProductionController.MaterializeDirectorTiming` → `MaterializeDirectorTimingAsync` (existing). No new endpoint.

After 200: reload

- `GET .../shots/{buildId}/{shotCode}/actions`
- `GET .../shots/{buildId}/{shotCode}/state`
- `GET .../shots/{buildId}/{shotCode}/create-video-input-preview`

Hydrate on shot open uses the same preview GET. Claim sent to `/actions` omits `timing` so graph seconds cannot leak into evaluate. IMPLEMENTED

No POST until Director clicks **5 giây** or **10 giây**. Same duration already AUTHORITATIVE → skip POST. IMPLEMENTED

---

## 5. REQUEST BODY (ACTUAL)

```json
{
  "buildId": "4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f",
  "shotCode": "EP01-SC01-SH02",
  "episodeCode": "EP01",
  "sceneCode": "SC01",
  "durationSec": 5
}
```

Then the same body with `"durationSec": 10`. LIVE_VERIFIED

DTO: `ProductionDirectorTimingRequestDto(BuildId, ShotCode, EpisodeCode, SceneCode, DurationSec)`. FACT

Failure probe (not a Director duration): `durationSec: 0` → 400. No snapshot write. LIVE_VERIFIED

---

## 6. STATE BEFORE / AFTER (OFFICIAL SH02)

### A — before any Director timing POST

| Item | Before | Class |
|---|---|---|
| CREATE_VIDEO.blockReason | `TIMING_PRODUCTION_DECISION_REQUIRED` | LIVE_VERIFIED |
| preview duration | `0` | LIVE_VERIFIED |
| UI (if opened then) | **Chưa quyết định** | TESTED (helper) + IMPLEMENTED |

### B — Director 5s

POST `durationSec: 5` → snapshot `cf1bdd40-0533-4452-a363-4135afc4e037`  
`timingSource=DIRECTOR` / `decisionType=EXPLICIT_PRODUCTION_DECISION` / `productionDurationSec=5`  
CREATE_VIDEO → `MISSING_PRODUCTION_AI_SELECTION` / missing `FrozenProviderModel`. LIVE_VERIFIED

### C — Director 10s (current tip)

POST `durationSec: 10` → new snapshot `6505b8a2-8a71-4d74-8d93-f1fae8319b6b` (5→10 is a new freeze; not overwrite).  
GET create-video-input-preview: `timingStatus=AUTHORITATIVE`, `durationSec=10`.  
CREATE_VIDEO still `MISSING_PRODUCTION_AI_SELECTION`. LIVE_VERIFIED

### After browser open of BÀN SHOT SH02 (reload path)

UI: **Production duration · 10 giây** · buttons 5 giây / 10 giây (10 selected).  
CREATE_VIDEO copy on desk: `MISSING_PRODUCTION_AI_SELECTION` · required includes Timing + FrozenProviderModel. LIVE_VERIFIED

SH02 KEYFRAME `91daaf4b-5550-4cbc-b9f9-0bd0be556cef` unchanged. LIVE_VERIFIED

---

## 7. TESTS

| ID | Result | Evidence |
|---|---|---|
| A SH02 no timing → chưa quyết định | PASS | Helper: missing / non-AUTHORITATIVE → `Chưa quyết định`. Live A: duration 0 + `TIMING_PRODUCTION_DECISION_REQUIRED`. |
| B Director 5s → POST + OS snapshot + blocker gone | PASS | Snap `cf1bdd40-…`; CREATE_VIDEO no longer TIMING_*. |
| C Director 10s → same | PASS | Snap `6505b8a2-…`; preview AUTHORITATIVE 10. |
| D POST error → no fake state | PASS | `durationSec: 0` 400; no graph seconds write; no local OS mutate; toast `Không lưu được Production duration.` |
| E Reload → OS authority | PASS | Fresh BÀN SHOT open after C shows 10 giây from preview GET, not `shot.seconds`. |
| F No auto CREATE_VIDEO | PASS | After B/C, CREATE_VIDEO `available=false`. Persist does not call `createProductionRequest` / permit / selection. |
| G SH01 unchanged | PASS | SH01 snap `7dbaa094-55ff-4eea-8413-8e8ce2cbf254` / 5s / CREATE_VIDEO `OFFICIAL_FILM_FORBIDDEN`. Persist has no SH01 hardcode. |

Smoke: `npx tsx src/modules/content/famixa-director-timing-ui.smoke.ts` → `FAMIXA_DIRECTOR_TIMING_UI_V1 ok`. TESTED

Browser: KIT_MKT admin → `/content/videos` → official EP01 6-shot → BÀN SHOT **Shot 02-SH02**. LIVE_VERIFIED

---

## 8. WHAT DID NOT HAPPEN

| Guard | Status |
|---|---|
| No AI / Runway / Veo / Wan / Fal / ElevenLabs call | CONFIRMED |
| No CREATE_VIDEO application / permit / provider run | CONFIRMED |
| No SH02 VIDEO AI selection written | CONFIRMED (blocker remains FrozenProviderModel) |
| No graph `shot.seconds` / `episode.shots[].seconds` write as timing authority | CONFIRMED |
| No copy of SH01 5s onto SH02 | CONFIRMED (SH02 latest is 10s; SH01 stays 5s) |
| No auto-pick 5 vs 10 | CONFIRMED (Director click only) |
| Graph 5s/10s editorial buttons left intact | CONFIRMED |
| Production OS contract / gate / snapshot schema unchanged | CONFIRMED |

---

## 9. AUTHORITY (UNCHANGED)

Production Timing SoT:

- PerformanceSnapshot
- `timingSource=DIRECTOR`
- `decisionType=EXPLICIT_PRODUCTION_DECISION`
- `productionDurationSec > 0`

Not SoT: `episode.shots[].seconds`, `shot.seconds`, SH01 timing, provider / Runway / Veo duration.

UI helper `isAuthoritativeDirectorTiming` accepts only DIRECTOR+EXPLICIT or preview `AUTHORITATIVE`. Provider-sourced duration is treated as **Chưa quyết định**. TESTED

---

## 10. NEXT BLOCKER (NOT THIS MISSION)

SH02 CREATE_VIDEO next: **`MISSING_PRODUCTION_AI_SELECTION` / FrozenProviderModel**.  
Do not persist a SH02 VIDEO selection or issue a permit unless a later mission says so.

STOP.
