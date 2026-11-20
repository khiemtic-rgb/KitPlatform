# FAMIXA PRODUCTION OS — REAL RUNWAY TEST EXECUTION V1 REPORT

Date: 2026-09-18  
Mode: FORENSIC → LIVE TEST EXECUTION  
Scope: TEST only  
Provider: runway / gen4_turbo  

**REAL RUNWAY START CALLS = 0**  
**REAL PROVIDER CALLS = 0**  
**REAL MEDIA GENERATION = 0**  

EP01 / EP99: not mutated  

Every statement is classified: FACT / IMPLEMENTED / TESTED / LIVE VERIFIED / ASSUMED / UNVERIFIED / RECOMMENDED.

Do not read this as a second paid generation.  
Do not read this as a source fix.  
Do not start another shot from this report.

---

## Verdict

**OUTCOME A — PREFLIGHT BLOCKED**

Classification: **PREFLIGHT_BLOCKED** / **ACTION_BLOCKED**

Exact reason: **MISSING_PERFORMANCE_SNAPSHOT** (live schema + live rows).

Phase C requires a frozen PerformanceSnapshot on TEST CREATE_VIDEO. Live local DB does not have `pack_content.performance_snapshot`. Input snapshots have no Performance* columns. No existing snapshot can be resolved. The mission forbids creating a snapshot, applying a migration, or changing source to unblock.

Therefore the single authorized Runway Start was never issued.

This is a valid Outcome A. It is not a silent architecture PASS. It is not a provider failure.

---

## 1. Preflight result

| Gate | Result | Class |
|---|---|---|
| Official film / EP01 / EP99 selected | No. Target is TEST sandbox only. | **LIVE VERIFIED** |
| TEST scope exists | Yes. `bbbbbbbb-0000-4000-8000-000000000371` kind=TEST | **LIVE VERIFIED** |
| Shot TEST-SC01-SH01 exists | Yes. `cccccccc-0000-4000-8000-000000000371` | **LIVE VERIFIED** |
| Physical CURRENT KEYFRAME | Yes. Artifact `33333333-0000-4000-8000-000000000371` VERIFIED, SHA match | **LIVE VERIFIED** |
| PerformanceSnapshot exists | **No.** Table missing. Zero resolvable snapshots. | **LIVE VERIFIED** |
| Action Contract GET==POST ready | Cannot be ready. TEST requires PerformanceSnapshot. | **FACT** (code) + **LIVE VERIFIED** (schema) |
| Explicit runway/gen4_turbo | Would be supplied explicitly. Not frozen this run. | **UNVERIFIED** this request |
| Cost UNKNOWN / over MaxCost | Catalog would quote 25 ≤ 30 for 5s gen4_turbo. Not evaluated live (stopped earlier). | **FACT** (catalog) / **UNVERIFIED** (live Evaluate) |
| Credentials | Org `video_config_json.runwayApiKey` present, length 132. Process env `RUNWAY_API_KEY` absent. Appsettings empty. | **CONFIGURED / NOT VERIFIED** |
| API process healthy | PID file exists; `/api/health` timed out. | **LIVE VERIFIED** (down/unresponsive) |
| In-flight / duplicate this request | This phase never created a request. Prior 4A job is a different request. | **LIVE VERIFIED** |

Stop condition hit: **PerformanceSnapshot is missing.** No automatic fix.

---

## 2. TEST scope / build / shot

| Field | Value | Class |
|---|---|---|
| TEST_RUN_ID | `aaaaaaaa-0000-4000-8000-000000000371` | **LIVE VERIFIED** |
| SCOPE_ID / BUILD_ID | `bbbbbbbb-0000-4000-8000-000000000371` | **LIVE VERIFIED** |
| TEST_SHOT_ID | `cccccccc-0000-4000-8000-000000000371` | **LIVE VERIFIED** |
| SHOT_CODE | `TEST-SC01-SH01` | **LIVE VERIFIED** |
| Kind | TEST | **LIVE VERIFIED** |
| Official EP01 | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` not selected | **LIVE VERIFIED** |
| Official EP99 | `77262ec0-74ad-459d-9128-f09bb553a5dd` not selected | **LIVE VERIFIED** |

---

## 3. Keyframe artifact identity

| Field | Value | Class |
|---|---|---|
| ArtifactId | `33333333-0000-4000-8000-000000000371` | **LIVE VERIFIED** |
| Role | KEYFRAME | **LIVE VERIFIED** |
| Binding | CURRENT:KEYFRAME (active) | **LIVE VERIFIED** |
| StorageKey | `os-test/aaaaaaaa-0000-4000-8000-000000000371/kf/22222222000040008000000000000371` | **LIVE VERIFIED** |
| Disk path | `src/KitPlatform.Api/App_Data/kit-video-i2v/os-test/aaaaaaaa-0000-4000-8000-000000000371/kf/22222222000040008000000000000371.mp4` | **LIVE VERIFIED** |
| Bytes | 232 | **LIVE VERIFIED** |
| SHA256 | `3db4d45fba5e2a8611a7777dd29c2f323462340ba4c0a1c2fc94ec132bf28a20` (disk = catalog = artifact) | **LIVE VERIFIED** |
| MIME | image/png (DB + PNG signature `89 50 4E 47`) | **LIVE VERIFIED** |
| Dimensions | 160×90 (catalog / fixture) | **FACT** (manifest) / disk PNG readable **LIVE VERIFIED** (signature) |
| Scope | TEST build + TEST-SC01-SH01 | **LIVE VERIFIED** |
| Source | TEST_KEYFRAME_V1 fixture, not graph/localStorage | **FACT** |

Physical keyframe gate: PASS. **LIVE VERIFIED**

Filename suffix `.mp4` is storage-key naming only. Bytes are PNG. **FACT**

---

## 4. PerformanceSnapshot identity

| Field | Value | Class |
|---|---|---|
| Table `pack_content.performance_snapshot` | Does not exist | **LIVE VERIFIED** |
| Repo migrations 373 / 374 | Present in git + `migration-files.content.txt` | **FACT** |
| Applied on this DB | No | **LIVE VERIFIED** |
| `production_task_input_snapshots` Performance* columns | None | **LIVE VERIFIED** |
| SnapshotId / Hash / Payload | Cannot resolve | **LIVE VERIFIED** |

Do not create a snapshot. Do not apply 373/374 during this execution. **FACT** (mission)

---

## 5–7. Story / VisualContract / Timing

Intended TEST facts from Production OS TEST context (not frozen this run):

| Field | Intended TEST value | Class |
|---|---|---|
| Story | `TEST STORY — child walks into a room.` | **FACT** (code catalog) |
| VisualContract | `TEST VISUAL — 16:9 interior, locked camera, no film graph.` | **FACT** |
| Timing | `5s` / DurationSec 5 | **FACT** |

Live InputSnapshot freeze for a new request was not performed. **UNVERIFIED** this request

---

## 8. Provider / model

Would have been explicit `runway` / `gen4_turbo`. Not persisted on a new task. **UNVERIFIED** this request

Prior 4A task `274cdcba-…` already froze runway/gen4_turbo and succeeded. That is a **different request**. Reusing it would be a second interpretation of an old job, not this phase’s one Start. This phase did not replay it. **FACT**

---

## 9. Cost decision

`FamixaProviderCostCatalog.Quote(runway, gen4_turbo, 5)` → 25 credits, MaxCost 30, Estimate. **FACT** (catalog)

Live Evaluate / Prepare cost row: not executed. **UNVERIFIED**

UNKNOWN did not reach the network because no network call occurred. **FACT**

---

## 10–14. Request / Task / InputSnapshot / Attempt / ProviderJobId

This phase created none.

| Field | This phase | Class |
|---|---|---|
| RequestId | none | **FACT** |
| TaskId | none | **FACT** |
| InputSnapshotId / Hash | none | **FACT** |
| AttemptId | none | **FACT** |
| ProviderJobId | none | **FACT** |
| Attempt count for this request | 0 | **FACT** |

---

## 15–17. Start timestamp / poll / provider terminal

No Start. No poll. No terminal provider status. **FACT**

---

## 18–25. Output / verification / artifact / binding / preview

No new output. No new artifact. No new binding. No preview claim.

Prior 4A VIDEO_TAKE `63bca29a-f899-4008-a1bf-6000787bafd6` remains CURRENT/VISIBLE on TEST-SC01-SH01. It is **not** this phase’s result. **LIVE VERIFIED** (read-only)

Browser preview: **UNVERIFIED** (API health failed; not claimed)

---

## 26. Legacy contamination check

No new production path ran. Legacy SeriesShotRun / acceptedTake.url / turbo were not used as authority for a new artifact. **FACT**

Whether a future unblocked run stays off legacy remains **UNVERIFIED**.

---

## 27. Cross-scope check

No new write. Official film not selected. **FACT**

---

## 28. Live DB verification

Read-only TEST queries only. **LIVE VERIFIED**

| Object | Observation |
|---|---|
| `production_scope_anchor` | TEST sandbox present |
| `production_test_shot` | SH01 + SH02 |
| `production_artifacts` | KEYFRAME `33333333-…` VERIFIED |
| `production_bindings` | CURRENT:KEYFRAME + prior 4A VIDEO_TAKE |
| `performance_snapshot` | relation does not exist |
| Official film rows | not written |

---

## 29. Live file verification

TEST keyframe file exists; SHA matches artifact and fixture catalog. **LIVE VERIFIED**

No new `os-test/.../video/` file was written by this phase. **FACT**

Official media paths were not opened for write. **FACT**

---

## 30. Provider call count

**REAL RUNWAY START CALLS = 0**  
**REAL PROVIDER CALLS = 0**  

No CRITICAL SAFETY VIOLATION (count never exceeded 1). **FACT**

---

## 31. Media generation count

**0** this phase. **FACT**

---

## 32. EP01 / EP99 safety

| Build | graph md5 | Class |
|---|---|---|
| EP01 `4ef164d6-…` | `12289be18284fbd4d4b166b82c09eba0` | **LIVE VERIFIED** (unchanged vs prior SoT) |
| EP99 `77262ec0-…` | `c7cd2516627332c2648c1509395e4a64` | **LIVE VERIFIED** (unchanged) |

---

## 33. Failure

**PREFLIGHT_BLOCKED** / **ACTION_BLOCKED**

Primary: live TEST database cannot supply a PerformanceSnapshot. Phase C `ActionRequiresSnapshot` for CREATE_VIDEO on non-official film is true. Evaluator fail-closed: `MISSING_PERFORMANCE_SNAPSHOT`.

Contributing (not used as a bypass excuse):

1. Migrations 373/374 not applied. **LIVE VERIFIED**
2. HTTP DTO / `ContentProductionController` Prepare/CreateVideo do not accept or attach `PerformanceSnapshotId` / `PerformanceHash`. Even after schema apply, the public POST would still omit WHAT identity. **FACT**
3. API `:5290` health timed out. **LIVE VERIFIED**
4. Process env has no `RUNWAY_API_KEY`; readiness is org-config CONFIGURED / NOT VERIFIED. **FACT**

Mission rules that were followed:

- Do not generate a keyframe  
- Do not create a PerformanceSnapshot  
- Do not apply a migration to “make it pass”  
- Do not retry  
- Do not call Runway  

---

## 34. Exact evidence

```
TARGET
TEST / TEST-SC01-SH01
test_run aaaaaaaa-0000-4000-8000-000000000371
scope    bbbbbbbb-0000-4000-8000-000000000371

KEYFRAME
33333333-0000-4000-8000-000000000371
VERIFIED CURRENT:KEYFRAME
sha256 3db4d45fba5e2a8611a7777dd29c2f323462340ba4c0a1c2fc94ec132bf28a20
physical PNG 232 bytes

PERFORMANCE
pack_content.performance_snapshot DOES_NOT_EXIST
input_snapshot performance columns: NONE
resolvable SnapshotId: NONE

ACTION
TEST CREATE_VIDEO requires PerformanceSnapshot
GET/POST cannot be AVAILABLE

NETWORK
REAL_CALL_COUNT = 0
ATTEMPT_COUNT   = 0

STOP
Do not Start Runway
```

---

## 35. Additional forensic notes

A previous checkpoint (4A) already completed one real Runway job on this same TEST shot:

- Request `eafbf0ac-6504-4aad-b448-a91e92b2eca9`
- Attempt `8d82f822-b767-4701-a23f-1f85b8a1bd39`
- ProviderJobId `2746c54e-6287-4d79-852f-b779b9648ab9`
- Artifact `63bca29a-f899-4008-a1bf-6000787bafd6` (1,029,598 bytes)

That job predates Phase A–C PerformanceSnapshot consumption. Replaying it would not prove this phase’s frozen WHAT→HOW chain. Starting a second paid job while snapshot identity cannot be frozen would violate the one-shot + fail-closed rules. **FACT**

GET readiness == POST execution: both would be blocked on missing snapshot. They agree on BLOCKED. **FACT** (code) **UNVERIFIED** (live HTTP, API down)

---

## Critical questions

**Q1.** Was exactly one real Runway Start called?  
**No. Zero.** Preflight blocked. **FACT**

**Q2.** Was providerJobId persisted before Poll?  
Not applicable. No Start. **FACT**

**Q3.** Can the Attempt be reconstructed from persisted data?  
No Attempt for this phase. **FACT**

**Q4.** Was the exact same providerJobId polled?  
No poll. **FACT**

**Q5.** Was a second provider job created?  
No. **FACT**

**Q6.** Did physical output become a verified ProductionArtifact?  
No new artifact. **FACT**

**Q7.** Does Artifact lineage point to the frozen PerformanceSnapshot?  
Cannot. Snapshot cannot be resolved. **LIVE VERIFIED**

**Q8.** Does CURRENT point to artifactId?  
No new CURRENT. Prior 4A VIDEO_TAKE remains. **LIVE VERIFIED**

**Q9.** Does VISIBLE point to artifactId?  
No new VISIBLE. Prior 4A VIDEO_TAKE remains. **LIVE VERIFIED**

**Q10.** Does preview resolve from VISIBLE artifact rather than provider URL?  
Not demonstrated this phase. Browser **UNVERIFIED**. Prior 4A preview identity is historical only.

**Q11.** Did any legacy path determine the output?  
No new output. **FACT**

**Q12.** Did any output cross shot/build/scope?  
No new output. **FACT**

**Q13.** Was any official film data modified?  
No. EP01/EP99 graph md5 unchanged. **LIVE VERIFIED**

**Q14.** Was exactly one real media generated?  
**Zero** this phase. **FACT**

---

## Success criteria A–S (this execution)

A–O do not apply to a completed new run. They fail-closed at preflight.

P. No legacy URL authority used to invent a result. **PASS** (no result invented)  
Q. No cross-scope write. **PASS**  
R. EP01/EP99 untouched. **PASS**  
S. Real media this phase ≤ 1. **PASS** (0)

---

## STOP

Do not retry.  
Do not apply 373/374 as part of this execution.  
Do not freeze a snapshot to unblock.  
Do not call Runway.  
Do not generate voice, lip-sync, mix, or another shot.  
Do not modify Director, EP01, EP99, Performance Contract, Timing V3, or Visual Authority.

The next decision is whether to apply the existing PerformanceSnapshot migrations in a **separate, explicit** operation, then re-run Real Runway Test Execution V1 as a new one-shot. That decision is outside this report.
