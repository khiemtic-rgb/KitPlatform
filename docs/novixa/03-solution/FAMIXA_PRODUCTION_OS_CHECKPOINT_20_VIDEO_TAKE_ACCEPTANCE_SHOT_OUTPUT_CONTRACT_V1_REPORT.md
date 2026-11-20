# FAMIXA PRODUCTION OS — CK20
# VIDEO TAKE ACCEPTANCE / SHOT OUTPUT CONTRACT V1

Date: 2026-09-23  
Mode: Accept the exact CK19 VIDEO_TAKE as the authoritative CREATE_VIDEO output of EP01-SC01-SH02.  
Safety: Start = 0. Poll = 0. No provider job. No permit. No refreeze. No Voice / LipSync / Mix / Final. No CK21.

---

## 1. VERDICT

**PASS — AUTHORITATIVE VIDEO_TAKE ACCEPTED**

Handoff: **CK20_COMPLETE**

CK20 stops here. Voice / LipSync / Mix / Final / multi-shot composition / CK21 were not invoked.

CK20 did not invoke Vertex Start.  
CK20 did not invoke Vertex Poll.  
CK20 did not create a provider job.

---

## Distinction (do not confuse with Start / Poll)

| Operation | CK18 | CK19 | CK20 |
|---|---|---|---|
| START `predictLongRunning` | 1 | 0 | **0** |
| POLL `fetchPredictOperation` | 0 | 1 | **0** |
| VIDEO_TAKE artifacts | 0 | 1 | **1** (same) |
| Acceptance binding | none | none | **ACCEPTED:VIDEO_TAKE** |

---

## 2. Exact target

| Field | Value |
|---|---|
| Build | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` |
| Shot | `EP01-SC01-SH02` |
| Action | `CREATE_VIDEO` |
| Provider | `google` |
| Model | `veo-3.1` |
| SelectionId | `c52b646f-86b9-4a43-834d-d94ab9f013b8` |

---

## 3. Task ID

`9cd27409-41fe-4194-b5a3-1b90f4d7b2a2`  
Status: **SUCCEEDED** (unchanged from CK19).

---

## 4. Attempt ID

`5a72a533-ca30-466e-99b0-357fa47bcdd8`  
Status: **PROVIDER_SUCCEEDED**. Attempt count remains **1**.

---

## 5. Snapshot ID / hash

| Field | Value |
|---|---|
| SnapshotId | `fde661bb-a33f-418d-ac68-5c6f4751f81f` |
| SnapshotHash | `56f8259e72c1403553b4deb4ebc58378dc60b41275372f08e6bd464d8fe4aebb` |
| DurationSec | 10 |
| PerformanceSnapshotId | `6505b8a2-8a71-4d74-8d93-f1fae8319b6b` |
| Snapshot mutations | **0** |

Loaded by SnapshotId. Not “latest snapshot”.

---

## 6. Provider / model

`google` / `veo-3.1` on task, attempt, snapshot, and ACTIVE VIDEO selection.

---

## 7. Selection ID

`c52b646f-86b9-4a43-834d-d94ab9f013b8`  
Status: **ACTIVE**. Selection mutations: **0**.

---

## 8. Operation identity

`projects/gen-lang-client-0509840195/locations/us-central1/publishers/google/models/veo-3.1-generate-001/operations/633ea037-d719-4e31-9492-e36442bd3e20`

Decoded from the existing attempt `provider_job_id`. Not inferred from timestamp or filename.

Start AUTH_HASH (CK18, carried in acceptance provenance):  
`AUTH_HASH_V1:b69ed488b6e2f59868dc366f61589035a00bd1fc31ac87c5b6e9f1bcba560d6a`

---

## 9. Artifact ID

`b44e530b-5b59-422f-9b80-e058a3a639b5`

Looked up by ArtifactId + TaskId + AttemptId. Not latest. Not directory scan.

VIDEO_TAKE count for this CREATE_VIDEO task: **1**.

---

## 10. Artifact role

`VIDEO_TAKE`

---

## 11. Artifact verification

**VERIFIED** (CK19 materialization, re-checked on CK20 accept).

Supported VIDEO_TAKE payload via existing `OfficialVeoOutputBytes.IsSupportedVideo` (ftyp / Production OS rules). No new transcoder. Bytes not altered.

---

## 12. Artifact acceptance

**ACCEPTED**

Durable binding role: `ACCEPTED:VIDEO_TAKE`  
Reuse of existing `ProductionWorkExecutionEngine.BindAsync` / `GetActiveBindingAsync`. No new artifact table. No new VIDEO_TAKE row.

---

## 13. SHA256

`21700e4f81c9e97422e99651effd4e33f9271539f7c546fb45f442dbda476f34`

Computed over the exact persisted bytes through `DirectoryProductionArtifactFileStore.Read(storageKey)`.

---

## 14. Byte length

**1,437,239**

---

## 15. MIME

`video/mp4`

---

## 16. Physical storage verification

| Field | Value |
|---|---|
| StorageKey | `os-veo-out/5a72a533ca30466e99b0357fa47bcdd8` |
| File | `App_Data/kit-video-i2v/os-veo-out/5a72a533ca30466e99b0357fa47bcdd8.mp4` |
| Length | 1,437,239 |
| SHA256 | `21700e4f…476f34` |
| LastWriteUtc | `2026-09-23T03:33:08.9255644Z` (CK19 write; **unchanged**) |

File was not moved, renamed, rewritten, or re-downloaded.

---

## 17. Canonical binding result

Build → Shot → CREATE_VIDEO → Task → Attempt → Snapshot → Operation → Artifact

All links present and unambiguous.

`SHOT_OUTPUT_BINDING_INVALID` was not raised.

---

## 18. Authoritative lookup result

`GetAuthoritativeVideoTake(4ef164d6-…, EP01-SC01-SH02)` returned exactly:

| Field | Value |
|---|---|
| ArtifactId | `b44e530b-5b59-422f-9b80-e058a3a639b5` |
| Role | `VIDEO_TAKE` |
| VerificationStatus | `VERIFIED` |
| AcceptanceStatus | `ACCEPTED` |

Read-only. No provider. No latest. No filesystem scan. No create.

This is the contract future downstream stages must consume. They must reference this artifact identity. They must not copy or silently replace the bytes. Voice / LipSync / Mix / Final were **not** implemented.

---

## 19. Provenance events

First live accept persisted (once):

| Event | Count |
|---|---|
| `VIDEO_TAKE_ACCEPTANCE_STARTED` | 1 |
| `ARTIFACT_VERIFIED` (acceptance re-verify) | 1 (lifetime ARTIFACT_VERIFIED = 2; CK19 already had 1) |
| `VIDEO_TAKE_ACCEPTED` | 1 |
| `VIDEO_TAKE_ACCEPTANCE_COMPLETED` | 1 |
| `CURRENT_BOUND` (engine bind of `ACCEPTED:VIDEO_TAKE`) | 1 |

Payload fields: BuildId, Shot, Action, TaskId, AttemptId, SnapshotId, SnapshotHash, Provider, Model, SelectionId, OperationId, ArtifactId, SHA256, byte length, VerificationStatus, AcceptanceStatus, AuthorizationHash.

No OAuth token, private key, service-account JSON, or credential payload.

Replay did not duplicate acceptance events.

---

## 20. Replay result

Second `AcceptDevelopmentAsync` invocation: **REPLAY**  
Same ArtifactId `b44e530b-…`. Same SHA. Same binding.  
No second VIDEO_TAKE. No second `VIDEO_TAKE_ACCEPTED`.  
Start 0. Poll 0. Permit issue/consume 0.

---

## 21. Tamper matrix

Deterministic (`ProductionOfficialVeoVideoTakeAcceptanceV1Regression`). All fail closed. No auto-repair.

| Case | Result |
|---|---|
| A Artifact missing | `OUTPUT_ARTIFACT_NOT_FOUND` |
| B Artifact hash changed | `ARTIFACT_HASH_MISMATCH` |
| C Artifact length changed | `ARTIFACT_LENGTH_MISMATCH` |
| D Artifact MIME changed | `ARTIFACT_MIME_MISMATCH` |
| E Artifact role changed | `ARTIFACT_ROLE_MISMATCH` |
| F Task mismatch | `OUTPUT_TASK_MISMATCH` |
| G Attempt mismatch | `OUTPUT_ATTEMPT_MISMATCH` |
| H Snapshot mismatch | `FREEZE_MISMATCH` |
| I Provider mismatch | `PROVIDER_MODEL_MISMATCH` |
| J Model mismatch | `PROVIDER_MODEL_MISMATCH` |
| K Operation mismatch | `OPERATION_PROVENANCE_MISMATCH` |
| L Missing operation | `OPERATION_PROVENANCE_MISSING` |
| M More than one VIDEO_TAKE | `OUTPUT_ARTIFACT_AMBIGUOUS` |
| N No VIDEO_TAKE | `OUTPUT_ARTIFACT_NOT_FOUND` |
| O Attempt to Start | `START_FORBIDDEN` |
| P Attempt to Poll | `POLL_FORBIDDEN` |
| Q Permit issue | `PERMIT_MUTATION_FORBIDDEN` |
| R Permit consume | `PERMIT_MUTATION_FORBIDDEN` |
| S Snapshot mutation | snapshot unchanged (`SNAPSHOT_MUTATION_FORBIDDEN` on closed path) |
| T Selection mutation | selection unchanged (`SELECTION_MUTATION_FORBIDDEN` on closed path) |

Closed boundary: `OfficialVeoAcceptanceClosedTransport` / `OfficialVeoAcceptanceClosedPermitStore`. Accidental Start/Poll/Issue/Consume throws; the normal accept path never calls them.

---

## 22. Provider Start count

**0**

Lifetime Start remains **1** (CK18).

---

## 23. Provider Poll count

**0**

Lifetime Poll remains **1** (CK19).

---

## 24. Provider job count

**0** new.

Lifetime jobs remain **1** (CK18 operation `633ea037-…`).

---

## 25. Permit issue count

**0**

Lifetime issue remains **1** (CK18B).

---

## 26. Permit consume count

**0**

Lifetime consume remains **1** (CK18).  
ConsumedAt still `2026-09-23 10:07:48 +07`.

---

## 27. Snapshot mutation count

**0**

---

## 28. Selection mutation count

**0**

---

## 29. Graph mutation count

**0** (`series_build.updated_at` still `2026-09-22 16:08:25 +07`)

---

## 30. Canonical content mutation count

**0** (Story / ActingBeat / VisualContract / keyframe / duration / cost unchanged)

---

## 31. Secret hits

**0** (acceptance payload scan for token / PEM / SA JSON / oauth / access_token = 0)

---

## 32. Regression tests

Deterministic CK5–CK20 suite **before** live accept: **21 passed / 0 failed**.

| Metric | Count |
|---|---|
| Previous CK5–CK19 baseline | 20 passed / 0 failed |
| CK20 added test classes | 1 (`ProductionOfficialVeoVideoTakeAcceptanceV1Tests`) |
| Total passed | **21** |
| Total failed | **0** |

| Class | Result |
|---|---|
| `ProductionCreateVideoFreezeIntegrityV1Tests` | PASS |
| `ProductionCreateVideoSucceededReplayFreezeIntegrityV1Tests` | PASS |
| `ProductionCreateVideoProviderBindingV1Tests` | PASS |
| `ProductionVeoMotionAdapterV1Tests` | PASS |
| `ProductionHttpVeoTransportV1Tests` | PASS |
| `ProductionRealVertexExecutionReadinessV1Tests` | PASS |
| `ProductionRealVertexExecutionPreflightV1Tests` | PASS |
| `ProductionRealVertexExecutionV1Tests` | PASS |
| `ProductionOfficialCreateVideoFreezeV1Tests` | PASS |
| `ProductionCostOfficialPermitAuthorizationV1Tests` | PASS |
| `ProductionAuthoritativeVeo31CostPolicyV1Tests` | PASS |
| `ProductionOfficialCreateVideoPermitAuthorizationV1Tests` | PASS |
| `ProductionOfficialVeoPaidNetworkAuthorizationV1Tests` | PASS |
| `ProductionOfficialVeoProductionAuthenticatorV1Tests` | PASS |
| `ProductionOfficialVeoHttpCompositionV1Tests` | PASS |
| `ProductionOfficialVeoExecutionAuthorizationV1Tests` | PASS |
| `ProductionOfficialVeoRealVertexStartV1Tests` | PASS |
| `ProductionOfficialVeoLiveOfficialContractBindV1Tests` | PASS |
| `ProductionOfficialVeoLiveOfficialContractMaterializationV1Tests` | PASS |
| `ProductionOfficialVeoPollRecoverMaterializationV1Tests` | PASS |
| `ProductionOfficialVeoVideoTakeAcceptanceV1Tests` | PASS |

---

## 33. Live CK20 test

`Ck20LiveVideoTakeAcceptanceTests` with `CK20_LIVE=1`.

| Field | Result |
|---|---|
| Verdict | `PASS — AUTHORITATIVE VIDEO_TAKE ACCEPTED` |
| Handoff | `CK20_COMPLETE` |
| First run | ACCEPTED |
| Second run | REPLAY |
| ArtifactId | `b44e530b-5b59-422f-9b80-e058a3a639b5` |
| Lookup | same ArtifactId |
| Provider Start | **0** |
| Provider Poll | **0** |
| Provider jobs created | **0** |

No Join / OAuth / HttpClient on the accept host. Transport is the closed acceptance boundary.

---

## 34. Final handoff

**CK20_COMPLETE**

**AUTHORITATIVE VIDEO_TAKE ACCEPTED**

Exact ArtifactId: `b44e530b-5b59-422f-9b80-e058a3a639b5`

SHOT → AUTHORITATIVE VIDEO_TAKE → VERIFIED → ACCEPTED

---

## Live counters

| Counter | After CK19 | After CK20 |
|---|---|---|
| Provider Start | 1 | **1** |
| Provider jobs | 1 | **1** |
| Poll | 1 | **1** |
| Permit issue | 1 | **1** |
| Permit consume | 1 | **1** |
| ProductionAttempt | 1 | **1** |
| VIDEO_TAKE artifacts | 1 | **1** |
| Snapshot | 1 | **1** |
| `ACCEPTED:VIDEO_TAKE` binding | 0 | **1** |

---

## Success criteria

- [x] exact CK19 artifact found
- [x] exactly one VIDEO_TAKE for exact CREATE_VIDEO task
- [x] ArtifactId / TaskId / AttemptId / SnapshotId / SnapshotHash match
- [x] Provider / Model / SelectionId / Operation identity match
- [x] physical bytes exist; SHA256 / length / MIME match
- [x] artifact verification PASS; marked VERIFIED
- [x] artifact accepted as authoritative
- [x] downstream lookup returns exact artifact
- [x] replay returns same artifact
- [x] no duplicate artifact
- [x] no provider Start / Poll / job
- [x] no permit issue / consume
- [x] no refreeze / selection / graph / canonical content mutation
- [x] no secrets persisted
- [x] tamper matrix PASS
- [x] regression suite PASS

---

## Explicit final statement

CK20 did not invoke Vertex Start.  
CK20 did not invoke Vertex Poll.  
CK20 did not create a provider job.

CK18 proved Start.  
CK19 proved Poll + materialize.  
CK20 proved Production OS can identify, verify, accept, and hand off that exact VIDEO_TAKE as the authoritative output of SH02.

Nothing beyond that belongs in CK20. The next checkpoint must be requested explicitly.
