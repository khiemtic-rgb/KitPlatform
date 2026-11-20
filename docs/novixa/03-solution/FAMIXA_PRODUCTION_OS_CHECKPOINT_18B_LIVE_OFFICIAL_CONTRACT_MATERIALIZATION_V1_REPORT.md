# CHECKPOINT 18B — LIVE OFFICIAL CONTRACT MATERIALIZATION V1

Date: 2026-09-23  
Mode: Canonical CK12A freeze + CK13B issue only.  
Safety: Vertex Start = 0. HTTP/OAuth/credential read = 0. Permit consume = 0. ProductionAttempt = 0. No CK18 Start.

---

## VERDICT

**PASS — OFFICIAL SNAPSHOT + PERMIT MATERIALIZED**

Handoff: **READY_FOR_CK18_REAL_START**

CK18B stops here. CK18 Real Vertex Start was not invoked.

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
| Live host | localhost `kitplatform` (Development) |

---

## 2. Preflight result

**PASS** before first write.

| Check | Result |
|---|---|
| A Official target | PASS (`RealVertexExecutionV1Lock`) |
| B ACTIVE VIDEO selection | PASS `c52b646f-…` google/veo-3.1 EXPLICIT DIRECTOR_SELECTION |
| C CREATE_VIDEO task | 0 before freeze — created by canonical `FreezeAsync` (CK12A path) |
| D Official CREATE_VIDEO snapshot | 0 |
| E Official matching permit | 0 |
| F Official keyframe | `91daaf4b-…` ByteSha256 `9fe2bd76034203a14531631fd1911e3ef15f1249c588259a17c692c952305d57` VERIFIED; local bytes matched |
| G Freeze reconstructable | PASS via `OfficialCreateVideoFreezeV1.FromEp01Sh02Canon` |
| H Permit reconstructable | PASS via `OfficialCreateVideoPermitAuthorizationV1` |
| I Provider/network | unused closed graph Start/Poll/HTTP/OAuth = 0 |
| J Ambiguity | none |

---

## 3. Snapshot before count

**0** official CREATE_VIDEO snapshots for official build + SH02.

---

## 4. Snapshot materialization method actually used

`OfficialCreateVideoFreezeV1.FreezeAsync` → `IProductionWorkStore.InsertSnapshotAsync` → `pack_content.production_task_input_snapshots`.

No manual INSERT.

`FreezeAsync` also created the official CREATE_VIDEO request/task (`9cd27409-41fe-4194-b5a3-1b90f4d7b2a2`, status `PROVIDER_FROZEN`) as part of the existing CK12A path.

---

## 5. Snapshot ID

`fde661bb-a33f-418d-ac68-5c6f4751f81f`

---

## 6. Snapshot hash

`56f8259e72c1403553b4deb4ebc58378dc60b41275372f08e6bd464d8fe4aebb`

(`ShotExecutionCompiler.SnapshotHash`)

---

## 7. Full 12-field AssertFrozenMatch result

Reloaded from live persistence, then `ProductionFrozenCreateVideoMatch.Assert`:

| Field | Live value | Result |
|---|---|---|
| ProviderId | `google` | PASS |
| ModelId | `veo-3.1` | PASS |
| Story | ProductionEp01Sh02IngressCanon | PASS |
| ActingBeat | ProductionEp01Sh02IngressCanon | PASS |
| VisualContract | ProductionEp01Sh02IngressCanon | PASS |
| DurationSec | **10** (not 8) | PASS |
| TimingHash | `01e81b441e96524c4100446f42812d97ddc784bb5fed2de2d3d4732a35078c9d` (`Hash("10.0s")`) | PASS |
| PerformanceSnapshotId | `6505b8a2-8a71-4d74-8d93-f1fae8319b6b` | PASS |
| PerformanceHash | `PERFORMANCE_HASH_V1:8fa1d71b14e46e64bf1cf4dc7b107ae7f39be69f3f79da1d55d24f33a116914a` | PASS |
| KeyframeArtifactId | `91daaf4b-5550-4cbc-b9f9-0bd0be556cef` | PASS |
| KeyframeHash | `9fe2bd76034203a14531631fd1911e3ef15f1249c588259a17c692c952305d57` | PASS |
| SnapshotHash | `56f8259e72c1403553b4deb4ebc58378dc60b41275372f08e6bd464d8fe4aebb` | PASS |

---

## 8. Keyframe ID / hash

| Field | Value |
|---|---|
| ArtifactId | `91daaf4b-5550-4cbc-b9f9-0bd0be556cef` |
| ByteSha256 | `9fe2bd76034203a14531631fd1911e3ef15f1249c588259a17c692c952305d57` |
| Bytes source | existing `App_Data/kit-video-i2v/os-kf/5a1325ae8a7b4f389f496e24f483ace7.mp4` (113493 B) |
| Download / replace | 0 |

---

## 9. Snapshot after count

**1**

---

## 10. Permit before count

**0** official google/veo-3.1 CREATE_VIDEO permits for SH02.

---

## 11. Permit materialization method actually used

`OfficialCreateVideoPermitAuthorizationV1.IssueAsync` → `IProductionExecutionPermitStore.IssueAsync` → `pack_content.production_execution_permit`.

No manual INSERT.

---

## 12. Permit ID

`97240e2f-e603-495b-863a-ebc9ef27b0a8`

---

## 13. Permit hash

`PERMIT_HASH_V1:5d05d4776680aa4a096bd2701a518db31d557f7b602331e18ebaf599c1580868`

(`ProductionExecutionPermitCanon.HashOf` — existing CK13B algorithm)

---

## 14. Permit Reason

`OFFICIAL_CREATE_VIDEO_PERMIT_V1;selectionId=c52b646f-86b9-4a43-834d-d94ab9f013b8;snapshotId=fde661bb-a33f-418d-ac68-5c6f4751f81f;snapshotHash=56f8259e72c1403553b4deb4ebc58378dc60b41275372f08e6bd464d8fe4aebb;provider=google;model=veo-3.1;variant=VIDEO_ONLY;resolution=720p;audio=false;sampleCount=1;amount=0.20;currency=USD;unit=count;costKind=Known;costAsOf=2026-09-22`

---

## 15. Permit state

`ISSUED`  
`ConsumedAt` = **null**

---

## 16. Permit binding verification

| Binding | Result |
|---|---|
| Build / shot / action | PASS |
| Provider / model | google / veo-3.1 PASS |
| SelectionId | `c52b646f-…` PASS |
| SnapshotId / SnapshotHash | exact materialized snapshot PASS |
| VIDEO_ONLY / 720p / audio=false / sampleCount=1 / USD 0.20 COUNT | PASS |

---

## 17. Permit after count

**1**

---

## 18. CK17 Evaluate result

`EXECUTION_AUTHORIZED`

---

## 19. CK17 AUTH_HASH recomputation result

`AUTH_HASH_V1:4a9dc37361b531e98d5bbc9fa1a8249a0e1ab0d3788918d2794ca05921155fe3`

`Evaluate.AuthorizationHash` == `ComputeAuthorizationHash` (exact).

No authorization table invented. Hash not persisted.

---

## 20. Exact authorization identity

Covered by CK17 `AUTH_HASH_V1` over: official build, SH02, CREATE_VIDEO, google, veo-3.1, selection `c52b646f-…`, snapshot `fde661bb-…` / hash `56f8259e…`, permit hash `PERMIT_HASH_V1:5d05d477…`, official Reason, VIDEO_ONLY, 720p, audio=false, sampleCount=1, paid-network / authenticator / HTTP composition state, StartUrl / PollUrl for `official-veo-compose` + `veo-3.1-generate-001`.

---

## 21. Mutation counts

| Mutation | Count |
|---|---|
| Snapshot create | **1** (first live run) |
| Snapshot update | 0 |
| Snapshot delete | 0 |
| Permit issue | **1** (first live run) |
| Permit update | 0 |
| Permit consume | 0 |
| ProductionAttempt | 0 |
| AttemptStarted | 0 |
| Graph | 0 |
| ACTIVE VIDEO selection | 0 (still `c52b646f-…` google/veo-3.1 ACTIVE) |
| Content / keyframe / duration / cost policy | 0 |
| CREATE_VIDEO task (via FreezeAsync) | 1 |

Second live run: snapshot reuse + permit reuse. No additional rows.

---

## 22. Side-effect counters

| Counter | Value |
|---|---|
| Vertex Start | 0 |
| Vertex jobs | 0 |
| Poll | 0 |
| OAuth | 0 |
| Network | 0 |
| Credential read / GetAccessToken | 0 |

---

## 23. Idempotency result

Second `MaterializeDevelopmentAsync`: `SnapshotReused=true`, `PermitReused=true`, same snapshot id/hash, same permit id/hash, same AUTH_HASH. Snapshot count stayed 1. Permit count stayed 1.

---

## 24. Negative-case matrix

| Case | Result |
|---|---|
| A wrong build | no snapshot |
| B wrong shot | no snapshot |
| C wrong action / non-VIDEO selection | no CREATE_VIDEO snapshot; CREATE_IMAGE untouched |
| D provider/model mismatch | no snapshot |
| E frozen contract mismatch | no permit |
| F keyframe mismatch | no snapshot / no permit |
| G duplicate snapshot | AMBIGUOUS, fail closed |
| H duplicate issued permit | AMBIGUOUS, no latest-pick |
| I consumed permit | no replacement issue |
| J permit binding mismatch | fail closed |
| K AUTH / CK18 | Evaluate AUTHORIZED; Start = 0 |
| L OAuth | GetAccessToken = 0 |
| M HTTP | Predict/Fetch = 0 |
| N provider Start | 0 |
| O repeat | idempotent |

---

## 25. Regression results (actual class names)

`dotnet test` CK5–CK18A + CK18B: **19 passed / 0 failed**.

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

Live arm (not in default suite): `Ck18bLiveMaterializationOnlyTests` PASS (create then reuse).

---

## 26. Explicit final statement

Vertex Start = 0  
Vertex jobs = 0  
Poll = 0  
OAuth = 0  
Network = 0  
Permit consumed = 0

---

## Final handoff

**READY_FOR_CK18_REAL_START**

CK18B did not call `predictLongRunning`, `fetchPredictOperation`, OAuth, Vertex, provider transport, `ProductionAttempt`, or `AttemptStarted`.
