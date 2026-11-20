# FAMIXA PRODUCTION OS — CHECKPOINT 4A
# ONE REAL RUNWAY VIDEO V1

Date: 2026-09-18  
Mode: CONTROLLED LIVE EXECUTION  
Official EP01: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
Official EP99: `77262ec0-74ad-459d-9128-f09bb553a5dd`

After one real Runway job: STOP.

---

## 1. VERDICT

PASS WITH GAPS

Exactly one real `runway` / `gen4_turbo` job completed through Production OS on official TEST SH01.

TEST request → ProductionRequest → Action Contract → ProductionTask → immutable InputSnapshot → provider/model freeze → Attempt → Runway Start → persist `providerJobId` → Poll/Recover → download physical MP4 → verify → ProductionArtifact VERIFIED → CURRENT:VIDEO_TAKE → VISIBLE:VIDEO_TAKE → Preview identity = that artifact.

Gaps are the two live proofs this checkpoint forbids faking:

- Live idempotent replay was not executed (would risk a second paid Start).
- Process-restart recovery was not executed (server was not killed).

Those are documented as SOURCE VERIFIED / LIVE UNVERIFIED. They are not silent PASSes.

---

## 2. PRE-FLIGHT

Recorded in `.tmp/os-4a-preflight.md` **before** any Runway HTTP.  
Prepare POST (`/api/content/production/requests/prepare`) produced the IDs.  
`GET /runway-http-audit` after Prepare: Start=0 Poll=0 Download=0.

```
SCOPE
TEST

TEST_RUN_ID
aaaaaaaa-0000-4000-8000-000000000371

SCOPE_ID
bbbbbbbb-0000-4000-8000-000000000371

SHOT
TEST-SC01-SH01

KEYFRAME_ARTIFACT_ID
33333333-0000-4000-8000-000000000371

KEYFRAME_SHA
3db4d45fba5e2a8611a7777dd29c2f323462340ba4c0a1c2fc94ec132bf28a20

KEYFRAME_STATUS
VERIFIED

KEYFRAME_BINDING
CURRENT:KEYFRAME

STORY
TEST STORY — child walks into a room.

ACTING
TEST ACTING — walks forward and looks around.

VISUAL_CONTRACT
TEST VISUAL — 16:9 interior, locked camera, no film graph.

TIMING
5 seconds performance/editorial intent

PROVIDER
runway

MODEL
gen4_turbo

PROVIDER_CAPABILITY
Motion

PROVIDER_DURATION
5

ASPECT
16:9

ESTIMATED_COST
25

MAX_COST
30

INPUT_SNAPSHOT_ID
2a563568-d3f3-4a45-b79a-33ca2579a696

INPUT_SNAPSHOT_HASH
7535bf34e47da70ca37c11454fdc11eda0772d43543a60edded027b37e1e4890

TASK_ID
274cdcba-c3ec-4680-8fc7-cc01157528ee

ATTEMPT_ID
8d82f822-b767-4701-a23f-1f85b8a1bd39

CONFIRMATION
EXPLICIT

NETWORK
REAL RUNWAY HTTP ENABLED FOR THIS TEST ONLY

FILM_MUTATION
FORBIDDEN
```

Additional freeze facts (DB, before Start):

- RequestId `eafbf0ac-6504-4aad-b448-a91e92b2eca9`
- Attempt status STARTED, `providerJobId` empty
- Compiler `SHOT_EXECUTION_COMPILER_V1` hash `5cf61896e444ab2765cb044fabe42e1c0a22d62dfcee5c33b0ee8dc0485d32fa`
- Parent artifact ids `["33333333-0000-4000-8000-000000000371"]`
- Snapshot trigger `fn_prod_snapshot_immutable` remains in place

Pre-flight: COMPLETE. Runway Start was authorized once.

---

## 3. EXECUTION IDENTITY

| Field | Value |
|---|---|
| RequestId | `eafbf0ac-6504-4aad-b448-a91e92b2eca9` |
| TaskId | `274cdcba-c3ec-4680-8fc7-cc01157528ee` |
| SnapshotId | `2a563568-d3f3-4a45-b79a-33ca2579a696` |
| SnapshotHash | `7535bf34e47da70ca37c11454fdc11eda0772d43543a60edded027b37e1e4890` |
| AttemptId | `8d82f822-b767-4701-a23f-1f85b8a1bd39` |
| AttemptNumber | 1 |
| ProviderId | `runway` |
| ModelId | `gen4_turbo` |
| ProviderJobId | `2746c54e-6287-4d79-852f-b779b9648ab9` |
| Task status | SUCCEEDED |
| Request status | COMPLETED |
| Attempt status | PROVIDER_SUCCEEDED |

Event order (request `eafbf0ac-…`):

REQUEST_CREATED → TASK_CREATED → INPUT_FROZEN → ATTEMPT_STARTED → **PROVIDER_SUBMITTED** → PROVIDER_SUCCEEDED → ARTIFACT_VERIFIED → CURRENT_BOUND → VISIBLE_BOUND

`PROVIDER_SUBMITTED` at 07:33:03+07; first poll HTTP follows that persist. Poll used the persisted job id only. Start was not called again.

---

## 4. RUNWAY CALLS

Process audit (`ProductionRunwayHttpAudit` after Execute):

| Call | Count |
|---|---|
| Start | 1 |
| Poll | 11 |
| Download | 1 |
| Total | 13 |

API log (this process, this job only):

- POST `https://api.dev.runwayml.com/v1/image_to_video` = 1
- GET `https://api.dev.runwayml.com/v1/tasks/2746c54e-6287-4d79-852f-b779b9648ab9` = 11
- GET CloudFront MP4 = 1

Veo = 0. Gemini = 0. Fal = 0. ElevenLabs = 0.

Poll 11 includes one same-process `RecoverFrozenAsync` (`GetTask` at loop i==1). No second Start.

---

## 5. COST

| Field | Value |
|---|---|
| Estimated cost | 25 credits (server catalog, 5s gen4_turbo) |
| MaxCost | 30 |
| Authorization | Explicit, TEST-scoped, this-execution, `runway`/`gen4_turbo`, process-local `ProductionPaidNetworkOneShotPermit.TryConsumeStart` once. `AuthorizePaidNetwork=true` on Prepare + Execute only. Global paid gate remains. |
| Actual billing | Not read from the Runway organization ledger. Spend is the one gen4_turbo 5s job `2746c54e-…`. |

Cost UNKNOWN did not occur. Estimate 25 ≤ 30. Guard passed before Start.

---

## 6. ARTIFACT

| Field | Value |
|---|---|
| ArtifactId | `63bca29a-f899-4008-a1bf-6000787bafd6` |
| Role | VIDEO_TAKE |
| StorageKey | `os-test/aaaaaaaa-0000-4000-8000-000000000371/video/8d82f822b7674701a23f1f85b8a1bd39` |
| Bytes | 1,029,598 |
| SHA256 | `f94a9d097e5d90c0721501540e4de458334588b0eb552a3c1e6878d84c8c8c17` |
| MIME | video/mp4 |
| Duration | 5.042 s |
| Verification | VERIFIED |

Physical file:

`src/KitPlatform.Api/App_Data/kit-video-i2v/os-test/aaaaaaaa-0000-4000-8000-000000000371/video/8d82f822b7674701a23f1f85b8a1bd39.mp4`

Disk SHA = artifact SHA. Non-zero. ftyp / moov / mdat / vide present. Duration > 0. Scope TEST. Shot TEST-SC01-SH01. Attempt `8d82f822-…`. Snapshot hash matches. Parent keyframe `33333333-…`. ProviderId runway. ModelId gen4_turbo.

Not stored under `os/`, `os-kf/`, `veo/`, or legacy Famixa take paths.

---

## 7. BINDINGS

Active on TEST-SC01-SH01 after the run:

| Role | ArtifactId | BindingId |
|---|---|---|
| CURRENT:KEYFRAME | `33333333-0000-4000-8000-000000000371` | `44444444-0000-4000-8000-000000000371` |
| CURRENT:VIDEO_TAKE | `63bca29a-f899-4008-a1bf-6000787bafd6` | `7b66df4a-f679-4ccc-9ab7-72aa4e74662d` |
| VISIBLE:VIDEO_TAKE | `63bca29a-f899-4008-a1bf-6000787bafd6` | `d82ae232-24fe-4192-8073-ace935215e65` |
| CURRENT | `63bca29a-f899-4008-a1bf-6000787bafd6` | `2d87b8dd-f33b-405c-b7e7-2dca3aa6cbfe` |
| VISIBLE | `63bca29a-f899-4008-a1bf-6000787bafd6` | `0cda6903-fe84-40db-870b-c82e292af372` |

Binding identity = artifactId. Not URL / latest / timestamp / task code / shot code alone.

---

## 8. PREVIEW

Execute response `previewKind = visible` returned the same identity:

| Field | Value |
|---|---|
| ArtifactId | `63bca29a-f899-4008-a1bf-6000787bafd6` |
| SHA | `f94a9d097e5d90c0721501540e4de458334588b0eb552a3c1e6878d84c8c8c17` |
| StorageKey | `os-test/aaaaaaaa-0000-4000-8000-000000000371/video/8d82f822b7674701a23f1f85b8a1bd39` |

`GetPreviewAsync` / `ResolveVisiblePreviewAsync` read VISIBLE → artifactId → storageKey. No `previewUrl` authority.

A separate GET `/preview/media` was not re-issued after Execute (auth-gated). Preview identity is proven by the Execute DTO + active VISIBLE row + disk SHA.

---

## 9. IDEMPOTENCY

SOURCE VERIFIED  
LIVE IDEMPOTENCY UNVERIFIED

Replay of the same idempotency key was not executed. After a successful paid Start, a second POST could consume a new process permit if the API were restarted, or be blocked in-process by the one-shot permit. Safety over completeness. Start count remains 1.

---

## 10. RECOVERY

SOURCE VERIFIED  
LIVE RECOVERY UNVERIFIED

Same-process `RecoverFrozenAsync` ran once during Poll (i==1) against persisted `providerJobId` `2746c54e-…`. The production server was not killed. Process-restart recovery is therefore not claimed.

---

## 11. FILM ISOLATION

| Field | Before | After |
|---|---|---|
| series_build count | 4 | 4 |
| EP01 `4ef164d6-…` graph md5 | `12289be18284fbd4d4b166b82c09eba0` | `12289be18284fbd4d4b166b82c09eba0` |
| EP99 `77262ec0-…` graph md5 | `c7cd2516627332c2648c1509395e4a64` | `c7cd2516627332c2648c1509395e4a64` |
| active_build_id | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` |
| Canon (`famixa_character`) | 7 | 7 |
| VA (`video_visual_system`) | 2 rows / hash `f05333ce07cf9d0ce66be380a364615d` | 2 rows / hash `f05333ce07cf9d0ce66be380a364615d` |
| FILM artifacts | 0 | 0 |
| FILM bindings | 0 | 0 |

NO CHANGE. No Film mutation.

---

## 12. LEGACY CONTAMINATION

None as authority.

The new VIDEO_TAKE is a ProductionArtifact with Attempt + snapshot hash + parent keyframe + `os-test/…/video/` storage. It is not authoritative through `turboTaskId`, `takeUrl`, `previewUrl`, `acceptedTake.url`, `videoApproved`, `runwayAttempts`, `nextShotProductionCommand`, localStorage, IndexedDB, session blobs, `LegacySeriesGraphAdapter`, or `graph_json`.

Download used a CloudFront URL only as a transport fetch. Authority after download is the stored bytes + SHA + artifactId.

---

## 13. TESTS

`dotnet test tests/KitPlatform.Content.Tests/KitPlatform.Content.Tests.csproj --filter FullyQualifiedName~ProductionPipelineV1Tests`

Passed 10 / 10.

Includes Work T01–T24, Pipeline, CreateVideo fixture, Action Contract T01–T11, Scope Anchor, TEST foundation T01–T16, KEYFRAME 3A T01–T24, CREATE_VIDEO 3B T01–T29, Bridge T01–T32, paid-network one-shot permit.

---

## 14. SOURCE VS LIVE

| Proof | Result |
|---|---|
| SOURCE VERIFIED | Yes — permit, preflight, two-phase Prepare/Execute, HTTP transport, audit, tests |
| LIVE API VERIFIED | Yes — Prepare (no HTTP) then one Execute (200, 26.3s) |
| LIVE DB VERIFIED | Yes — request/task/snapshot/attempt/artifact/bindings/events |
| LIVE STORAGE VERIFIED | Yes — physical MP4 SHA matches artifact |

---

## 15. FILES CHANGED

4A-only (this checkpoint). Earlier Production OS / Fal / Veo files are not claimed here.

- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionPaidNetworkPermit.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoPreflight.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionPaidNetworkPermitV1Regression.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionRunwayHttpAudit.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionApiDtos.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionTestMp4.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionWorkExecutionEngine.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionStateMachines.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/IProductionExecutionService.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionExecutionService.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/Providers/IMotionExecutionAdapter.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/Providers/IRunwayTransport.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/Production/Providers/RunwayMotionExecutionAdapter.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/Production/HttpRunwayTransport.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentRunwayClient.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentPackDependencyInjection.cs`
- `src/KitPlatform.Api/Controllers/Content/ContentProductionController.cs`
- `tests/KitPlatform.Content.Tests/ProductionPipelineV1Tests.cs`
- `docs/novixa/03-solution/FAMIXA_PRODUCTION_OS_CHECKPOINT_4A_ONE_REAL_RUNWAY_VIDEO_V1_REPORT.md`

---

## 16. INCIDENTS

- `:5290` DLL lock required `Stop-ApiDevAll` before rebuild. Not a compile defect.
- Unrelated `NotifyQueueWorker` Dapper materialization error in API logs. Not on the Production OS path.
- Official 160×90 / 232-byte TEST KEYFRAME was accepted by Runway. No keyframe was regenerated.
- Previous CURRENT/VISIBLE VIDEO_TAKE pointers moved from the dry-run artifact `6e04ae0a-…` to the new Runway artifact. The dry-run and 3B fixture **rows and files** were not deleted or rewritten.
- Three `CURRENT_BOUND` events: `CURRENT:VIDEO_TAKE`, `CURRENT`, and the role mapper for the extra current-style bind. All reference artifact `63bca29a-…`.

No second Start. No automatic retry. No code patch during the live HTTP window after Start.

`ProductionTestMp4.TryReadDuration` gained ISO-BMFF `mvhd` v1 support **before** Start, so a real Runway MP4 would not fail verification on timescale layout alone.

---

## 17. REMAINING GAPS

- Live idempotent replay: SOURCE VERIFIED / LIVE UNVERIFIED.
- Process-restart recovery: SOURCE VERIFIED / LIVE UNVERIFIED.
- Runway organization credit ledger was not queried.
- Separate GET `/preview` + `/preview/media` after Execute was not re-issued; identity is proven by Execute DTO + VISIBLE + disk.

---

## 18. EXACT NEXT STEP

Stop. Wait for the operator to name the next Production OS phase.

Do not run a second video. Do not run Veo. Do not run LipSync. Do not modify Film. Do not fix Director / Video Studio UI.
