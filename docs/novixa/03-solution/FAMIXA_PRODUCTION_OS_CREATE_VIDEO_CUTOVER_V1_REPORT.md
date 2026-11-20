# FAMIXA PRODUCTION OS — CREATE_VIDEO VERTICAL SLICE 1 CUTOVER V1

Date: 2026-09-17  
Status words only: IMPLEMENTED / TESTED / UNVERIFIED / BLOCKED

Do not read this as production-ready.

## 1. Architecture path implemented

UI "Tạo video"
→ POST `/api/content/production/requests` (`userAction` frozen as `CREATE_VIDEO`)
→ ProductionRequest
→ Action Contract (`CREATE_VIDEO` / `VIDEO_TAKE`)
→ ProductionTask
→ InputSnapshot (story / acting / visual / keyframe artifactId+SHA / timing / provider / model / compiled prompt hashes)
→ Provider/Model freeze on Task + Attempt (`fixture` / `fixture-motion-v1`)
→ Attempt
→ `IMotionExecutionAdapter` (HOW only)
→ physical bytes written to artifact file store
→ MIME + duration + SHA256 verify
→ ProductionArtifact
→ CURRENT binding by artifactId
→ VISIBLE binding by artifactId
→ Shot production state
→ GET preview / preview/media from VISIBLE only

Paid adapters throw until this fixture path is proven live.

Same OS objects are reused later for CREATE_IMAGE / CREATE_VOICE / CREATE_LIPSYNC / MIX_VIDEO / ACCEPT / ASSEMBLY. Those workflows are not implemented in this pass.

## 2. Files created

- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/IProductionArtifactFileStore.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoV1Regression.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionApiDtos.cs`
- `src/KitPlatform.Api/Controllers/Content/ContentProductionController.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/ProductionArtifactFileStore.cs`
- `docs/novixa/03-solution/FAMIXA_PRODUCTION_OS_CREATE_VIDEO_CUTOVER_V1_REPORT.md`

Phase 1+2 + pipeline foundation already existed (entities, engine, graph, FixtureMotionAdapter, mig 368/369).

## 3. Files modified

- `ContentPackDependencyInjection.cs` — fixture adapter, file store, create-video service
- `VideoGenerationExecutionService.cs` — register CREATE_VIDEO regression
- `tests/KitPlatform.Content.Tests/ProductionPipelineV1Tests.cs`
- `client/admin/src/shared/api/content.api.ts`
- `client/admin/src/modules/content/ContentFamixaSeriesTab.tsx`
- `client/admin/src/modules/content/ContentFamixaShotProduction/ShotProductionWorkspace.tsx`
- `client/admin/src/modules/content/ContentFamixaShotProduction/ShotProductionCta.ts`
- `client/admin/src/modules/content/Production/Legacy/LegacySeriesGraphAdapter.cs` path: `src/Packs/Content/.../Legacy/LegacySeriesGraphAdapter.cs`
- Desk smokes that asserted `sendTurbo` inside `startShotMotion`

Canon / VA / Universe / Shot Contract / Timing / provider abstraction / Phase 1+2 domain were not redesigned.

## 4. Old authority removed from CREATE_VIDEO

Desk "Tạo video" no longer calls `POST /series/turbo` or `sendTurbo`.

Still present and marked **LEGACY / NON-AUTHORITATIVE** (not deleted):

- `sendTurbo` / `startContentSeriesTurbo`
- `turboTaskId`, `runwayAttempts`, `takeUrl`, `previewUrl`, `acceptedTake.url`, `videoApproved`
- localStorage / IndexedDB / session blobs
- `nextShotProductionCommand` (other stages only; CREATE_VIDEO CTA uses GET `/actions`)
- `resolveCurrentProductionVideoSource` (skipped when OS VISIBLE URL is set)

## 5. API endpoints

All under `/api/content/production` (ADMIN + `kit_content`):

| Method | Path | Role |
|---|---|---|
| POST | `/requests` | CREATE_VIDEO (action never remapped) |
| POST | `/keyframes` | import existing KEYFRAME pixels as CREATE_IMAGE lineage |
| GET | `/requests/{id}` | request |
| GET | `/tasks/{id}` | task |
| GET | `/shots/{buildId}/{shotCode}/actions` | CREATE_VIDEO availability + artifacts |
| GET | `/shots/{buildId}/{shotCode}/state` | shot graph |
| GET | `/shots/{buildId}/{shotCode}/preview` | VISIBLE only; else explicit EMPTY |
| GET | `/shots/{buildId}/{shotCode}/preview/media` | VISIBLE storage bytes |

## 6. DB writes

On CREATE_VIDEO (after mig 368/369):

- `pack_content.production_requests`
- `pack_content.production_tasks`
- `pack_content.production_task_input_snapshots`
- `pack_content.production_attempts`
- `pack_content.production_artifacts`
- `pack_content.production_bindings` (CURRENT + VISIBLE)
- `pack_content.production_task_events`

Physical bytes: `KitVideoVideoStore` named file (`os/{attemptId}`).

Local SQL schema: **IMPLEMENTED** — mig 368 + 369 applied to `kitplatform` on 2026-09-17. Live CREATE_VIDEO row writes from a browser click: **UNVERIFIED**.

## 7. State transitions

Request: SUBMITTED → IN_PROGRESS  
Task: CREATED → PROVIDER_FROZEN → RUNNING → SUCCEEDED  
Attempt: STARTED → PROVIDER_SUBMITTED → SUCCEEDED  
Artifact: VERIFIED or FAILED_IDENTITY  
Binding: CURRENT + VISIBLE by artifactId  

Story change: downstream CURRENT/VISIBLE unbound; old artifact immutable; next CREATE_VIDEO = new Task + new InputSnapshot.

Late older Attempt: status LATE; cannot replace CURRENT/VISIBLE.

Duplicate idempotencyKey: replay existing Request/Task/Artifact. No second adapter execution.

## 8. Fixture test result

| Check | Status |
|---|---|
| In-memory CREATE_VIDEO slice (request/task/snapshot/attempt/artifact/CURRENT/VISIBLE/preview) | TESTED |
| Duplicate idempotency | TESTED |
| SH01 / SH02 isolation | TESTED |
| Story change invalidates VISIBLE; artifact immutable; new Task | TESTED |
| GET actions CREATE_VIDEO | TESTED |
| Late Attempt 1 cannot replace CURRENT/VISIBLE | TESTED |
| `tests/KitPlatform.Content.Tests` (4 facts) | TESTED |
| Browser Tạo video on one shot | UNVERIFIED |
| Reload / clear localStorage / clear IDB | UNVERIFIED |
| Live API + Postgres | UNVERIFIED |

## 9. Real provider test result

BLOCKED.

Paid motion adapter is refused until the fixture path is proven on a live shot. No Veo / Runway / Wan job was sent.

## 10. Evidence IDs

In-memory regression only. No live `requestId` / `providerJobId` / `storageKey` from a browser run this pass.

After a live fixture click, collect:

requestId, taskId, snapshotId, attemptId, providerId, modelId, providerJobId, artifactId, byteSha256, storageKey, bindingId, preview source.

## 11. Remaining blockers

1. One-shot fixture click on a shot with verified KEYFRAME (SH02 preferred) after login KIT_MKT.
2. Reload + clear localStorage/IDB proof on that shot.
3. Only then switch the same Attempt execution to one real provider adapter and run exactly one paid generation.

Mig 368/369 applied locally. API rebuilt and listening on :5290. Browser CREATE_VIDEO still UNVERIFIED.

## 12. Next workflow

After live fixture CREATE_VIDEO is proven on one shot: one real provider generation with evidence IDs.

Then CREATE_IMAGE as the same OS path (not a new orchestrator). CREATE_VOICE / CREATE_LIPSYNC / MIX_VIDEO / ACCEPT / ASSEMBLY stay unbuilt.
