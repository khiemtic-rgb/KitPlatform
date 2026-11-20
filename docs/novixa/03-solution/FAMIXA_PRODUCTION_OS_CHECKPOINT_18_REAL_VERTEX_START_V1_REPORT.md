# CHECKPOINT 18 — REAL VERTEX START V1

Date: 2026-09-23  
Mode: Exactly one live `predictLongRunning` for the already-materialized official CK18B contract.  
Safety: No poll. No recover. No output materialization. No CK19. No automatic Start retry.

---

## VERDICT

**PASS — REAL VERTEX START ACCEPTED**

Handoff: **READY_FOR_CK19_POLL_RECOVER**

CK18 stops here. CK19 was not invoked.

---

## 1. Target

| Field | Value |
|---|---|
| Build | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` |
| Shot | `EP01-SC01-SH02` |
| Action | `CREATE_VIDEO` |
| Provider | `google` |
| Model | `veo-3.1` |
| SelectionId | `c52b646f-86b9-4a43-834d-d94ab9f013b8` |
| Vertex model | `veo-3.1-generate-001` |
| CREATE_VIDEO task | `9cd27409-41fe-4194-b5a3-1b90f4d7b2a2` |
| Live host | localhost `kitplatform` (Development) |

---

## 2. CK18B artifact verification

Reloaded from live persistence by exact IDs. No latest-pick. No graph reconstruct.

| Artifact | Expected | Live | Result |
|---|---|---|---|
| Snapshot | `fde661bb-a33f-418d-ac68-5c6f4751f81f` | same | PASS |
| SnapshotHash | `56f8259e72c1403553b4deb4ebc58378dc60b41275372f08e6bd464d8fe4aebb` | same | PASS |
| Permit | `97240e2f-e603-495b-863a-ebc9ef27b0a8` | same | PASS |
| PermitHash | `PERMIT_HASH_V1:5d05d4776680aa4a096bd2701a518db31d557f7b602331e18ebaf599c1580868` | same | PASS |
| Selection | `c52b646f-86b9-4a43-834d-d94ab9f013b8` google / veo-3.1 ACTIVE VIDEO | same DIRECTOR_SELECTION | PASS |
| Keyframe | `91daaf4b-5550-4cbc-b9f9-0bd0be556cef` SHA `9fe2bd76034203a14531631fd1911e3ef15f1249c588259a17c692c952305d57` | bytes SHA matched | PASS |
| DurationSec | 10 | 10 | PASS |

---

## 3. CK17 AUTH_HASH verification

`OfficialVeoExecutionAuthorizationV1.Evaluate` re-run against live artifacts.

| Hash | Value | Result |
|---|---|---|
| Contract AUTH_HASH (compose project `official-veo-compose`, CK18B identity) | `AUTH_HASH_V1:4a9dc37361b531e98d5bbc9fa1a8249a0e1ab0d3788918d2794ca05921155fe3` | MATCH — required before credential read |
| Start AUTH_HASH (authoritative SA project `gen-lang-client-0509840195`) | `AUTH_HASH_V1:b69ed488b6e2f59868dc366f61589035a00bd1fc31ac87c5b6e9f1bcba560d6a` | AUTHORIZED — used for Start URL / ledger key |

State: **EXECUTION_AUTHORIZED**. Credential / OAuth / HTTP opened only after the contract hash matched.

---

## 4. Final frozen match

`ProductionFrozenCreateVideoMatch.Assert` against the live persisted snapshot:

| Field | Value | Result |
|---|---|---|
| ProviderId | `google` | PASS |
| ModelId | `veo-3.1` | PASS |
| DurationSec (OS) | 10 | PASS — snapshot not mutated to 8 |
| TimingHash | Hash(`10.0s`) = `01e81b441e96524c4100446f42812d97ddc784bb5fed2de2d3d4732a35078c9d` | PASS |
| PerformanceSnapshotId | `6505b8a2-8a71-4d74-8d93-f1fae8319b6b` | PASS |
| PerformanceHash | `PERFORMANCE_HASH_V1:8fa1d71b14e46e64bf1cf4dc7b107ae7f39be69f3f79da1d55d24f33a116914a` | PASS |
| KeyframeArtifactId | `91daaf4b-5550-4cbc-b9f9-0bd0be556cef` | PASS |
| KeyframeHash | `9fe2bd76034203a14531631fd1911e3ef15f1249c588259a17c692c952305d57` | PASS |
| SnapshotHash | `56f8259e72c1403553b4deb4ebc58378dc60b41275372f08e6bd464d8fe4aebb` | PASS |
| Provider wire duration | 8 | transport mapping only |

---

## 5. Permit state before Start

| Field | Value |
|---|---|
| PermitId | `97240e2f-e603-495b-863a-ebc9ef27b0a8` |
| Status | **ISSUED** |
| ConsumedAt | null |
| Build / shot / action | exact official |
| Provider / model | google / veo-3.1 |
| SelectionId | `c52b646f-86b9-4a43-834d-d94ab9f013b8` |
| SnapshotId / hash | exact CK18B |
| Cost | VIDEO_ONLY · 720p · generateAudio=false · sampleCount=1 · USD 0.20 / COUNT |

Permit issue during CK18: **0**.

---

## 6. Permit claim result

Durable claim via existing `OfficialVeoStartLedger` keyed by Start AUTH_HASH + permitId, persisted at `App_Data/kit-video-i2v/ck18-official-start-ledger.json`.

Forensic note: the first armed invocation (before hash-column fix) claimed IN_FLIGHT, then failed locally on `request_payload_hash VARCHAR(64)` **before** HTTP. That attempt is **NOT_SENT**. The ledger was reclaimed only because AttemptId was still empty and no operation identity existed. The second explicit invocation won the rematch and sent the one Start.

Winner: 1 real Start. No concurrent second Start.

---

## 7. Attempt ID

`5a72a533-ca30-466e-99b0-357fa47bcdd8`

| Field | Value |
|---|---|
| Task | `9cd27409-41fe-4194-b5a3-1b90f4d7b2a2` |
| Attempt number | 1 |
| Provider / model | google / veo-3.1 |
| Status | `PROVIDER_SUBMITTED` |
| StartedAt | 2026-09-23 10:07:45 +07 |
| ProviderJobId | encoded operation below |

ProductionAttempt count = **1**.

---

## 8. Request payload hash

`REQ_HASH_V1:3938ce57a6b6efa78454000eeb74712ec93233e6a96d87f924c731cf4c222c10`

Computed from the exact outbound Vertex JSON body.  
Persisted on the attempt row as the 64-char SHA-256 (`3938ce57…4c222c10`) because `production_attempts.request_payload_hash` is `VARCHAR(64)`. Ledger and provenance keep the `REQ_HASH_V1:` prefix.

---

## 9. Safe request summary

| Field | Value |
|---|---|
| provider | google |
| model | veo-3.1 |
| vertexModel | veo-3.1-generate-001 |
| durationOS | 10 |
| durationProvider | 8 |
| resolution | 720p |
| aspectRatio | 16:9 |
| sampleCount | 1 |
| generateAudio | false |
| keyframeId | `91daaf4b-5550-4cbc-b9f9-0bd0be556cef` |
| keyframeHash | `9fe2bd76034203a14531631fd1911e3ef15f1249c588259a17c692c952305d57` |
| requestHash | `REQ_HASH_V1:3938ce57a6b6efa78454000eeb74712ec93233e6a96d87f924c731cf4c222c10` |
| prompt | from frozen Story + ActingBeat + VisualContract (not logged) |
| seed / lastFrame / image URL / gs:// / gcsUri | forbidden — not present |

---

## 10. Project

`gen-lang-client-0509840195`  
(authoritative Join `project_id`; not `ck16-compose-project` / fake / test)

---

## 11. Location

`us-central1` (canonical Veo default)

---

## 12. Start URL

`https://us-central1-aiplatform.googleapis.com/v1/projects/gen-lang-client-0509840195/locations/us-central1/publishers/google/models/veo-3.1-generate-001:predictLongRunning`

---

## 13. Poll URL (recorded only — not called)

`https://us-central1-aiplatform.googleapis.com/v1/projects/gen-lang-client-0509840195/locations/us-central1/publishers/google/models/veo-3.1-generate-001:fetchPredictOperation`

---

## 14. OAuth count

**1** (`OfficialVeoJoinFileHttpAuthenticator.Calls`). Credential source: existing `VEO_JOIN_FILE` / `org_settings.video_config_json.veoJoinFile` chain.

---

## 15. Network count

**2** (OAuth token grant + one Vertex Start). Not interpreted as Start retries.

---

## 16. Provider Start count

**1** (`predictLongRunning`)

---

## 17. Provider job count

**1**

---

## 18. HTTP status

**200**

---

## 19. Operation name

`projects/gen-lang-client-0509840195/locations/us-central1/publishers/google/models/veo-3.1-generate-001/operations/633ea037-d719-4e31-9492-e36442bd3e20`

---

## 20. Encoded operation ID

`veo_cHJvamVjdHMvZ2VuLWxhbmctY2xpZW50LTA1MDk4NDAxOTUvbG9jYXRpb25zL3VzLWNlbnRyYWwxL3B1Ymxpc2hlcnMvZ29vZ2xlL21vZGVscy92ZW8tMy4xLWdlbmVyYXRlLTAwMS9vcGVyYXRpb25zLzYzM2VhMDM3LWQ3MTktNGUzMS05NDkyLWUzNjQ0MmJkM2UyMA`

---

## 21. Provider operation status if returned

Not present on the Start response. Vertex `predictLongRunning` returned operation identity only. CK18 did not call `fetchPredictOperation` to read `done` / provider status.

---

## 22. Permit consumption result

| Field | After Start |
|---|---|
| Status | **CONSUMED** |
| ConsumedAt | 2026-09-23 10:07:48 +07 |
| Consume count | **1** |
| Issue count | **0** |
| Official permit row count | **1** |

---

## 23. Execution state

| Surface | State |
|---|---|
| Ledger outcome | `RUNNING` |
| OfficialVeoStartResult | STARTED / RUNNING |
| Attempt | `PROVIDER_SUBMITTED` |
| Task | `RUNNING` |
| Provenance ExecutionState (pre-Start event) | `IN_FLIGHT` then ledger completed `RUNNING` |

---

## 24. Provenance persistence result

Canonical `ProductionAttempt` + `ATTEMPT_STARTED` event payload (existing `production_task_events`, no parallel table).

Persisted: attemptId, Start AUTH_HASH, build, shot, CREATE_VIDEO, google / veo-3.1, selectionId, snapshotId/hash, permitId/hash, cost variant `VIDEO_ONLY/720p/audio=false/sampleCount=1`, USD 0.20 COUNT, OS duration 10, provider duration 8, keyframe id/hash, StartUrl, PollUrl, Vertex model, project, location, `REQ_HASH_V1:…`, timestamp, execution state.

Also: `OfficialVeoStartLedger` file with operation identity.

---

## 25. Secret redaction result

**secret hits = 0**

`RedactionCheck` = 0. Provenance JSON has no `private_key`, PEM, `access_token`, Bearer Authorization header, or service-account JSON. Token was not persisted.

---

## 26. Retry count

**0**

`OfficialVeoRealVertexStartV1.AutomaticRetryCount = 0`  
`ProductionVeoHttpClientPolicy.StartAutomaticRetryCount = 0`  
`HttpVeoMotionTransport.SendAsync` is a single `_http.SendAsync`. No Polly. No adapter/transport loop.

The VARCHAR(64) local failure was **NOT_SENT** (HTTP never left). The rematch after that proven NOT_SENT is the one authorized Start, not an automatic retry of a transmitted request.

---

## 27. Poll count

**0**

`fetchPredictOperation` was not called. No Recover. No output download. No video materialization.

---

## 28. Canonical mutation counts

| Mutation | Count |
|---|---|
| ProductionAttempt | **1** |
| AttemptStarted events | 2 (engine empty payload + provenance payload) |
| Permit consume | **1** |
| Provider operation identity | **1** |
| Snapshot create/update/delete | **0** |
| Permit issue / reissue | **0** |
| ACTIVE VIDEO selection | **0** (still `c52b646f-…`) |
| Graph / Story / ActingBeat / VisualContract | **0** |
| Keyframe / duration / cost policy | **0** |
| CREATE_VIDEO artifacts / bindings | **0** |

Allowed CK18 mutations only.

---

## 29. Replay / concurrency protection result

Ledger now holds `RUNNING` + operation name for Start AUTH_HASH + permit `97240e2f-…`.

A further CK18 invocation for this pair must return **START_REPLAY** and must not call `predictLongRunning` again. That rematch was **not** executed.

Concurrent duplicate path: existing unique ISSUED permit index + ledger claim. Winner = 1 Start.

---

## 30. Regression results (actual class names)

Deterministic CK5–CK18B suite **before** live Start: **19 passed / 0 failed**.

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

Live arm (env `CK18_LIVE=1` only): `Ck18LiveVertexStartOnlyTests` PASS.

---

## Live counters

| Counter | Before Start | After accepted Start |
|---|---|---|
| Provider Start | 0 | **1** |
| Provider jobs | 0 | **1** |
| OAuth token acquisition | 0 | **1** |
| Network requests | 0 | **2** |
| Poll | 0 | **0** |
| Permit issue | 0 | **0** |
| Permit consume | 0 | **1** |
| ProductionAttempt | 0 | **1** |
| AttemptStarted | 0 | **1** attempt / 2 events |

---

## Explicit final statement

predictLongRunning = **1**  
fetchPredictOperation = **0**  
automatic retry = **0**  
permit consumed = **1**  
operation identity persisted  
secrets persisted = **0**  
CK19 = **not invoked**

---

## Final handoff

**READY_FOR_CK19_POLL_RECOVER**

CK18 ends. The next checkpoint is CK19 — Poll / Recover / output materialization. CK19 must be invoked explicitly.
