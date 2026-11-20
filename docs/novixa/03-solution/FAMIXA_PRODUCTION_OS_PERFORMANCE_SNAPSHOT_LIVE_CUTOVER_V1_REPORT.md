# FAMIXA PRODUCTION OS — PERFORMANCE SNAPSHOT LIVE CUTOVER V1 REPORT

Date: 2026-09-18  
Mode: FORENSIC → IMPLEMENT → TEST → LIVE DB VERIFICATION  
Scope: TEST only (`TEST-SC01-SH01` / sandbox 371)  
Provider on request: runway / gen4_turbo  

**REAL PROVIDER CALLS = 0**  
**REAL RUNWAY START CALLS = 0**  
**REAL MEDIA GENERATION = 0**  

EP01 / EP99: not mutated  

Every statement is classified: FACT / IMPLEMENTED / TESTED / LIVE VERIFIED / ASSUMED / UNVERIFIED / RECOMMENDED.

This is not a Runway execution phase.  
Do not start Real Runway Test Execution V2 from this report.

---

## 1. Executive Verdict

**PASS**

Classification: **PHASE F PASS**

The architectural gap from Phase E (`MISSING_PERFORMANCE_SNAPSHOT` because live schema and HTTP identity were missing) is closed.

The live chain now exists:

Performance Contract  
→ persisted `pack_content.performance_snapshot`  
→ CREATE_VIDEO GET/POST  
→ ProductionRequest  
→ ProductionTask  
→ ProductionTaskInputSnapshot  
→ same PerformanceSnapshotId + PerformanceHash  

The unpaid live POST used the existing Prepare boundary. It created Request / Task / InputSnapshot / Attempt `STARTED` and **did not** call Runway.

**FACT** + **IMPLEMENTED** + **TESTED** + **LIVE VERIFIED**

---

## 2. Forensic Trace

Traced before edits. **FACT**

| Step | Current authority | Finding |
|---|---|---|
| A. Model | `PerformanceSnapshot` / `PerformanceContractService` | Reused. No V2 / second orchestrator. |
| B. Migration 373 | `migrations/373_pack_content_performance_snapshot.sql` | Additive `CREATE TABLE IF NOT EXISTS`. |
| C. Migration 374 | `migrations/374_pack_content_production_performance_binding.sql` | Additive columns + FK + index. |
| D. Ordering | `migration-files.content.txt` | 373 then 374 are last two content files. |
| E. Live schema before | `performance_snapshot` missing; input snapshot had no Performance* columns | Phase E still true before apply. |
| F. Store / service | `PerformanceSnapshotRepository` INSERT/GET only | No UPDATE. Immutability = insert-once + hash check. |
| G. HTTP DTO | `ProductionCreateVideoRequestDto` | Missing snapshot identity before this phase. |
| H. Prepare | `POST /api/content/production/requests/prepare` | Existing stop-before-provider boundary. |
| I. CreateVideo | `POST /api/content/production/requests` | Would execute adapter if paid+network authorized. Not used for live POST. |
| J. Evaluator | `CreateVideoActionContractGate` + `ProductionPerformanceConsumption` + `EvaluateCreateVideoAsync` | One shared path for GET and POST. |
| K. Application | `ProductionCreateVideoService.PlanCreateVideoAsync` | Already copied snapshot id/hash into frozen input. |
| L. InputSnapshot | `ProductionWorkRepository` + mig 374 columns | Ready after 374. |
| M. Tests | Phase A/B/C suites | Extended with Phase F T01–T30. |

Phase E failure `MISSING_PERFORMANCE_SNAPSHOT` had two stacked causes:

1. Live table absent. **FACT**
2. HTTP DTO/controller did not carry `PerformanceSnapshotId` / `PerformanceHash`. **FACT**

Both are closed. **IMPLEMENTED** + **LIVE VERIFIED**

---

## 3. Migration 373

File: `migrations/373_pack_content_performance_snapshot.sql`  
Manifest: `deploy/ubuntu/migration-files.content.txt` only  

Up: `CREATE TABLE IF NOT EXISTS pack_content.performance_snapshot`  
Columns: `performance_snapshot_id` PK, `build_id`, `shot_code`, `episode_code`, `scene_code`, `performance_hash`, `payload_json`, `source_json`, `created_by`, `frozen_at`  
Constraint: `ck_perf_snap_hash_prefix` (`PERFORMANCE_HASH_V1:%`)  
Index: `ix_perf_snap_scope (build_id, shot_code)`  
Down: not present as a paired Down(); file is additive IF NOT EXISTS. **FACT**

No official-film backfill. No provider columns. **FACT**

These are the intended Phase A schema. They were incomplete only in the sense that they were **not applied**. They are not contradictory. **FACT**

---

## 4. Migration 374

File: `migrations/374_pack_content_production_performance_binding.sql`  

Up: `ADD COLUMN IF NOT EXISTS` on `production_task_input_snapshots`:

- `performance_snapshot_id UUID`
- `performance_hash VARCHAR(96)`
- `performance_payload_json TEXT`

FK: `performance_snapshot_id → performance_snapshot(performance_snapshot_id) ON DELETE RESTRICT`  
Check: `ck_prod_input_perf_hash_prefix`  
Index: `ix_prod_input_perf_snap`  
Down: not a paired Down(); additive IF NOT EXISTS. **FACT**

No second snapshot table. No official backfill. **FACT**

---

## 5. Live DB Before

| Field | Value | Class |
|---|---|---|
| Database | `kitplatform` | **LIVE VERIFIED** |
| User | `kitplatform` | **LIVE VERIFIED** |
| Host / port | localhost / 5432 | **LIVE VERIFIED** |
| Connection secrets | not printed | **FACT** |
| `pack_content.schema_migrations` | MISSING | **LIVE VERIFIED** |
| Current applied tracker | none; content migs applied as SQL files | **LIVE VERIFIED** |
| Target | apply 373 then 374 only | **FACT** |
| `performance_snapshot` | MISSING | **LIVE VERIFIED** |
| Input Performance* columns | NONE | **LIVE VERIFIED** |
| EP01 graph md5 | `415fedf306ed23d1ace94a794400b440` | **LIVE VERIFIED** |
| EP99 graph md5 | `c7cd2516627332c2648c1509395e4a64` | **LIVE VERIFIED** |
| TEST keyframe SHA | `3db4d45fba5e2a8611a7777dd29c2f323462340ba4c0a1c2fc94ec132bf28a20` | **LIVE VERIFIED** |
| Historical video SHA / bytes | `f94a9d097e5d90c0721501540e4de458334588b0eb552a3c1e6878d84c8c8c17` / 1029598 | **LIVE VERIFIED** |

EP01 md5 in older reports was `12289be18284fbd4d4b166b82c09eba0`. That change predates this phase. Phase F used the **current** before-fingerprint and verified it unchanged after. **LIVE VERIFIED**

---

## 6. Live DB After

Applied **only** 373 then 374 via `psql -f` with `ON_ERROR_STOP`. Not `update-database`. **LIVE VERIFIED**

| Check | After | Class |
|---|---|---|
| `pack_content.performance_snapshot` | EXISTS | **LIVE VERIFIED** |
| Input Performance* columns | `performance_snapshot_id,performance_hash,performance_payload_json` | **LIVE VERIFIED** |
| FK | `production_task_input_snapshots_performance_snapshot_id_fkey` | **LIVE VERIFIED** |
| Indexes | `ix_perf_snap_scope`, `ix_prod_input_perf_snap` | **LIVE VERIFIED** |
| EP01 md5 | `415fedf306ed23d1ace94a794400b440` unchanged | **LIVE VERIFIED** |
| EP99 md5 | `c7cd2516627332c2648c1509395e4a64` unchanged | **LIVE VERIFIED** |
| Official snapshot rows | 0 | **LIVE VERIFIED** |

---

## 7. PerformanceSnapshot Schema

Ownership is Production OS scope: `build_id` + `shot_code` (plus episode/scene). No tenant column. No browser/localStorage authority. **FACT**

No provider/model columns. **FACT**

Hash prefix constrained. Payload is TEXT canonical JSON. Frozen at insert. **FACT** + **LIVE VERIFIED**

---

## 8. TEST PerformanceSnapshot Identity

Canonical TEST WHAT from `ProductionTestPerformanceFixture` (not invented story copy, not AI):

| Field | Value | Class |
|---|---|---|
| PerformanceSnapshotId | `8231d8a9-a6e3-47b7-bc02-c9fbff48bb3e` | **LIVE VERIFIED** |
| BuildId / ScopeId | `bbbbbbbb-0000-4000-8000-000000000371` | **LIVE VERIFIED** |
| ShotCode | `TEST-SC01-SH01` | **LIVE VERIFIED** |
| Episode / Scene | TEST / SC01 | **LIVE VERIFIED** |
| Line | L1 / CHAR-001 / locked spoken line | **FACT** (fixture) + **LIVE VERIFIED** (row) |
| Acting | turns / stands / window / kitchen / uneasy | **FACT** (fixture) |
| CREATE_VIDEO claim Story | `TEST STORY — child walks into a room.` | **FACT** (`ProductionTestCreateVideoContext`) |
| Visual / Timing claim | TEST VISUAL 16:9 locked camera / 5s | **FACT** |
| Attached to EP01/EP99 | No | **LIVE VERIFIED** |

The snapshot is the frozen Performance Contract WHAT. Story/Visual/Timing claims remain CREATE_VIDEO inputs, not provider HOW. **FACT**

---

## 9. Snapshot Hash

| Field | Value | Class |
|---|---|---|
| PerformanceHash | `PERFORMANCE_HASH_V1:7715ad4e51dde4264eaa679f43dc01246b0b25c992e99fff44cd27c0b05ab8f1` | **LIVE VERIFIED** |
| Algorithm | existing `PerformanceContractCanon.PerformanceHashOf` / SHA256 of canonical payload | **FACT** |
| `hash(payload) == stored hash` | true (`digest(payload_json,'sha256')`) | **LIVE VERIFIED** |
| GET reload hash match | true | **LIVE VERIFIED** |

No second hashing algorithm. **FACT**

---

## 10. Snapshot Immutability

Store API is INSERT + GET only. Duplicate insert → `SNAPSHOT_IMMUTABLE`. Get verifies live hash vs stored hash. **FACT** + **TESTED** (T05)

No UPDATE endpoint. A later freeze creates a new SnapshotId; original row stays. **TESTED**

Live GET of `8231d8a9-…` returned the same hash and payload. **LIVE VERIFIED**

---

## 11. CREATE_VIDEO DTO/API

`ProductionCreateVideoRequestDto` now accepts:

- `PerformanceSnapshotId`
- `PerformanceHash`

`ContentProductionController` passes them into `ShotExecutionInput` for Prepare, CreateVideo, and other OS actions.

GET `/shots/{buildId}/{shotCode}/actions` and `/state` accept:

- `performanceSnapshotId`
- `performanceHash`
- `providerId`
- `modelId`

**IMPLEMENTED**

GET without those query params still fail-closed for TEST (`MISSING_PERFORMANCE_SNAPSHOT`). **LIVE VERIFIED**

---

## 12. Action Contract

CREATE_VIDEO still requires `PerformanceSnapshot` for non-official-film scopes via `ProductionPerformanceConsumption.CollectMissing` inside `CreateVideoActionContractGate.Evaluate`.  

GET and POST both call `EvaluateCreateVideoAsync` → same bind (`PerformanceSnapshotBinding.BindAsync`). **FACT** + **TESTED** (T11)

Official film still skips snapshot (legacy CREATE_*). **FACT**

---

## 13. GET Readiness

Live GET `CREATE_VIDEO` on TEST-SC01-SH01 with explicit runway/gen4_turbo:

| Case | Available | Block | Class |
|---|---|---|---|
| No snapshot | false | `MISSING_PERFORMANCE_SNAPSHOT` | **LIVE VERIFIED** |
| Valid snapshot + hash | true | none | **LIVE VERIFIED** |
| Valid id + wrong hash | false | `FAILED_IDENTITY` | **LIVE VERIFIED** |
| SH02 + SH01 snapshot | false | `CROSS_SHOT_FORBIDDEN` | **LIVE VERIFIED** |

`FAILED_IDENTITY` is the existing Phase B/C bind-time hash mismatch code. Canonical equivalent of the mission name `PERFORMANCE_SNAPSHOT_HASH_MISMATCH`. **FACT**

`CROSS_SHOT_FORBIDDEN` is the existing scope mismatch code. Canonical equivalent of `PERFORMANCE_SNAPSHOT_SCOPE_MISMATCH`. **FACT**

---

## 14. POST Readiness

Live unpaid POST was `POST /api/content/production/requests/prepare` with explicit snapshot + runway/gen4_turbo + `AuthorizePaidNetwork=true`.

Prepare authorizes paid network eligibility but does **not** call `StartFrozenAsync` / `HttpRunwayTransport`. **FACT** + **LIVE VERIFIED** (audit 0)

In-memory POST/Prepare missing/hash/scope cases match GET. **TESTED**

---

## 15. ProductionRequest

| Field | Value | Class |
|---|---|---|
| RequestId | `2522ec00-d9e6-46d6-b276-ccd4a77ed2aa` | **LIVE VERIFIED** |
| UserAction | `CREATE_VIDEO` | **LIVE VERIFIED** |
| Status | `IN_PROGRESS` | **LIVE VERIFIED** |
| Shot | `TEST-SC01-SH01` | **LIVE VERIFIED** |
| IdempotencyKey | `PHASE_F_LIVE_CUTOVER_V1` | **LIVE VERIFIED** |

One request. No duplicate created. **LIVE VERIFIED**

---

## 16. ProductionTask

| Field | Value | Class |
|---|---|---|
| TaskId | `88a376e6-b1af-4a39-b999-cd6026da8b3b` | **LIVE VERIFIED** |
| Action | `CREATE_VIDEO` | **LIVE VERIFIED** |
| Status | `RUNNING` | **LIVE VERIFIED** |
| ProviderId / ModelId | `runway` / `gen4_turbo` | **LIVE VERIFIED** |
| InputSnapshotId | `76b35eb4-e708-4a71-8818-dfe1db6537b9` | **LIVE VERIFIED** |

HOW is on the task. **LIVE VERIFIED**

---

## 17. ProductionTaskInputSnapshot

| Field | Value | Class |
|---|---|---|
| SnapshotId | `76b35eb4-e708-4a71-8818-dfe1db6537b9` | **LIVE VERIFIED** |
| PerformanceSnapshotId | `8231d8a9-a6e3-47b7-bc02-c9fbff48bb3e` | **LIVE VERIFIED** |
| PerformanceHash | exact match to snapshot | **LIVE VERIFIED** |
| Provider / Model | `runway` / `gen4_turbo` | **LIVE VERIFIED** |
| KeyframeArtifactId | `33333333-0000-4000-8000-000000000371` | **LIVE VERIFIED** |

Exact identity consumed. No substituted snapshot. **LIVE VERIFIED**

---

## 18. WHAT / HOW separation

PerformanceSnapshot payload contains no `runway` / `gen4_turbo` / provider keys. **LIVE VERIFIED** (`payload_has_runway=false`, HTTP `SNAP_LEAK=False`)

Task + InputSnapshot freeze `runway` / `gen4_turbo`. **LIVE VERIFIED**

Canon `ScanForbidden` rejects provider fields in snapshot JSON. **FACT** + **TESTED** (T18)

---

## 19. Cross-scope protection

SH02 cannot consume SH01 snapshot: `CROSS_SHOT_FORBIDDEN`. **TESTED** + **LIVE VERIFIED** (GET)

Cross-build / cross-test-run: `CROSS_SCOPE_FORBIDDEN`. **TESTED** (T28/T29)

No task execution / no provider call on those paths. **TESTED**

---

## 20. Hash mismatch protection

Valid SnapshotId + wrong hash → bind throws `FAILED_IDENTITY`. GET blocked. Prepare/CreateVideo blocked. **TESTED** + **LIVE VERIFIED** (GET)

No silent replacement with the DB hash. **FACT**

---

## 21. Missing snapshot protection

No claim → `MISSING_PERFORMANCE_SNAPSHOT`. **TESTED** + **LIVE VERIFIED**

Unknown SnapshotId → `SNAPSHOT_NOT_FOUND` (existing canonical for “id provided but absent”). **TESTED** (T08)

---

## 22. Idempotency

In-memory: second Prepare with the same key returns the same Request/Task/InputSnapshot. **TESTED** (T22)

Live: one key `PHASE_F_LIVE_CUTOVER_V1`. One request. **LIVE VERIFIED**

No paid retry. **FACT**

---

## 23. Provider call count

| Probe | Count | Class |
|---|---|---|
| Live `runway-http-audit` before | start=0 total=0 | **LIVE VERIFIED** |
| Live `runway-http-audit` after Prepare | start=0 poll=0 download=0 total=0 | **LIVE VERIFIED** |
| FakeRunwayTransport Create/HTTP | 0 | **TESTED** (T19) |
| Attempt.ProviderJobId | empty | **LIVE VERIFIED** |
| Attempt status | `STARTED` | **LIVE VERIFIED** |

---

## 24. Media generation count

Live artifacts on the new task: **0**. **LIVE VERIFIED**  
No new VIDEO_TAKE. Historical `63bca29a-…` untouched. **LIVE VERIFIED**  
In-memory Prepare created no task artifacts. **TESTED** (T20)

---

## 25. Legacy fallback check

ActingHash / latest graph / localStorage / acceptedTake are not snapshot authority. ActingHash-only claim is blocked (`MISSING_PERFORMANCE_SNAPSHOT` or `INVALID_PERFORMANCE_HASH`). **TESTED** (T21)

CREATE_VIDEO does not recompute a missing snapshot. **FACT**

---

## 26. TEST keyframe integrity

| Field | Value | Class |
|---|---|---|
| ArtifactId | `33333333-0000-4000-8000-000000000371` | **LIVE VERIFIED** |
| SHA256 | `3db4d45fba5e2a8611a7777dd29c2f323462340ba4c0a1c2fc94ec132bf28a20` | **LIVE VERIFIED** |
| Role / binding | KEYFRAME / ACTIVE `CURRENT:KEYFRAME` | **LIVE VERIFIED** |
| Mutated this phase | No | **LIVE VERIFIED** |

---

## 27. Historical TEST video integrity

| Field | Value | Class |
|---|---|---|
| ArtifactId | `63bca29a-f899-4008-a1bf-6000787bafd6` | **LIVE VERIFIED** |
| SHA256 | `f94a9d097e5d90c0721501540e4de458334588b0eb552a3c1e6878d84c8c8c17` | **LIVE VERIFIED** |
| Bytes | 1029598 | **LIVE VERIFIED** |
| Used as this phase execution | No | **FACT** |
| Lineage rewritten | No | **LIVE VERIFIED** |

---

## 28. EP01 integrity

Build `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
graph md5 before = after = `415fedf306ed23d1ace94a794400b440`  
Freeze on EP01 is `OFFICIAL_FILM_FORBIDDEN`. **TESTED** + **LIVE VERIFIED**

---

## 29. EP99 integrity

Build `77262ec0-74ad-459d-9128-f09bb553a5dd`  
graph md5 before = after = `c7cd2516627332c2648c1509395e4a64`  
Freeze on EP99 is `OFFICIAL_FILM_FORBIDDEN`. **TESTED** + **LIVE VERIFIED**

---

## 30. Tests

Suite: `PerformanceSnapshotLiveCutoverV1Regression`  
xUnit: `Performance_snapshot_live_cutover_v1_T01_through_T30`  

`ProductionPipelineV1Tests`: **15/15 PASS** including Phase F. **TESTED**

| Id | Result |
|---|---|
| T01 migration creates table | PASS |
| T02 migration order / IF NOT EXISTS | PASS |
| T03 TEST snapshot creation | PASS |
| T04 hash verification | PASS |
| T05 immutability | PASS |
| T06 valid CREATE_VIDEO readiness | PASS |
| T07 missing snapshot blocked | PASS |
| T08 wrong snapshot id blocked | PASS |
| T09 wrong hash blocked | PASS |
| T10 wrong scope blocked | PASS |
| T11 GET == POST evaluator | PASS |
| T12 Request created | PASS |
| T13 Task created | PASS |
| T14 InputSnapshot created | PASS |
| T15 exact SnapshotId | PASS |
| T16 exact PerformanceHash | PASS |
| T17 provider/model frozen separately | PASS |
| T18 snapshot has no provider/model | PASS |
| T19 no provider call | PASS |
| T20 no media generation | PASS |
| T21 no legacy fallback | PASS |
| T22 idempotency | PASS |
| T23 EP01 unchanged | PASS |
| T24 EP99 unchanged | PASS |
| T25 TEST keyframe SHA | PASS |
| T26 historical video not rewritten | PASS |
| T27 cross-shot | PASS |
| T28 cross-build | PASS |
| T29 cross-test-run | PASS |
| T30 missing API classified separately | PASS |

T30 note: the first live HTTP pass hit a leftover process from another tree on :5290 and 404’d Production routes. After starting this repo’s API, `/api/health` and Production OS routes worked. **LIVE VERIFIED**

---

## 31. API health

`GET http://localhost:5290/api/health` → `ok`  
Process: `E:\KitPlatform\src\KitPlatform.Api\bin\Debug\net10.0\KitPlatform.Api.exe`  

**LIVE VERIFIED**

---

## 32. Live DB verification

Identity chain:

```
PerformanceSnapshot.Id 8231d8a9-a6e3-47b7-bc02-c9fbff48bb3e
        ↓
ProductionTaskInputSnapshot.PerformanceSnapshotId 8231d8a9-…
PerformanceSnapshot.Hash
        ==
ProductionTaskInputSnapshot.PerformanceHash
PERFORMANCE_HASH_V1:7715ad4e51dde4264eaa679f43dc01246b0b25c992e99fff44cd27c0b05ab8f1
```

**LIVE VERIFIED**

---

## 33. Exact evidence

| Item | Evidence | Class |
|---|---|---|
| Mig 373/374 applied | `psql -f` COMMIT; table/columns/FK exist | **LIVE VERIFIED** |
| TEST snapshot | id `8231d8a9-…` hash `…b05ab8f1` | **LIVE VERIFIED** |
| GET missing / ready / hash / scope | see §13 | **LIVE VERIFIED** |
| Prepare | request `2522ec00-…` task `88a376e6-…` input `76b35eb4-…` attempt `4fb46ca0-…` STARTED | **LIVE VERIFIED** |
| Runway HTTP | 0/0/0 | **LIVE VERIFIED** |
| New artifacts | 0 | **LIVE VERIFIED** |
| xUnit | 15/15 | **TESTED** |

---

## 34. Remaining gaps

1. Hash-mismatch error string remains `FAILED_IDENTITY` (existing bind code), not the mission alias `PERFORMANCE_SNAPSHOT_HASH_MISMATCH`. Behavior is fail-closed. **FACT**
2. Unknown SnapshotId remains `SNAPSHOT_NOT_FOUND`, not `MISSING_PERFORMANCE_SNAPSHOT`. **FACT**
3. Director UI still does not send snapshot query params. API accepts them; UI cutover was out of scope. **FACT**
4. Live POST used Prepare (existing deferred command). `POST /requests` still executes the adapter if paid+network is fully authorized. Next Runway phase must use the one-shot permit + ExecutePrepared, not a second Prepare. **RECOMMENDED**
5. Attempt exists in `STARTED` with empty ProviderJobId. That is the existing Prepare lifecycle, not a fake success. **FACT**
6. `pack_content.schema_migrations` still does not exist; content mig history is file-apply. **FACT**

No second orchestrator. No provider architecture change. No Timing V3 / Visual Authority / Director change. **FACT**

---

## 35. Recommended next phase

**FAMIXA PRODUCTION OS — REAL RUNWAY TEST EXECUTION V2**

Use this frozen chain only:

- Scope `bbbbbbbb-0000-4000-8000-000000000371`
- Shot `TEST-SC01-SH01`
- PerformanceSnapshot `8231d8a9-a6e3-47b7-bc02-c9fbff48bb3e`
- PerformanceHash `PERFORMANCE_HASH_V1:7715ad4e51dde4264eaa679f43dc01246b0b25c992e99fff44cd27c0b05ab8f1`
- Prepared request `2522ec00-d9e6-46d6-b276-ccd4a77ed2aa`
- Prepared attempt `4fb46ca0-b0df-43e0-92fe-c06286bea983` (`STARTED`, no job id)

Exactly one real Start. Do not create another snapshot. Do not mutate EP01/EP99. Do not reuse historical artifact `63bca29a-…` as this execution.

**RECOMMENDED**

This report stops here. **FACT**

---

## Critical questions

| Q | Answer | Class |
|---|---|---|
| Q1. Does `pack_content.performance_snapshot` exist? | Yes | **LIVE VERIFIED** |
| Q2. Were ONLY 373/374 applied? | Yes (`psql -f` those two files) | **LIVE VERIFIED** |
| Q3. Can canonical TEST snapshot be resolved? | Yes `8231d8a9-…` | **LIVE VERIFIED** |
| Q4. Is PerformanceHash verified? | Yes, payload digest matches | **LIVE VERIFIED** |
| Q5. Is the snapshot immutable? | Insert-only + hash check; reload unchanged | **TESTED** + **LIVE VERIFIED** |
| Q6. Does CREATE_VIDEO receive exact snapshot identity? | Yes, DTO + InputSnapshot | **IMPLEMENTED** + **LIVE VERIFIED** |
| Q7. Reject hash mismatch? | Yes, `FAILED_IDENTITY` | **TESTED** + **LIVE VERIFIED** |
| Q8. Reject scope mismatch? | Yes, `CROSS_SHOT_FORBIDDEN` / `CROSS_SCOPE_FORBIDDEN` | **TESTED** + **LIVE VERIFIED** |
| Q9. GET readiness = POST readiness? | Yes, one evaluator | **TESTED** + **LIVE VERIFIED** |
| Q10. InputSnapshot references exact snapshot? | Yes | **LIVE VERIFIED** |
| Q11. Provider/model separate from snapshot? | Yes | **LIVE VERIFIED** |
| Q12. Any provider called? | No | **LIVE VERIFIED** |
| Q13. Any media generated? | No | **LIVE VERIFIED** |
| Q14. Was Runway called? | No | **LIVE VERIFIED** |
| Q15. EP01 modified? | No | **LIVE VERIFIED** |
| Q16. EP99 modified? | No | **LIVE VERIFIED** |
| Q17. TEST keyframe modified? | No | **LIVE VERIFIED** |
| Q18. Historical TEST video modified? | No | **LIVE VERIFIED** |
| Q19. Complete Contract → Snapshot → CREATE_VIDEO → InputSnapshot? | Yes | **LIVE VERIFIED** |
| Q20. Safe to proceed to Real Runway Test Execution V2? | Yes, from this gap. Start V2 only as its own phase, one Start, this frozen chain. | **RECOMMENDED** |

---

## Success criteria A–S

| | Criterion | Result |
|---|---|---|
| A | Schema exists live | PASS **LIVE VERIFIED** |
| B | Canonical TEST snapshot exists | PASS **LIVE VERIFIED** |
| C | Hash valid | PASS **LIVE VERIFIED** |
| D | Immutable | PASS **TESTED** + **LIVE VERIFIED** |
| E | CREATE_VIDEO resolves exact snapshot | PASS **LIVE VERIFIED** |
| F | Hash mismatch fail-closed | PASS **LIVE VERIFIED** |
| G | Scope mismatch fail-closed | PASS **LIVE VERIFIED** |
| H | Missing snapshot fail-closed | PASS **LIVE VERIFIED** |
| I | GET and POST same evaluator | PASS **TESTED** |
| J | InputSnapshot exact reference | PASS **LIVE VERIFIED** |
| K | Provider/model separate | PASS **LIVE VERIFIED** |
| L | Zero provider calls | PASS **LIVE VERIFIED** |
| M | Zero media generation | PASS **LIVE VERIFIED** |
| N | TEST keyframe unchanged | PASS **LIVE VERIFIED** |
| O | Historical TEST video unchanged | PASS **LIVE VERIFIED** |
| P | EP01 unchanged | PASS **LIVE VERIFIED** |
| Q | EP99 unchanged | PASS **LIVE VERIFIED** |
| R | No legacy fallback authority | PASS **TESTED** |
| S | No second orchestrator | PASS **FACT** |

---

## Final stop

Phase F is complete.

Do not call Runway.  
Do not generate video.  
Do not retry.  
Do not modify Director, EP01, EP99, Timing V3, Performance Contract semantics, or Visual Authority.

Next phase, if started later: **REAL RUNWAY TEST EXECUTION V2**, one Start, this frozen chain only.
