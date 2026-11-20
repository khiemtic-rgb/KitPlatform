# FAMIXA PRODUCTION OS — FULL SHOT STATE + PREVIEW AUTHORITY CUTOVER V1

Date: 2026-09-19  
Mode: FORENSIC → IMPLEMENT → TEST → LIVE VERIFY  
Scope: Authority cutover for CURRENT / VISIBLE / ACCEPTED / PREVIEW on **OS-controlled shots**.  
Safety: provider calls this phase = 0. Media generation this phase = 0. EP01 / EP99 not written.

Every claim is classified: FACT / IMPLEMENTED / TESTED / LIVE_VERIFIED / ASSUMED / UNVERIFIED / RECOMMENDED.

---

## 1. Executive Verdict

**PASS_WITH_GAPS**

For OS-controlled shots, Production OS is now the authority plane for execution state and preview:

USER → Production OS → Request → Task → Snapshot → Attempt → Artifact → Binding → Shot State → Preview

`ProductionBinding` is the sole CURRENT / VISIBLE / ACCEPTED authority on that path.  
`ProductionArtifact` is the sole physical-media authority.  
Preview resolves only: **VISIBLE → artifactId → ProductionArtifact → storageKey → bytes**.  
If there is no VISIBLE binding, preview is **EMPTY**.

This phase did **not** delete the legacy film plane. Official EP01 / EP99 Director still reads `previewUrl` / `takeUrl` / `acceptedTake.url` as **legacy film display authority**. That is leftover by design (no over-cutover), and it is the main remaining gap.

CREATE_VOICE and physical FINAL remain out of scope, as required.

---

## 2. Exact forensic findings before implementation

Traced before any authority code change.

| Symbol | Read/Write | Current Role (before) | Target Role | Risk |
|---|---|---|---|---|
| `ProductionBinding` CURRENT / CURRENT:* | R/W | AUTHORITY (OS) | AUTHORITY | Low |
| `ProductionBinding` VISIBLE / VISIBLE:* | R/W | AUTHORITY (OS preview) | AUTHORITY | Low |
| `ProductionBinding` ACCEPTED / ACCEPTED:* | R/W | AUTHORITY (OS) | AUTHORITY | Low |
| `ProductionArtifact` | R/W | AUTHORITY physical | AUTHORITY | Low |
| `ResolveVisiblePreviewAsync` / `GetPreviewAsync` | R | AUTHORITY preview | AUTHORITY | Low — empty if no VISIBLE |
| `GetShotStateAsync` | R | AUTHORITY state from bindings | AUTHORITY | Low |
| `MaterializeOutputAsync` / `MaterializeMediaAsync` Bind* | W | AUTHORITY **without late-lineage guard** | AUTHORITY + protected bind | **HIGH** — older task could steal CURRENT/VISIBLE |
| `ApplyLateResultAsync` | W attempt | Does not bind | Same | Low |
| `BindAsync` | W | Overwrites any existing role | Explicit bind stays; auto-complete uses `BindProtectedAsync` | HIGH if left unguarded |
| `previewUrl` / `takeUrl` / `lipsyncUrl` | R/W | LEGACY AUTHORITY (official film) | LEGACY NON-AUTHORITY for OS | HIGH if OS preview used them |
| `acceptedTake.url` | R/W | LEGACY AUTHORITY | LEGACY NON-AUTHORITY for OS | HIGH |
| `fallbackMotionUrl` | R | BYPASS into player | DERIVED only when OS VISIBLE exists | HIGH |
| `assembleVideoUrl` | R | LEGACY assemble + preview fallthrough | LEGACY assemble only; **not** OS preview | HIGH |
| `resolveTakeUrl` | R | LEGACY AUTHORITY | LEGACY / DERIVED | HIGH |
| `SeriesShotRun` | R/W | LEGACY AUTHORITY official | LEGACY NON-AUTHORITY for OS | HIGH |
| `ShotProductionFinalStore` | R/W | LEGACY FINAL | CACHE / NON-AUTHORITY preview | Med |
| `nextShotProductionCommand` | R | DERIVED command; reads legacy URLs | DERIVED only; must not bind | Med |
| `osPreviewByShot` / `osStateByShot` | R/W | CACHE of OS GET | CACHE | Low |
| `kit-famixa-tts` | R/W | AUTHORITY official voice | CACHE (voice cutover later) | Med if treated as video preview |
| `content-local-image-library` / engine picker | R/W | UI MEMORY | UI MEMORY | Low |

FACT: the steal hole was automatic bind after materialize, not `ApplyLateResultAsync`.

---

## 3. Files changed

| File | Why |
|---|---|
| `ProductionWorkExecutionEngine.cs` | `BindProtectedAsync` late-lineage guard; preview carries role + mime + reason |
| `IProductionExecutionService.cs` / `ProductionExecutionService.cs` | Protected bind APIs; fixture complete uses protected bind |
| `ProductionCreateVideoService.cs` / `.Actions.cs` | Materialize persists artifact, refuses late CURRENT/VISIBLE steal |
| `ProductionApiDtos.cs` / `ContentProductionController.cs` | Preview DTO + media use VISIBLE artifact mime |
| `content.api.ts` | Client preview meta: role / mime / duration |
| `ShotProductionWorkspace.tsx` | TEST shot: no VISIBLE → empty; no graph / assemble / final fallthrough |
| `ShotProductionOrchestrator.ts` | Classified DERIVED / LEGACY; not rewritten |
| `LegacySeriesGraphAdapter.cs` | Catalog leftover non-authority fields |
| `ProductionShotStatePreviewAuthorityV1Regression.cs` | T01–T44 |
| `ProductionShotStatePreviewAuthorityV1Tests.cs` | xUnit host |
| `VideoGenerationExecutionService.cs` / `ProductionTestCreateVideoV1Regression.cs` | Wire suite |
| `famixa-shot-state-preview-authority.smoke.ts` | Source-path acceptance |
| `famixa-production-os-ui-cutover.smoke.ts` | Empty TEST preview |

---

## 4. Files NOT changed

Provider adapters, Runway / Veo / Gemini / ElevenLabs / Fal clients, P06 / KIT Video Engine, EP01 / EP99 / Canon / Master / DNA / PRP / CRP / Visual Authority / Visual Universe, Performance Contract semantics, Timing V3, Motion Contract, `HttpRunwayTransport`, schema migrations, `SeriesShotRun` schema, `ShotProductionFinalStore` implementation, `kit-famixa-tts` store, legacy `/series/*` endpoints (not deleted).

FACT.

---

## 5. Authority matrix BEFORE

| Concern | Authority before |
|---|---|
| CURRENT (OS) | `ProductionBinding` **and** late `Materialize*` could overwrite it |
| VISIBLE (OS) | `ProductionBinding` **and** late `Materialize*` could overwrite it |
| ACCEPTED (OS) | `ProductionBinding` (materialize did not auto-accept) |
| PREVIEW (OS API) | VISIBLE → artifact → bytes |
| PREVIEW (TEST UI) | OS GET if present; else could still enter graph / final resolvers |
| PREVIEW (official film) | `assembleVideoUrl` / `resolveTakeUrl` / `acceptedTake.url` |
| Shot state (OS API) | Bindings + tasks |
| Shot state (official desk) | `ShotProductionState` + `resolveTakeUrl` |

FACT (forensic).

---

## 6. Authority matrix AFTER

| Concern | Authority after |
|---|---|
| CURRENT (OS-controlled) | `ProductionBinding` only. Auto-complete uses `BindProtectedAsync`. Explicit user bind may still name an exact `artifactId`. |
| VISIBLE (OS-controlled) | `ProductionBinding` VISIBLE only. No latest-artifact / latest-URL. |
| ACCEPTED (OS-controlled) | `ProductionBinding` ACCEPTED. Late materialize cannot bind ACCEPTED. |
| PREVIEW (OS API + TEST UI) | VISIBLE → Artifact → `storageKey` → bytes. No VISIBLE → EMPTY. |
| PREVIEW (official film) | Still legacy URL fields. **LEGACY / NON-AUTHORITY for OS. Still AUTHORITY for official Director.** |
| Shot state (OS API) | `GetShotStateAsync` from bindings + dependency nodes |
| `nextShotProductionCommand` | DERIVED command projection. Cannot bind. |
| IndexedDB / localStorage | CACHE / UI MEMORY. Not OS preview authority. |

IMPLEMENTED.

---

## 7. CURRENT authority

Read: `GetShotStateAsync` → active `CURRENT` or `CURRENT:*`.  
Write (automatic): `BindCurrentProtectedAsync` — refuses superseded / LATE / older-than-active-binding lineage (`LATE_RESULT_NOT_CURRENT`).  
Write (explicit): `BindCurrentAsync(artifactId)` — exact artifact, user/API.

TESTED T02, T05, T15, T18, T37.  
LIVE_VERIFIED TEST-SC01-SH01 `currentArtifactId = 5872d0a9-0c3f-42b0-9fe5-21f1433ab831` = active CURRENT binding.

---

## 8. VISIBLE authority

Read: `ResolveVisiblePreviewAsync` loads **only** role `VISIBLE`. No CURRENT fallback. No latest artifact.  
Write: same protected vs explicit split as CURRENT.

TESTED T01, T04, T16, T19, T36.  
LIVE_VERIFIED TEST-SC01-SH01 `visibleArtifactId` = same `5872d0a9-…`.  
LIVE_VERIFIED TEST-SC01-SH02 `artifactId = 583d7700-…` (different shot, different artifact).

---

## 9. ACCEPTED authority

Read: `GetShotStateAsync.AcceptedArtifactId` from `ACCEPTED` binding.  
Automatic materialize does not bind ACCEPTED.  
Protected bind of an older task cannot overwrite ACCEPTED.  
Explicit `AcceptArtifactAsync(artifactId)` may bind that exact artifact.

TESTED T03, T06, T17, T20, T38.  
LIVE_VERIFIED TEST-SC01-SH01 `acceptedArtifactId = null` (no ACCEPTED binding on SH01). SH02 has ACCEPTED MIX `638bd185-…` in DB. FACT.

---

## 10. Preview authority

Canonical:

`GET /production/shots/{buildId}/{shotCode}/preview`  
→ `GetPreviewAsync`  
→ VISIBLE binding  
→ `ProductionArtifact`  
→ `storageKey`

`GET .../preview/media` reads those bytes and (on the new controller) uses artifact mime.

Empty rule: no VISIBLE → `{ empty: true, reason: VISIBLE_MISSING }`.

TESTED T01, T04, T07–T11 (source: OS path has zero `previewUrl` / `takeUrl` / `lipsyncUrl` / `acceptedTake` / `fallbackMotionUrl` / `assembleVideoUrl` / `latestTake` / `currentTake`).  
LIVE_VERIFIED SH01 preview artifact + SHA + `os-test/...` storageKey. Media SHA256 = `E6553595…C9080C4E` = artifact `byte_sha256`. Unknown shot SH99 empty. EP01-SC01-SH01 empty.

New DTO fields `role` / `mimeType` / `durationSec` are IMPLEMENTED. They are **not LIVE_VERIFIED** because the running API process was not rebuilt (file lock). Old process still returns artifactId / sha / storageKey / fileUrl only.

---

## 11. Shot State authority

`GetShotStateAsync` derives nodes from `ProductionTask` + `ProductionBinding` + `ProductionArtifact` + `ShotDependencyGraph`. It does not read SeriesShotRun or URLs.

TEST UI hydrates `osStateByShot` from GET `/state`. Desk CTAs stay 1:1 via existing `applyProductionOsActionAuthority`. `nextShotProductionCommand` is not used as artifact authority.

TESTED. LIVE_VERIFIED SH01 state `currentArtifactId` / `visibleArtifactId` match bindings.

---

## 12. Legacy paths still reachable

Still reachable, not deleted:

- `POST /series/still|turbo|lipsync`, assemble endpoints  
- `resolveTakeUrl` / `assembleVideoUrl` / `stampMuteTake`  
- Official Director player graph URLs  
- `kit-famixa-tts`, `ShotProductionFinalStore`, local image library  
- `nextShotProductionCommand`

FACT. Classified LEGACY / COMPATIBILITY.

---

## 13. Legacy paths still authoritative, if any

| Path | Role now |
|---|---|
| Official EP01/EP99 Director preview | **Still AUTHORITY** for official film display. LEGACY BYPASS — CUTOVER REQUIRED (future). |
| `kit-famixa-tts` official voice resolve | Still AUTHORITY for official-film TTS. Deferred CREATE_VOICE CUTOVER. |
| `ShotProductionFinalStore` | Still AUTHORITY for official FINAL blob. Not OS preview. Deferred FINAL. |
| OS-controlled TEST preview / state | **Not authoritative.** |

FACT.

---

## 14. Late-result protection

Implemented in `BindProtectedAsync`:

- same artifact → idempotent return  
- task SUPERSEDED → `LATE_RESULT_NOT_CURRENT`  
- attempt LATE → refuse  
- existing binding’s `BoundByTask.CreatedAt >=` incoming task → refuse  
- artifact may remain VERIFIED (historical)

`MaterializeOutputAsync` / `MaterializeMediaAsync` catch that reason and keep the existing binding.

`ApplyLateResultAsync` still does not bind.

TESTED T15–T17. Not LIVE_VERIFIED with a second real Runway job (would violate 0 provider calls).

---

## 15. Cross-shot protection

Preview lookup is `(buildId, shotCode)`. Artifact owner task must match. Planted cross-build VISIBLE throws `CROSS_SHOT_FORBIDDEN`.

TESTED T12, T13, T29.  
LIVE_VERIFIED SH01 artifact `5872d0a9-…` ≠ SH02 artifact `583d7700-…`. Unknown SH99 empty.

---

## 16. Cross-scope protection

TEST artifacts live under `os-test/{testRunId}/...`. Official film has **0** production_tasks and **0** active bindings. EP01 preview empty.

TESTED T14, T30. LIVE_VERIFIED.

---

## 17. Test matrix T01–T44

Suite: `FAMIXA_PRODUCTION_OS_SHOT_STATE_PREVIEW_AUTHORITY_CUTOVER_V1`  
xUnit: 17/17 Content.Tests passed, including this suite.  
Smoke: `famixa-shot-state-preview-authority.smoke.ts` PASS. `famixa-production-os-ui-cutover.smoke.ts` PASS.

| ID | Result | Class |
|---|---|---|
| T01 VISIBLE resolves | PASS | TESTED + LIVE_VERIFIED |
| T02 CURRENT resolves | PASS | TESTED + LIVE_VERIFIED |
| T03 ACCEPTED resolves | PASS | TESTED |
| T04 no VISIBLE → empty | PASS | TESTED + LIVE_VERIFIED (SH99) |
| T05 no CURRENT → empty | PASS | TESTED |
| T06 no ACCEPTED → empty | PASS | TESTED + LIVE_VERIFIED (SH01) |
| T07 previewUrl cannot override | PASS | TESTED (OS path source scan) |
| T08 takeUrl cannot override | PASS | TESTED |
| T09 acceptedTake.url cannot override | PASS | TESTED |
| T10 lipsyncUrl cannot override | PASS | TESTED |
| T11 fallbackMotionUrl cannot override | PASS | TESTED |
| T12 cross-shot rejected | PASS | TESTED + LIVE_VERIFIED |
| T13 cross-build rejected | PASS | TESTED |
| T14 cross-scope rejected | PASS | TESTED + LIVE_VERIFIED (EP01 empty) |
| T15 late cannot steal CURRENT | PASS | TESTED |
| T16 late cannot steal VISIBLE | PASS | TESTED |
| T17 late cannot steal ACCEPTED | PASS | TESTED |
| T18 CURRENT only via bind | PASS | TESTED |
| T19 VISIBLE only via bind | PASS | TESTED |
| T20 ACCEPTED only via accept/bind | PASS | TESTED |
| T21 old artifact immutable | PASS | TESTED |
| T22 story invalidates downstream | PASS | TESTED |
| T23 keyframe invalidates motion+ | PASS | TESTED |
| T24 motion invalidates lipsync+ | PASS | TESTED |
| T25 voice invalidates lipsync+ | PASS | TESTED |
| T26 lipsync invalidates mix+ | PASS | TESTED |
| T27 reload preserves OS binding | PASS | TESTED + LIVE_VERIFIED (DB still bound) |
| T28 reload does not resurrect legacy URL | PASS | TESTED |
| T29 TEST artifact stays on its shot | PASS | TESTED + LIVE_VERIFIED |
| T30 EP01/EP99 untouched | PASS | LIVE_VERIFIED |
| T31 no provider calls this phase | PASS | LIVE_VERIFIED (GET only; audit `startCount` unchanged at 1 from Phase G) |
| T32 no media generation this phase | PASS | FACT (fixture bytes in tests; live used existing `os-test` file) |
| T33 no official-film DB writes | PASS | LIVE_VERIFIED (film tasks = 0, film bindings = 0) |
| T34 preview uses physical bytes | PASS | TESTED + LIVE_VERIFIED (1 249 856 bytes) |
| T35 preview SHA = artifact SHA | PASS | TESTED + LIVE_VERIFIED |
| T36 preview artifactId = VISIBLE | PASS | TESTED + LIVE_VERIFIED |
| T37 CURRENT artifactId = binding | PASS | TESTED + LIVE_VERIFIED |
| T38 ACCEPTED artifactId = binding | PASS | TESTED |
| T39 no latest-artifact fallback | PASS | TESTED |
| T40 no latest-URL fallback | PASS | TESTED |
| T41 nextShotProductionCommand cannot mutate binding | PASS | TESTED (source + `MayDriveNewBinding=false`) |
| T42 localStorage cannot mutate binding | PASS | TESTED |
| T43 IndexedDB cannot mutate binding | PASS | TESTED |
| T44 SeriesShotRun cannot override ProductionBinding | PASS | TESTED |

---

## 18. Live API verification

No POST create. No provider start.

| Call | Result |
|---|---|
| `GET /production/shots/{TEST}/{TEST-SC01-SH01}/state` | current=visible=`5872d0a9-…` |
| `GET .../preview` | same artifactId, sha `e6553595…0c4e`, storageKey `os-test/aaaaaaaa-…/video/4fb46ca0…` |
| `GET .../preview/media` | 200 `video/mp4` 1 249 856 bytes; SHA256 = artifact |
| `GET .../TEST-SC01-SH02/preview` | artifact `583d7700-…` |
| `GET .../TEST-SC01-SH99/preview` | empty `VISIBLE_MISSING` |
| `GET .../{EP01}/EP01-SC01-SH01/preview` | empty `VISIBLE_MISSING` |
| `GET /production/runway-http-audit` | `startCount=1` (Phase G historical; this phase added 0) |

LIVE_VERIFIED.

Running API is the **pre-rebuild** process. New preview `role`/`mimeType`/`durationSec` fields were not served. IMPLEMENTED, not LIVE_VERIFIED.

---

## 19. Provider calls count

**0 this phase.** FACT / LIVE_VERIFIED (`startCount` still 1 from Phase G).

---

## 20. Media generation count

**0 this phase.** FACT. Tests used in-memory fixture MP4. Live media was the existing TEST artifact file.

---

## 21. EP01 mutation check

| Check | Value |
|---|---|
| series_build id | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` |
| updated_at | `2026-09-18 23:01:02 +07` (not this session) |
| graph_json md5 | `415fedf306ed23d1ace94a794400b440` |
| production_tasks | 0 |
| active bindings | 0 |
| OS preview | EMPTY |

LIVE_VERIFIED.

---

## 22. EP99 mutation check

| Check | Value |
|---|---|
| series_build id | `77262ec0-74ad-459d-9128-f09bb553a5dd` |
| updated_at | `2026-09-07 16:52:54 +07` |
| graph_json md5 | `c7cd2516627332c2648c1509395e4a64` |
| production_tasks | 0 |
| active bindings | 0 |

LIVE_VERIFIED.

---

## 23. Remaining blockers

1. **Official film still has a second authority plane.** Director preview for EP01/EP99 still uses `previewUrl` / `takeUrl` / `lipsyncUrl` / `acceptedTake.url`. This phase forbade deleting that plane. **LEGACY BYPASS — CUTOVER REQUIRED** for official film. FACT.

2. **CREATE_VOICE** still treats `kit-famixa-tts` as official-film voice authority. Explicitly deferred. FACT.

3. **FINAL / FFmpeg / ShotProductionFinalStore** not cut over. Explicitly deferred. Preview no longer falls through to them on TEST. FACT.

4. **Live API process not rebuilt.** New preview metadata fields exist in source only until the API is restarted. IMPLEMENTED / UNVERIFIED live.

5. Late-result guard is not proven against a second **real** overlapping provider job. TESTED in-process only. Doing that live would violate 0 provider calls.

No schema migration was required. No new orchestrator was added. No competing state machine was added.

---

## 24. Exact next phase recommendation

**FAMIXA PRODUCTION OS — CREATE_VOICE CUTOVER V1**

Reason: this report’s remaining OS-adjacent authority hole is official-film voice identity (`kit-famixa-tts`), which this phase was forbidden to migrate. After voice, the honest remaining plane is official-film CURRENT/VISIBLE/ACCEPTED/PREVIEW (`SeriesShotRun` URLs).

Do not start official-film preview cutover and voice cutover in the same phase.

RECOMMENDED.

---

## Why not PASS

A–J hold for **OS-controlled** shots in source + regression + live GET. They do **not** hold for the whole Famixa film surface: official Director is still a second plane. Live API also was not recycled, so the enriched preview contract is not live. That is enough to refuse a clean PASS.

STOP.
