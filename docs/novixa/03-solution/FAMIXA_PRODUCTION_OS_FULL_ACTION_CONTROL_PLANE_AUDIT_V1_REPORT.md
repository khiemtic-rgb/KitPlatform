# FAMIXA PRODUCTION OS — FULL ACTION CONTROL PLANE AUDIT V1

Date: 2026-09-19  
Mode: FORENSIC AUDIT ONLY  
Scope: Famixa production execution (Content / KIT_MKT). Official EP01 / EP99 read-only.

**Provider calls = 0**  
**Media generations = 0**  
**Database writes = 0**  
**Source changes = 0**  
**UI changes = 0**

Every claim is classified: FACT / IMPLEMENTED / TESTED / LIVE_VERIFIED / ASSUMED / UNVERIFIED / RECOMMENDED.

Phase G CREATE_VIDEO (TEST Runway) is closed evidence, not this phase’s work. Do not treat other actions as the same level.

---

## 1. EXECUTIVE VERDICT

**CUTOVER REQUIRED**

The Production OS control plane **exists** and is the only path that can produce a durable Request → Task → InputSnapshot → Attempt → Artifact → Binding → OS Preview chain.

It is **not** yet the single canonical execution plane for the entire Famixa film system.

Two planes coexist:

| Plane | What it is | Where it is authority today |
|---|---|---|
| Production OS | Request/Task/Snapshot/Attempt/Artifact/Binding | TEST CREATE_VIDEO real Runway (Phase G). Other OS actions: unpaid fixture, tested. |
| Legacy film plane | `SeriesShotRun`, `previewUrl`, `acceptedTake.url`, `takeUrl`, `lipsyncUrl`, IndexedDB TTS, `nextShotProductionCommand` | Official EP01/EP99 Director workspace. Still reachable APIs. |

CREATE_VIDEO on TEST is **LEVEL 4** (real provider + physical artifact + bindings). It is **not LEVEL 5**: the Director UI does not send PerformanceSnapshot / provider / `AuthorizePaidNetwork`, and its confirm copy says fixture-only.

Other OS actions are implemented as fixture adapters that refuse paid+network. Gemini, ElevenLabs, Fal, Series Turbo, and `IVideoGenerationProvider` remain registered outside Production OS.

IndexedDB `kit-famixa-tts` is still a **voice-byte store used as authority** for official-film TTS resolve (`resolveLineAudio` → `loadTtsBlobAny`). That is a **BLOCKER** for CREATE_VOICE cutover.

The system can become one control plane. It is not one today.

---

## 2. SCOPE AND SAFETY

| Rule | Status | Class |
|---|---|---|
| No implementation / patch / migration | held | **FACT** |
| No provider / AI / media generation | held | **FACT** |
| No UI / DB write | held | **FACT** |
| Runway path not modified | held | **FACT** |
| EP01 / EP99 not mutated | not written | **FACT** |
| Phase G chain not reused or rewritten | not touched | **FACT** |

Read sources: Production OS application/infrastructure, `ContentProductionController`, `ContentController` legacy series routes, admin Director / Series tab / Shot Production, migrations 368–374, existing regressions and smokes.

No live DB write. Phase G identities below are prior **LIVE_VERIFIED** evidence, not re-executed.

---

## 3. CURRENT PRODUCTION OS ARCHITECTURE

### 3.1 Declared plane — FACT + IMPLEMENTED

```
User intent
  → ProductionRequest          pack_content.production_requests
  → ProductionTask             pack_content.production_tasks
  → ProductionTaskInputSnapshot (immutable, 1:1 task)
  → Provider/Model freeze      task + snapshot columns
  → ProductionAttempt
  → Adapter (IMotion / IImage / IVoice / ILipSync / IMix / IAssembly)
  → Physical bytes + SHA
  → ProductionArtifact
  → ProductionBinding CURRENT / VISIBLE / ACCEPTED
  → Preview: VISIBLE → Artifact → storageKey → bytes
```

Declared actions (`ProductionActions.All`):

`CREATE_IMAGE` `CREATE_VIDEO` `CREATE_VOICE` `CREATE_LIPSYNC` `MIX_VIDEO` `ACCEPT` `BIND` `REGENERATE` `ASSEMBLE`

There is **no** `PREVIEW` action (GET only). There is **no** `FINAL` action. **FACT**

### 3.2 Runtime entry — FACT

`POST /api/content/production/requests`  
`ContentProductionController.CreateVideo`

| `body.Action` | Service method |
|---|---|
| `CREATE_VIDEO` (default) | `ProductionCreateVideoService.CreateVideoAsync` |
| `CREATE_IMAGE` | `CreateImageAsync` |
| `CREATE_VOICE` | `CreateVoiceAsync` |
| `CREATE_LIPSYNC` | `CreateLipsyncAsync` |
| `MIX_VIDEO` | `MixVideoAsync` |
| `ACCEPT` | `AcceptOsAsync` |
| `REGENERATE` | `RegenerateOsAsync` |
| `ASSEMBLE` | `AssembleOsAsync` |
| `BIND` or unknown | `ACTION_REMAP_FORBIDDEN` |

### 3.3 Adapter registration — FACT

`ContentPackDependencyInjection.cs`:

| Port | Live registration | Paid/network |
|---|---|---|
| `IRunwayTransport` | `HttpRunwayTransport` | paid+network |
| `RunwayMotionExecutionAdapter` | resolver when provider=runway model=gen4_turbo | paid |
| `IMotionExecutionAdapter` default | `FixtureMotionAdapter` | unpaid |
| `IImageExecutionAdapter` | `ImportImageAdapter` | unpaid import |
| `IVoiceExecutionAdapter` | `FixtureVoiceAdapter` | unpaid |
| `ILipSyncExecutionAdapter` | `FixtureLipSyncAdapter` | unpaid |
| `IMixExecutionAdapter` | `FixtureMixAdapter` | unpaid |
| `IAssemblyExecutionAdapter` | `FixtureAssemblyAdapter` | unpaid plan id only |
| `IVideoGenerationProvider` | `RunwayVideoGenerationProvider` | **legacy, still registered** |

Any OS action except CREATE_VIDEO Runway throws `PAID_NETWORK_FORBIDDEN` if the adapter is paid or requires network. **IMPLEMENTED**

### 3.4 Dual plane — FACT

Legacy HTTP still exists on `ContentController`:

- `POST /api/content/series/turbo`
- `POST /api/content/series/still`
- `POST /api/content/series/lipsync`
- `POST /api/content/series/assemble`
- `POST …/video-generation-execution/execute` (`IVideoGenerationProvider`)

Director Series tab **warns and refuses** several of those from named handlers. The routes remain reachable. Graph fields remain the official-film preview source.

---

## 4. ACTION-BY-ACTION AUDIT

Coverage levels (mission §15). Do not copy CREATE_VIDEO’s level onto other actions.

| Action | Level | Evidence |
|---|---|---|
| CREATE_IMAGE | **3** unpaid OS fixture/import; Gemini still outside | `CreateImageAsync` + CompleteActionAuthority tests |
| CREATE_VIDEO | **4** TEST Runway Phase G; **not 5** (Director is not that path) | Phase G report; UI omits snapshot/provider/paid |
| CREATE_VOICE | **3** OS fixture; film IDB = AUTHORITY | `FixtureVoiceAdapter`; `kit-famixa-tts` |
| CREATE_LIPSYNC | **3** OS fixture; Fal legacy | `CreateLipsyncAsync`; `POST /series/lipsync` |
| MIX_VIDEO | **3** OS fixture; not FFmpeg | `FixtureMixAdapter` |
| ACCEPT | **3** OS binding; film `acceptedTake.url` remains | `AcceptOsAsync` |
| BIND | **2** internal Bind*; POST `BIND` forbidden | `ExecuteOsActionAsync` default remap |
| REGENERATE | **3** fixture motion only | `RegenerateOsAsync` |
| PREVIEW | **4** TEST OS GET media; **1** film URL plane | `ResolveVisiblePreviewAsync` vs `previewUrl` |
| ASSEMBLY | **2** rows, no physical file | `AssembleOsAsync` |
| FINAL | **0** | no `ProductionActions.Final` |

### A. CREATE_IMAGE

**Level: 3 (unpaid OS) + legacy Gemini still reachable**

Trace (OS):

```
Director CTA Tạo hình / os-create-image
  → ContentFamixaSeriesTab.executeProductionOsAction('CREATE_IMAGE')
  → createProductionRequest({ action: 'CREATE_IMAGE' })
  → ContentProductionController.CreateVideo
  → ProductionCreateVideoService.CreateImageAsync
  → ImportImageAdapter if MediaBytes ≥ 32 else FixtureImageAdapter
  → OpenFrozenAsync → Request/Task/Freeze/Attempt
  → ExecuteFrozenAsync (synthetic PNG or imported bytes)
  → MaterializeMediaAsync → KEYFRAME artifact → BindCurrent (CURRENT:KEYFRAME)
```

| Question | Answer | Class |
|---|---|---|
| On Production OS? | Yes, fixture/import | **IMPLEMENTED** + **TESTED** |
| Gemini on OS? | No Gemini image adapter | **FACT** |
| Paid blocked? | `IsPaid \|\| RequiresNetwork` → `PAID_NETWORK_FORBIDDEN` | **FACT** |
| Series Still separate? | `POST /series/still` still on `ContentController` | **FACT** |
| UI still call still? | Named handlers call `productionOsLegacyRefuseNote('CREATE_IMAGE', 'POST /series/still')` | **IMPLEMENTED** |
| Still route reachable? | Yes if any other client calls it | **FACT** |
| localStorage/IDB image authority? | `content-local-image-library.ts` IndexedDB exists | **FACT** |
| Preview for image? | OS CREATE_IMAGE does **not** bind VISIBLE | **FACT** |
| Latest generated image? | Official film still uses `keyframeDataUrl` / run still URL in `ShotProductionWorkspace` | **FACT** |

UI confirm: “Fixture — không gửi Gemini”. **FACT**

Character Master / Identity Conditioned / `_characterStudio.GenerateAsync` / `_videoPixels.GenerateAsync` are **separate** generation parks, not Production OS CREATE_IMAGE. **FACT**

### B. CREATE_VIDEO

**Level: 4 on TEST Runway (Phase G). UI path Level 3 fixture if it can pass the snapshot gate.**

Proven Phase G chain (do not reuse as this audit’s execution):

Request `2522ec00-…` → Task `88a376e6-…` → InputSnapshot `76b35eb4-…` → Attempt `4fb46ca0-…` → job `62311c1e-…` → Artifact `5872d0a9-…` → CURRENT/VISIBLE.

Trace (canonical paid):

```
POST /production/requests + PreparedAttemptId + AuthorizePaidNetwork
  → CreateVideoAsync
  → ExecutePreparedCreateVideoAsync
  → PlanCreateVideoAsync (resolver → RunwayMotionExecutionAdapter)
  → RunAttemptLifecycleAsync
  → StartFrozenAsync → HttpRunwayTransport.CreateImageToVideoAsync
  → SubmitProviderJobAsync (persist ProviderJobId, PROVIDER_SUBMITTED)
  → PollUntilDoneAsync(persisted id)
  → FetchOutputAsync → VerifyArtifact → BindCurrent + BindVisible
```

**FACT** + Phase G **LIVE_VERIFIED**

| Topic | Current code | Class |
|---|---|---|
| Fixture adapter | `FixtureMotionAdapter` default fallback when provider/model omitted | **FACT** |
| Runway adapter | resolver + `HttpRunwayTransport` | **FACT** + **LIVE_VERIFIED** |
| Veo adapter on OS | none in `ProductionMotionCatalog` (fixture + runway only) | **FACT** |
| UI sends provider/model? | `executeProductionOsAction` does **not** send `providerId`/`modelId` | **FACT** |
| UI sends snapshot? | GET/POST claim has no `performanceSnapshotId` | **FACT** |
| TEST snapshot required? | `ProductionPerformanceConsumption` yes for non-official-film | **FACT** |
| Official film snapshot? | skipped; paid film CREATE_VIDEO blocked in Evaluate | **FACT** |
| UI paid Start? | no `AuthorizePaidNetwork`; confirm says fixture | **FACT** |
| Idempotency | unique key; inflight replay; Prepare+ExecutePrepared | **IMPLEMENTED** + **TESTED** + Phase G **LIVE_VERIFIED** |
| Late result on live Start | `MaterializeOutputAsync` always rebinds; does **not** call `ApplyLateResultAsync` | **FACT** |
| `ApplyLateResultAsync` | never binds; tested | **TESTED** |
| Cross-scope | snapshot bind + `RequireParent` + preview shot match | **IMPLEMENTED** + **TESTED** + Phase G **LIVE_VERIFIED** |

Deviations from the proven architecture (report only):

1. Director CREATE_VIDEO is not the Phase G path.
2. Catalog GET without snapshot → TEST CREATE_VIDEO `MISSING_PERFORMANCE_SNAPSHOT` → CTA hidden.
3. Default motion adapter in DI is Fixture; Runway only when explicitly selected.
4. `IVideoGenerationProvider` / `POST video-generation-execution/execute` still registered.

Veo remains a **legacy engine** (`persistFamixaMotionEngine`, `refuseUnwiredVeo`), not an OS motion adapter. **FACT**

### C. CREATE_VOICE

**Level: 3 on OS fixture. Official film: IDB AUTHORITY = BLOCKER.**

OS trace:

```
executeProductionOsAction('CREATE_VOICE')
  → lineId LINE-{shot} / CHAR-TEST / VOICE-TEST
  → CreateVoiceAsync
  → EvaluateCreateVoiceAsync (PerformanceSnapshot required on TEST)
  → FixtureVoiceAdapter.ExecuteFrozenAsync → ProductionTestWav
  → CURRENT:VOICE only (no VISIBLE)
```

| Store | Role | Class |
|---|---|---|
| OS `os-test/{run}/voice/{attempt}` | fixture physical artifact | **IMPLEMENTED** + **TESTED** |
| `famixa-voice-physical-identity.ts` | artifactId → storage key contract | **IMPLEMENTED** |
| `famixa-voice-physical-idb-store.ts` | `indexedDB.open('kit-famixa-tts')` | **FACT** |
| `resolveLineAudio` / `loadTtsBlobAny` | plays/hydrates IDB/memory blobs | **FACT** |
| ElevenLabs | `ContentElevenLabsClient`; Series tab refuses “ElevenLabs / IDB TTS” for OS CTA | **FACT** |

IndexedDB is **AUTHORITY** for official-film spoken-line audio resolve, not a cache in front of an OS Voice artifact. **BLOCKER**

F5-TTS: no OS adapter. **FACT**

### D. CREATE_LIPSYNC

**Level: 3 fixture OS. Fal is legacy, not OS.**

OS:

```
RequireParent VIDEO_TAKE + VOICE (same build+shot)
  → EvaluateCreateLipsyncAsync (snapshot on TEST)
  → FixtureLipSyncAdapter → ProductionTestMp4
  → LIPSYNC artifact → CURRENT:LIPSYNC + VISIBLE
```

Frozen parents are snapshot `ParentArtifactIds`. Re-read of graph after freeze: no. **FACT**

| Legacy | Status | Class |
|---|---|---|
| `POST /series/lipsync` | route exists; Series handler refuses | **FACT** |
| Fal client / two-face / Take55–56 | still in `famixa-lipsync-*`, `content-famixa-prod-v2` | **FACT** |
| `lipsyncUrl` as playable | `content-famixa-final-source.ts` `assembleVideoUrl` / `lipsyncVideoUrl` | **FACT** |
| Speaker target on OS | PerformanceSnapshot + claim; fixture ignores targeting | **FACT** |

No Fal run in this audit. **UNVERIFIED** live Fal identity.

### E. MIX_VIDEO

**Level: 3 fixture OS. Not server FFmpeg. Not client-as-OS.**

OS: require LIPSYNC parent → `FixtureMixAdapter` → synthetic MP4 → MIX + CURRENT + VISIBLE.

Contract allows `MotionOrLipsyncArtifactId`; live `MixVideoAsync` **requires lipsyncArtifactId** only. **FACT**

Legacy:

- `POST /content/series/assemble` refused from mix CTA.
- Client mix / `ENSURE_MIX` / `ShotProductionFinalStore` IndexedDB still exist for official-film “Hoàn thiện / final”. **FACT**

Mix does not mutate source artifacts on the OS path (new bytes, new ArtifactId). **IMPLEMENTED** + **TESTED**

### F. ACCEPT

**Level: 3 OS. Not generation. Film still has `acceptedTake`.**

OS: explicit `AcceptArtifactId` → same-scope verified artifact → Request/Task/Snapshot → synthetic job `accept-…` → `AcceptArtifactAsync` + `ACCEPTED:{role}`.

| Risk | Code | Class |
|---|---|---|
| Accept other shot | `RequireParentAsync` CrossShot/CrossScope | **IMPLEMENTED** + **TESTED** |
| Accept unverified | `FAILED_VERIFICATION` | **FACT** |
| Stale artifact | possible if still VERIFIED and same shot; no freshness gate beyond identity | **FACT** |
| `acceptedTake.url` | still on `SeriesShotRun` / `content-famixa-final-source.ts` | **FACT** |

ACCEPT creates an Attempt (`os` / `accept-v1`). Unnecessary for generation; it is a decision attempt. **FACT**

### G. BIND

**Declared. Not a user Action on the POST switch.**

Internal: `BindCurrentAsync` / `BindVisibleAsync` / `BindNamedAsync` / `AcceptArtifactAsync`.

`ProductionActions.Bind` in schema CHECK and registry. POST `Action=BIND` → `ACTION_REMAP_FORBIDDEN`. **FACT**

No “bind latest” API. Bindings take exact `artifactId`. **FACT**

Roles: `CURRENT`, `VISIBLE`, `ACCEPTED`, typed `CURRENT:ROLE` / `VISIBLE:ROLE` / `ACCEPTED:ROLE`, plus `EDITORIAL_SOURCE`, `FINAL` constants. **FINAL binding is not written by AssembleOsAsync.** **FACT**

### H. REGENERATE

**Level: 3. Always FixtureMotionAdapter. New Request/Task/Attempt.**

`RegenerateOsAsync` requires prior VIDEO_TAKE. Refuses if replay returns the same ArtifactId. Prior SHA must be unchanged.

Does **not** reuse Runway / ExecutePrepared. UI confirm is fixture. **FACT**

Automatic retries:

| Kind | Exists? | Class |
|---|---|---|
| OS automatic second Start | no (permit + no retry loop) | **FACT** + Phase G **LIVE_VERIFIED** |
| Transport retry | not in `HttpRunwayTransport` | **FACT** |
| UI `osInflightRef` | blocks duplicate same-shot action | **IMPLEMENTED** |
| `sameFingerprintBlindRetry` | Shot Production orchestrator | **FACT** (legacy desk) |
| Failed-task retry | `OpenFrozenAsync` starts **new Attempt** on same Task | **FACT** |

### I. PREVIEW

**Two resolvers.**

OS (canonical):

```
GET /production/shots/{buildId}/{shotCode}/preview
  → GetPreviewAsync → ResolveVisiblePreviewAsync
  → VISIBLE binding → Artifact → storageKey
GET …/preview/media → ReadPreviewMediaAsync → file bytes
```

Cross-shot mismatch throws `CROSS_SHOT_FORBIDDEN`. **FACT** + Phase G **LIVE_VERIFIED** (TEST)

Director film preview (`ShotProductionWorkspace`, `content-famixa-final-source.ts`):

| Symbol | Classification |
|---|---|
| `VISIBLE → Artifact → bytes` (TEST `osPreviewByShot`) | AUTHORITY for TEST after OS fetch |
| `acceptedTake.url` | LEGACY AUTHORITY |
| `previewUrl` / `takeUrl` / `lipsyncUrl` | LEGACY AUTHORITY / DANGEROUS BYPASS |
| `fallbackMotionUrl` | LEGACY |
| blob `URL.createObjectURL` from OS media | DERIVED VIEW (TEST) |
| `nextShotProductionCommand` | DERIVED command, not artifact authority |
| localStorage engine picker | CACHE for engine choice, not artifact |

### J. ASSEMBLY

**PARTIAL / NOT a physical FINAL.**

`AssembleOsAsync`: ACCEPTED artifacts per listed shot → Request/Task/Snapshot → `FixtureAssemblyAdapter.PlanFrozenAsync` (string job id) → `CreateAssemblyAsync` inserts `FilmAssembly` + `FilmAssemblyShot` rows.

No FFmpeg. No ASSEMBLY ProductionArtifact. No FINAL binding. DTO `storageKey` is `"assembly/" + assemblyId`. **FACT** + **TESTED** (authority suite)

### K. FINAL

**NOT_IMPLEMENTED on Production OS.**

Client: `ShotProductionFinalStore` IndexedDB; `resolveFinalSource` / `assembleVideoUrl` on graph URLs; `READY_FINAL` orchestrator command.

No FINAL Request/Task/Artifact. **FACT**

### Additional actions found

| Name | Kind |
|---|---|
| `PREPARE` `POST /requests/prepare` | deferred CREATE_VIDEO freeze, not a separate action enum |
| Kit Video `video-generation-execution` | legacy production shot park |
| `CREATE_VIDEO_CONTRACT` / approve contract | contract park, not OS action |
| Character / still / pixel generate | image parks |

---

## 5. MASTER ACTION MATRIX

| Action | UI entry | API | Request | Task | InputSnapshot | Provider/Model Freeze | Attempt | ProviderJobId | Physical Output | Verification | Artifact | Binding | Preview | Idempotency | Late Result Guard | Cross-Scope Guard | Legacy Bypass | Status | Evidence |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| CREATE_IMAGE | Tạo hình / os-create-image | POST /production/requests | yes | yes | yes | fixture/import | yes | fixture/import id | PNG fixture or import | size/SHA | KEYFRAME | CURRENT:KEYFRAME | no VISIBLE | yes | unbind via InvalidateDownstream | RequireParent N/A | series/still + Gemini parks | TESTED_ONLY | CreateImageAsync; CompleteActionAuthority |
| CREATE_VIDEO | Tạo video (OS) or Phase G POST | POST /production/requests | yes | yes | yes | runway or fixture | yes | persisted before poll (paid) | MP4 | ISO-BMFF + duration | VIDEO_TAKE | CURRENT+VISIBLE | OS GET media | yes | ApplyLate tested; live Materialize rebinds | snapshot + scope | turbo + IVideoGenerationProvider | LIVE_VERIFIED (TEST paid); UI PARTIAL | Phase G report; CreateVideoAsync |
| CREATE_VOICE | Nạp thoại | same POST | yes | yes | yes | fixture-voice-v1 | yes | fixture id | WAV fixture | WAV decode | VOICE | CURRENT:VOICE | not OS preview | yes | same Materialize | snapshot scope | ElevenLabs + kit-famixa-tts IDB | PARTIAL | CreateVoiceAsync; IDB store |
| CREATE_LIPSYNC | Khớp môi | same POST | yes | yes | yes | fixture-lipsync-v1 | yes | fixture id | fixture MP4 | MP4 rules | LIPSYNC | CURRENT+VISIBLE | OS if VISIBLE | yes | same | parent same shot | series/lipsync + Fal | TESTED_ONLY | CreateLipsyncAsync |
| MIX_VIDEO | Hoàn thiện | same POST | yes | yes | yes | fixture-mix-v1 | yes | fixture id | fixture MP4 | MP4 rules | MIX | CURRENT+VISIBLE | OS if VISIBLE | yes | same | parent same shot | series/assemble + client mix | TESTED_ONLY | MixVideoAsync |
| ACCEPT | Dùng kết quả | same POST | yes | yes | yes | os/accept-v1 | yes | accept- prefix | none new | existing VERIFIED | none new | ACCEPTED + ACCEPTED:role | no | replay if same | N/A | RequireParent | acceptedTake.url | TESTED_ONLY | AcceptOsAsync |
| BIND | none | not on POST switch | schema only | — | — | — | — | — | — | — | — | internal Bind* | — | — | — | shot+build | — | PARTIAL | ACTION_REMAP_FORBIDDEN |
| REGENERATE | Tạo lại | same POST | new | new | new | fixture-motion only | new | fixture | fixture MP4 | MP4 | new VIDEO_TAKE | CURRENT+VISIBLE | OS | prior ArtifactId in key | prior immutable | same shot | blind retry orch | TESTED_ONLY | RegenerateOsAsync |
| PREVIEW | player | GET preview + media | no | no | no | no | no | no | read existing | — | via VISIBLE | requires VISIBLE | OS path | N/A | no | shot match | URL resolvers | LIVE_VERIFIED (TEST OS); LEGACY (film) | ResolveVisiblePreviewAsync; final-source.ts |
| ASSEMBLE | Lắp tập | same POST | yes | yes | yes | fixture-assembly-v1 | yes | plan string | **no concat file** | row-level | **no ASSEMBLY artifact** | none FINAL | none | yes | N/A | per-shot ACCEPTED | client final IDB | PARTIAL | AssembleOsAsync |
| FINAL | READY_FINAL | none OS | no | no | no | no | no | no | client IDB / URLs | no | NOT_IMPLEMENTED | NOT_IMPLEMENTED | graph URL | — | — | — | ShotProductionFinalStore | NOT_IMPLEMENTED | no ProductionActions.Final |

Status vocabulary only as specified.

---

## 6. AUTHORITY MATRIX

| Concern | Current Authority | Target Authority | Violating Paths | Severity |
|---|---|---|---|---|
| Story | Shot graph + OS snapshot at freeze | Shot WHAT + snapshot | UI reads live `shot.story` at click | MEDIUM |
| Acting | graph actingBeat / derived snapshot | PerformanceSnapshot | UI concatenates beat fields; TEST snapshot unused by UI | HIGH |
| Visual Contract | `shot.visual` | snapshot | same | MEDIUM |
| Keyframe | OS CURRENT:KEYFRAME on TEST; film `keyframeDataUrl` / still URL | OS KEYFRAME artifact | local image library IDB; series/still | HIGH |
| Timing | `shot.seconds` / dialogue floor | Timing V3 + snapshot | UI `timing: String(shot.seconds)` | MEDIUM |
| Provider | OS freeze if sent; else fixture fallback; film engine picker | frozen on Task | `persistFamixaMotionEngine`; omitted UI fields | HIGH |
| Model | same | frozen on Task | same | HIGH |
| Request | OS table when OS POST | ProductionRequest | turbo / still / lipsync / video-generation-execution | CRITICAL |
| Task | OS table | ProductionTask | legacy execution ids | CRITICAL |
| Input Snapshot | OS immutable row | snapshot | graph re-read for film preview | CRITICAL |
| Attempt | OS row | Attempt | browser turboTaskId | HIGH |
| ProviderJobId | OS attempt (paid video) | persisted Attempt | Fal/turbo task ids on run | HIGH |
| Artifact | OS for OS actions | ProductionArtifact | URL-as-take | CRITICAL |
| CURRENT | OS bindings on TEST | CURRENT:role | SeriesShotRun current take | CRITICAL |
| VISIBLE | OS on TEST | VISIBLE | previewUrl | CRITICAL |
| ACCEPTED | OS ACCEPTED + film acceptedTake | ACCEPTED binding | `acceptedTake.url` | CRITICAL |
| Preview | OS media on TEST; URLs on film | VISIBLE → bytes | `content-famixa-final-source.ts` | CRITICAL |
| Shot State | GET /state (OS) **and** SeriesShotRun | OS nodes + bindings | `nextShotProductionCommand` snapshot | CRITICAL |
| Assembly | OS rows, no file | FilmAssembly + artifacts | client assemble | HIGH |
| Final Film | client IDB + URLs | FINAL artifact | ShotProductionFinalStore | CRITICAL |
| Voice bytes | IDB kit-famixa-tts | OS VOICE artifact | `createIdbVoicePhysicalStore` | CRITICAL |

---

## 7. LEGACY BYPASS MATRIX

| Legacy Path | Entry Point | Still Reachable? | Can Generate? | Can Bind? | Can Change Preview? | Authority Risk | Cutover Required |
|---|---|---:|---:|---:|---:|---|---|
| Series Turbo | `ContentController` POST series/turbo; Series tab refuses named send | yes | yes if called | no OS bind | yes via run.previewUrl | HIGH | yes |
| Series Still | POST series/still; refuse in Series tab | yes | Gemini still | no OS bind | keyframeDataUrl | HIGH | yes |
| Series Lipsync | POST series/lipsync; refuse | yes | Fal if called | no OS bind | lipsyncUrl | HIGH | yes |
| Series assemble | POST series/assemble; refuse mix/export | yes | server mix | no OS bind | graph | HIGH | yes |
| IVideoGenerationProvider | POST video-engine/…/video-generation-execution/execute | yes | yes | own park | own artifact | HIGH | yes |
| ContentRunwayClient | HttpRunwayTransport **and** RunwayVideoGenerationProvider | yes | yes | OS only via adapter | legacy takes | HIGH | isolate to OS only |
| ContentGeminiClient | still / character / pixels | yes | yes | no | still URLs | HIGH | yes |
| ContentElevenLabsClient | voice services; OS CTA refused | yes | yes | IDB | audio URLs | CRITICAL | yes |
| ContentFalClient | lipsync pipeline | yes | yes | no OS | lipsyncUrl | HIGH | yes |
| SeriesShotRun | graph_json runs | yes | no (state) | acceptedTake | yes | CRITICAL | yes |
| nextShotProductionCommand | ShotProductionOrchestrator | yes | drives ENSURE_* | no | desk phase | HIGH | remap to OS actions |
| kit-famixa-tts IDB | famixa-voice-physical-idb-store; resolveLineAudio | yes | store | voice authority | playback | CRITICAL | yes |
| ShotProductionFinalStore IDB | final store | yes | client final | no OS FINAL | player | HIGH | yes |
| content-local-image-library IDB | local stills | yes | cache/import | no | still | MEDIUM | yes |
| video-generation-execution | ContentController 4739+ | yes | yes | own | own | HIGH | yes |

“Can Bind?” = write Production OS CURRENT/VISIBLE/ACCEPTED. Legacy paths do not.

---

## 8. UI CTA MATRIX

| UI Button | File | Handler | Intended Action | Actual Action | API | 1:1? | Risk |
|---|---|---|---|---|---|---|---|
| Tạo hình | SeriesTab + production-os-action-authority | executeProductionOsAction | CREATE_IMAGE | CREATE_IMAGE fixture if available | POST /production/requests | yes if catalog available | TEST snapshot N/A; film FILM_PROTECTED |
| Tạo video | same | same / Phase G not used | CREATE_VIDEO | OS fixture **or hidden** (no snapshot claim); film protected | POST without provider/snapshot/paid | **no vs Phase G** | HIGH |
| Nạp thoại | same | same | CREATE_VOICE | fixture CHAR-TEST/VOICE-TEST; hidden if no snapshot | POST | label 1:1, inputs stub | HIGH |
| Khớp môi | same | same | CREATE_LIPSYNC | fixture if MOTION+VOICE nodes | POST | yes if catalog | HIGH |
| Hoàn thiện | same | same | MIX_VIDEO | fixture if LIPSYNC node | POST | yes if catalog | MEDIUM |
| Dùng kết quả | same | same | ACCEPT | OS ACCEPT mix/lipsync/visible/current | POST | yes | MEDIUM |
| Tạo lại | same | REGENERATE | REGENERATE | fixture motion only | POST | yes | HIGH vs Runway |
| Lắp tập | same | ASSEMBLE | ASSEMBLE | OS rows, no file | POST | yes | MEDIUM |
| Preview player | ShotProductionWorkspace | previewBind / takeUrl / osPreview | PREVIEW | OS blob on TEST **else** graph URL | GET OS **or** URL | **no** | CRITICAL |
| Assemble/Final (orch) | ShotProductionOrchestrator READY_FINAL | client final | FINAL | IDB/URL | none OS | no | CRITICAL |
| Legacy Tạo video | startShotMotion = no-op; sendTurbo warn | CREATE_VIDEO | refused | none | 1:1 refuse | film stuck |
| Picture/still leftover | still handlers warn | CREATE_IMAGE | refused | none | — | film stuck |
| nextShotProductionCommand ENSURE_* | ShotProductionDirector | various | may still label legacy phases | not OS action ids | no | HIGH |

`productionActionForDeskCta` maps several legacy action strings (`motion`, `picture`, `lipsync`, `mix`, `use-current-take`) onto OS actions. That is **label remapping**, not a silent POST remap: `executeProductionOsAction` sends the mapped action and rejects `result.action !== action`. **FACT**

`applyProductionOsActionAuthority` can **insert** a CREATE_VIDEO CTA when catalog says available, even beside approve-picture. **FACT**

Official film: `mayMutateProductionOs` false → `FILM_PROTECTED`. Combined with turbo refuse: **official film generation CTAs have no remaining authorized generator** from those handlers. **FACT**. Other entry points **UNVERIFIED**.

---

## 9. ARTIFACT LINEAGE MATRIX

| Artifact Role | Producer Action | Physical Store | SHA | Task | Attempt | ProviderJobId | InputSnapshot | Binding | Preview Source |
|---|---|---|---|---|---|---|---|---|---|
| KEYFRAME | CREATE_IMAGE / test register | os-test/…/kf or import | yes | yes | yes | fixture/import | hash | CURRENT:KEYFRAME | not OS video preview |
| VIDEO_TAKE | CREATE_VIDEO / REGENERATE | os-test/…/video/{attempt} | yes | yes | yes | runway or fixture | hash + perf via snap | CURRENT+VISIBLE | OS VISIBLE |
| VOICE | CREATE_VOICE | os-test/…/voice or os-voice | yes | yes | yes | fixture | hash | CURRENT:VOICE | not OS preview/media (video/mp4 only) |
| LIPSYNC | CREATE_LIPSYNC | os-test video folder | yes | yes | yes | fixture | parents frozen | CURRENT+VISIBLE | OS VISIBLE |
| MIX | MIX_VIDEO | os-test video folder | yes | yes | yes | fixture | parent lipsync | CURRENT+VISIBLE | OS VISIBLE |
| ASSEMBLY | ASSEMBLE | **no file** | **no** | task exists | attempt exists | plan string | parent ids | none | NOT_IMPLEMENTED |
| FINAL | NOT_IMPLEMENTED | client IDB / URLs | NOT_IMPLEMENTED | no | no | no | no | no | LEGACY URL |

OS preview media endpoint always returns `video/mp4`. Voice cannot be previewed through it. **FACT**

---

## 10. SHOT DEPENDENCY GRAPH

Target (`ShotDependencyGraph`, **IMPLEMENTED**):

```
STORY → ACTING → KEYFRAME → MOTION → VOICE → LIPSYNC → MIX → ACCEPTED → ASSEMBLY
```

FINAL is **not** a graph node. **FACT**

| Arrow | Actual source | Actual action | Artifact | Invalidation | Binding | Status |
|---|---|---|---|---|---|---|
| Story → Acting | graph / PerformanceSnapshot | not an OS action | snapshot WHAT | InvalidateDownstream Story | unbind downstream CURRENT/VISIBLE; Accepted if story/acting/kf/motion | IMPLEMENTED (OS); film graph separate |
| Acting → Keyframe | none automatic | CREATE_IMAGE | KEYFRAME | Keyframe change invalidates motion+ | CURRENT:KEYFRAME | PARTIAL |
| Keyframe → Motion | CURRENT:KEYFRAME | CREATE_VIDEO | VIDEO_TAKE | Motion change invalidates lipsync+ | CURRENT+VISIBLE video | LIVE_VERIFIED TEST video; UI BYPASS film |
| Motion → Voice | parallel, not dependent in graph | CREATE_VOICE | VOICE | Voice → lipsync+ | CURRENT:VOICE | PARTIAL; IDB BYPASS |
| Motion+Voice → Lipsync | parent ArtifactIds | CREATE_LIPSYNC | LIPSYNC | Lipsync → mix+ | CURRENT+VISIBLE | TESTED_ONLY fixture |
| Lipsync → Mix | lipsync ArtifactId | MIX_VIDEO | MIX | Mix → accepted/assembly | CURRENT+VISIBLE | TESTED_ONLY; contract vs code mismatch (lipsync-only) |
| Mix → Accept | explicit ArtifactId | ACCEPT | same artifact | Accepted → assembly | ACCEPTED | TESTED_ONLY |
| Accept → Assembly | ACCEPTED ids list | ASSEMBLE | assembly rows | none downstream | none | PARTIAL |
| Assembly → Final | — | — | — | — | — | NOT IMPLEMENTED |
| Film arrows | SeriesShotRun URLs | turbo/still/lipsync/mix/final | URL | graph flags | acceptedTake | BYPASS |

`InvalidateDownstreamAsync` unbinds typed roles and supersedes succeeded downstream tasks. **IMPLEMENTED** + **TESTED** (story mutation in CompleteActionAuthority). Not proven that Director calls it on graph edits. **UNVERIFIED** UI wiring.

---

## 11. IDEMPOTENCY AND RETRY AUDIT

| Action | Same idempotency | In-flight | Explicit retry | Changed input | Changed provider |
|---|---|---|---|---|---|
| CREATE_VIDEO | replay succeeded artifact | `IN_FLIGHT_REPLAY` if job empty; FinishExisting if job set | new Attempt via RetryCreateVideo / OpenFrozen failed | new Task (`NewTaskForChangedInputAsync`) | new Task (`NewTaskForProviderChangeAsync`) |
| Other OS OpenFrozen | replay succeeded | inflight throw | new Attempt on Failed task | new Request if new idem token | new freeze |
| UI | `os-ui:{action}:{build}:{shot}` | `osInflightRef` | REGENERATE new key + motionId | new story changes key only if token includes it — **UI key omits input hash** | not sent |

UI idempotency does **not** include PerformanceHash / story hash (except REGENERATE motionId). Same button can replay a succeeded fixture task after the user edits story. **FACT** — HIGH for fixture UI.

Paid CREATE_VIDEO one-shot permit: second Start forbidden in-process. **IMPLEMENTED** + Phase G **LIVE_VERIFIED**

---

## 12. LATE RESULT AUDIT

| Path | Old task can steal CURRENT/VISIBLE/ACCEPTED/PREVIEW? | Class |
|---|---|---|
| `ApplyLateResultAsync` | No. Marks LATE; never binds | **TESTED** |
| `MaterializeOutputAsync` / `MaterializeMediaAsync` | **Yes, if a later task already bound** — BindCurrent unbinds previous | **FACT** |
| Phase G one-shot | No second Start | **LIVE_VERIFIED** |
| UI inflight | same action same shot blocked | **IMPLEMENTED** |
| Two different idempotency keys | structurally possible | **FACT** |
| ACCEPTED | Accept is explicit ArtifactId; late video bind does not auto-accept | **FACT** |
| Film Fal late patch | smokes claim acceptedTake unchanged | **TESTED** (client); not OS |

**BLOCKER** for a general multi-task control plane: live materialize does not consult newest-task-wins. Mitigated today by one-shot permit + UI inflight, not by Materialize.

---

## 13. CROSS-SCOPE AUDIT

| Guard | Code | Class |
|---|---|---|
| Snapshot build/shot | `PerformanceSnapshotBinding` `CROSS_SHOT_FORBIDDEN` / `CROSS_SCOPE_FORBIDDEN` | **TESTED** + Phase F/G **LIVE_VERIFIED** |
| Parent artifact | `RequireParentAsync` | **IMPLEMENTED** + **TESTED** |
| Preview | `ResolveVisiblePreviewAsync` build+shot | **FACT** + Phase G **LIVE_VERIFIED** |
| Assembly shot rows | owner task shot must match | **FACT** |
| Official film paid video | Evaluate blocks paid official film | **FACT** |
| UI TEST build rewrite | `productionOsBuildIdForShot` forces TEST sandbox for TEST shot codes | **FACT** |

SHOT A artifact cannot bind as SHOT B VISIBLE through OS preview. Film graph `runs[shotId]` is per-shot but URLs can be copied across shots in graph JSON (no OS guard). **FACT**

---

## 14. INPUT FREEZE AUDIT

`FreezeInputAsync` persists story, acting, visual, hashes, keyframe id/hash, voice/motion hashes, timing hash, provider, model, parents, performance id/hash/payload. Unique per task. **FACT**

After freeze, adapters take `ProductionTaskInputSnapshot`. CREATE_VIDEO re-reads **keyframe bytes by frozen KeyframeArtifactId**, not graph. If those bytes were mutated in place, execution would see new pixels under the same ArtifactId. Artifacts are intended immutable; no DB trigger enforces byte immutability. **FACT**

UI freezes **live graph fields at click**, not the Phase F PerformanceSnapshot, and does not send snapshot ids. TEST video/voice/lipsync Evaluate then fail or skip CTA. **FACT**

Official film CREATE_* skip PerformanceSnapshot (`ActionRequiresSnapshot` false). **FACT**

---

## 15. PREVIEW AUTHORITY AUDIT

| Resolver | Authority? | Class |
|---|---|---|
| `ResolveVisiblePreviewAsync` | yes, OS | **FACT** + **LIVE_VERIFIED** TEST |
| `osPreviewByShot` object URL | derived from OS media | **IMPLEMENTED** |
| `resolveTakeUrl` / `assembleVideoUrl` / `lipsyncVideoUrl` | URL authority | **FACT** |
| `acceptedTake.url` | URL authority | **FACT** |
| `ShotProductionWorkspace` player (`takeUrl`, `lipsyncUrl`, `previewBind.finalUrl`) | URL / client final | **FACT** |
| Comment “display only / current-valid from snap.execution” | intent; `hasUsableVideo` still uses those URLs | **FACT** |

Bypasses of `VISIBLE → Artifact → storage` are the film player paths above.

---

## 16. DATABASE FORENSIC

From migrations 368, 369, 370, 371, 372, 373, 374 (schema-as-designed). **FACT**

| Table | PK | Notable constraints |
|---|---|---|
| production_requests | request_id | UNIQUE idempotency_key; action CHECK includes ASSEMBLE (372) |
| production_tasks | task_id | action CHECK; no INPUT_CHANGED failure |
| production_task_input_snapshots | snapshot_id | UNIQUE task_id; 374 performance_* columns |
| production_attempts | attempt_id | UNIQUE (task_id, attempt_number) |
| production_artifacts | artifact_id | SHA, storage_key, input_snapshot_hash; no PerformanceSnapshotId column |
| production_bindings | binding_id | role + artifact_id; unbind via unbound_at |
| production_task_events | event_id | append-only events |
| production_scope_anchor / test shot | scope | TEST vs FILM |
| performance_snapshot | performance_snapshot_id | immutable insert; hash prefix CHECK |
| film_assembly / assembly_shot | assembly rows | 372 assemble action; no FINAL file |

Insufficient for FINAL physical artifact. BIND action allowed in CHECK but unused. Artifact Performance* is join-only. No unique “one active CURRENT per shot+role” enforced beyond application unbind. **FACT**

`series_build.graph_json` remains. 368 comment: legacy compatibility, not deleted. **FACT**

---

## 17. TEST FORENSIC

| Suite | Scope | Actions | Kind | Paid |
|---|---|---|---|---|
| ProductionWorkExecutionV1Regression | engine | freeze, late, bind | UNIT | 0 |
| ProductionPipelineV1Regression | pipeline | image/video fixture | UNIT | 0 |
| ProductionTestCreateVideoV1Regression | TEST video | CREATE_VIDEO fixture | UNIT | 0 |
| ProductionCreateVideoV1Regression | create video | late/bind | UNIT | 0 |
| ProductionRunwayBridgeV1Regression | runway bridge | adapter, cost, late | UNIT (fake transport) | 0 |
| ProductionPaidNetworkPermitV1Regression | permit | Start consume | UNIT | 0 |
| PerformanceSnapshotLiveCutoverV1Regression | Phase F | snapshot+prepare | UNIT | 0 |
| PerformanceConsumptionCutoverV1Regression | Phase C | video/voice/lipsync gates | UNIT | 0 |
| ProductionCompleteActionAuthorityV1Regression | all OS actions fixture | image…assemble, regen | UNIT | 0 |
| ProductionTestKeyframe / Scope / ActionContract | TEST harness | KEYFRAME | UNIT | 0 |
| Phase G | TEST Runway | CREATE_VIDEO | REAL PROVIDER TEST (prior) | 1 Start |
| famixa-production-os-ui-cutover.smoke.ts | UI wiring | CTA 1:1, refuse | UNIT (source slice) | 0 |
| production-os-action-authority.smoke.ts | desk map | same | UNIT | 0 |
| famixa-*-lipsync/final/mix smokes | client graph | URLs | UNIT | 0 |

No new tests run. No paid tests run this phase.

---

## 18. HISTORICAL FAILURE RECHECK

| Historical Failure | Can still happen? | Exact Code Path | Root Cause | Severity |
|---|---:|---|---|---|
| Create video A → receive video B | Yes on film URL plane; No on Phase G chain if bindings hold | `assembleVideoUrl` / `previewUrl`; OS preview uses VISIBLE artifact | dual preview authority | CRITICAL |
| Attach voice → image changes | Yes if still/IDB stills refresh independently of KEYFRAME artifact | still handlers, keyframeDataUrl, local image library | image not OS-bound on film | HIGH |
| Button disappears | Yes | `applyProductionOsActionAuthority` hides CTA when catalog `available !== true` (missing snapshot) | catalog fail-closed + UI omits snapshot | HIGH |
| Take from another desk appears | OS preview no; graph yes if URL copied | `runs[shotId].previewUrl` assignment | no OS guard on graph JSON | CRITICAL |
| Old artifact becomes current | Yes if older Materialize binds later | `BindCurrentAsync` unbinds previous | live late-bind | HIGH |
| Latest provider output in another shot | OS no; graph yes | series run map | graph authority | CRITICAL |
| Task success, UI other output | Yes | OS state vs SeriesShotRun player | dual shot state | CRITICAL |
| Input changed, task still valid | Yes on UI idempotency without input hash; No if `NewTaskForChangedInput` used | `productionOsIdempotencyKey`; Evaluate vs frozen snap | UI key; film graph | HIGH |
| Late Fal/take overwrites accept | Client smokes say no write to acceptedTake; OS Accept separate | famixa-lipsync-late-result-reconciliation | two accept authorities | HIGH |

Do not treat “OS would prevent it” as film-player prevention.

---

## 19. CUTOVER GAP LIST

1. Dual shot-state: SeriesShotRun + OS GET /state.
2. Dual preview: URL fields vs VISIBLE artifact.
3. Dual accept: `acceptedTake.url` vs ACCEPTED binding.
4. Voice IDB AUTHORITY (`kit-famixa-tts`).
5. UI OS POST omits PerformanceSnapshot, provider/model, paid permit.
6. TEST CREATE_VIDEO/VOICE/LIPSYNC CTAs fail-closed without snapshot query params.
7. Official film: OS mutation forbidden + legacy refused = no authorized generate path from those handlers.
8. Image/Voice/Lipsync/Mix adapters are fixture-only.
9. No Veo/Wan OS adapter.
10. `IVideoGenerationProvider` and series/* still registered.
11. BIND action not executable; FINAL not implemented; ASSEMBLY has no file.
12. `Materialize*` late-bind can overwrite newer CURRENT/VISIBLE.
13. Preview media is video/mp4 only (no voice).
14. `nextShotProductionCommand` still a second orchestrator.
15. Gemini / character / pixel parks still generate outside OS.

---

## 20. RECOMMENDED ARCHITECTURE

Target (unchanged):

```
USER INTENT
  → ProductionRequest
  → ActionContract (1:1 CTA)
  → ProductionTask
  → InputSnapshot (WHAT + frozen HOW)
  → Provider/Model Freeze
  → Attempt
  → Provider Adapter (one port per capability)
  → Physical Artifact + Verify
  → ProductionArtifact
  → Binding CURRENT / VISIBLE / ACCEPTED
  → Preview = VISIBLE → storage
  → Shot state = bindings + dependency nodes
  → Assembly of ACCEPTED artifacts
  → FINAL artifact
```

Map leftovers into that target — do not run them in parallel as authority:

| Leftover | Becomes |
|---|---|
| SeriesShotRun preview/take/lipsync/acceptedTake | DERIVED view or deleted |
| series/turbo, still, lipsync, assemble | compatibility refuse or delete after OS adapters exist |
| IVideoGenerationProvider | delete or wrap only behind OS CREATE_VIDEO |
| ContentRunwayClient | transport for RunwayMotionExecutionAdapter only |
| Gemini / ElevenLabs / Fal clients | IImage / IVoice / ILipSync adapters |
| kit-famixa-tts IDB | CACHE in front of OS VOICE storageKey |
| nextShotProductionCommand | labels over GET /actions only |
| ShotProductionFinalStore | CACHE of FINAL artifact bytes |
| Fixture adapters | TEST unpaid only |

WHAT stays on the shot graph. HOW stays on Task/Attempt. Provider never becomes Canon.

---

## 21. NEXT SINGLE PHASE

**FULL SHOT STATE + PREVIEW AUTHORITY CUTOVER**

### WHY

Phase G already proved one real OS execution can own Artifact + CURRENT + VISIBLE + OS preview. The remaining systemic failures (“video A vs B”, “other desk take”, “success but other UI output”) come from **SeriesShotRun URL authority still sitting beside OS bindings**, not from a missing Runway improvement.

Voice IDB is a BLOCKER for CREATE_VOICE, but cutting over voice while the player still prefers `previewUrl` / `acceptedTake.url` leaves the film plane split. Shot-state unification is the smallest change that makes **every** later provider cutover land on one plane.

### WHAT it changes

- Director / Studio preview and “current / visible / accepted” read Production OS bindings (or fail-closed empty).
- `content-famixa-final-source` / workspace player stop treating provider URLs as authority.
- GET /actions claim includes PerformanceSnapshot when the shot has one (so TEST CTAs match Phase F/G).
- CTA availability remains 1:1 with the action catalog.

### WHAT it does NOT change

- Runway adapter, permit, Phase G rows.
- Gemini / ElevenLabs / Fal adapters (still fixture).
- Canon, Visual Authority, Timing V3, Performance Contract schema.
- Official film bytes / graph story WHAT.
- No paid Start.

### Frozen

- Phase G TEST video artifact and historical 4A artifact.
- EP01 `4ef164d6-…` / EP99 `77262ec0-…` media and graph fingerprints.
- CREATE_VIDEO paid one-shot rules.

### Must be proven

- Preview bytes SHA == VISIBLE artifact SHA after reload (TEST).
- Official film player does not adopt `acceptedTake.url` / `previewUrl` as CURRENT/VISIBLE.
- No CTA remap (`result.action === posted action`).
- Empty VISIBLE is empty — not “latest URL”.

### Must NOT be executed

- Real Runway / Veo / Wan / Gemini / ElevenLabs / Fal.
- Second TEST video Start.
- Graph rewrite of EP01/EP99.

### PASS

OS bindings are the only production authority for CURRENT / VISIBLE / ACCEPTED / preview on surfaces that execute OS actions. URL fields are display-non-authority or gone from those resolvers. Provider calls = 0.

### STOP

Any official-film graph mutation, any paid Start, any new provider adapter, any “temporary” URL fallback marked as current.

---

## 22. STOP CONDITIONS

Do not implement from this report.

Do not modify Runway, Veo, Gemini, ElevenLabs, Fal, Director layout, Canon, VA, Timing, Performance Contract.

Do not generate media.

The next architecture decision is: whether to run **FULL SHOT STATE + PREVIEW AUTHORITY CUTOVER** as specified above.

------------------------------------------------------------
STOP
------------------------------------------------------------

- provider calls = 0
- media generations = 0
- database writes = 0
- source changes = 0
- UI changes = 0
