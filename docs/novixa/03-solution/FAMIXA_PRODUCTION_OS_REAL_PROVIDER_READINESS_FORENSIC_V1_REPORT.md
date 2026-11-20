# FAMIXA PRODUCTION OS — REAL PROVIDER READINESS FORENSIC V1

Date: 2026-09-18  
Mode: FORENSIC / READ-ONLY  
No provider calls. No paid calls. No DB writes. No source patches.

---

## 1. Verdict

**BLOCKED**

Checkpoint 3B proved unpaid CREATE_VIDEO through Production OS. A real Motion provider cannot enter that same boundary today.

Expected:

```
Production OS → IMotionExecutionAdapter → provider adapter → vendor API
```

Actual:

```
Production OS → IMotionExecutionAdapter → FixtureMotionAdapter only
                                              (paid throw if IsPaid)

Parallel (not OS):
  IFamixaMotionProvider → RunwayMotionProvider / VeoMotionProvider → clients
  IVideoGenerationProvider → RunwayVideoGenerationProvider → ContentRunwayClient
```

There is no Runway or Veo `IMotionExecutionAdapter`. DI registers one singleton: `FixtureMotionAdapter`. `ProductionCreateVideoService.CreateVideoAsync` still throws if `_motion.IsPaid`. The request DTO cannot name a provider or model. Production OS has no cost guard. In-flight duplicate POST can create a second task and call the adapter again.

FAMIXA cannot safely spend money on ONE real CREATE_VIDEO through Production OS yet.

---

## 2. Runway

### Provider boundary

Not on Production OS. Two parallel ports exist.

| Port | File | Class | Methods |
|---|---|---|---|
| OS (unused by Runway) | `Production/Providers/IMotionExecutionAdapter.cs` | `IMotionExecutionAdapter` | `ExecuteFrozenAsync` L11 |
| Famixa HOW | `FamixaProviderOrchestrationV1Rules.cs` | `IFamixaMotionProvider` | `StartAsync` / `GetAsync` / `RecoverAsync` L146–152 |
| Legacy execution | `ContentContracts.cs` | `IVideoGenerationProvider` | `GenerateAsync` / `ResumeAsync` L4082–4087 |

### Model

- Famixa catalog: `runway` / `gen4_turbo` (`FamixaProviderIds.Runway` L10, descriptor L208–215)
- HTTP client hardcode: `ContentRunwayClient.TurboModel = "gen4_turbo"` L14
- Video-generation provider id: `"RUNWAY"` (`RunwayVideoGenerationProvider.ProviderId` L21) — different string from Famixa `runway`

### Adapter

`FamixaProviderAdapters.cs` `RunwayMotionProvider` L38–112  
Does **not** implement `IMotionExecutionAdapter`. Returns `OutputUrl`, not bytes.

### Registry

- `ContentPackDependencyInjection.cs` L178: `AddSingleton<IMotionExecutionAdapter, FixtureMotionAdapter>()`
- L65: `AddScoped<RunwayMotionProvider>()`
- L168: `AddScoped<IVideoGenerationProvider, RunwayVideoGenerationProvider>()`
- Famixa router default Motion list: `[runway, wan]` — Veo not default (`FamixaProviderRouterV1.cs` L43)

### Start

`ContentRunwayClient.CreateImageToVideoAsync` L67–106  
POST `v1/image_to_video`, model `gen4_turbo`, returns vendor task `id`.

`RunwayMotionProvider.StartAsync` L49–77 wraps that. Duration remapped: `>= 8 → 10 else 5` (L55).

`RunwayVideoGenerationProvider.GenerateAsync` L26–54 also starts, then **blocks in-process poll** (`WaitAsync` L62–98, 90 × 2s).

### Poll

`ContentRunwayClient.GetTaskAsync` L108–149  
GET `v1/tasks/{id}` → status + `output` URL.

`RunwayMotionProvider.PollAsync` L85–104 = Get/Recover.

### Recover

`RunwayMotionProvider.RecoverAsync` L82–83 → same `PollAsync`.  
`RunwayVideoGenerationProvider.ResumeAsync` L56–59 → `WaitAsync(providerRequestId)`.

Production OS has no poll/recover on `IMotionExecutionAdapter`.

### Output

Famixa path: vendor URL in `FamixaProviderResult.OutputUrl` (L96).  
`RunwayVideoGenerationProvider`: fetches bytes via `IContentSeriesTakeProxyService.FetchAsync` (L84–90).  
Neither writes `ProductionArtifact` / CURRENT / VISIBLE.

### Artifact / Binding / Preview

Only if output later entered `ProductionCreateVideoService` L141–149 (adapter bytes → file store → `VerifyArtifactAsync` → `BindCurrent` / `BindVisible` → `GetPreviewAsync`). Runway never reaches those lines.

### Cost

`FamixaProviderCostCatalog.Quote` (`FamixaProviderOrchestrationV1Rules.cs` L555–567): 5 cr/s, billed 5s or 10s → **25 or 50 credits**, `CostKind.Estimate`.  
**Not consulted** by `ProductionCreateVideoService`.

### Auth

`ContentRunwayClient.ResolveAsync` L41–48 → `ContentVideoConfigParser.Resolve` L159–164: org `runwayApiKey`, secret ref, `ContentOptions.RunwayApiKey`, `Content:RunwayApiKey`, env `RUNWAY_API_KEY`. Header on HTTP only.

### Retry

OS: `ProductionWorkExecutionEngine.RetryAsync` L329–330 = `StartAttemptAsync` (new attempt, same task). **Not called** by `CreateVideoAsync`.  
A new POST with a new idempotency key creates a new request+task+adapter call.

### Failure handling

`ContentRunwayClient.EnsureOk` throws on non-success.  
`RunwayMotionProvider` wraps via `FamixaProviderErrors.Wrap`.  
`RunwayVideoGenerationProvider` swallows start/poll/fetch exceptions into `FAILED` / `PROCESSING` results (L47–49, L71–73, L92–94). Poll timeout → `PROCESSING` with no bytes (L97).

---

## 3. Veo 3.1

### Provider boundary

Same split. No `IMotionExecutionAdapter` implementation.

### Model

- Famixa: `google` / `veo-3.1` (`FamixaProviderIds.Google` L12, descriptor L226–233)
- Client: `ContentVeoClient.CatalogModel = "veo-3.1"` L15; `VertexModel = "veo-3.1-generate-001"` L16
- Stale OS catalog: `ProductionOsRules.VeoVideoProfile` L199–210 marks `IMAGE_TO_VIDEO` **UNSUPPORTED** (not used by CREATE_VIDEO OS path)

### Adapter

`FamixaProviderAdapters.cs` `VeoMotionProvider` L114–178  
`IFamixaMotionProvider` only. `OutputUrl` is `FamixaVeoTakePlaybackV1.PublicPath`, not Production OS storage.

### Registry

`ContentPackDependencyInjection.cs` L43–46 HttpClient, L67 `AddScoped<VeoMotionProvider>()`.  
Not registered as `IMotionExecutionAdapter`.  
Famixa default Motion routing **excludes** Google (`FamixaProviderRouterV1.cs` L43). Veo is explicit-engine + Join only.

### Start

`ContentVeoClient.CreateImageToVideoAsync` L131–181  
Vertex `predictLongRunning`, `generateAudio = false`, duration snapped to 4 / 6 / 8 (L143), aspect `16:9` or `9:16` (L314–318). Returns `veo_` + base64 operation name (L230–236).

### Poll / Recover

`GetTaskAsync` L183–227: `fetchPredictOperation`.  
`VeoMotionProvider.GetAsync` / `RecoverAsync` L153–157 → same poll.  
On success: persist bytes under KitVideo folder `"veo"` (L226) and return a **public path URL** (L227).

### Output

Physical file exists on the **legacy Veo store** (`PersistNamed("veo", …)`), not `os-test/{test_run_id}/video/`. Authority to Famixa callers is URL/path, not `ProductionArtifact`.

### Artifact / Binding / Preview

Not on Production OS. Preview of a Veo take is `FamixaVeoTakePlaybackV1`, not `ResolveVisiblePreviewAsync`.

### Cost

`FamixaProviderCostCatalog.Quote` has **no Google/Veo branch**. Falls through to `CostKind.Unknown`, amount `null` (L623–632). Descriptor billing: `"UNKNOWN"` (L232).  
Famixa router rejects unknown cost **only if MaxCost is set** (`FamixaProviderRouterV1.cs` L227–237). Production OS never sets MaxCost.

### Auth

`ContentVeoClient.ResolveAsync` L48–54 → Join file: org `veoJoinFile`, secret ref, `Content:VeoJoinFile`, env `VEO_JOIN_FILE` (`ContentVideoConfig.cs` L173–178). JWT from service account; `private_key` not logged (class comment L12). Token is HTTP-only.

### Retry

Same OS gap as Runway. Client recover = re-poll same operation name. Safe **if** the same `veo_` id is reused. Production OS has no place to store/reuse that id except `Attempt.ProviderJobId` after `SubmitProviderJobAsync` — which only runs after `ExecuteFrozenAsync` already returned bytes.

### Failure handling

Start: non-success throws (`CreateImageToVideoAsync` L175–176).  
Poll: HTTP fail → `FAILED` (L203–204); LRO error → `FAILED` (L208–211); done without bytes → `FAILED` (L218–222); GCS-only → `FAILED` (L220–221). No in-adapter timeout loop (caller must poll).

---

## 4. Provider boundary violations

| Finding | Provider | File | Class | Method | Classification | Severity |
|---|---|---|---|---|---|---|
| Only Fixture implements `IMotionExecutionAdapter` | both | `ContentPackDependencyInjection.cs` L178 | DI | — | GAP | **CRITICAL** |
| Paid CREATE_VIDEO hard-blocked | both | `ProductionCreateVideoService.cs` L85–86 | `ProductionCreateVideoService` | `CreateVideoAsync` | CORE_REQUIRED | **CRITICAL** (blocks spend; leftover after 3B) |
| Request cannot name provider/model | both | `ProductionApiDtos.cs` L3–16 | `ProductionCreateVideoRequestDto` | — | GAP | **CRITICAL** |
| Freeze always uses DI adapter ids | both | `ProductionCreateVideoService.cs` L138 | `ProductionCreateVideoService` | `CreateVideoAsync` | CORE_REQUIRED | **CRITICAL** |
| `ExecuteFrozenAsync` is sync-complete; no Start/Poll/Recover | both | `IMotionExecutionAdapter.cs` L11 | `IMotionExecutionAdapter` | `ExecuteFrozenAsync` | GAP | **CRITICAL** |
| Famixa motion returns URL not OS artifact | Runway, Veo | `FamixaProviderAdapters.cs` L91–98, L164–171 | `RunwayMotionProvider` / `VeoMotionProvider` | `PollAsync` | LEGACY_AUTHORITY (Famixa) / not OS | HIGH |
| `IVideoGenerationProvider` id `RUNWAY` ≠ Famixa `runway` | Runway | `RunwayVideoGenerationProvider.cs` L21 | `RunwayVideoGenerationProvider` | `ProviderId` | ADAPTER_LOCAL | MEDIUM |
| Veo I2V marked UNSUPPORTED in old catalog | Veo | `ProductionOsRules.cs` L199–210 | `ProductionOsRules` | `VeoVideoProfile` | LEGACY_READ_ONLY | MEDIUM |
| Duration remapped 5/10 | Runway | `FamixaProviderAdapters.cs` L55 | `RunwayMotionProvider` | `StartAsync` | ADAPTER_LOCAL | HIGH |
| Duration remapped 4/6/8 | Veo | `ContentVeoClient.cs` L143 | `ContentVeoClient` | `CreateImageToVideoAsync` | ADAPTER_LOCAL | HIGH |
| Aspect forced 1280:720 | Runway | `RunwayVideoGenerationProvider.cs` L103–108 | `RunwayVideoGenerationProvider` | `MapRatio` | ADAPTER_LOCAL | MEDIUM |
| `RunwayI2vPrompt` rewrite not in OS snapshot | Runway | `ContentRunwayClient.cs` L82 | `ContentRunwayClient` | `CreateImageToVideoAsync` | ADAPTER_LOCAL | HIGH |
| Veo persist folder `veo/` not TEST `os-test/…/video` | Veo | `ContentVeoClient.cs` L226 | `ContentVeoClient` | `GetTaskAsync` | LEGACY_AUTHORITY | HIGH |
| In-flight/failed POST can start a second task+adapter call | both | `ProductionCreateVideoService.cs` L127–141 | `ProductionCreateVideoService` | `CreateVideoAsync` | VIOLATION (for paid) | **CRITICAL** |
| Veo cost UNKNOWN | Veo | `FamixaProviderOrchestrationV1Rules.cs` L623–632 | `FamixaProviderCostCatalog` | `Quote` | GAP | **CRITICAL** |
| Production OS has no MaxCost | both | `ProductionCreateVideoService.cs` | `ProductionCreateVideoService` | `CreateVideoAsync` | GAP | **CRITICAL** |
| `gen4_turbo` / `veo-3.1` only in tests/comments inside `Production/` | both | `ProductionWorkExecutionV1Regression.cs` L81, L131, L175 | regression | — | LEGACY_READ_ONLY | LOW |

Core Application orchestration (`ProductionCreateVideoService`, `ProductionExecutionService`, `CreateVideoActionContractGate`) does not branch on Runway/Veo/gen4_turbo/vendor URLs. Vendor strings do not affect OS execution today. The violation is **absence of a paid adapter on the proven port**, plus leftover paid block and missing selection/cost/async/idempotency gates.

---

## 5. Input Snapshot

Record: `ProductionEntities.cs` `ProductionTaskInputSnapshot` L39–65.  
Written by `ProductionWorkExecutionEngine.FreezeInputAsync` L59–89.

| Field | Proven 3B live (fixture) | Sufficient to reproduce? |
|---|---|---|
| scope | task.Scope.BuildId = TEST `bbbbbbbb-…0371` | yes (on task, not snapshot row) |
| shotCode | task.Scope.ShotCode = `TEST-SC01-SH01` | yes (on task) |
| Story | `TEST STORY — child walks into a room.` | yes |
| story hash | `de423b2e…4f4e` | yes |
| ActingBeat | `TEST ACTING — walks forward and looks around.` | yes |
| acting hash | `d277c174…3f19` | yes |
| VisualContract | `TEST VISUAL — 16:9 interior…` | yes |
| visual hash | `9e977078…6523` | yes |
| Timing / timing hash | `5s` / `93e3d8c5…b3b7` | yes |
| DurationSec | 5 | yes |
| AspectRatio | `16:9` | yes |
| keyframeArtifactId | `33333333-…0371` | yes |
| keyframe physical identity | `KeyframeHash` = PNG SHA `3db4d45f…8a20` | yes (hash, not bytes) |
| providerId | `fixture` | yes |
| modelId | `fixture-motion-v1` | yes |
| CompilerOutput / compiler hash | ShotExecutionCompiler V1 text | yes for OS prompt; **not** vendor rewrite |
| inputSnapshotHash | `444baf03…c699` | yes |
| ParentArtifactIds | `[33333333-…0371]` | yes |

Missing for an exact **vendor** replay: remapped duration, `RunwayI2vPrompt` text, last-frame URI, Vertex `veo-3.1-generate-001` vs catalog `veo-3.1`, Join project/location. Snapshot is sufficient to reproduce the OS request, not the exact HTTP body a current client would send.

---

## 6. Provider/model freeze

Trace (actual OS path):

1. User POST `ContentProductionController.CreateVideo` L99–119 — **no provider/model fields**
2. `ProductionCreateVideoService.CreateVideoAsync` L85–86 — paid reject
3. `EvaluateCreateVideoAsync` uses `_motion.ProviderId` / `_motion.ModelId` (`ProductionExecutionService.cs` L100)
4. `FreezeInputAsync(taskId, input, _motion.ProviderId, _motion.ModelId)` L138
5. Task + snapshot persist those ids (`FreezeInputAsync` L84–85)
6. `StartAttemptAsync` copies `task.ProviderId` / `task.ModelId` (`ProductionWorkExecutionEngine.cs` L95–100)
7. `ExecuteFrozenAsync(snap)` L141 — adapter is the same DI instance

`NewTaskForProviderChangeAsync` L305–326 creates a **new task** if ids change. Replay rejects snapshot vs live adapter mismatch (`ReplayCreateVideoAsync` L188–191).

No silent Runway/Veo fallback on the OS path. The lock is to **fixture**.

| Verdict | PASS (no silent paid fallback) + GAP (cannot select a real provider) |

---

## 7. Provider job identity

| Stage | Fixture (OS, proven) | Runway (parallel) | Veo (parallel) |
|---|---|---|---|
| Start id | `fixture-motion-` + task prefix | Runway task `id` | `veo_` + operation name |
| Attempt.providerJobId | set in `SubmitProviderJobAsync` **after** adapter returns (`CreateVideoAsync` L145) | never written to Production Attempt | never written to Production Attempt |
| Poll / recover | N/A (sync) | same task id | same `veo_` id |
| Output identity | SHA256 of bytes | vendor URL then fetched bytes (video-gen path only) | file under `veo/` + public path |

Late result: `ApplyLateResultAsync` L248–271 marks Late; cannot steal CURRENT if a current binding exists or attempt number is older. **SOURCE VERIFIED / LIVE UNVERIFIED.** Not wired to HTTP or to Famixa poll.

Unknown job / collision / poll after restart: Famixa `CanHandleTask` prefixes (`rw_` strip, `veo_`). Production OS has no recover-after-restart loop. If process dies after vendor start but before `SubmitProviderJobAsync`, the vendor job id is **lost**.

| Verdict | GAP |

---

## 8. Artifact verification

`VerifyArtifactCoreAsync` (`ProductionWorkExecutionEngine.cs` L150–189):

| Check | Implemented? |
|---|---|
| bytes length ≥ 32 | yes — else `FAILED_IDENTITY`, no artifact |
| SHA256 | stored |
| MIME | stored as given, not validated |
| duration | stored as given, not decoded |
| decodability | **no** |
| scope / shot | inherited from task; not re-checked here |
| attempt lineage | `artifact.AttemptId` |
| provider job | `attempt.ProviderJobId` |
| model | on attempt/task, not on artifact row |
| inputSnapshotHash | yes |

`BindAsync` L198–199 requires `VERIFIED`.

| Case | Actual |
|---|---|
| Provider SUCCESS + no/short bytes | `CreateVideoAsync` L142–143 throws `FAILED_IDENTITY` before verify; no bind |
| Provider SUCCESS + invalid MP4 (bytes ≥ 32) | **VERIFIED** (3B decode is suite-only, not engine) |
| wrong scope | `EvaluateCreateVideoAsync` L95–96 + `RequireArtifactInRequestScopeAsync` **before** adapter |
| wrong shot | keyframe owner shot must match (`CreateVideoActionContractGate` L67–73) |
| missing provider identity | attempt can have null job id until submit; verify still succeeds |

| Verdict | PASS for empty output / bind gate / cross-scope-before-call. GAP for decode/MIME/duration. |

---

## 9. Retry / idempotency

| Event | Actual |
|---|---|
| Same idempotency + succeeded task | `ReplayCreateVideoAsync` returns existing; no adapter call (3B T21) |
| Same idempotency + no succeeded task (in-flight or failed) | replay null → `CreateRequestAsync` returns existing → `CreateTaskAsync` **inserts a new task** → `ExecuteFrozenAsync` **again** (`CreateVideoAsync` L127–141 + `CreateTaskAsync` L36–47) |
| Same task submitted twice | no OS HTTP for “submit task”; `RetryAsync` would add attempt 2 |
| Browser retries POST after success | replay — safe |
| Browser retries POST while first still running | **second paid generation** |
| Provider/poll callback twice | OS has no callback; Famixa poll is pull |

Expected for paid: no uncontrolled second generation. **Not met** for in-flight/failed replay.

| Verdict | GAP / BLOCKED for paid |

---

## 10. Late result

`ApplyLateResultAsync` L248–271: older or post-current attempt → `LATE`, event `LATE_RESULT_IGNORED`, CURRENT/VISIBLE unchanged.

3B T26: SOURCE VERIFIED. No live late-result API: **LIVE UNVERIFIED**.

Famixa poll has no CURRENT/VISIBLE awareness. A late Famixa URL write to graph/`takeUrl` would be a **legacy** steal, not OS bind.

---

## 11. Cost guard

| Layer | Behavior |
|---|---|
| Production OS CREATE_VIDEO | **none**. No MaxCost, no catalog, no UNKNOWN fail-closed |
| Famixa router | If MaxCost set and amount null → `UNKNOWN_COST` reject (`FamixaProviderRouterV1.cs` L227–237). If MaxCost omitted, unknown is eligible |
| Runway catalog | 5 cr/s → 25 (5s) or 50 (10s) ESTIMATE |
| Veo catalog | **UNKNOWN** |
| `IVideoGenerationProvider` result | `CostUnknown: true` (`RunwayVideoGenerationProvider` L87–90) |

A paid OS call (if the leftover throw were removed and a paid adapter injected) would run **with no cost policy**.

| Verdict | BLOCKED |

---

## 12. Capability contract

| | Runway `gen4_turbo` | Veo 3.1 |
|---|---|---|
| IMAGE_TO_VIDEO | Famixa + `RunwayVideoProfile` SUPPORTED; client POST I2V | Client **does** I2V; `VeoVideoProfile` says UNSUPPORTED |
| Mute output | AUDIO UNSUPPORTED (`ProductionOsRules` L196) | `generateAudio = false` (`ContentVeoClient` L168) |
| Native audio | no | client forces off |
| Duration | adapter 5 or 10s; profile DURATION_5S/10S SUPPORTED | client 4 / 6 / 8 |
| Aspect | client `ratio`; video-gen MapRatio always `1280:720` | `16:9` or `9:16` |
| Last frame | `CreateImageToVideoAsync` optional `lastImage` L73 | not in client start payload |
| Input MIME | data-URI image | data-URI png/jpeg/webp; URL rejected L300–301 |
| Input transport | data-URI in JSON | base64 in Vertex instance |
| Provider ID | `runway` vs `RUNWAY` | `google` |
| Model ID | `gen4_turbo` | catalog `veo-3.1` / Vertex `veo-3.1-generate-001` |

Vendor duration/aspect live in adapters/clients, not in Production OS. That is correct **if** they stay adapter-local. They are not on `IMotionExecutionAdapter` yet.

---

## 13. Auth / secret safety

| Provider | Resolution | Enters snapshot / task / artifact / binding / UI / events? |
|---|---|---|
| Runway | CONFIG + SECRET REF + ENV `RUNWAY_API_KEY` | No — HTTP header only |
| Veo | CONFIG + SECRET REF + ENV `VEO_JOIN_FILE` (Join JSON) | No — JWT in HTTP only; logs project/location not `private_key` |

Not printed. Classification: **ENV / SECRET REF / CONFIG**.  
Production OS entities do not have API key fields.

Live configured? **UNKNOWN** (this audit did not read org settings or env values).

---

## 14. Failure matrix

| Failure | Attempt | Task | Artifact | Binding |
|---|---|---|---|---|
| Adapter throw after `StartAttempt` | stays `STARTED` | `RUNNING` | none | unchanged |
| Bytes < 32 | submitted maybe; then throw | not succeeded | none | unchanged |
| Verify empty | `FAILED_IDENTITY` on task | `FAILED_IDENTITY` | none | unchanged |
| HTTP 4xx/5xx (Famixa) | n/a on OS | n/a | n/a | n/a |
| Runway poll timeout | n/a on OS; video-gen returns `PROCESSING` no bytes | — | — | — |
| Veo success no file | Famixa `FAILED` | — | — | — |
| Download fail (Runway video-gen) | `FAILED` result | — | — | — |
| Restart during poll | OS: vendor id lost if not yet `SubmitProviderJob`; Famixa: recover if task id kept | — | — | — |
| Invalid MP4 ≥ 32 bytes | OS **VERIFIED** | `SUCCEEDED` | created | **would bind** |

Provider failure on the OS path cannot rebind CURRENT/VISIBLE because bind is after verify. Invalid-but-long MP4 **can** become CURRENT/VISIBLE.

---

## 15. Legacy contamination

| Path | Classification vs Production OS CREATE_VIDEO |
|---|---|
| `POST /api/content/production/requests` | authority for OS (fixture only) |
| `POST /series/turbo` / `turboTaskId` / `sendTurbo` | NON-AUTHORITY for OS (`LegacySeriesGraphAdapter` L17–28). Still AUTHORITY for Director desk |
| `takeUrl` / `previewUrl` / `acceptedTake.url` | NON-AUTHORITY for OS preview (`GetPreviewAsync` uses VISIBLE). AUTHORITY on series graph if Director writes them |
| `videoApproved` / `runwayAttempts` / `nextShotProductionCommand` | NON-AUTHORITY for OS |
| localStorage / IndexedDB / session blobs | NON-AUTHORITY for OS |
| `compileI2vPrompt` / `VideoVisualIngressV1Rules.RunwayI2vPrompt` | ADAPTER_LOCAL on Runway client; not OS compiler |
| `graph_json` | NON-AUTHORITY for OS; FILM isolation proven in 3B |
| `video_generation_execution` | parallel AUTHORITY for that subsystem; not OS |
| Famixa `OutputUrl` | AUTHORITY for Famixa motion UI |

No AUTHORITY leak **into** the proven OS POST path. Director turbo remains a live AUTHORITY path **beside** OS. Using it for “the real run” would bypass Production OS.

---

## 16. Live runtime readiness

Configuration/DI prove clients **can** be invoked on legacy ports. This is not proof of a successful generation.

| | Production OS | Legacy / Famixa |
|---|---|---|
| **Runway** | **BLOCKED** (no adapter, paid throw, no selection, no cost) | **READY** if `RUNWAY_API_KEY` or org key present — **UNKNOWN** whether key is set (not read) |
| **Veo 3.1** | **BLOCKED** (same + cost UNKNOWN + I2V catalog stale) | **READY** if Join file present — **UNKNOWN** whether Join is set (not read) |

---

## 17. One-real-run safety

**BLOCKED**

| Required | Status |
|---|---|
| TEST scope / keyframe / story / acting / visual / timing | present (3B) |
| explicit provider | missing on DTO and OS freeze |
| explicit model | missing |
| cost guard | missing on OS; Veo UNKNOWN |
| one request / one task / one attempt | true after **success**; false if POST retried in-flight |
| isolated TEST storage | OS yes; Veo client writes `veo/` |
| artifact verification + CURRENT/VISIBLE + preview | OS yes for fixture bytes |
| `IMotionExecutionAdapter` for the paid vendor | **missing** |
| paid execution allowed | **hard-blocked** |

---

## 18. Git diff

This phase did not edit source.

`git diff --stat` / `git status` before and after the audit: **no new source hunks from this forensic**. The working tree was already dirty (3B Production OS files, prior Content work, untracked reports). That dirt is pre-existing, not introduced to “run checks.”

Expected “clean tree” was already false before this phase. Forensic added only this report file.

---

## 19. Critical blockers

1. No Runway/Veo `IMotionExecutionAdapter` — paid vendors cannot enter the proven OS port.
2. `CreateVideoAsync` throws on `IsPaid` (`ProductionCreateVideoService.cs` L85–86).
3. No explicit provider/model on the Production request; freeze is the DI singleton (`fixture`).
4. `IMotionExecutionAdapter` cannot Start/Poll/Recover; a real job started inside `ExecuteFrozenAsync` cannot survive restart.
5. Failed/in-flight idempotent POST creates a **new task** and calls the adapter again.
6. No Production OS cost guard; Veo catalog cost is UNKNOWN.
7. Famixa/Veo output authority is URL / `veo/` store, not `os-test/{test_run_id}/video/` + `ProductionArtifact`.

---

## 20. Recommended next phase

**REAL PROVIDER ADAPTER BRIDGE V1 (ONE provider, TEST only, still no live spend until the bridge is proven unpaid-shaped)**

Implement a single `IMotionExecutionAdapter` (recommend Runway first: known cost, existing byte fetch) that:

- is selected by explicit frozen `ProviderId`/`ModelId` on the request
- uses Start → persist `providerJobId` on the Attempt **before** wait → Poll/Recover
- returns physical bytes into existing `Write` / `VerifyArtifactAsync` / bind / preview
- writes only `os-test/{test_run_id}/video/`
- fails closed on UNKNOWN cost and on in-flight duplicate POST
- does not call the vendor until that bridge is reviewed

Do not lift the paid throw, do not touch Veo in the same phase, do not use turbo/`takeUrl`.

**STOP.**
