# FAMIXA PRODUCTION OS — REAL RUNWAY TEST EXECUTION V2 REPORT

Date: 2026-09-19  
Mode: FORENSIC → PREPARED EXECUTION → ONE LIVE PROVIDER CALL → VERIFY  
Scope: TEST only (`bbbbbbbb-0000-4000-8000-000000000371` / `TEST-SC01-SH01`)  
Provider / model: `runway` / `gen4_turbo`  

**REAL RUNWAY START CALLS = 1**  
**REAL PROVIDER TOTAL OPERATIONS = 12** (Start 1 + Poll 10 + Download 1)  
**REAL MEDIA GENERATION = 1**  
**SECOND START = 0**  
**RETRY = 0**  

Every statement is classified: FACT / IMPLEMENTED / TESTED / LIVE VERIFIED / ASSUMED / UNVERIFIED / RECOMMENDED.

This phase does **not** judge video quality. It proves execution correctness.

After this report: **STOP**.

---

## 1. Executive Verdict

**PASS**

Classification: **PHASE G PASS**

One frozen Phase F chain was executed once through Production OS. Runway returned one job. The job id was persisted on the existing Attempt before polling. Physical MP4 bytes were downloaded under `os-test/{testRunId}/video/…`, verified, stored as one `VIDEO_TAKE` ProductionArtifact, and bound as `CURRENT:VIDEO_TAKE` + `VISIBLE:VIDEO_TAKE`. Preview resolved from the VISIBLE artifact, not from a provider URL.

This is the first complete live proof of:

PerformanceSnapshot  
→ ProductionTaskInputSnapshot  
→ ExecutePrepared  
→ IMotionExecutionAdapter / RunwayMotionExecutionAdapter  
→ HttpRunwayTransport / Runway  
→ ProviderJobId persisted  
→ physical Artifact  
→ CURRENT + VISIBLE  
→ preview

Historical 4A artifact `63bca29a-…` was not reused or overwritten. EP01 and EP99 graph fingerprints are unchanged.

---

## 2. Live preflight

Gates A–O against live DB **before** the provider Start:

| Gate | Result | Class |
|---|---|---|
| A Request exists | `2522ec00-d9e6-46d6-b276-ccd4a77ed2aa` `CREATE_VIDEO` `IN_PROGRESS` | **LIVE VERIFIED** |
| B Task exists | `88a376e6-b1af-4a39-b999-cd6026da8b3b` `RUNNING` | **LIVE VERIFIED** |
| C InputSnapshot exists | `76b35eb4-e708-4a71-8818-dfe1db6537b9` | **LIVE VERIFIED** |
| D Attempt exists | `4fb46ca0-b0df-43e0-92fe-c06286bea983` | **LIVE VERIFIED** |
| E Attempt STARTED | `STARTED` | **LIVE VERIFIED** |
| F ProviderJobId EMPTY | empty string | **LIVE VERIFIED** |
| G ProviderId = runway | match | **LIVE VERIFIED** |
| H ModelId = gen4_turbo | match | **LIVE VERIFIED** |
| I PerformanceSnapshotId | `8231d8a9-a6e3-47b7-bc02-c9fbff48bb3e` | **LIVE VERIFIED** |
| J PerformanceHash | exact `PERFORMANCE_HASH_V1:7715ad4e…b05ab8f1` | **LIVE VERIFIED** |
| K KeyframeArtifactId | `33333333-0000-4000-8000-000000000371` | **LIVE VERIFIED** |
| L Keyframe SHA | `3db4d45fba5e2a8611a7777dd29c2f323462340ba4c0a1c2fc94ec132bf28a20` | **LIVE VERIFIED** |
| M Scope TEST | build `bbbbbbbb-…0371` / kind `TEST` | **LIVE VERIFIED** |
| N Shot TEST-SC01-SH01 | match | **LIVE VERIFIED** |
| O No job on this attempt | `ATTEMPT_JOBS = 0` | **LIVE VERIFIED** |

No second Request / Task / Snapshot / Attempt / Keyframe was created. **LIVE VERIFIED**

---

## 3. Process / API identity

API was down at the start of this phase (`NO KitPlatform.Api PROCESS`, port 5290 idle). Restored with `scripts/api-dev.ps1` `Ensure-ApiDev -SkipWatcher -SkipBuild` from `E:\KitPlatform`. Restart is not a provider call.

| Check | Result | Class |
|---|---|---|
| Process path | `E:\KitPlatform\src\KitPlatform.Api\bin\Debug\net10.0\KitPlatform.Api.exe` | **LIVE VERIFIED** |
| `dotnet run` project | `E:\KitPlatform\src\KitPlatform.Api\KitPlatform.Api.csproj` `--launch-profile https` | **LIVE VERIFIED** |
| `GET /api/health` | `{"status":"ok"}` | **LIVE VERIFIED** |
| Production route | `GET /api/content/production/requests/{id}` 200 | **LIVE VERIFIED** |
| Other tree (`KitPlatform-novixa`) | not running on :5290 | **LIVE VERIFIED** |

---

## 4. Test scope

| Field | Value | Class |
|---|---|---|
| TestRunId | `aaaaaaaa-0000-4000-8000-000000000371` | **LIVE VERIFIED** |
| ScopeId / BuildId | `bbbbbbbb-0000-4000-8000-000000000371` | **LIVE VERIFIED** |
| Kind | `TEST` | **LIVE VERIFIED** |
| Shot | `TEST-SC01-SH01` | **LIVE VERIFIED** |
| Episode / Scene | `TEST` / `SC01` | **LIVE VERIFIED** |

---

## 5. PerformanceSnapshot

| Field | Value | Class |
|---|---|---|
| Id | `8231d8a9-a6e3-47b7-bc02-c9fbff48bb3e` | **LIVE VERIFIED** |
| Build / Shot | TEST sandbox / `TEST-SC01-SH01` | **LIVE VERIFIED** |
| Frozen at | 2026-09-18 22:57:22 +07 (Phase F) | **LIVE VERIFIED** |
| Mutated this phase | No | **LIVE VERIFIED** |

---

## 6. PerformanceHash

| Field | Value | Class |
|---|---|---|
| Stored hash | `PERFORMANCE_HASH_V1:7715ad4e51dde4264eaa679f43dc01246b0b25c992e99fff44cd27c0b05ab8f1` | **LIVE VERIFIED** |
| `digest(payload_json,'sha256')` | `7715ad4e51dde4264eaa679f43dc01246b0b25c992e99fff44cd27c0b05ab8f1` | **LIVE VERIFIED** |
| GET `/performance-snapshots/{id}` | same hash | **LIVE VERIFIED** |

---

## 7. Keyframe

| Field | Value | Class |
|---|---|---|
| ArtifactId | `33333333-0000-4000-8000-000000000371` | **LIVE VERIFIED** |
| Role / status | `KEYFRAME` / `VERIFIED` | **LIVE VERIFIED** |
| Binding | active `CURRENT:KEYFRAME` | **LIVE VERIFIED** |
| SHA | `3db4d45fba5e2a8611a7777dd29c2f323462340ba4c0a1c2fc94ec132bf28a20` | **LIVE VERIFIED** |
| Bytes | 232, PNG signature `89 50 4E 47` | **LIVE VERIFIED** |
| Storage | `os-test/aaaaaaaa-…0371/kf/22222222000040008000000000000371` | **LIVE VERIFIED** |
| New keyframe generated | No | **LIVE VERIFIED** |

---

## 8. Request

| Field | Pre | Post | Class |
|---|---|---|---|
| RequestId | `2522ec00-d9e6-46d6-b276-ccd4a77ed2aa` | same | **LIVE VERIFIED** |
| Action | `CREATE_VIDEO` | same | **LIVE VERIFIED** |
| Status | `IN_PROGRESS` | `COMPLETED` | **LIVE VERIFIED** |
| IdempotencyKey | `PHASE_F_LIVE_CUTOVER_V1` | same | **LIVE VERIFIED** |
| Count | 1 | 1 | **LIVE VERIFIED** |

---

## 9. Task

| Field | Pre | Post | Class |
|---|---|---|---|
| TaskId | `88a376e6-b1af-4a39-b999-cd6026da8b3b` | same | **LIVE VERIFIED** |
| Status | `RUNNING` | `SUCCEEDED` | **LIVE VERIFIED** |
| Provider / Model | runway / gen4_turbo | same | **LIVE VERIFIED** |
| InputSnapshotId | `76b35eb4-…` | same | **LIVE VERIFIED** |
| Count for this request | 1 | 1 | **LIVE VERIFIED** |

---

## 10. InputSnapshot

| Field | Value | Class |
|---|---|---|
| SnapshotId | `76b35eb4-e708-4a71-8818-dfe1db6537b9` | **LIVE VERIFIED** |
| SnapshotHash | `82405e434c4eaac5ed47f877fd747d19c5b4ae2c66f7390e93635961cf38d4e0` | **LIVE VERIFIED** |
| PerformanceSnapshotId | `8231d8a9-…` | **LIVE VERIFIED** |
| PerformanceHash | exact Phase F hash | **LIVE VERIFIED** |
| Provider / Model | runway / gen4_turbo | **LIVE VERIFIED** |
| KeyframeArtifactId / SHA | `33333333-…` / expected SHA | **LIVE VERIFIED** |
| Duration / Aspect | 5 / 16:9 | **LIVE VERIFIED** |
| Mutated after Phase F | No | **LIVE VERIFIED** |

---

## 11. Attempt

| Field | Pre | Post | Class |
|---|---|---|---|
| AttemptId | `4fb46ca0-b0df-43e0-92fe-c06286bea983` | same | **LIVE VERIFIED** |
| Number | 1 | 1 | **LIVE VERIFIED** |
| Status | `STARTED` | `PROVIDER_SUCCEEDED` | **LIVE VERIFIED** |
| ProviderJobId | EMPTY | `62311c1e-471d-4254-a800-3637ba1de850` | **LIVE VERIFIED** |
| Second attempt | — | none | **LIVE VERIFIED** |

This is a different job from historical `2746c54e-6287-4d79-852f-b779b9648ab9`. **LIVE VERIFIED**

---

## 12. One-shot permit

Existing `ProductionPaidNetworkOneShotPermit` (process singleton). Matches only TEST sandbox + `TEST-SC01-SH01` + runway + gen4_turbo. `TryConsumeStart` runs only inside `HttpRunwayTransport.CreateImageToVideoAsync`. **FACT** + **IMPLEMENTED**

Fresh API process: StartCount 0 before network. **LIVE VERIFIED** (`GET /runway-http-audit` 0/0/0)

No second orchestration mechanism was added. **FACT**

---

## 13. Cost decision

Canonical guard: `ProductionMotionCostGuard.Evaluate` → `FamixaProviderCostCatalog.Quote(runway, gen4_turbo, 5)`.

| Field | Value | Class |
|---|---|---|
| Frozen duration | 5 seconds | **LIVE VERIFIED** |
| Catalog quote | 5 × 5 = 25 credits | **FACT** |
| MaxCost | 30 | **FACT** |
| 25 ≤ 30 | allowed | **FACT** |
| Execution passed cost gate | yes (Start occurred) | **LIVE VERIFIED** |

No manual price invented. **FACT**

---

## 14. Provider / model

| Surface | Provider | Model | Class |
|---|---|---|---|
| Task | runway | gen4_turbo | **LIVE VERIFIED** |
| InputSnapshot | runway | gen4_turbo | **LIVE VERIFIED** |
| Attempt | runway | gen4_turbo | **LIVE VERIFIED** |
| Transport HTTP | `api.dev.runwayml.com/v1/image_to_video` | (client default `gen4_turbo`) | **LIVE VERIFIED** + **FACT** |

No Veo / Wan / Fal / other model. **LIVE VERIFIED**

---

## 15. ProviderJobId

`62311c1e-471d-4254-a800-3637ba1de850`

Persisted on Attempt. Copied onto Artifact. Used for every subsequent `GET /v1/tasks/{id}`. Audit `lastProviderJobId` matches. **LIVE VERIFIED**

---

## 16. Start

Exactly one ExecutePrepared:

`POST /api/content/production/requests`  
`PreparedAttemptId=4fb46ca0-…`  
`AuthorizePaidNetwork=true`  
same TEST body / snapshot / keyframe / runway / gen4_turbo

Server log:

1. `ContentProductionController.CreateVideo`
2. `ContentRunwayClient` `POST https://api.dev.runwayml.com/v1/image_to_video` → HTTP 200
3. `CreateVideo` finished HTTP 200 in 24701 ms with `ProductionCreateVideoResultDto`

`GET /runway-http-audit` after Start: `startCount=1`. **LIVE VERIFIED**

PowerShell client threw `PSInvalidOperationException` (NonInteractive prompt) while reading the response. The server still completed. Response JSON body is therefore **UNVERIFIED** on the client; server 200 + DB completion are **LIVE VERIFIED**.

---

## 17. Poll timeline

Code order: Start → `SubmitProviderJobAsync` (persist + `PROVIDER_SUBMITTED`) → `PollUntilDoneAsync` using the persisted id. **FACT**

Live events:

| Time (+07) | Event |
|---|---|
| 2026-09-18 22:57:23 | REQUEST_CREATED / TASK_CREATED / INPUT_FROZEN / ATTEMPT_STARTED (Phase F) |
| 2026-09-19 09:11:32 | PROVIDER_SUBMITTED |
| 2026-09-19 09:11:55 | PROVIDER_SUCCEEDED |

Poll HTTP: 10 `GET /v1/tasks/62311c1e-…` (audit `pollCount=10`). First poll log line is after the Start 200, and `PROVIDER_SUBMITTED` is recorded before `PROVIDER_SUCCEEDED`. **LIVE VERIFIED**

Per-poll PROCESSING payloads are not stored as OS events. Intermediate poll statuses: **UNVERIFIED** (count and terminal are live).

---

## 18. Provider terminal result

| Field | Value | Class |
|---|---|---|
| Terminal | SUCCESS | **LIVE VERIFIED** |
| Attempt | `PROVIDER_SUCCEEDED` | **LIVE VERIFIED** |
| Download | CloudFront transport GET HTTP 200 | **LIVE VERIFIED** |
| Failure classification | not applicable | **FACT** |

---

## 19. Physical output

| Field | Value | Class |
|---|---|---|
| Path | `E:\KitPlatform\src\KitPlatform.Api\App_Data\kit-video-i2v\os-test\aaaaaaaa-0000-4000-8000-000000000371\video\4fb46ca0b0df43e092fec06286bea983.mp4` | **LIVE VERIFIED** |
| StorageKey | `os-test/aaaaaaaa-0000-4000-8000-000000000371/video/4fb46ca0b0df43e092fec06286bea983` | **LIVE VERIFIED** |
| Bytes | 1,249,856 | **LIVE VERIFIED** |
| SHA256 | `e65535956d5f1e2613e5843fdb449bd75aaec5e9bb3de7091b5c06cdc9080c4e` | **LIVE VERIFIED** |
| Tied to AttemptId | filename is AttemptId `N` | **LIVE VERIFIED** |
| Tied to ProviderJobId | Artifact + Attempt columns | **LIVE VERIFIED** |
| EP01 / EP99 / legacy veo dirs | not used | **LIVE VERIFIED** |

---

## 20. Verification

| Check | Result | Class |
|---|---|---|
| Bytes exist | yes | **LIVE VERIFIED** |
| SHA256 | matches DB | **LIVE VERIFIED** |
| MIME | `video/mp4` | **LIVE VERIFIED** |
| ISO-BMFF ftyp / moov / mdat | present | **LIVE VERIFIED** |
| Video stream | `vide` + ffprobe `h264` 1280×720 | **LIVE VERIFIED** |
| Decodable | ffprobe duration `5.041667` | **LIVE VERIFIED** |
| Duration > 0 | DB `5.042` | **LIVE VERIFIED** |
| Scope / shot / task / attempt | TEST / TEST-SC01-SH01 / `88a376e6` / `4fb46ca0` | **LIVE VERIFIED** |
| Provider / model / job | runway / gen4_turbo / `62311c1e` | **LIVE VERIFIED** |
| InputSnapshotHash | exact | **LIVE VERIFIED** |
| PerformanceSnapshotId / Hash | exact via InputSnapshot join | **LIVE VERIFIED** |

Server `VerifyArtifactCoreAsync` marked `VERIFIED`. **LIVE VERIFIED**

---

## 21. ProductionArtifact

| Field | Value | Class |
|---|---|---|
| ArtifactId | `5872d0a9-0c3f-42b0-9fe5-21f1433ab831` | **LIVE VERIFIED** |
| Role | `VIDEO_TAKE` | **LIVE VERIFIED** |
| Status | `VERIFIED` | **LIVE VERIFIED** |
| TaskId / AttemptId | exact frozen ids | **LIVE VERIFIED** |
| ProviderJobId | `62311c1e-…` | **LIVE VERIFIED** |
| InputSnapshotHash | `82405e43…d4e0` | **LIVE VERIFIED** |
| SHA / storage / MIME / duration | see §19–20 | **LIVE VERIFIED** |
| Count for this task | 1 | **LIVE VERIFIED** |
| Different from historical 4A | yes (`63bca29a-…` unchanged) | **LIVE VERIFIED** |

`ProductionArtifact` has no dedicated PerformanceSnapshot columns; identity resolves through InputSnapshot. **FACT** (existing schema)

Provider URL is not the Artifact authority. **LIVE VERIFIED**

---

## 22. CURRENT binding

Active:

- `CURRENT:VIDEO_TAKE` `eafbe82a-9edb-4917-82d5-f75898a01c0f` → `5872d0a9-…`
- `CURRENT` alias `fbe1751b-4bc6-43b4-8f36-5625c0aac2f7` → `5872d0a9-…`

Bound by Task `88a376e6-…`. Historical CURRENT on `63bca29a-…` is unbound, not deleted. **LIVE VERIFIED**

`CURRENT:KEYFRAME` remains `33333333-…`. **LIVE VERIFIED**

---

## 23. VISIBLE binding

Active:

- `VISIBLE:VIDEO_TAKE` `4b43e499-ec31-4fc2-8edd-faa9537c2c8f` → `5872d0a9-…`
- `VISIBLE` `fbb2bdf0-07ea-4c4e-8840-3e281e58f6dd` → `5872d0a9-…`

**LIVE VERIFIED**

---

## 24. Preview

`GET /api/content/production/shots/{TEST}/{TEST-SC01-SH01}/preview`:

| Field | Value | Class |
|---|---|---|
| empty | false | **LIVE VERIFIED** |
| artifactId | `5872d0a9-…` | **LIVE VERIFIED** |
| byteSha256 | `e6553595…0c4e` | **LIVE VERIFIED** |
| storageKey | `os-test/…/video/4fb46ca0…` | **LIVE VERIFIED** |
| fileUrl | Production OS `/preview/media` | **LIVE VERIFIED** |

`GET …/preview/media` bytes = 1,249,856, SHA matches Artifact. **LIVE VERIFIED**

Browser Director UI playback: **UNVERIFIED** (not opened). Do not claim browser LIVE VERIFIED.

---

## 25. Complete lineage

```
Request 2522ec00-…
  → Task 88a376e6-…
    → InputSnapshot 76b35eb4-…
      → PerformanceSnapshot 8231d8a9-…
      → PerformanceHash PERFORMANCE_HASH_V1:7715ad4e…b05ab8f1
    → Attempt 4fb46ca0-…
      → ProviderJobId 62311c1e-…
        → Artifact 5872d0a9-… VIDEO_TAKE VERIFIED
          → CURRENT:VIDEO_TAKE
          → VISIBLE:VIDEO_TAKE
            → Preview media = VISIBLE Artifact bytes
```

Equalities:

| Invariant | Result | Class |
|---|---|---|
| InputSnapshot.PerformanceSnapshotId == Snapshot.Id | true | **LIVE VERIFIED** |
| InputSnapshot.PerformanceHash == Snapshot.Hash | true | **LIVE VERIFIED** |
| Attempt.ProviderId == runway | true | **LIVE VERIFIED** |
| Attempt.ModelId == gen4_turbo | true | **LIVE VERIFIED** |
| Artifact.AttemptId == Attempt.Id | true | **LIVE VERIFIED** |
| Artifact.ProviderJobId == Attempt.ProviderJobId | true | **LIVE VERIFIED** |
| Artifact.InputSnapshotHash == InputSnapshot.Hash | true | **LIVE VERIFIED** |
| Artifact Performance* via InputSnapshot | true | **LIVE VERIFIED** |
| CURRENT.artifactId == Artifact.Id | true | **LIVE VERIFIED** |
| VISIBLE.artifactId == Artifact.Id | true | **LIVE VERIFIED** |
| Preview source == VISIBLE Artifact | true | **LIVE VERIFIED** |

---

## 26. Cross-scope check

Active bindings for Artifact `5872d0a9-…` exist only on TEST scope + `TEST-SC01-SH01`.

`TEST-SC01-SH02` CURRENT/VISIBLE video remains `583d7700-…`.  
EP01 / EP99 builds have no binding to the new artifact. **LIVE VERIFIED**

---

## 27. Legacy contamination check

| Claim | Evidence | Class |
|---|---|---|
| Production OS initiated | `POST /api/content/production/requests` → `ContentProductionController.CreateVideo` | **LIVE VERIFIED** |
| ExecutePrepared path | `PreparedAttemptId` set; no new Request | **FACT** + **LIVE VERIFIED** |
| IMotionExecutionAdapter | resolver registers `RunwayMotionExecutionAdapter` for runway/gen4_turbo | **FACT** |
| RunwayMotionExecutionAdapter | StartFrozen → `IRunwayTransport.CreateImageToVideo` | **FACT** |
| HttpRunwayTransport | DI `IRunwayTransport` → `HttpRunwayTransport`; live `ContentRunwayClient` HTTP | **FACT** + **LIVE VERIFIED** |
| Legacy `IVideoGenerationProvider` | not on this path | **FACT** |
| Legacy Series Turbo endpoint | no turbo HTTP in this execution | **LIVE VERIFIED** |
| SeriesShotRun / acceptedTake.url authority | Artifact storageKey is `os-test/…`, preview is OS media | **LIVE VERIFIED** |
| Provider URL as Artifact authority | no | **LIVE VERIFIED** |

Runtime CLR stack dump was not captured. Path is proven by controller log + DI + one `image_to_video` + OS events. Call-stack claim: **FACT** + **LIVE VERIFIED** (HTTP/OS), not a debugger stack.

---

## 28. Historical artifact check

`63bca29a-f899-4008-a1bf-6000787bafd6`:

| Field | Pre | Post |
|---|---|---|
| SHA | `f94a9d097e5d90c0721501540e4de458334588b0eb552a3c1e6878d84c8c8c17` | same |
| Bytes | 1,029,598 | same |
| StorageKey | `os-test/…/video/8d82f822b7674701a23f1f85b8a1bd39` | same |
| ProviderJobId | `2746c54e-…` | same |
| File LastWriteTime | 2026-09-18 07:33:28 +07 | unchanged |

Bindings to it were unbound when the new CURRENT/VISIBLE were written. Row and bytes were not overwritten. **LIVE VERIFIED**

---

## 29. EP01 check

Build `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
graph md5 before = after = `415fedf306ed23d1ace94a794400b440`  

**LIVE VERIFIED**

---

## 30. EP99 check

Build `77262ec0-74ad-459d-9128-f09bb553a5dd`  
graph md5 before = after = `c7cd2516627332c2648c1509395e4a64`  

**LIVE VERIFIED**

---

## 31. Provider call count

| Operation | Count | Class |
|---|---|---|
| Start (`POST /v1/image_to_video`) | 1 | **LIVE VERIFIED** |
| Poll (`GET /v1/tasks/62311c1e-…`) | 10 | **LIVE VERIFIED** |
| Download (transport GET) | 1 | **LIVE VERIFIED** |
| Total provider operations | 12 | **LIVE VERIFIED** |
| Second Start | 0 | **LIVE VERIFIED** |

---

## 32. Media generation count

Exactly one video file written for this Attempt. One `VIDEO_TAKE` artifact. **LIVE VERIFIED**

---

## 33. Retry count

0. No second Start, Attempt, Task, Request, snapshot, or keyframe. **LIVE VERIFIED**

---

## 34. Test results

This phase is live execution, not a new xUnit suite.

Prior architecture coverage remains: Phase D bridge + Phase F live-cutover regressions. **TESTED** (previous phases)

Live G gates above: **LIVE VERIFIED**

---

## 35. Exact evidence

| Item | Evidence |
|---|---|
| API identity | process path `E:\KitPlatform\…\KitPlatform.Api.exe`; health `ok` |
| Preflight A–O | live SQL before Start |
| Manifest | printed; Start=0; ProviderJobId empty |
| Start | `.dev/api.log` `POST /v1/image_to_video` 200; audit start=1 |
| Persist-before-poll | `PROVIDER_SUBMITTED` 09:11:32 then polls; code `SubmitProviderJobAsync` before `PollUntilDoneAsync` |
| Job | `62311c1e-471d-4254-a800-3637ba1de850` |
| Artifact | `5872d0a9-0c3f-42b0-9fe5-21f1433ab831` VERIFIED |
| File SHA | `e65535956d5f1e2613e5843fdb449bd75aaec5e9bb3de7091b5c06cdc9080c4e` |
| Preview | OS preview DTO + media SHA match |
| EP01 / EP99 | md5 unchanged |
| Historical 4A | SHA/bytes/key unchanged |

---

## 36. Remaining gaps

1. Browser Director preview was not opened. API + filesystem preview is live; browser remains **UNVERIFIED**.
2. Individual PROCESSING poll payloads are not OS events. Count and terminal are live.
3. `ProductionArtifact` still resolves Performance identity via InputSnapshot join, not dedicated columns. **FACT**
4. Client did not persist the POST JSON body (NonInteractive). Server 200 is live.
5. Unrelated `NotifyQueueWorker` Dapper errors appeared in the API log. Not on this path. **FACT**

No second Start is recommended. The next decision is outside this phase.

---

## 37. Verdict

**PHASE G PASS**

Exactly one real Runway Start through Production OS produced one persisted job, one verified TEST video artifact, and CURRENT + VISIBLE bindings on the frozen Phase F chain. Official films and the historical 4A artifact were not mutated.

---

## Critical questions

| Q | Answer | Class |
|---|---|---|
| Q1 Exactly ONE real Runway Start? | Yes | **LIVE VERIFIED** |
| Q2 Second Start attempted? | No | **LIVE VERIFIED** |
| Q3 Exactly ONE provider job? | Yes, `62311c1e-…` | **LIVE VERIFIED** |
| Q4 ProviderJobId persisted before Poll? | Yes (`PROVIDER_SUBMITTED` then polls) | **LIVE VERIFIED** |
| Q5 Could Poll use persisted id? | Yes; all GETs used `62311c1e-…` | **LIVE VERIFIED** |
| Q6 Did Runway return a video? | Yes; download 200 + decodable MP4 | **LIVE VERIFIED** |
| Q7 Physical bytes downloaded? | Yes, 1,249,856 | **LIVE VERIFIED** |
| Q8 MP4 physically verified? | Yes (boxes, h264, duration > 0, SHA) | **LIVE VERIFIED** |
| Q9 ProductionArtifact created? | Yes `5872d0a9-…` | **LIVE VERIFIED** |
| Q10 Artifact → exact Attempt? | Yes | **LIVE VERIFIED** |
| Q11 Artifact → exact ProviderJobId? | Yes | **LIVE VERIFIED** |
| Q12 Artifact preserves InputSnapshotHash? | Yes | **LIVE VERIFIED** |
| Q13 Artifact preserves PerformanceSnapshotId? | Yes via InputSnapshot | **LIVE VERIFIED** |
| Q14 Artifact preserves PerformanceHash? | Yes via InputSnapshot | **LIVE VERIFIED** |
| Q15 CURRENT → new ArtifactId? | Yes | **LIVE VERIFIED** |
| Q16 VISIBLE → new ArtifactId? | Yes | **LIVE VERIFIED** |
| Q17 Preview from VISIBLE artifact? | Yes (API + bytes) | **LIVE VERIFIED** |
| Q18 Legacy URL used as authority? | No | **LIVE VERIFIED** |
| Q19 Cross-shot/build/scope contamination? | No | **LIVE VERIFIED** |
| Q20 EP01 modified? | No | **LIVE VERIFIED** |
| Q21 EP99 modified? | No | **LIVE VERIFIED** |
| Q22 Historical TEST artifact modified? | No | **LIVE VERIFIED** |
| Q23 Real provider Starts? | 1 | **LIVE VERIFIED** |
| Q24 Real media generations? | 1 | **LIVE VERIFIED** |
| Q25 Retry performed? | No | **LIVE VERIFIED** |
| Q26 First complete REAL OS → Runway → Artifact → Binding proof? | Yes | **LIVE VERIFIED** |

---

## STOP

No retry. No second shot. No Veo / Wan / image / voice / lip-sync / mix / assemble. No Director / EP01 / EP99 / Performance Contract / Timing V3 / Visual Authority change.

The next decision is based only on this report.
