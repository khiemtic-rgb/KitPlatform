# FAMIXA PRODUCTION OS — UI CUTOVER V1 REPORT

Date: 2026-09-18  
Scope: Director / Series production CTAs → Production OS only  
Live providers: none  
Film mutation: none

## 1. EXECUTIVE VERDICT

**PASS WITH GAPS.**

Director desk production CTAs now map 1:1 to Production Actions and POST `/content/production/requests`. OS-enabled handlers refuse `/series/still`, `/series/turbo`, `/series/lipsync`, and `/content/series/assemble`. TEST-SC01-SH02 fixture chain was executed with the same request body the UI builds. Preview after Mix is VISIBLE → MIX artifact. Film EP01/EP99 hashes and SH01 4A video are unchanged.

Gaps: REGENERATE is mapped in UI but the running API returns `ACTION_REMAP_FORBIDDEN`. GET `/actions` on the running process does not yet list ASSEMBLE (source adds it; API not recycled). Browser desk clicks were not completed. Character master-ref still uses `/series/still` (non-shot, classified LEGACY).

Do not claim film production is ready.

## 2. COMPLETE UI ACTION MAP

| Label | File | Handler | Before | After | Endpoint | ProductionAction | Class |
|---|---|---|---|---|---|---|---|
| Tạo hình / Tạo hình mới | ShotProductionWorkspace → SeriesTab | `os-create-image` → `executeProductionOsAction` | `confirmCreateShotImage` → `generateSceneKf` → Gemini | OS request, Film refused | POST `/content/production/requests` Action=CREATE_IMAGE | CREATE_IMAGE | IMPLEMENTED |
| Tạo video | same | `os-create-video` | already OS CREATE_VIDEO; Studio `sendTurbo` still existed | OS only; `sendTurbo` refused | POST `/production/requests` CREATE_VIDEO | CREATE_VIDEO | IMPLEMENTED |
| Nạp thoại / Tạo thoại | Workspace Nạp thoại + desk | `os-create-voice` | `ensureShotVoice` ElevenLabs/IDB | OS CREATE_VOICE; `ensureShotVoice` refused | POST `/production/requests` CREATE_VOICE | CREATE_VOICE | IMPLEMENTED |
| Khớp môi / Khớp môi mới / Hỏi lại môi | Workspace | `os-create-lipsync` | `startLipsync` → `/series/lipsync` | OS; `startLipsync` refused | POST `/production/requests` CREATE_LIPSYNC | CREATE_LIPSYNC | IMPLEMENTED |
| Hoàn thiện / Hoàn thiện Shot | Workspace | `os-create-mix` | `assembleOneShot` → `/content/series/assemble` | OS; assemble refused | POST `/production/requests` MIX_VIDEO | MIX_VIDEO | IMPLEMENTED |
| Dùng video này / Dùng kết quả này | Workspace | `os-accept` | `acceptExistingTake` / `videoApproved` local | OS ACCEPT with artifactId | POST `/production/requests` ACCEPT | ACCEPT | IMPLEMENTED |
| Lắp tập | Desk tertiary when available | `os-assemble` | `exportEditorialCut` URL assemble | OS ASSEMBLE; export refused | POST `/production/requests` ASSEMBLE | ASSEMBLE | IMPLEMENTED (GET catalog pending API recycle) |
| Tạo lại / Tạo lại video | Desk remap | `os-regenerate` | remake turbo | POST REGENERATE | POST `/production/requests` REGENERATE | REGENERATE | IMPLEMENTED in UI; backend FORBIDDEN |
| Tạo video (Studio batch) | SeriesTab `startSceneTurbo` / `sendTurbo` | refused | POST `/series/turbo` | refuse + structured note | none | — | IMPLEMENTED refuse |
| Tạo hình (batch KF) | `generateSceneKf` / `generateKfFromCanon` | refused | POST `/series/still` | refuse | none | — | IMPLEMENTED refuse |
| Character Tạo hình | ContentFamixaCharacterWorkspace `generateNextRef` | unchanged | POST `/series/still` | LEGACY character refs, not shot OS | POST `/series/still` | — | OUT OF SCOPE / LEGACY |
| Legacy motion | `startShotMotion` | no-op | none | none | none | — | FACT |

## 3. FILES CHANGED

- `client/admin/src/shared/api/content.api.ts` — `createProductionRequest`, `getProductionShotState`, claim params, LEGACY marks
- `client/admin/src/modules/content/production-os-action-authority.ts` — full CTA remap, TEST scope gate, parent/idempotency helpers
- `client/admin/src/modules/content/ContentFamixaShotProduction/ShotProductionCta.ts` — os-* staff actions
- `client/admin/src/modules/content/ContentFamixaShotProduction/ShotProductionWorkspace.tsx` — all os-* → OS; TEST preview = VISIBLE only
- `client/admin/src/modules/content/ContentFamixaSeriesTab.tsx` — execute all actions; refuse legacy; hydrate state; TEST desk shots; shot-scoped KF lock
- `client/admin/src/modules/content/ContentFamixaShotProduction/ShotProductionAssemble.ts` — LEGACY mark
- `src/Packs/Content/.../ProductionExecutionService.cs` — ASSEMBLE row in GET `/actions`
- `client/admin/src/modules/content/famixa-production-os-ui-cutover.smoke.ts` — new
- `client/admin/src/modules/content/production-os-action-authority.smoke.ts` — execute path updated
- `client/admin/src/modules/content/famixa-video-studio-ux.smoke.ts` — createProductionRequest string
- `docs/novixa/03-solution/FAMIXA_PRODUCTION_OS_UI_CUTOVER_V1_REPORT.md` — this file

No second orchestrator. CREATE_VIDEO 3B/4A path not duplicated.

## 4. UI → PRODUCTION OS ROUTING

FACT / IMPLEMENTED:

```
visible CTA
  → applyProductionOsActionAuthority / authorizeOsDeskCta  (GET /actions)
  → os-* StaffShotAction
  → onExecuteOsAction(shotId, ProductionAction)
  → mayExecuteProductionAction
  → mayMutateProductionOs (TEST only)
  → createProductionRequest({ action, parent artifactIds, idempotencyKey })
  → POST /content/production/requests
  → Task / Snapshot / Attempt / Artifact / Binding
  → GET /state + GET /preview/media
```

Film EP01/EP99: CTA may appear from GET `/actions`; execute returns `PROVIDER_UNAVAILABLE · FILM_PROTECTED`. No POST. FACT from handler source.

TEST shots `TEST-SC01-SH01` / `TEST-SC01-SH02` are injected into the director desk list only (not persisted into `graph_json`).

## 5. ACTION AVAILABILITY

IMPLEMENTED: desk CTAs exist only when GET `/actions` `available === true`. Fail-closed if catalog missing.

UI does not gate on `takeUrl` / `previewUrl` / `acceptedTake` / `lipsyncUrl` / `latestVoice` for OS execution.

MIX still requires a LIPSYNC artifactId in the handler even if the catalog also allows motion — `MISSING_LIPSYNC` if LIPSYNC node empty. IMPLEMENTED.

ASSEMBLE availability is in source `BuildActions`. LIVE GET on the running API (not recycled) listed CREATE_* / MIX / ACCEPT / REGENERATE, not ASSEMBLE. LIVE VERIFIED gap.

## 6. ARTIFACT IDENTITY

IMPLEMENTED: OS execute sends `motionArtifactId`, `voiceArtifactId`, `lipsyncArtifactId`, `acceptArtifactId`, `assemblyArtifactIds` from GET `/state` nodes. No URL identity on that path.

LIVE VERIFIED TEST-SC01-SH02 (UI idempotency `os-ui:{action}:{build}:{shot}`):

| Action | requestId | taskId | attemptId | artifactId |
|---|---|---|---|---|
| CREATE_IMAGE | c007dc16-… | 6c635fff-… | fe819f33-… | 16c1a4e8-… |
| CREATE_VIDEO | 57c4b90f-… | bc3ad53d-… | f704819f-… | 78d57d06-… |
| CREATE_VOICE | 0492fb2a-… | d515c1c1-… | 333a0a3a-… | 3a753477-… |
| CREATE_LIPSYNC | 34c139da-… | 1e0e7964-… | 6fecd3e8-… | 49deaa6b-… |
| MIX_VIDEO | 4fecf495-… | 11483688-… | eff90738-… | 638bd185-… |
| ACCEPT | da84b77d-… | ad6e8492-… | 1e2c64e2-… | 638bd185-… |
| ASSEMBLE | e03401ed-… | 634e2fad-… | 5c3b0369-… | 638bd185-… (accepted list) |

Providers were `fixture-*` / `os accept-v1`. No paid adapter.

## 7. PREVIEW AUTHORITY

IMPLEMENTED: TEST shots use `osPreviewUrl` from GET preview media only. No `takeUrl` / `lipsyncUrl` / `acceptedTake.url` fallback.

LIVE VERIFIED: GET preview `empty=false`, `artifactId=638bd185-…` (MIX). Reload state `visibleArtifactId=638bd185-…`, `currentArtifactId=78d57d06-…` (VIDEO_TAKE). Task SUCCESS ≠ VISIBLE. FACT.

Browser player rendering of that blob: UNVERIFIED.

Film shots may still show legacy graph media as DISPLAY only. FACT.

## 8. ACCEPT AUTHORITY

IMPLEMENTED: ACCEPT sends `acceptArtifactId` = MIX ∥ LIPSYNC ∥ VISIBLE ∥ MOTION from OS state, never a URL.

LIVE VERIFIED: ACCEPT artifactId `638bd185-…` = MIX; reload `acceptedArtifactId` same.

## 9. ASSEMBLY INPUT AUTHORITY

IMPLEMENTED: ASSEMBLE sends ordered `assemblyArtifactIds` + `assemblyShotCodes` from ACCEPTED bindings.

LIVE VERIFIED: ASSEMBLE request `e03401ed-…`, fixture assembly, input MIX `638bd185-…`. No FINAL / FILM READY claim.

## 10. HYDRATION / RELOAD

IMPLEMENTED: on `studioShotId` / `buildId` change, UI fetches GET `/actions`, GET `/state`, GET `/preview` (+ media if VISIBLE). TEST shots use TEST scope id `bbbbbbbb-0000-4000-8000-000000000371`.

LIVE VERIFIED: re-GET state after the chain reconstructed the same VISIBLE/ACCEPTED MIX id. No duplicate CREATE_VIDEO task on second POST.

localStorage / IDB are not used as authority on this path. FACT from handler.

## 11. CROSS-SHOT ISOLATION

IMPLEMENTED: OS busy is `osInflightRef[shotId]`. Preview/actions/state keyed by shotId. TEST vs Film build split.

LIVE VERIFIED: CREATE_LIPSYNC on TEST-SC01-SH01 with SH02 VIDEO_TAKE+VOICE → `CROSS_SHOT_FORBIDDEN`. No new SH01 adapter artifact.

`kfInflightRef` is now `Record<shotId, true>` so SH01 KF no longer globally blocks SH02. IMPLEMENTED. `turboLockRef` remains on the refused legacy turbo path only — not used by OS execute. FACT.

## 12. IDEMPOTENCY

IMPLEMENTED: `os-ui:{action}:{buildId}:{shotCode}` plus in-flight guard.

LIVE VERIFIED: two CREATE_VIDEO POSTs → same requestId `57c4b90f-…`, same artifact `78d57d06-…`.

## 13. LEGACY ENDPOINT AUDIT

Static scan after cutover:

| Occurrence | Classification |
|---|---|
| `content.api.ts` `startContentSeriesTurbo` / `generateContentSeriesStill` / `startContentSeriesLipsync` / `assembleContentSeriesCut` | LEGACY NON-AUTHORITY client kept |
| SeriesTab dead bodies after refuse `return` | READ-ONLY COMPATIBILITY strings; not reachable from OS CTAs |
| `ContentFamixaCharacterWorkspace.tsx` `generateContentSeriesStill` | LEGACY character master-ref, not shot desk |
| `ShotProductionAssemble.ts` | LEGACY body helper, unused by OS execute |

OS `executeProductionOsAction` contains none of `/series/still|turbo|lipsync` or `assembleContentSeriesCut`. TESTED by `famixa-production-os-ui-cutover.smoke.ts`.

## 14. LEGACY STATE AUDIT

Still present as cache/display, not OS execution authority:

- `acceptedTake.url`, `videoApproved`, `takeUrl`, `previewUrl`, `lipsyncUrl`, `ttsBlobs`
- `directorPictureVideoSurface` still emits graph CTAs; authority remap strips/replaces them
- `resolveCurrentProductionVideoSource` unused for TEST preview
- `turboBusy` / `lipsyncBusy` remain shot-id strings; `assembleBusy` boolean unused on refused export

OUT OF SCOPE to delete.

## 15. TEST RESULTS

| Test | Result | Class |
|---|---|---|
| `production-os-action-authority.smoke.ts` | PASS | TESTED |
| `famixa-production-os-ui-cutover.smoke.ts` | PASS | TESTED |
| `famixa-explicit-mix-action.smoke.ts` | PASS | TESTED |
| `famixa-video-studio-ux.smoke.ts` | PASS | TESTED |
| `content-famixa-editorial-cut.smoke.ts` | PASS | TESTED |
| `famixa-shot-stale-remake-gate.smoke.ts` | PASS | TESTED |
| `npm run build` / full `tsc --noEmit` | many pre-existing errors outside this cutover | UNVERIFIED as clean admin build |

## 16. LIVE TEST SCOPE RESULTS

TEST build `bbbbbbbb-0000-4000-8000-000000000371`, shot TEST-SC01-SH02.

| UI event (contract) | Result | Class |
|---|---|---|
| A CREATE_IMAGE | fixture IMAGE `16c1a4e8-…` | LIVE VERIFIED (API body = UI) |
| B CREATE_VIDEO | fixture VIDEO `78d57d06-…` | LIVE VERIFIED |
| C CREATE_VOICE | fixture VOICE `3a753477-…` LineId `LINE-TEST-SC01-SH02` | LIVE VERIFIED |
| D CREATE_LIPSYNC | fixture LIPSYNC `49deaa6b-…` parents VIDEO+VOICE ids | LIVE VERIFIED |
| E MIX_VIDEO | fixture MIX `638bd185-…` parent LIPSYNC | LIVE VERIFIED |
| F ACCEPT | ACCEPT binding on `638bd185-…` | LIVE VERIFIED |
| G ASSEMBLE | fixture assembly, accepted `[638bd185-…]` | LIVE VERIFIED |
| Reload | VISIBLE=MIX, CURRENT=VIDEO | LIVE VERIFIED |
| Duplicate CREATE_VIDEO | same request/task/artifact | LIVE VERIFIED |
| SH01 LIPSYNC + SH02 parents | CROSS_SHOT_FORBIDDEN | LIVE VERIFIED |
| REGENERATE | ACTION_REMAP_FORBIDDEN | LIVE VERIFIED gap |
| Browser CTA clicks | Videos hub opened; desk click intercepted | UNVERIFIED |

Trace: `.tmp/os-ui-cutover-live.json`.

## 17. FILM INTEGRITY BEFORE / AFTER

| Probe | Before | After | Class |
|---|---|---|---|
| EP01 `4ef164d6-…` graph md5 | `12289be18284fbd4d4b166b82c09eba0` | same | LIVE VERIFIED |
| EP99 `77262ec0-…` graph md5 | `c7cd2516627332c2648c1509395e4a64` | same | LIVE VERIFIED |
| series_build count | 4 | 4 | LIVE VERIFIED |
| film_art on EP01 build | 0 | 0 | LIVE VERIFIED |
| film_bind on EP01 build | 0 | 0 | LIVE VERIFIED |
| SH01 4A VIDEO `63bca29a-…` SHA | `f94a9d097e5d90c0…8c8c17` | same | LIVE VERIFIED |

Canon / VA / EP01 / EP99 not modified. Complete Authority historical SH02 IMAGE `9877cd99-…` remains as an immutable row; CURRENT KEYFRAME on SH02 is now UI-cutover `16c1a4e8-…`. TEST only.

## 18. BUILD RESULTS

- Admin smokes listed in §15: PASS. TESTED.
- Full admin `tsc --noEmit`: fails on many pre-existing files; not introduced as a clean-build claim. UNVERIFIED.
- API process was not recycled after adding ASSEMBLE to `BuildActions`. Source compiled in-repo; running GET catalog stale for ASSEMBLE. FACT.

## 19. UNVERIFIED

- Actual browser clicks of Tạo hình / Tạo video / … on TEST-SC01-SH02
- Browser reload of the Director desk reconstructing preview from VISIBLE
- Cross-scope HTTP from UI (Film POST blocked in handler; not live-posted)
- GET `/actions` ASSEMBLE row on the running API
- REGENERATE new Task/Attempt (backend missing)
- Physical byte compare of preview/media vs MIX `size_bytes` after this chain
- Character workspace still path (intentionally not cut over)

## 20. OUT OF SCOPE

- Old preview quirks, styling, copy, modals
- SH01 4A micro-fixes
- Acting / Timing / VA / Canon
- Real Runway / Veo / Gemini / Fal / ElevenLabs
- Deleting legacy endpoints
- FINAL / FILM READY
- Character master-ref generation
- Admin-wide tsc debt

## 21. REMAINING GAPS

1. REGENERATE UI posts Action=REGENERATE; API `ExecuteOsActionAsync` has no case → `ACTION_REMAP_FORBIDDEN`.
2. Running API GET `/actions` lacks ASSEMBLE until recycle of the BuildActions change.
3. Browser desk CTA path not click-verified.
4. Character `/series/still` remains a non-OS generation path.
5. Film desk still displays legacy graph media (DISPLAY), which can confuse operators even though execute is blocked.
6. Dual catalog vs execute: Film GET `/actions` may mark CREATE_VIDEO available while UI refuses POST (correct for no-spend; UX shows a dead CTA until a real-provider phase).

## 22. NEXT PHASE

Recommended (not started):

1. Recycle Content API so GET `/actions` includes ASSEMBLE.
2. Implement REGENERATE as a new Request/Task/Attempt on the existing action contract — do not call `/series/turbo`.
3. Browser-only acceptance on TEST-SC01-SH02 (no Film).
4. Decide Film CTA policy: hide unavailable paid actions vs show `PROVIDER_UNAVAILABLE`.
5. Only then consider a real-provider UI bridge. No spend in that decision memo.

STOP. No Runway. No Veo. No Gemini. No Fal. No ElevenLabs. No Acting redesign. No FINAL. No FILM READY.
