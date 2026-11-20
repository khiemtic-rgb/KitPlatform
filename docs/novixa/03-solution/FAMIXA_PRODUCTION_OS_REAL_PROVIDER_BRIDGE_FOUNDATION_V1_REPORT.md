# FAMIXA PRODUCTION OS — REAL PROVIDER BRIDGE FOUNDATION V1

Date: 2026-09-18  
Mode: IMPLEMENTATION / NO NETWORK  
Official EP01: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
Official EP99: `77262ec0-74ad-459d-9128-f09bb553a5dd`  
TEST sandbox (reused, not recreated):

- `test_run_id` = `aaaaaaaa-0000-4000-8000-000000000371`
- `scope_id` = `bbbbbbbb-0000-4000-8000-000000000371`
- `shot_code` = `TEST-SC01-SH01`
- KEYFRAME `artifactId` = `33333333-0000-4000-8000-000000000371`

Provider: `runway` / `gen4_turbo` through `RunwayMotionExecutionAdapter` + `DryRunRunwayTransport`.  
No HTTP to Runway. No spend. Paid network gate remains.

---

## 1. Verdict

**PASS**

Runway can be selected, frozen, started, persisted, polled/recovered, verified, and handed back to Production OS without contacting Runway.

- SOURCE: T01–T32 pass (`ProductionPipelineV1Tests` 9/9)
- LIVE API: one dry-run `CREATE_VIDEO` on official TEST SH01 after `:5290` rebuild
- Paid network safety gate kept (`IsPaid && RequiresNetwork` → `PAID_NETWORK_FORBIDDEN`)
- Dry-run transport `HttpCallCount = 0`

---

## 2. Architecture

**Before**

Production OS → injected `IMotionExecutionAdapter` (Fixture only) → blocking `ExecuteFrozenAsync` → persist `providerJobId` after generate → verify bytes≥32.

**After**

```
Production OS
    ↓ request ProviderId/ModelId
Provider selection + catalog
    ↓
IProductionMotionAdapterResolver (fail closed)
    ↓
IMotionExecutionAdapter
    ├─ FixtureMotionAdapter
    └─ RunwayMotionExecutionAdapter
           ↓
        IRunwayTransport
           └─ DryRunRunwayTransport   (this phase)
              later: HTTP transport (not registered)
```

Start → persist `providerJobId` → Poll/Recover → Fetch physical bytes → ProductionArtifact → CURRENT → VISIBLE.

---

## 3. Provider selection

**Request**

`CreateVideoApiCommand` / `ProductionCreateVideoRequestDto` carry `ProviderId` and `ModelId`.

- Both omitted → injected default adapter (`fixture` / `fixture-motion-v1`). Not a fallback from Runway.
- One omitted → `MISSING_PROVIDER_MODEL`
- Explicit `runway` / `gen4_turbo` → Runway adapter

**Resolver**

`ProductionMotionAdapterResolver` rejects unknown provider, unknown model, provider/model mismatch, disabled provider, unsupported capability. No silent fallback.

**Registry**

`ProductionMotionCatalog`: fixture (unpaid Motion) and `runway/gen4_turbo` (paid Motion). Server catalog only.

**Freeze**

Task + InputSnapshot store resolved `ProviderId`/`ModelId`. Replay with a different pair → `PROVIDER_CHANGED` / `MODEL_CHANGED`. Mutation requires a new Task.

---

## 4. Runway adapter

| Item | Value |
|---|---|
| File | `src/Packs/Content/.../Production/Providers/RunwayMotionExecutionAdapter.cs` |
| Class | `RunwayMotionExecutionAdapter` |
| Methods | `StartFrozenAsync`, `PollFrozenAsync`, `RecoverFrozenAsync`, `FetchOutputAsync`, `ExecuteFrozenAsync` (compose only) |
| Transport | `IRunwayTransport` / `DryRunRunwayTransport` |

Adapter owns vendor mapping (duration 5/10, aspect, job-id shape). Production OS does not own Runway HTTP. `ContentRunwayClient` / `RunwayMotionProvider` were not called and were not modified.

---

## 5. Async lifecycle

| Step | Behavior |
|---|---|
| Start | `CreateImageToVideoAsync` → `runway-dryrun-{taskId:N}` |
| providerJobId | Dry-run prefix, never a vendor UUID |
| Persist | `SubmitProviderJobAsync` **before** Poll |
| Poll | `GetTaskAsync` using the persisted id |
| Recover | same `GetTaskAsync`; never creates a second job |
| Fetch | `DownloadOutputAsync` → physical MP4 bytes |

Fixture implements the same contract. `ExecuteCreateVideoFixtureAsync` also uses Start → persist → Poll → Fetch.

---

## 6. Cost guard

| Item | Value |
|---|---|
| Catalog | Existing `FamixaProviderCostCatalog` — Runway `gen4_turbo` 5 credits/sec |
| Quote | 5s → 25 credits; 10s → 50 credits (estimate) |
| MaxCost | Server `DefaultMaxCredits = 30`. Client MaxCost is not authority |
| Unknown cost | `UNKNOWN_COST` → BLOCK |
| Over budget | 10s / 50 > 30 → `OVER_BUDGET` → BLOCK, no Start |

Unpaid fixture is not treated as UNKNOWN=0. Cost is evaluated in memory; no new cost column / migration.

---

## 7. Idempotency

| Case | Behavior |
|---|---|
| Success replay | Same request / task / attempt / artifact; no second Start |
| In-flight replay | Resume persisted `providerJobId`; no second Task/Attempt/job |
| Failure replay | No automatic new Task |
| Retry | `RetryAttempt=true` → new Attempt, same Task, same frozen provider/model |

---

## 8. Artifact verification

VIDEO_TAKE only (not KEYFRAME / MIX):

| Check | Gate |
|---|---|
| MIME | must be `video/mp4` |
| Container | `ftyp` + `moov` + `mdat` |
| Stream | `vide` handler present |
| Duration | `mvhd` measurable and > 0 |
| SHA | SHA256 of physical bytes |
| Scope / shot / lineage | existing snapshot + parent artifact ids |

Invalid MP4 → `FAILED_VERIFICATION`, never `VERIFIED`, never CURRENT.

---

## 9. Dry-run execution

**SOURCE (in-memory harness)** — T01–T32.

**LIVE API** (`:5290` rebuilt, KIT_MKT admin, idempotency `os-dryrun-20260918-v1`):

| Field | Value |
|---|---|
| RequestId | `52eef47e-027f-4d71-a198-10dc8af47cea` |
| TaskId | `a254865f-e01a-499a-bbff-5ebbaa3ba761` |
| SnapshotId | `9bb1c8a1-6aae-4490-837d-5e897ba3d63e` |
| AttemptId | `986d1dbc-718b-41c9-8047-69f0d5fa668c` |
| ProviderJobId | `runway-dryrun-a254865fe01a499abbff5ebbaa3ba761` |
| ArtifactId | `6e04ae0a-5b26-4f0c-a463-327060121f95` |
| CURRENT | bound to that artifact (`CURRENT` + `CURRENT:VIDEO_TAKE`) |
| VISIBLE | bound to that artifact (`VISIBLE` + `VISIBLE:VIDEO_TAKE`) |
| Preview | same artifact / SHA / storageKey |

Idempotent replay returned the same identities. Events: `ATTEMPT_STARTED` → `PROVIDER_SUBMITTED` → `PROVIDER_SUCCEEDED` → `ARTIFACT_VERIFIED`.

3A KEYFRAME `33333333-…0371` remains `CURRENT:KEYFRAME`. Prior 3B VIDEO_TAKE was not deleted.

---

## 10. Physical output

| Field | Value |
|---|---|
| path | `App_Data/kit-video-i2v/os-test/aaaaaaaa-0000-4000-8000-000000000371/video/986d1dbc718b41c9804769f0d5fa668c.mp4` |
| storageKey | `os-test/aaaaaaaa-0000-4000-8000-000000000371/video/986d1dbc718b41c9804769f0d5fa668c` |
| bytes | 707 |
| SHA | `524ff617ef18e8d163c4e12bc7ea65ef3497fb99b4b9ad6fde641afaf1431708` |
| duration | 5 |
| MIME | `video/mp4` |

Preview media SHA matches artifact SHA. Not `os/`, `os-kf/`, or `veo/`.

---

## 11. Tests

`dotnet test tests/KitPlatform.Content.Tests --filter ProductionPipelineV1Tests` → **9/9 pass**.

| Test | Result |
|---|---|
| T01 request accepts runway/gen4_turbo | PASS (source + live) |
| T02 unknown provider rejected | PASS (source) |
| T03 provider/model mismatch rejected | PASS (source) |
| T04 model capability rejected | PASS (source) |
| T05 provider/model frozen | PASS (source + live) |
| T06 Attempt created | PASS (source + live) |
| T07 Start returns dry-run providerJobId | PASS (source + live) |
| T08 providerJobId persisted BEFORE Poll | PASS (source; live event order SUBMITTED before SUCCEEDED) |
| T09 Poll uses persisted providerJobId | PASS (source) |
| T10 Recover uses persisted providerJobId | PASS (source) |
| T11 dry-run does not make HTTP | PASS (source) |
| T12 physical MP4 exists | PASS (source + live) |
| T13 MP4 decodable | PASS (source) |
| T14 MIME verified | PASS (source + live) |
| T15 duration verified | PASS (source + live) |
| T16 SHA256 verified | PASS (source + live) |
| T17 VIDEO_TAKE artifact VERIFIED | PASS (source + live) |
| T18 CURRENT binding | PASS (source + live) |
| T19 VISIBLE binding | PASS (source + live) |
| T20 Preview resolves VISIBLE | PASS (source + live) |
| T21 success replay = no second execution | PASS (source + live) |
| T22 in-flight replay = no second execution | PASS (source) / LIVE UNVERIFIED |
| T23 retry new Attempt, same Task | PASS (source) |
| T24 provider/model mutation rejected | PASS (source) |
| T25 invalid MP4 cannot become VERIFIED | PASS (source) |
| T26 cost UNKNOWN rejected | PASS (source) |
| T27 cost over max rejected | PASS (source) |
| T28 late attempt cannot steal VISIBLE | PASS (source) / LIVE UNVERIFIED |
| T29 TEST cannot consume FILM artifact | PASS (source) |
| T30 FILM cannot consume TEST artifact | PASS (source) |
| T31 existing Production OS suites pass | PASS |
| T32 Runway real HTTP call count = 0 | PASS (source; dry-run transport only) |

---

## 12. Runtime

| Gate | Status |
|---|---|
| SOURCE VERIFIED | T01–T32 in-memory on Production OS + Runway adapter + DryRun transport |
| LIVE API VERIFIED | `:5290` rebuilt/restarted; POST `/requests` + idempotent replay + GET request/task/preview/media |
| LIVE DB VERIFIED | request / task / snapshot / attempt / artifact / bindings / events |
| LIVE STORAGE VERIFIED | physical MP4 under `os-test/{test_run_id}/video/`; SHA matches preview |

Source tests were not treated as live proof. T22/T28 live paths were not re-run against HTTP (no second paid-shaped Start).

GET `/actions` still evaluates FrozenProviderModel from the default Fixture adapter unless the POST body selects Runway. Execution authority is the POST contract, not GET.

---

## 13. Provider calls

| Call | Count |
|---|---|
| Runway HTTP | **0** |
| Veo | 0 |
| Gemini | 0 |
| Fal | 0 |
| ElevenLabs | 0 |
| Paid provider calls | **0** |
| Image generation | 0 |
| Real video generation | 0 |
| Dry-run Start (live) | 1 |

Dry-run job id is `runway-dryrun-…`, distinguishable from a vendor task id.

---

## 14. FILM isolation

Before and after the live dry-run:

| Check | Before | After |
|---|---|---|
| `series_build` count | 4 | 4 |
| `active_build_id` | official EP01 `4ef164d6-…2c7f` | same |
| EP01 `graph_json` MD5 | `12289be18284fbd4d4b166b82c09eba0` | same |
| EP99 `graph_json` MD5 | `c7cd2516627332c2648c1509395e4a64` | same |
| FILM artifacts | 0 | 0 |
| FILM bindings | 0 | 0 |
| Canon (`famixa_character`) | 7 | 7 |

EP01 / EP99 / active build / graph / Canon / Visual Authority / FILM artifacts and bindings were not written.

---

## 15. Legacy contamination

None on the new OS Runway path.

No `turboTaskId`, `takeUrl`, `previewUrl`, `acceptedTake.url`, `videoApproved`, `runwayAttempts`, `nextShotProductionCommand`, localStorage, IndexedDB, session blobs, `LegacySeriesGraphAdapter`, or `graph_json` as authority.

Final authority: physical bytes → ProductionArtifact → CURRENT → VISIBLE.

---

## 16. Files changed

**Created**

- `ProductionMotionCatalog.cs`
- `ProductionMotionCostGuard.cs`
- `IProductionMotionAdapterResolver.cs`
- `Providers/IRunwayTransport.cs`
- `Providers/DryRunRunwayTransport.cs`
- `Providers/RunwayMotionExecutionAdapter.cs`
- `ProductionRunwayBridgeV1Regression.cs`
- `docs/novixa/03-solution/FAMIXA_PRODUCTION_OS_REAL_PROVIDER_BRIDGE_FOUNDATION_V1_REPORT.md`

**Modified**

- `Providers/IMotionExecutionAdapter.cs` (async lifecycle; Fixture adapted)
- `ProductionCreateVideoService.cs`
- `ProductionExecutionService.cs`
- `ProductionWorkExecutionEngine.cs` (VIDEO_TAKE verification)
- `ProductionTestMp4.cs` (`TryReadDuration`)
- `ProductionApiDtos.cs`
- `CreateVideoActionContractGate.cs` (claim ProviderId/ModelId)
- `ProductionWorkIds.cs` (failure reasons)
- `ContentProductionController.cs`
- `ContentPackDependencyInjection.cs`
- `VideoGenerationExecutionService.cs`
- `ProductionPipelineV1Tests.cs`
- Contract / 3B / work / cutover test adapters and MP4 fixtures

**Migration:** none. `providerJobId` already exists.

UI / Veo / Gemini / Fal / ElevenLabs / Canon / VA / `series_build` / `graph_json`: not modified.

---

## 17. Remaining blockers

None that block this dry-run foundation.

Intentional holds (not regressions):

- Real Runway HTTP transport is not registered. `PaidNetworkForbidden` still blocks `IsPaid && RequiresNetwork`.
- Veo is not onboarded.
- Cost quote is evaluated, not persisted on Attempt.
- GET `/actions` does not yet take request ProviderId (POST does).

---

## 18. Exact next step

Register a real `HttpRunwayTransport` behind the existing `PaidNetworkForbidden` gate and prove one **explicitly authorized** paid `runway/gen4_turbo` CREATE_VIDEO on TEST only.

Do not remove the safety gate. Do not onboard Veo. Do not change UI.

END.
