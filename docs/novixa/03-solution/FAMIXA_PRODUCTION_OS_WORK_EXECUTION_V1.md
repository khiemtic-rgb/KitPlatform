# FAMIXA Production OS — Work Execution Control Plane V1

Status: **PHASE 1 + PHASE 2 implemented. PHASE 3 not started.**  
Mode: architecture + domain + schema + repositories + state machines.  
Provider calls = 0. Media generation = 0. UI patches = 0.

Architecture id: `FAMIXA_PRODUCTION_OS_WORK_EXECUTION_V1`  
Migration: `368_pack_content_production_work_execution.sql`  
Manifest: `deploy/ubuntu/migration-files.content.txt` only.

---

## 1. Architecture diagram

```
UI CTA (label 1:1 action)          LEGACY desk graph / localStorage / IDB
        │                                      │
        ▼                                      ▼
 GET available actions            LegacySeriesGraphAdapter (read-only)
        │                                      │
        ▼                                      ✕ not authority
 PRODUCTION APPLICATION
   Request → Task → Snapshot → Attempt → Artifact → Binding → Event
        │
        ├─ Action Contract Registry (WHAT)
        └─ Provider adapters below the contract (HOW)
                Gemini / Veo / Runway / Fal / ElevenLabs
        │
        ▼
 pack_content.production_*     + file store (bytes, keyed by storage_key)
        │
        ▼
 Preview = VISIBLE binding → artifact → storage_key
```

FAMIXA owns story, shot, canon, timing, lineage, binding.  
Providers own vendor API, poll, recovery. They never own CURRENT / VISIBLE / ACCEPTED.

---

## 2. Domain model

| Entity | Owns |
|---|---|
| `ProductionRequest` | Exact user action. Cannot remap. Idempotent. |
| `ProductionTask` | One action + scope + frozen provider/model. |
| `ProductionTaskInputSnapshot` | Immutable story / acting / visual / compiler / hashes. |
| `ProductionAttempt` | One provider job. Late results stay here. |
| `ProductionArtifact` | Physical bytes: `byteSha256`, size, mime, storageKey. |
| `ProductionBinding` | Shot ↔ artifact role: CURRENT / ACCEPTED / VISIBLE / EDITORIAL_SOURCE / FINAL. |
| `ProductionTaskEvent` | Immutable audit. |
| `FilmAssembly` / `FilmAssemblyShot` | Ordered final film references. |

Scope on every executable row: `buildId` (`series_build.id`) + `episodeCode` + `sceneCode` + `shotCode`.

Shot identity reuses Famixa Series codes (`EP01-SC01-SH02`). No new Episode/Scene/Shot tables. No FK to locked `video_production_shot` (CHAR-001 spec path is a different engine).

---

## 3. Database ERD

```
series_build (existing)
    │
    ├── production_requests
    │       └── production_tasks
    │               ├── production_task_input_snapshots  (1:1, immutable)
    │               ├── production_attempts
    │               │       └── production_artifacts
    │               └── production_bindings ──► production_artifacts
    │
    ├── production_task_events ──► request / task / attempt / artifact
    └── production_film_assemblies
            └── production_film_assembly_shots ──► artifacts
```

Constraints:

| Constraint | Purpose |
|---|---|
| `uq_prod_request_idempotency` | T15 |
| `uq_prod_attempt_number (task, n)` | Retry = new attempt |
| `uq_prod_attempt_provider_job` where job id present | External job identity |
| `uq_prod_binding_active (build, shot, role) WHERE unbound_at IS NULL` | One active role |
| `ix_prod_artifact_sha` | Content hash lookup (not unique — same bytes may appear on two tasks) |
| `failure_code <> 'INPUT_CHANGED'` | Retired umbrella state |
| Snapshot / event / verified-bytes triggers | Immutability |

`byte_sha256` is **indexed, not unique**. Same bytes ⇒ same hash (T16). Two tasks may legally produce identical fixtures. Lineage stays on `task_id` + `attempt_id`.

---

## 4. State machines

**Request:** DRAFT → SUBMITTED → IN_PROGRESS → COMPLETED | FAILED | CANCELLED

**Task:** CREATED → INPUT_FROZEN → PROVIDER_FROZEN → RUNNING → SUCCEEDED | FAILED_IDENTITY | FAILED  
RUNNING / SUCCEEDED / FAILED_IDENTITY → SUPERSEDED when input or provider/model changes.

**Attempt:** STARTED → PROVIDER_SUBMITTED → PROVIDER_SUCCEEDED | PROVIDER_FAILED  
Those terminal states → LATE (original attempt keeps the late payload). LATE never auto-binds.

**Artifact:** UNVERIFIED → VERIFIED | FAILED_VERIFICATION  
Provider SUCCESS + missing/short bytes = **FAILED_IDENTITY**, not SUCCEEDED.

**Binding roles are not a task status.** SUCCESS ≠ CURRENT ≠ VISIBLE ≠ ACCEPTED ≠ FINAL.

Drift codes (replace `INPUT_CHANGED`):

`INPUT_SNAPSHOT_MISMATCH` `STALE_TASK` `STALE_ATTEMPT` `PROVIDER_CHANGED` `MODEL_CHANGED` `KEYFRAME_CHANGED` `STORY_CHANGED` `ACTING_CHANGED` `VOICE_CHANGED` `MOTION_CHANGED` `BINDING_CHANGED`

---

## 5. Action contract

Registry: `ProductionActionContractRegistry`.

| Action | CTA | Output | Forbidden silent remap |
|---|---|---|---|
| CREATE_IMAGE | Tạo hình | IMAGE / KEYFRAME | video, voice, lipsync |
| CREATE_VIDEO | Tạo video | VIDEO_TAKE | image, voice, lipsync |
| CREATE_VOICE | Tạo giọng nói | VOICE | image, video, lipsync |
| CREATE_LIPSYNC | Khớp môi | LIPSYNC | image, video, voice |
| MIX_VIDEO | Hoàn thiện Shot | MIX | latest URL / acceptedTake.url |
| ACCEPT | Dùng video này | ACCEPTED binding | visible URL |
| BIND | Gắn artifact | CURRENT/VISIBLE | latest playable |
| REGENERATE | Tạo lại video | new Task/Attempt | mutate history |

Success for CREATE_VIDEO: verified physical artifact + attempt lineage + snapshot hash match. Nothing else.

If another action is required, create an explicit **child Request/Task**. No generic `run()`.

---

## 6. Provider boundary

Keep existing `IFamixaProvider*` / registry / router / adapters.

Core work engine must not contain production-semantic `if veo / if runway / if wan`.  
Phase 3+ adapters map vendor I/O under a frozen `providerId` + `modelId` on the Attempt. Changing either creates a **new Task**.

Unavailable provider → `PROVIDER_UNAVAILABLE` + fail closed. No hidden fallback.

---

## 7. Storage model

| Layer | Allowed to hold |
|---|---|
| PostgreSQL `pack_content.production_*` | Metadata, lineage, hashes, bindings |
| File store (`storage_key`) | Physical media |
| IndexedDB / localStorage / session blob | Cache only |
| Provider job id | External execution id only |

Preview: VISIBLE binding → artifact → storage. If no VISIBLE → explicit empty. Cross-shot resolve fails closed.

---

## 8. API contracts (Phase 3 — specified, not implemented)

```
POST /api/content/production/requests
POST /api/content/production/tasks
POST /api/content/production/tasks/{id}/snapshot
POST /api/content/production/tasks/{id}/attempts
POST /api/content/production/attempts/{id}/provider-job
POST /api/content/production/attempts/{id}/verify-artifact
POST /api/content/production/bindings
GET  /api/content/production/shots/{buildId}/{shotCode}/actions
GET  /api/content/production/shots/{buildId}/{shotCode}/preview
GET  /api/content/production/tasks/{id}
```

No graph PUT as authority. No `acceptedTake.url` write path.

---

## 9. UI action contract (Phase 4 — specified, not implemented)

UI displays only `GET .../actions`.  
Each button label maps 1:1 via `ProductionUiActionMap`.  
`nextShotProductionCommand` must not choose a different action than the clicked CTA.

---

## 10. Migration strategy

1. Apply `368` on Content park only (`migration-files.content.txt`).
2. Do **not** delete `series_build.graph_json`, `video_generation_job`, or `video_generation_execution`.
3. New writes go to `production_*`.
4. Phase 11: stop treating graph / localStorage as authority; keep as import/debug.

Local apply is the operator’s choice. This phase does not write live production rows.

---

## 11. Legacy isolation

`LegacySeriesGraphAdapter` lists fields that must not drive new bindings:

`takeUrl` `previewUrl` `lipsyncUrl` `turboTaskId` `acceptedTake.url` `videoApproved` `runwayAttempts` localStorage / IDB / session blobs / `nextShotProductionCommand` / `INPUT_CHANGED` / `compileI2vPrompt.holdBody` / kit `video_generation_*`.

`IsAuthoritative()` = false.

---

## 12. Test matrix

In-memory engine suite `ProductionWorkExecutionV1Regression` / `tests/KitPlatform.Content.Tests` (**TESTED** 2026-09-17: 1/1 passed, T01–T24). Not a live Veo/Fal run.

| Id | Assertion |
|---|---|
| T01–T03 | CREATE_VIDEO A/B belong to A/B; no cross contamination |
| T04 | SH01 artifact cannot bind as SH02 VISIBLE |
| T05 | SUCCESS without bytes → FAILED_IDENTITY |
| T06 | SUCCESS artifact is not VISIBLE until bind |
| T07 | Preview = exact VISIBLE artifact |
| T08 | Late result cannot replace CURRENT |
| T09 | Model change → new Task |
| T10 | Story change → new Task + `STORY_CHANGED` |
| T11 | Retry → new Attempt |
| T12–T13 | CTA 1:1 CREATE_VIDEO / CREATE_LIPSYNC |
| T14 | Hold-body compiler keeps story |
| T15 | Idempotent request |
| T16–T17 | SHA256 identity |
| T18–T19 | Reload / cleared browser cache still uses DB binding |
| T20 | Provider unavailable fail-closed |
| T21 | Mix uses explicit artifactId |
| T22 | Assembly order |
| T23 | Accept names artifactId |
| T24 | Old artifact immutable after regenerate |

Paid live generation is **forbidden** until Phase 4 vertical slice + this suite stays green.

---

## 13. Code module plan

Created:

```
Application/Production/
  ProductionWorkIds.cs
  ProductionEntities.cs
  ProductionActionContracts.cs
  ProductionStateMachines.cs
  ShotExecutionInput.cs
  IProductionWorkStore.cs
  InMemoryProductionWorkStore.cs
  ProductionWorkExecutionEngine.cs
  IProductionWorkExecutionService.cs
  ProductionWorkExecutionV1Regression.cs
  Legacy/LegacySeriesGraphAdapter.cs

Infrastructure/Production/
  ProductionWorkRepository.cs
  ProductionWorkExecutionService.cs

migrations/368_pack_content_production_work_execution.sql
```

DI: `IProductionWorkStore` → `ProductionWorkRepository`; engine + `IProductionWorkExecutionService`.

---

## 14. Files to modify later (Phase 3+) — not this pass

| File | Future change |
|---|---|
| `ContentController.cs` | Production APIs only — no graph authority |
| `ContentFamixaSeriesTab.tsx` | CTA → request API |
| `ShotProductionCta.ts` / `ShotProductionOrchestrator.ts` | Retire as authority; availability API |
| `ShotProductionExecution.ts` | Retire `INPUT_CHANGED` umbrella |
| `famixa-lipsync-motion-authority.ts` | Preview from VISIBLE only |
| `content-famixa-series.ts` `compileI2vPrompt` | Replace with `ShotExecutionCompiler` |
| `content-famixa-series.ts` `saveSeriesPilot` | Cache, not SoT |
| `ShotProductionPreview.tsx` | Bind VISIBLE storage |

Do not patch those in Phase 1–2.

---

## 15. Files to retire as authority (not delete yet)

- `nextShotProductionCommand`
- `validateMotionCallback` / `motionCallbackRunPatch` INPUT_CHANGED fallbacks
- `acceptExistingTake()` URL bind
- `resolveCurrentProductionVideoSource` latest-URL heuristic
- `deskFalPlayableForShot` leftover blob as SoT
- Graph `acceptedTake.url` / `videoApproved` as production success

---

## 16. Phase 1+2 review (stop here)

**Implemented:** domain, ERD, state machines, action contracts, in-memory + SQL repositories, transactional verify, story compiler that cannot drop story, T01–T24, migration 368, legacy catalog.

**Not implemented:** HTTP APIs, UI rewrite, CREATE_VIDEO live provider slice, image/voice/lipsync/mix/accept UI, final film export, deletion of legacy paths.

**Next (after review):** Phase 3 — Request → Task → Attempt → Artifact → Binding API. Still zero paid generation.
