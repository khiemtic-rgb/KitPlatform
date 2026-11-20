# FAMIXA PRODUCTION OS — CK19
# POLL / RECOVER / OUTPUT MATERIALIZATION V1

Date: 2026-09-23  
Mode: Poll / Recover / materialize the exact CK18 Vertex operation.  
Safety: Start = 0. No new provider job. No new permit. No refreeze. No CK20.

---

## VERDICT

**PASS — OUTPUT MATERIALIZED**

Handoff: **CK19_COMPLETE**

CK19 stops here. CK20 / Mix / Voice / LipSync / Final were not invoked.

---

## Distinction (do not confuse with Start)

| Operation | CK18 | CK19 |
|---|---|---|
| START `predictLongRunning` | 1 | **0** |
| POLL `fetchPredictOperation` | 0 | **1** (first run) |
| RECOVER | not used | same Poll of the same operation (2nd loop slot unused — already DONE) |
| OUTPUT MATERIALIZATION | none | **1** verified VIDEO_TAKE |

---

## 1. Exact target

| Field | Value |
|---|---|
| Build | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` |
| Shot | `EP01-SC01-SH02` |
| Action | `CREATE_VIDEO` |
| Provider | `google` |
| Model | `veo-3.1` |
| SelectionId | `c52b646f-86b9-4a43-834d-d94ab9f013b8` |
| Vertex model | `veo-3.1-generate-001` |
| Project | `gen-lang-client-0509840195` |
| Location | `us-central1` |

---

## 2. Task ID

`9cd27409-41fe-4194-b5a3-1b90f4d7b2a2`  
Status after CK19: **SUCCEEDED** (was RUNNING).

---

## 3. Attempt ID

`5a72a533-ca30-466e-99b0-357fa47bcdd8`  
Status after CK19: **PROVIDER_SUCCEEDED**. Attempt count remains **1**.

---

## 4. Snapshot ID / hash

| Field | Value |
|---|---|
| SnapshotId | `fde661bb-a33f-418d-ac68-5c6f4751f81f` |
| SnapshotHash | `56f8259e72c1403553b4deb4ebc58378dc60b41275372f08e6bd464d8fe4aebb` |
| DurationSec | 10 (unchanged) |
| Snapshot mutations | **0** |

---

## 5. Permit ID / hash

| Field | Value |
|---|---|
| PermitId | `97240e2f-e603-495b-863a-ebc9ef27b0a8` |
| PermitHash | `PERMIT_HASH_V1:5d05d4776680aa4a096bd2701a518db31d557f7b602331e18ebaf599c1580868` |
| Status | **CONSUMED** (ConsumedAt still 2026-09-23 10:07:48 +07 — CK18 only) |
| Issue during CK19 | **0** |
| Consume during CK19 | **0** |

---

## 6. Start AUTH_HASH

`AUTH_HASH_V1:b69ed488b6e2f59868dc366f61589035a00bd1fc31ac87c5b6e9f1bcba560d6a`

Ledger key matches this hash + permit. Durable Start ledger remains the one-shot Start fence.

---

## 7. Operation identity

`projects/gen-lang-client-0509840195/locations/us-central1/publishers/google/models/veo-3.1-generate-001/operations/633ea037-d719-4e31-9492-e36442bd3e20`

Encoded: `veo_cHJvamVjdHMvZ2VuLWxhbmctY2xpZW50LTA1MDk4NDAxOTUvbG9jYXRpb25zL3VzLWNlbnRyYWwxL3B1Ymxpc2hlcnMvZ29vZ2xlL21vZGVscy92ZW8tMy4xLWdlbmVyYXRlLTAwMS9vcGVyYXRpb25zLzYzM2VhMDM3LWQ3MTktNGUzMS05NDkyLWUzNjQ0MmJkM2UyMA`

---

## 8. Poll URL

`https://us-central1-aiplatform.googleapis.com/v1/projects/gen-lang-client-0509840195/locations/us-central1/publishers/google/models/veo-3.1-generate-001:fetchPredictOperation`

Called. `predictLongRunning` was not called.

---

## 9. Number of Poll requests

First CK19 run: **1** (Vertex already DONE).  
Second CK19 run: **0** (terminal replay — existing artifact).

---

## 10. HTTP statuses

First Poll: **200**

---

## 11. Final provider state

**SUCCEEDED** / DONE. Ledger outcome **SUCCEEDED**.

---

## 12. Output extraction result

Inline `bytesBase64Encoded` from the Vertex `response.videos[]` payload.  
Not a GCS URL. Not `gs://`. Not a KitVideoVideoStore public URL. Not “latest local file”.

---

## 13. Output artifact ID

`b44e530b-5b59-422f-9b80-e058a3a639b5`

Role `VIDEO_TAKE`. Verification **VERIFIED**. Bound to attempt `5a72a533-…` and snapshot hash `56f8259e…`.

---

## 14. Output SHA256

`21700e4f81c9e97422e99651effd4e33f9271539f7c546fb45f442dbda476f34`

---

## 15. Output byte length

**1,437,239** bytes · MIME `video/mp4`  
Physical file: `App_Data/kit-video-i2v/os-veo-out/5a72a533ca30466e99b0357fa47bcdd8.mp4` (same length).

---

## 16. Materialization result

Persisted via existing Production OS artifact path (`InsertArtifact` / `VerifyArtifact` + `IProductionArtifactFileStore`).  
VIDEO_TAKE count for this CREATE_VIDEO task: **1**. Second invocation returned the same artifact id.

---

## 17. Provenance events

| Event | Result |
|---|---|
| POLL_STARTED | persisted |
| PROVIDER_SUCCEEDED | persisted |
| ARTIFACT_VERIFIED | persisted |
| POLL_COMPLETED | persisted |

Payloads include build, shot, CREATE_VIDEO, google / veo-3.1, selectionId, taskId, attemptId, snapshotId/hash, permitId, Start AUTH_HASH, operation identity, terminal status, artifact id / SHA / length. No token / PEM / SA JSON.

---

## 18. Start count

**0** additional. Lifetime Start remains **1** (CK18).

---

## 19. Poll count

**1** (first live run). Replay Poll count **0**.

---

## 20. Provider job count

**1** (the CK18 operation). No new job.

---

## 21. Permit consume count

CK19: **0**. Lifetime consume: **1** (CK18). ConsumedAt unchanged.

---

## 22. Snapshot mutation count

**0**

---

## 23. Selection mutation count

**0** (still `c52b646f-…` google / veo-3.1)

---

## 24. Graph mutation count

**0**

---

## 25. Canonical content mutation count

**0** (Story / ActingBeat / VisualContract / keyframe / duration / cost policy unchanged)

---

## 26. Secret hits

**0** (`RedactionCheck` = 0; event payload scan = 0)

---

## 27. Automatic retry count

**0**. `OfficialVeoPollOnlyTransport` throws `START_REPLAY` if Start is attempted. Poll is bounded (`OsPollAttempts` 90 × 2s). Timeout class is `POLL_TIMEOUT`, not another Start.

---

## 28. Replay result

Second `PollDevelopmentAsync` invocation: **REPLAY — EXISTING ARTIFACT**  
Same artifact `b44e530b-…`. Start 0. Permit issue/consume 0. Snapshot 0. Job 0.

---

## 29. Concurrency / recovery result

Durable ledger (same CK18 file) now `SUCCEEDED` + operation identity.  
A later process restart with the same operation id returns the existing terminal artifact and does not Poll or Start.  
Start after CK18 remains **START_REPLAY** (ledger still has operation identity).

---

## 30. Regression tests (actual class names)

Deterministic CK5–CK19 suite **before** live Poll: **20 passed / 0 failed**.

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

Live arm (`CK19_LIVE=1`): `Ck19LivePollOnlyTests` PASS.

Negative matrix (deterministic):

| Case | Result |
|---|---|
| A missing operation identity | FAIL CLOSED · `UNKNOWN_PROVIDER` · Poll 0 |
| B mismatched operation/task | FAIL CLOSED · `FAILED_IDENTITY` · Poll 0 |
| C mismatched provider/model | FAIL CLOSED · `PROVIDER_MODEL_MISMATCH` · Poll 0 |
| D Poll missing output | `PROVIDER_OUTPUT_MISSING` |
| E unsupported output (`gs://`) | `PROVIDER_OUTPUT_UNSUPPORTED` |
| F malformed provider response | `INVALID_PROVIDER_RESPONSE` |
| G Poll timeout | `POLL_TIMEOUT` · attempt stays SUBMITTED · Start 0 |
| H provider terminal error | `PROVIDER_FAILED` · Start 0 |
| I second CK19 | replay existing artifact |
| J Start after CK18 | `START_REPLAY` · PredictCalls stay 1 |
| K missing snapshot | `INPUT_NOT_FROZEN` |
| L snapshot hash mismatch | `FREEZE_MISMATCH` |
| M permit mutation | CK19 issue=0 consume=0 |

---

## Live counters

| Counter | After CK18 | After CK19 |
|---|---|---|
| Provider Start | 1 | **1** |
| Provider jobs | 1 | **1** |
| Poll | 0 | **1** |
| OAuth (Poll token) | — | **1** |
| Network (OAuth + Poll) | — | **2** |
| Permit issue | 1 | **1** |
| Permit consume | 1 | **1** |
| ProductionAttempt | 1 | **1** |
| VIDEO_TAKE artifacts | 0 | **1** |
| Snapshot | 1 | **1** |
| Automatic Start retry | 0 | **0** |

---

## Explicit final statement

START `predictLongRunning` = **0**  
POLL `fetchPredictOperation` = **1**  
provider job = **1** (CK18)  
output extracted from provider response bytes  
artifact materialized exactly once  
SHA recorded  
second invocation idempotent  
no new permit / snapshot / selection / graph / content  
secrets persisted = **0**  
CK20 = **not invoked**

---

## Final handoff

**CK19_COMPLETE**

CK19 ends at provider output → verified bytes → Production OS artifact → provenance.  
The next checkpoint must be requested explicitly.
