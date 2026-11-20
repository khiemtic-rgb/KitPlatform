# FAMIXA FILM PRODUCTION SYSTEM — END-TO-END FORENSIC V1

Date: 2026-09-18  
Mode: READ-ONLY FORENSIC / NO NETWORK / NO GENERATION / NO PATCH

Official EP01: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`  
Official EP99: `77262ec0-74ad-459d-9128-f09bb553a5dd`  
TEST sandbox (evidence only): `aaaaaaaa-0000-4000-8000-000000000371` / `bbbbbbbb-0000-4000-8000-000000000371`

No source, database, Film, UI, or provider was modified.

---

## 0. PURPOSE

System-level audit of whether FAMIXA has **one** authoritative architecture from script to final film, with deterministic lineage from user intent to physical artifact.

This is not a bug list, UI pass, provider integration, or generation run.

---

## 1. EXISTING PROVEN CHECKPOINTS

Treated as evidence. Not re-implemented.

| Checkpoint | Verdict | Classification |
|---|---|---|
| Production OS Foundation | PASS | TESTED + LIVE VERIFIED (schema 368–371, FILM anchors) |
| Controlled TEST Harness | PASS | TESTED + LIVE VERIFIED |
| CREATE_VIDEO Action Contract | PASS | TESTED + LIVE VERIFIED |
| CREATE_VIDEO Fixture (3B) | PASS | TESTED + LIVE VERIFIED (unpaid TEST VIDEO_TAKE) |
| Runway Bridge Foundation | PASS | TESTED + LIVE VERIFIED (dry-run, no HTTP) |
| Checkpoint 4A | PASS WITH GAPS | LIVE VERIFIED: one real `runway`/`gen4_turbo` on TEST only |

4A known gaps (evidence, not this forensic’s work):

- live idempotent replay UNVERIFIED
- live process-restart recovery UNVERIFIED
- Runway ledger UNVERIFIED
- separate `/preview/media` GET after Execute UNVERIFIED

4A did **not** prove FILM, EP01, Voice, LipSync, Mix, Assembly, or UI cutover.

---

## 2. MASTER QUESTION

**If tomorrow FAMIXA receives a complete film script, can the system execute the entire film-production lifecycle from script to final film through ONE authoritative architecture, with deterministic lineage from user intent to final physical artifact?**

**No.**

Evidence: Production OS control plane exists and is real for CREATE_VIDEO (TEST) and CREATE_IMAGE (server only). The live film desk still executes picture, voice, lipsync, mix, accept, and assembly through a second authority: `SeriesPilotState` / `graph_json` / URLs / IndexedDB / legacy `/content/series/*`. Assembly does not consume accepted `artifactId`s. No `FILM READY` predicate exists. Paid real motion on FILM is not authorized. Therefore a complete script cannot travel script → final film on one lineage.

Classification: FACT (traced). Verdict in §27: **NOT READY**.

---

## 3. FULL PIPELINE — TRACE SUMMARY

| Stage | Exists as WHAT | Exists as OS execution | Live film path today |
|---|---|---|---|
| FILM | `series_build` + FILM scope anchor | Scope only | Graph + localStorage |
| STORY | `packDraft` / scenes / lines | Claim fields on CREATE_VIDEO only | Graph AUTHORITY |
| SCENE | `scenes[]` | None | Graph AUTHORITY |
| SHOT | `episode.shots[]` + shotCode | Scope identity | Graph AUTHORITY |
| CHARACTER / CANON | `FAMIXA_CHAR_CANON` + Character Studio Master | Contract input name only | Canon AUTHORITY |
| ACTING | Two stores: `shot.actingBeat` + `LinePerformance` | CREATE_VIDEO claim only | Split AUTHORITY |
| VISUAL DIRECTION | VisualSpec → Contract → prompt | CREATE_VIDEO claim only | Compiler AUTHORITY on guarded paths |
| KEYFRAME | IDB + `kfSourceHash` + OS KEYFRAME | OS RegisterKeyframe **implemented**; desk uses Gemini still | **Dual** |
| MOTION | OS CREATE_VIDEO + legacy turbo | OS on Director desk (fixture/TEST); staff/studio still turbo | **Dual** |
| VOICE | `voiceAssets[lineId]` + ElevenLabs + IDB | Contract only | Legacy AUTHORITY |
| LIPSYNC | Fal + `lipsyncUrl` | Contract only | Legacy AUTHORITY |
| MIX | Client FFmpeg assemble | Engine `MixExplicitAsync` no HTTP | Legacy AUTHORITY |
| EDITORIAL | `content-famixa-editorial-cut.ts` overlay | Binding role `EDITORIAL_SOURCE` unused | Derived / overlay |
| ASSEMBLY | Tables + `AssembleAsync` | No HTTP; no FINAL artifact | Legacy URLs |
| FINAL FILM | Client IDB final blob | Binding role `FINAL` unused | Legacy AUTHORITY |

Cross-cutting OS primitives (Request, Task, Snapshot, Attempt, Artifact, Binding, Event, Storage, Cost, Retry, Recovery) are **IMPLEMENTED** for the control plane and **LIVE VERIFIED** only on TEST CREATE_VIDEO / fixture KEYFRAME.

---

## 4. AUTHORITY AUDIT

| Object | AUTHORITY (intended OS) | AUTHORITY (actual live film) | SECONDARY | CACHE | LEGACY | Can non-authority execute? |
|---|---|---|---|---|---|---|
| Character Identity | Character Studio Master SHA + CHAR canon | Same + graph `characters[]` | DNA draft, PRP/CRP slots | `kit-famixa-canon` IDB | `canonFileName` | Yes — KF can use session data URL |
| Visual Universe | `FAMIXA_VISUAL_MODE` | Stamped on run at generate | Scene Master | — | Missing `visualMode` = legacy KF | Guarded ingress: no. Unguarded staff paths: UNVERIFIED |
| Story | `packDraft` / shot story | Same | `storyMemory` | localStorage | slim `graph_json` | Yes — CREATE_VIDEO can take claim text |
| Scene | `scenes[]` | Same | Scene Master | — | — | N/A (no OS scene task) |
| Shot | `shotId` + `shotCode` + buildId | Same | — | — | older EP01 builds | OS scopes by buildId+shotCode |
| Acting | **Split** — see §9 | `actingBeat` (visual) + locked `LinePerformance` (voice) | regex infer | — | — | Yes — two contracts, not one |
| Timing | Timing V3 layered clocks | Same | fingerprints | — | `shot.seconds` | Yes — provider 5/10 can leak into request |
| Provider / Model | Frozen snapshot | OS freeze on CREATE_VIDEO; elsewhere orchestration + turbo | catalog | — | `IVideoGenerationProvider` | Yes — staff turbo bypasses OS freeze |
| Keyframe | OS KEYFRAME artifactId | `pictureRevisionId` + `kfSourceHash` + IDB | `keyframeDataUrl` | `kit-famixa-kf` | disk canon jpg | **Yes** — Tạo hình never POSTs `/production/keyframes` |
| Motion | OS VIDEO_TAKE artifactId | Dual: OS VISIBLE **or** `takeUrl`/`runwayAttempts` | covering-take heuristic | `osPreviewByShot` | turbo / `previewUrl` | **Yes** |
| Voice | OS VOICE artifactId | `voiceAssets[lineId]` + physical IDB | session TTS blobs | session | lineId-as-id | **Yes** |
| LipSync | OS LIPSYNC artifactId | `lipsyncUrl` + `lipsyncTaskId` + frozen URL bind | playable blob | session Fal | `mot:{shotId}:{n}` | **Yes** |
| Mix | OS MIX artifactId | IDB final + `assembleFp` | editorial overlay | — | assemble HTTP URLs | **Yes** |
| Assembly | `production_film_assembly_shots.artifact_id` | Client timeline + `/series/assemble` URLs | PreviewCutPlan | — | latest playable | **Yes** |

`LegacySeriesGraphAdapter.IsAuthoritative() => false` is SOURCE declaration. Live film desk still **executes** those fields. Classification: GAP (declaration ≠ cutover).

---

## 5. STAGE-BY-STAGE FORENSIC

Legend: OS = Production OS. Desk = Director / Series graph.

### 5.1 STORY

| Field | Finding | Class |
|---|---|---|
| INPUT | Script / `packDraft` | FACT |
| INPUT AUTHORITY | Client graph | IMPLEMENTED |
| INPUT IDENTITY | buildId + episode | FACT |
| INPUT SNAPSHOT | Only when CREATE_VIDEO freezes story text | PARTIAL |
| TASK / ACTION | None | GAP |
| PROVIDER | None | FACT |
| OUTPUT ARTIFACT | None | GAP |
| BINDING | None | GAP |
| NEXT-STAGE INPUT | scenes / shots / acting | FACT |
| STATE | `scriptLocked` | COMPAT |
| SUCCESS | Parse produces production shots (`shotHasValidAction`) | IMPLEMENTED |
| FAILURE | Invalid action / empty beat | IMPLEMENTED |
| RETRY / RECOVERY | Re-parse | LEGACY |
| STALE / INVALIDATION | Full re-parse wipes all shot media | TESTED (client) |
| UI SOURCE | Story editor | FACT |
| STORAGE | localStorage + slim `graph_json` | FACT |
| LEGACY PATH | `series_pilot` PUT | FACT |
| CROSS-SHOT RISK | Episode switch wipes all `runs` | RISK |
| CROSS-PRODUCTION RISK | buildId namespaces IDB | IMPLEMENTED |
| Verdict | WHAT exists. Not an OS node. | C |

### 5.2 SCENE

| Field | Finding | Class |
|---|---|---|
| INPUT | Parsed scenes | FACT |
| AUTHORITY | `scenes[]` | IMPLEMENTED |
| IDENTITY | sceneId | FACT |
| SNAPSHOT / TASK / ARTIFACT | None | GAP |
| NEXT | Shots + scene dialogue + Scene Master | FACT |
| STALE | Scene Master overlay; not OS invalidated | GAP |
| CROSS-SHOT | Scene Master shared across shots in scene | RISK (continuity, not OS) |
| Verdict | Structural WHAT only. | C |

### 5.3 SHOT

| Field | Finding | Class |
|---|---|---|
| INPUT | Shot graph + `dialogueSegmentIds` | FACT |
| AUTHORITY | `episode.shots[]` | IMPLEMENTED |
| IDENTITY | shotId (client) / shotCode (OS) | FACT — **two identities** |
| SNAPSHOT | OS scope = buildId + shotCode | IMPLEMENTED |
| TASK | Per-action when OS used | PARTIAL |
| SUCCESS | Valid action + beat | IMPLEMENTED |
| CROSS-SHOT | OS forbids cross-scope artifact use | TESTED |
| CROSS-PRODUCTION | FILM vs TEST anchors | LIVE VERIFIED |
| Verdict | Identity split (shotId vs shotCode) is a lineage gap. | C |

### 5.4 CHARACTER / CANON

| Field | Finding | Class |
|---|---|---|
| INPUT | CHAR-00n roster + Master SHA | FACT |
| AUTHORITY | `FAMIXA_CHAR_CANON` + Character Studio lock | IMPLEMENTED |
| IDENTITY | characterId + Master SHA | FACT |
| SNAPSHOT | CREATE_IMAGE contract lists CharacterIdentity; RegisterKeyframe does **not** freeze canon SHA | GAP |
| TASK | None | GAP |
| OUTPUT | Not an OS artifact | GAP |
| NEXT | Visual contract / KF refs | FACT |
| STALE | Re-parse remaps roster; Master not Director-mutated | FACT |
| Verdict | Canon exists. Not bound into OS snapshot. | C |

### 5.5 ACTING

| Field | Finding | Class |
|---|---|---|
| INPUT | Director Acting Beat + line performance | FACT |
| AUTHORITY | **Two contracts** — see §9 | FACT |
| IDENTITY | shot.actingBeat vs lineId.performance | FACT |
| SNAPSHOT | CREATE_VIDEO freezes acting **string** only | PARTIAL |
| TASK | None | GAP |
| OUTPUT | Not an artifact | GAP |
| NEXT | Visual prompt + TTS settings | FACT |
| STALE | Beat save → `actingBeatPendingMotion`; line change → voiceFp | IMPLEMENTED (desk) |
| UI | Acting Beat panel + dialogue performance | FACT |
| Verdict | No shared Acting → Visual+Voice contract. | D |

### 5.6 VISUAL DIRECTION

| Field | Finding | Class |
|---|---|---|
| INPUT | Script action + canon + scene master | FACT |
| AUTHORITY | VisualSpec → VisualContract → prompt compilers | IMPLEMENTED |
| IDENTITY | Contract hash / fingerprints | FACT |
| SNAPSHOT | CREATE_VIDEO freezes visual **string** | PARTIAL |
| PROVIDER BYPASS | Guarded still/I2V ingress rejects raw prompt | TESTED |
| NEXT | KEYFRAME then MOTION | FACT |
| STALE | Stamp-at-generate; not retro-edited | FACT |
| Verdict | Intra-production compiler exists. Not OS-owned. | B |

### 5.7 KEYFRAME

| Field | Finding | Class |
|---|---|---|
| INPUT | Visual contract + canon refs | FACT |
| AUTHORITY (OS) | KEYFRAME artifact + CURRENT:KEYFRAME | IMPLEMENTED |
| AUTHORITY (desk) | `pictureRevisionId` + `kfSourceHash` + IDB | IMPLEMENTED |
| TASK | CREATE_IMAGE OS path exists | IMPLEMENTED |
| ACTION | Desk CTA calls `POST /content/series/still` | FACT |
| PROVIDER | Gemini (legacy still) | FACT |
| ATTEMPT | OS only if `/production/keyframes` | GAP on desk |
| OUTPUT | OS KEYFRAME **or** graph+IDB | DUAL |
| BINDING | OS CURRENT:KEYFRAME unused by Tạo hình | GAP |
| NEXT | CREATE_VIDEO requires OS verified KEYFRAME | TESTED |
| RETRY | Idempotent SHA key on OS register | IMPLEMENTED |
| STALE | New picture → motionNeedsRemake; OS InvalidateDownstream exists | DUAL |
| UI | Tạo hình | FACT |
| STORAGE | IDB / OS `os-test/.../kf` / film kf folder | FACT |
| LEGACY | Gemini still + data URL | AUTHORITY on desk |
| CROSS-SHOT | `kfInflightRef` global | RISK |
| Verdict | Server OS path unused by film CTA. | C |

### 5.8 MOTION

| Field | Finding | Class |
|---|---|---|
| INPUT | Story, acting, visual, timing, KEYFRAME artifact | FACT |
| AUTHORITY | OS CREATE_VIDEO on Director when `/actions` available | IMPLEMENTED |
| IDENTITY | VIDEO_TAKE artifactId | LIVE VERIFIED (TEST 4A) |
| SNAPSHOT | Frozen + provider/model freeze | LIVE VERIFIED (TEST) |
| TASK / ATTEMPT | Yes | LIVE VERIFIED (TEST) |
| PROVIDER | Default fixture; Runway HTTP TEST one-shot only | FACT |
| OUTPUT | VIDEO_TAKE | LIVE VERIFIED (TEST) |
| BINDING | CURRENT:VIDEO_TAKE + VISIBLE:VIDEO_TAKE | LIVE VERIFIED (TEST) |
| NEXT | LipSync / Mix (legacy still reads URLs) | GAP |
| RETRY | New attempt same task; no auto-retry on 4A | SOURCE VERIFIED |
| RECOVERY | RecoverFrozen + persist jobId | SOURCE VERIFIED; live restart UNVERIFIED |
| STALE | OS supersede + desk fingerprints | DUAL |
| UI | Tạo video → OS; staff/studio → turbo | FACT |
| LEGACY | `POST /series/turbo` still live | RISK |
| CROSS-SHOT | turboLockRef; OS scoped by shotCode | RISK / OK |
| Verdict | Strongest OS stage. FILM paid + staff bypass remain. | B |

### 5.9 VOICE

| Field | Finding | Class |
|---|---|---|
| INPUT | Line text + voiceId + LinePerformance | FACT |
| AUTHORITY | `voiceAssets[lineId]` + physical IDB | IMPLEMENTED (legacy) |
| IDENTITY | lineId; physical artifactId emerging | PARTIAL |
| SNAPSHOT | OS contract LineText+VoiceId — **no executor** | GAP |
| TASK / ATTEMPT | None | GAP |
| PROVIDER | ElevenLabs | FACT |
| OUTPUT ARTIFACT | OS VOICE role unused | GAP |
| BINDING | None | GAP |
| NEXT | LipSync wants VoiceArtifactId in contract; live uses duration + blobs | GAP |
| RETRY | Client regenerate | LEGACY |
| RECOVERY | Rehydrate IDB | LEGACY |
| STALE | voiceFp vs line/voice/emotion | IMPLEMENTED (desk) |
| UI | Nạp thoại / Tạo thoại | FACT |
| STORAGE | IDB `FAMIXA:voice:{buildId}:{artifactId}:{voiceId}` | FACT |
| LEGACY | Session TTS maps (lost on F5) | RISK |
| CROSS-SHOT | TTS keyed by lineId (shared lines) | RISK |
| Verdict | Not Production OS. | D |

### 5.10 LIPSYNC

| Field | Finding | Class |
|---|---|---|
| INPUT | Motion URL + voice + speaker pin | FACT |
| AUTHORITY | `lipsyncUrl` / Fal task | IMPLEMENTED (legacy) |
| IDENTITY | lipsyncTaskId; bind `mot:{shotId}:{n}` | WEAK |
| SNAPSHOT | Contract wants MotionArtifactId + VoiceArtifactId — **not executed** | GAP |
| TASK | None in OS | GAP |
| PROVIDER | Fal | FACT |
| OUTPUT | URL, not LIPSYNC artifact | GAP |
| BINDING | None in OS | GAP |
| NEXT | Mix resolver may pick covering take over lipsync | RISK |
| RETRY | Re-POST after dropDeadLipsync | LEGACY |
| RECOVERY | Poll turbo?taskId | LEGACY |
| STALE | Fingerprint + newer motion release | IMPLEMENTED (desk) |
| UI | Khớp môi → `POST /series/lipsync` | FACT |
| LEGACY PATH | Entire stage | AUTHORITY |
| CROSS-SHOT | turboLockRef shared with motion | RISK |
| Verdict | Not Production OS. | D |

### 5.11 MIX

| Field | Finding | Class |
|---|---|---|
| INPUT | Heuristic video URL + voice overlay | FACT |
| AUTHORITY | `assembleOneShot` + IDB final | IMPLEMENTED (legacy) |
| IDENTITY | assembleFp + URLs (`videoArtifactId: lipsyncUrl \|\| takeUrl`) | HEURISTIC |
| SNAPSHOT | Engine MixExplicitAsync freezes source artifactId — **no HTTP** | SOURCE ONLY |
| TASK | Engine-only | GAP |
| PROVIDER | Local FFmpeg | FACT |
| OUTPUT | IDB blob, not MIX artifact | GAP |
| BINDING | Not bound | GAP |
| NEXT | Editorial / episode assemble | FACT |
| STALE | mixFp vs assembleFp | IMPLEMENTED (desk) |
| UI | Hoàn thiện → `POST /series/assemble` | FACT |
| LEGACY | Forbidden by MIX contract (`latest take`, `acceptedTake.url`) — **still used** | GAP |
| Verdict | Contract forbids the live path. | D |

### 5.12 EDITORIAL

| Field | Finding | Class |
|---|---|---|
| INPUT | In/out overlay on lipsync or take URL | FACT |
| AUTHORITY | `editorialCut` — does not mutate artifacts | FACT |
| IDENTITY | Overlay only | DERIVED |
| TASK / ARTIFACT / BINDING | `EDITORIAL_SOURCE` unused | GAP |
| NEXT | Assembly trim | FACT |
| STALE | `EDITORIAL_SOURCE_GONE` if URL expires | RISK |
| Verdict | Overlay, not OS stage. | D |

### 5.13 ASSEMBLY

| Field | Finding | Class |
|---|---|---|
| INPUT | Caller-supplied (shotCode, artifactId, duration) in engine | SOURCE |
| LIVE INPUT | URLs + PreviewCutPlan | FACT |
| AUTHORITY | Engine AssembleAsync checks artifact scope | IMPLEMENTED (unused) |
| TASK | Not a registered ProductionAction | GAP |
| OUTPUT | FilmAssembly row + shot refs — **no rendered FINAL bytes** | GAP |
| EVENTS | ASSEMBLY_CREATED / ASSEMBLY_RENDERED — **no emitters** | GAP |
| ORDER | Integer order in table | IMPLEMENTED (schema) |
| MISSING / WRONG / DUPLICATE SHOT | CrossShotForbidden if artifact/shot mismatch; no film-ready completeness check | PARTIAL |
| RETRY / REBUILD | None | GAP |
| UI | Episode assemble / Hoàn thiện | LEGACY |
| Verdict | **NOT IMPLEMENTED** as Production OS film assembly. Types/tables are not a pipeline. | D |

### 5.14 FINAL FILM

| Field | Finding | Class |
|---|---|---|
| INPUT | Per-shot IDB finals + timeline | FACT |
| AUTHORITY | Client `CANONICAL_FINAL_RENDER = SERVER_FFMPEG` + IDB | LEGACY |
| IDENTITY | final blob + assembleFp | WEAK |
| TASK / SNAPSHOT / ATTEMPT | None | GAP |
| OUTPUT ARTIFACT | FINAL binding unused | GAP |
| BINDING | None | GAP |
| PREVIEW | Player chain (finalUrl > lipsyncUrl > takeUrl) | HEURISTIC |
| FILM READY | **Does not exist** | GAP |
| Verdict | No OS final film. | D |

---

## 6. EXACT DATA FLOW

### CREATE_IMAGE

```
UI "Tạo hình"
→ ShotProductionWorkspace action picture | os-create-image
→ confirmCreateShotImage → generateSceneKf
→ POST /content/series/still   (Gemini)
→ runs[shotId] + IndexedDB KF
```

OS path exists and is unused:

```
POST /content/production/keyframes
→ RegisterKeyframeAsync
→ Request/Task/Snapshot/Attempt/KEYFRAME/CURRENT:KEYFRAME
```

Authority today: **legacy still**. Classification: FACT.

### CREATE_VIDEO

```
Director "Tạo video" (only if GET /actions CREATE_VIDEO.available)
→ applyProductionOsActionAuthority → os-create-video
→ POST /content/production/requests
→ CreateVideoAsync
→ Request/Task/Snapshot/Attempt/adapter/VIDEO_TAKE/CURRENT+VISIBLE
→ GET preview/media → osPreviewByShot blob
```

Staff / StudioView:

```
Tạo video → startSceneTurbo → POST /content/series/turbo
```

TEST 4A:

```
prepare + AuthorizePaidNetwork → Execute → HttpRunwayTransport (one Start)
```

Authority: **OS on Director desk; legacy on staff/studio; paid Runway TEST-only**. FACT.

### CREATE_VOICE

```
Nạp thoại → ensureShotVoice → ElevenLabs / IDB hydrate
```

No Production OS HTTP. Authority: **client + ElevenLabs**. FACT.

### CREATE_LIPSYNC

```
Khớp môi → startLipsync → POST /content/series/lipsync → Fal
→ lipsyncUrl / lipsyncTaskId on graph
```

Contract requires MotionArtifactId + VoiceArtifactId. Live does not. Authority: **legacy Fal**. FACT.

### MIX_VIDEO

```
Hoàn thiện → assembleOneShot
→ resolveCurrentProductionVideoSource (URL heuristic)
→ POST /content/series/assemble
→ IDB final
```

Engine `MixExplicitAsync` is regression-only. Authority: **legacy assemble**. FACT.

### ACCEPT

```
Dùng video này → acceptExistingTake → acceptedTake { n, url, hashes }
```

OS `AcceptAsync` exists, no HTTP. Authority: **local graph**. FACT.

### BIND

Internal to OS CREATE_VIDEO success. No public BIND API. Desk does not write OS bindings for other stages. FACT.

### REGENERATE

CTA map `"Tạo lại video" → REGENERATE`. No `RegenerateAsync`. CREATE_VIDEO `RetryAttempt` = new Attempt same Task, not new Task. Desk remake may be stripped or no-op (`motion` return). Authority: **undefined / mixed**. GAP.

### PREVIEW

OS:

```
GET .../preview → VISIBLE → artifactId → storageKey
GET .../preview/media → bytes
```

Desk:

```
osPreviewUrl
→ execution.currentArtifacts.motion
→ resolveCurrentProductionVideoSource
→ finalUrl | lipsyncUrl | takeUrl | keyframeUrl
```

Authority: **OS when blob present; else legacy heuristic**. FACT.

### ASSEMBLY / FINAL EXPORT

```
Client timeline / assembleOneShot / POST /series/assemble
```

`CreateAssemblyAsync` unused by HTTP. FINAL role unused. Authority: **legacy**. NOT IMPLEMENTED as OS film export. FACT.

---

## 7. ARTIFACT LINEAGE

| Type | Parent identified by | Class | “latest” can substitute? |
|---|---|---|---|
| KEYFRAME | OS: none / import SHA. Desk: pictureRevisionId + kfSourceHash | STRONG (desk hash) / STRONG (OS if used) | Yes — session data URL / IDB latest for shot |
| VIDEO_TAKE | OS: keyframeArtifactId on snapshot. Desk: pictureRevisionId vs attempts | STRONG (OS TEST) / WEAK (desk) | Yes — resolveTakeUrl latest attempt / acceptedTake.url / previewUrl |
| VOICE | lineId + physical artifactId | WEAK → emerging STRONG | Yes — session blob for lineId |
| LIPSYNC | lipsyncTaskId + `mot:{shotId}:{n}` + URLs | WEAK / LEGACY | Yes — latest lipsyncUrl; covering take can beat it |
| MIX | assembleFp + URL string labeled videoArtifactId | HEURISTIC | **Yes — designed to** |
| FINAL | IDB proveFinalArtifact vs assembleFp | WEAK | Yes — player fallbacks |

CRITICAL paths where **latest output** substitutes for **required artifactId**:

1. `resolveCurrentProductionVideoSource` / `resolveTakeUrl` — FACT  
2. `assembleOneShot` `videoArtifactId: run.lipsyncUrl \|\| run.takeUrl` — FACT  
3. `acceptExistingTake` URL — FACT  
4. Preview chain `finalUrl > lipsyncUrl > takeUrl` — FACT  
5. Speech-floor covering take overriding bound lipsync — FACT  

OS CREATE_VIDEO / preview (TEST) do **not** use latest. LIVE VERIFIED on TEST only.

---

## 8. TASK / ATTEMPT AUDIT

| Action | Request | Task | Snapshot | Attempt | Artifact | Binding | Bypass |
|---|---|---|---|---|---|---|---|
| CREATE_IMAGE | Y | Y | Y | Y | Y | Y | Desk still bypasses |
| CREATE_VIDEO | Y | Y | Y | Y | Y | Y | Staff turbo bypasses |
| CREATE_VOICE | N | N | N | N | N | N | Entire stage |
| CREATE_LIPSYNC | N | N | N | N | N | N | Entire stage |
| MIX_VIDEO | engine only | engine | engine | engine | engine | N | Desk assemble |
| ACCEPT | N | N | N | N | N | engine only | Desk acceptedTake |
| BIND | internal | — | — | — | — | Y | Desk URLs |
| REGENERATE | N | N | N | N | N | N | Unwired |
| ASSEMBLY | N | N | N | N | refs only | N | Client assemble |
| FINAL | N | N | N | N | N | unused | IDB |

| Concern | CREATE_VIDEO (OS) | Other film actions |
|---|---|---|
| Idempotency | SOURCE + LIVE fixture; 4A live replay UNVERIFIED | UNVERIFIED / client-only |
| Retry | Explicit RetryAttempt; 4A no auto-retry | Client re-click |
| Provider/model freeze | LIVE VERIFIED TEST | UNVERIFIED |
| Input freeze | LIVE VERIFIED TEST | Fingerprints on desk only |
| Attempt identity | LIVE VERIFIED TEST | Fal/turbo task ids |
| Provider job identity | LIVE VERIFIED TEST (persist before poll) | turboTaskId / lipsyncTaskId |
| Late result | ApplyLateResultAsync ignores if CURRENT exists — SOURCE | LATE take excluded from duration; covering-take exception |
| Restart recovery | SOURCE; 4A live UNVERIFIED | UNVERIFIED |

Never promote source to live. 4A gaps remain UNVERIFIED.

---

## 9. ACTING + EMOTIONAL PERFORMANCE

Canonical places **today**:

| Concern | Where it lives | Drives visual? | Drives voice? |
|---|---|---|---|
| Character emotional state | LinePerformance.emotion (locked) + actingBeat emotion line | Via I2V compile | Via TTS voice_settings |
| Acting intention | `shot.actingBeat` | Yes | No (not automatically) |
| Emotion / intensity / energy | LinePerformance + derived defaults | Partial (prompt line) | Partial (style/speed) |
| Delivery / rhythm / pause / speed / pitch | LinePerformance → `actingTtsVoiceSettings` | No | Yes |
| Voice performance | ElevenLabs settings + line text | No | Yes |
| Visual performance | actingBeat before/during (action, gaze, prop, emotion) | Yes | No |

**Shared authoritative Acting contract that drives both Visual and Voice: NO.**

Exact gap: two stores (`ShotActingBeat` vs locked `LinePerformance`) are not one snapshot. CREATE_VIDEO freezes a single acting string; CREATE_VOICE is unimplemented; TTS ignores actingBeat; I2V ignores LinePerformance except where KF compile reads scene-first performance.

Do not invent Voice Emotion here. Classification: GAP.

---

## 10. VISUAL CONTINUITY

Intended chain:

```
Canon → Visual Authority / Mode → Visual Contract → Keyframe → Motion
```

| Link | Status | Class |
|---|---|---|
| Canon → Visual Authority | Character Studio Master + FAMIXA_VISUAL_MODE | IMPLEMENTED |
| Visual Authority → Visual Contract | compileVisualContract | IMPLEMENTED |
| Visual Contract → Keyframe | compileNarrativeStillPrompt; guarded ingress | TESTED |
| Keyframe → Motion | OS requires KEYFRAME artifact; desk uses hash / revision | DUAL |
| Provider bypass of compiler | Forbidden on Series still / I2V law paths | TESTED |
| Intra-production continuity | Fingerprints + pictureRevisionId + motionNeedsRemake | IMPLEMENTED (desk) |
| OS snapshot of visual contract | String freeze on CREATE_VIDEO only | PARTIAL |
| DNA | Explicitly DRAFT, not LOCK | FACT |

Invariant **intra-production continuity** is pursued on the desk compiler + fingerprints. It is **not** universally enforced by OS bindings. FILM 4A isolation proved Film pixels were not mutated by TEST — not that EP01 shots share one visual lineage.

Classification: B (architecture present, not the single runtime authority).

---

## 11. PROVIDER / MODEL ARCHITECTURE

Intended: Provider/Model = HOW. Story/Acting/Visual/Timing = WHAT.

| Capability | Registry / resolver | Adapter | Transport | Cost | Auth | Job | Poll / Recover | Output → Artifact |
|---|---|---|---|---|---|---|---|---|
| Picture | Gemini image provider | Legacy still service | Gemini HTTP | Catalog exists | Org settings | Legacy job | Client/server still | **Not OS** |
| Motion | ProductionMotionCatalog + Fixture + Runway resolver | FixtureMotionAdapter / RunwayMotionExecutionAdapter | DryRun / HttpRunwayTransport | Cost guard LIVE on 4A | Paid one-shot TEST | Persist before poll | Poll + Recover | **OS VIDEO_TAKE** |
| Voice | ElevenLabsVoiceProvider | Legacy | ElevenLabs HTTP | Catalog | Org | Client | — | **Not OS** |
| LipSync | FalLipSyncProvider | Legacy + famixa-lipsync-* | Fal HTTP | Catalog | Org | lipsyncTaskId | GET turbo?taskId | **Not OS** |

Vendor leak into Core:

- `ProductionTestMp4` / fixture encode is TEST HOW — acceptable.
- Runway duration 5/10 mapped in adapter — WHAT timing can be coerced to provider buckets. FACT.
- `KitVideoVideoStore.PersistNamed` error strings still say `VEO_TAKE`. FACT (naming leak, not authority).
- MIX contract forbids URL authority; live mix uses URLs. GAP.
- Default DI `IMotionExecutionAdapter` = Fixture. Real Runway is opt-in TEST. FACT.

No provider recommendation. No benchmark.

---

## 12. TIMING

Documented clocks (`famixa-shot-production-timing.ts`, DOCUMENT_ID `FAMIXA_SHOT_TIMING_CONTRACT_V3`):

| Clock | Authority order | Role |
|---|---|---|
| Performance duration | measured take → `shot.timing.performanceDurationSec` → provider 5\|10 | Motion / Fal length |
| Editorial / production | DIRECTOR if `timing.source=DIRECTOR` else performance | Assemble trim |
| Provider duration | success take → `shot.seconds` | I2V 5/10 |
| Dialogue floor | voice measured + holds | Floor only |
| Voice clock | `voiceAssets[lineId].duration` | Speech fit |
| Assemble clip | speech fit OR full Fal take | Preview cut |

**Multiple competing duration authorities: YES, by design on the desk.**

OS CREATE_VIDEO freezes one `DurationSec` / timing string. 4A used 5s → provider 5 / estimate 25.

Provider-specific leak into WHAT: I2V request duration is quantized to 5 or 10. FACT.

Voice is explicitly **not** performance authority (comment in timing module) but **is** a floor that can override preview/assemble via covering take. FACT.

---

## 13. STALE / INVALIDATION

### Production OS graph (source)

`ShotDependencyGraph` + `InvalidateDownstreamAsync`:

| Change | Unbinds typed CURRENT/VISIBLE of | Supersedes succeeded tasks |
|---|---|---|
| Story / Acting / Keyframe | Motion, LipSync, Mix (+ ACCEPTED) | CREATE_VIDEO, CREATE_LIPSYNC, MIX_VIDEO |
| Motion | LipSync, Mix (+ ACCEPTED) | CREATE_LIPSYNC, MIX_VIDEO |
| Voice | LipSync, Mix | CREATE_LIPSYNC, MIX_VIDEO |
| LipSync | Mix | MIX_VIDEO |

SOURCE VERIFIED. LIVE UNVERIFIED on FILM (downstream OS artifacts do not exist there).

### Desk fingerprints (live film)

| Trigger | KF | Motion | Voice | LipSync | Mix |
|---|---|---|---|---|---|
| Story re-parse | Wiped | Wiped | Lock cleared | Wiped | N/A |
| Acting beat save | — | pending remake | — | only after motion remake | — |
| Line text / voice | kfFp maybe | motionFp | voiceFp | lipsyncFp | mixFp |
| New KF approve | new revision | remake; may clear take/lipsync | — | cleared if remake | stale |
| Provider late | — | excluded from duration | — | bind may hold | covering take may win |

Do not assume OS invalidation runs when the desk changes story. Desk and OS stale systems are **not the same runtime**. GAP.

---

## 14. CROSS-SHOT ISOLATION

| Mechanism | Class | Risk |
|---|---|---|
| OS buildId + shotCode + CROSS_SHOT / CROSS_SCOPE | AUTHORITY | Prevents SH01 artifact on SH02 **when OS is used** |
| `osActionsByShot` / `osPreviewByShot` | CACHE | Keyed by shotId — OK |
| localStorage `runs[shotId]` | AUTHORITY (desk) | Per-shot writes OK |
| `turboLockRef` | RISK | SH01 lipsync blocks SH02 turbo |
| `kfInflightRef` | RISK | Global single KF |
| `ttsBlobs` by lineId | CACHE / RISK | Shared dialogue lines |
| `lipsyncBusyId` / `turboBusy` | RISK | UI shot ≠ busy shot |
| Staff `videoApproved` from any URL | LEGACY | Success without OS binding |
| TEST vs FILM anchors | AUTHORITY | 4A FILM isolation LIVE VERIFIED |

SH01 request → SH02 **OS artifact**: no path found in OS write APIs.  
SH01 request → SH02 **desk media**: no `patchRun` cross-id found.  
Production A → Production B: OS scope guards TESTED; desk IDB keys include buildId.

Operational cross-shot hazard is **shared locks**, not silent artifact rebinding.

---

## 15. UI / BACKEND CONTRACT

| CTA | UI believes | Backend actually | 1:1? |
|---|---|---|---|
| Tạo hình | CREATE_IMAGE | `POST /series/still` (Gemini) | **No** — OS keyframe unused |
| Tạo video (Director, OS available) | CREATE_VIDEO | `POST /production/requests` | **Yes** on that surface |
| Tạo video (staff / StudioView) | Create video | `POST /series/turbo` | **No** |
| Tạo video (desk `motion` if unmapped) | Create video | **no-op** | **No** |
| Nạp thoại | CREATE_VOICE | ElevenLabs / IDB | **No OS** |
| Khớp môi | CREATE_LIPSYNC | `POST /series/lipsync` | **No OS** |
| Hoàn thiện | MIX_VIDEO | `POST /series/assemble` | **No OS** |
| Dùng video này | ACCEPT | local `acceptedTake` | **No OS** |
| Tạo lại video | REGENERATE | Unwired / retry overlap | **No** |

UI can show success / preview / current / accepted **without** OS binding:

- `osPreviewByShot` after OS video while `runs[]` has no artifactId — FACT  
- `videoApproved` from `lipsyncUrl \|\| previewUrl \|\| takeUrl` — FACT  
- `acceptExistingTake` — FACT  
- `previewUrl` fallback in `resolveTakeUrl` — RISK  

---

## 16. PREVIEW AUTHORITY

Expected: VISIBLE → artifactId → physical storage.

| Path | Rule | Class |
|---|---|---|
| OS GET preview | VISIBLE only; empty = VISIBLE_MISSING | IMPLEMENTED + TESTED + LIVE (TEST) |
| Director hero | `osPreviewUrl` first | CACHE override |
| Then | execution CURRENT motion | DERIVED |
| Then | `resolveCurrentProductionVideoSource` | HEURISTIC / LEGACY |
| Then | finalUrl > lipsyncUrl > takeUrl > keyframe | LEGACY |
| `previewUrl` | Documented display-only; still in take resolver chain | RISK |

Exceptions that can become authoritative on the desk: blob URL, Fal playable, covering take, `acceptedTake.url`, `takeUrl`. FACT.

---

## 17. ASSEMBLY / FINAL FILM

**Is Assembly Production OS controlled?** No.

| Concern | Engine / schema | Live desk |
|---|---|---|
| Consumes accepted artifactIds | AssembleAsync requires artifactId + shotCode + same build | **URLs + heuristic** |
| Shot order | Integer | PreviewCutPlan |
| Accepted shot | ACCEPTED binding unused by assemble | `acceptedTake` optional influence |
| Stale / missing / wrong / duplicate | CrossShotForbidden only | Fingerprints / operator |
| Artifact verification | Looks up artifact row | proveFinalArtifact on IDB |
| Final output storage | None (no render) | IDB + `/series/assemble` |
| Final artifact / binding / preview | FINAL unused | Player heuristics |
| Rebuild / retry / resume | None | Re-click assemble |

Types and tables exist. **Do not treat that as a pipeline.** NOT IMPLEMENTED.

---

## 18. FINAL FILM DEFINITION OF DONE

Required expression:

every shot valid · required artifacts verified · dependencies satisfied · no stale · no unresolved task · no orphan · no cross-shot/production contamination · accepted bindings valid · assembly order valid · final artifact verified + bound · final preview valid

**This predicate does not exist** in code (no `FILM_READY` / equivalent). GAP.

Closest fragments: shot node state, desk fingerprints, AssembleAsync row insert. None compose a film-ready check.

---

## 19. ERROR TAXONOMY

OS typed reasons exist (`ProductionFailureReasons`, 47 constants). Execution uses a subset. Missing / unused vs requested list:

| Requested | OS today |
|---|---|
| INPUT_INVALID | Split into MISSING_* / FAILED_IDENTITY / INPUT_MUTATED |
| INPUT_CHANGED | **Retired** — never persist |
| PROVIDER_CHANGED / MODEL_CHANGED | Implemented |
| PROVIDER_UNAVAILABLE / COST_UNKNOWN / OVER_BUDGET | Implemented |
| PROVIDER_FAILED | Event + attempt status; umbrella message possible |
| PROVIDER_TIMEOUT | **No dedicated code** — poll loop → PROVIDER_UNAVAILABLE | GAP |
| ARTIFACT_INVALID / MISSING | FAILED_VERIFICATION / FAILED_IDENTITY / VISIBLE_MISSING |
| ARTIFACT_STALE | STALE_TASK / node STALE; no ARTIFACT_STALE | GAP |
| BINDING_INVALID | BINDING_CHANGED (graph); no BINDING_INVALID | PARTIAL |
| LINEAGE_INVALID | Cross-shot / snapshot mismatch — not named LINEAGE_INVALID | PARTIAL |
| CROSS_SCOPE / CROSS_SHOT | Implemented |
| DUPLICATE_EXECUTION | IN_FLIGHT_REPLAY | PARTIAL |
| RECOVERY_REQUIRED | **No** | GAP |

Umbrella risks: `MISSING_DEPENDENCY`, `PROVIDER_UNAVAILABLE`, `FAILED_IDENTITY` hide distinct causes. Client `ORCH_GATE.*` is a second taxonomy, unmapped. FACT.

---

## 20. REPORT DISCIPLINE

Every section above is tagged. Nothing is PASS merely because a type exists. Nothing is LIVE VERIFIED except prior checkpoints and this read of current source. FILM runtime of Voice/LipSync/Mix/Assembly through OS is **UNVERIFIED** because those paths are not implemented.

---

## 21. MASTER PIPELINE MATRIX

| Stage | Authority | Input | Task | Snapshot | Provider | Output Artifact | Binding | Next Stage | State | Retry | Recovery | Stale | Legacy | Verdict |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| Story | Graph | Script | N | N | — | N | N | Scene/Shot | lock | re-parse | — | wipe media | series_pilot | C |
| Scene | Graph | Story | N | N | — | N | N | Shot | Scene Master | — | — | overlay | — | C |
| Shot | Graph + OS scope | Scene | Partial | Scope only | — | N | N | Canon/Acting | shotCode | — | — | dual id | shotId | C |
| Canon | Character Studio | Roster | N | N | — | N | N | Visual | Master SHA | — | — | remap | DNA draft | C |
| Acting | Split desk | Shot/line | N | string on video | — | N | N | Visual+Voice | two stores | — | — | pending motion | infer | D |
| Visual | Compiler | Canon+script | N | string on video | HOW only | N | N | Keyframe | stamp | regen | — | fingerprints | missing mode | B |
| Keyframe | Dual | Visual | OS unused by CTA | OS import | Gemini | OS or IDB | OS unused | Motion | revision/hash | SHA idem | — | remake | still API | C |
| Motion | OS desk / turbo staff | KF+WHAT | Y (OS) | Y (OS) | Fixture/Runway/turbo | VIDEO_TAKE or URL | OS or none | Voice/Lip | CURRENT/VISIBLE | RetryAttempt | Recover SOURCE | dual | turbo | B |
| Voice | Legacy | Line | N | N | ElevenLabs | N | N | LipSync | voiceAssets | re-TTS | IDB | voiceFp | session | D |
| LipSync | Legacy | URLs | N | N | Fal | N | N | Mix | lipsyncUrl | re-POST | poll | lipsyncFp | Fal | D |
| Mix | Legacy | URLs | engine only | engine only | FFmpeg | N | N | Editorial | assembleFp | re-assemble | — | mixFp | assemble API | D |
| Editorial | Overlay | URLs | N | N | — | N | unused | Assembly | trim | — | — | URL gone | — | D |
| Assembly | Schema only | artifactIds unused | N | N | — | refs | N | Final | — | N | N | N | client | D |
| Final | Legacy IDB | blobs | N | N | FFmpeg | N | unused | — | — | N | N | N | player | D |

A/B/C/D defined in §23.

---

## 22. MASTER DEPENDENCY GRAPH

```
WHAT (authoritative content — today: SeriesPilotState / graph)
  FILM (series_build / FILM scope)
    SCENE
      SHOT
        STORY ──────────────┐
        ACTING (beat) ──────┼─→ VISUAL CONTRACT ─→ KEYFRAME ─→ MOTION ─┐
        ACTING (line perf) ─┴─→ VOICE ─────────────────────────────────┤
                                                                       └─→ LIPSYNC ─→ MIX ─→ ACCEPT ─→ ASSEMBLY ─→ FINAL
HOW (provider/model) ────────── Gemini / Runway|Veo / ElevenLabs / Fal / FFmpeg

OS CONTROL PLANE (authoritative execution — today: CREATE_VIDEO TEST + CREATE_IMAGE server)
  Request → Task → InputSnapshot → Attempt → Artifact → Binding
                    ↑ WHAT freeze     ↑ HOW freeze

DERIVED: editorial overlay, preview caption, fingerprints
CACHE: IDB KF/voice/final, osPreviewByShot, session TTS/Fal
LEGACY AUTHORITY (still executes): takeUrl, previewUrl, acceptedTake.url,
  videoApproved, runwayAttempts, lipsyncUrl, /series/still|turbo|lipsync|assemble
```

WHAT vs HOW is clear in OS CREATE_VIDEO.  
AUTHORITY vs DERIVED vs CACHE is **not** clear on the live film desk: cache and legacy still execute.

---

## 23. PRODUCTION READINESS MATRIX

Classification only. Not a score.

| Stage | Class | Meaning |
|---|---|---|
| Story | C | Partial / legacy mixed |
| Scene | C | Partial / legacy mixed |
| Shot | C | Partial / identity split |
| Canon | C | Partial / not OS-bound |
| Acting | D | Missing shared contract / unsafe split |
| Visual | B | Architecture implemented, runtime not OS-owned |
| Keyframe | C | OS exists; film CTA unused |
| Motion | B | OS proven on TEST; FILM/staff incomplete |
| Voice | D | Missing / unsafe for OS lineage |
| LipSync | D | Missing / unsafe for OS lineage |
| Mix | D | Missing / unsafe (URL authority) |
| Editorial | D | Missing as OS stage |
| Assembly | D | Missing / unsafe |
| Final | D | Missing / unsafe |

---

## 24. ROOT CAUSES

### ROOT CAUSE A — Multiple execution authorities

Production OS declares itself the only orchestrator. Live film still executes through Series graph + `/content/series/*` + IndexedDB. `LegacySeriesGraphAdapter` is a comment, not a cutover.

Evidence: §4, §6, §15.

### ROOT CAUSE B — Missing shared Acting / Performance contract

Visual performance and voice performance are two stores. No single snapshot drives both. Emotion/timing/delivery cannot be frozen once for the shot.

Evidence: §9, stage Acting.

### ROOT CAUSE C — Artifact lineage is not universal

Only KEYFRAME (server) and VIDEO_TAKE (TEST/Director OS) use artifactId. Voice, LipSync, Mix, Accept, Assembly, Final identify parents by URL, lineId, latest, or `mot:{shotId}:{n}`.

Evidence: §7, §17.

### ROOT CAUSE D — Assembly is not Production OS controlled

`AssembleAsync` is unused. No FINAL artifact. No FILM READY. Desk assemble consumes heuristics the MIX contract forbids.

Evidence: §17, §18.

### ROOT CAUSE E — UI / backend action mismatch

CTA labels map to OS actions in a dictionary, then handlers call different endpoints — or no-op. Staff “Tạo video” bypasses OS.

Evidence: §15.

---

## 25. MASTER REMEDIATION PLAN

Do not implement here. Minimum architectural phases (not one phase per bug):

### Phase 1 — Production OS Complete Action Authority V1

- **Objective:** Every registered shot action (CREATE_IMAGE, CREATE_VOICE, CREATE_LIPSYNC, MIX_VIDEO, ACCEPT) plus ASSEMBLY executes only through Request → Task → Snapshot → Attempt → Artifact → Binding, with parent = artifactId.
- **Dependencies:** Existing OS foundation, Action Contracts, CREATE_VIDEO TEST/4A (preserve).
- **Affected:** Voice, LipSync, Mix, Accept, Assembly, Keyframe cutover (server already exists).
- **Preserve:** 4A TEST Runway path, paid gate, FILM isolation, fixture suites, Action Contract CREATE_VIDEO.
- **Required:** Executors + HTTP for remaining actions; Mix/Assembly refuse URL authority; no live providers in this phase (fixture/import adapters).
- **New contracts:** Voice/LipSync/Mix snapshots naming parent artifactIds; FILM READY predicate skeleton (definition only).
- **Migration:** None of EP01/EP99/graph/Canon.
- **Tests:** Source regressions that prove bypass of `/series/still|turbo|lipsync|assemble` is impossible on the OS executors.
- **Live verification:** TEST-scope unpaid fixtures only. No Film run. No spend.
- **Exit:** No registered action can complete without an OS artifactId parent chain.

### Phase 2 — Shared Acting / Performance Contract V1

- **Objective:** One frozen Acting snapshot drives visual and voice WHAT.
- **Dependencies:** Phase 1 snapshots.
- **Preserve:** Timing V3 layering; no Voice Emotion product work beyond the contract.
- **Exit:** CREATE_VIDEO and CREATE_VOICE read the same Acting snapshot hash.

### Phase 3 — FILM READY + Assembly Render V1

- **Objective:** Assembly consumes ACCEPTED artifactIds; emits verified FINAL; FILM READY is evaluable.
- **Dependencies:** Phase 1.
- **Preserve:** Editorial as overlay, not authority.
- **Exit:** FILM READY true iff every required artifact verified, no stale, order valid, FINAL bound.

### Phase 4 — FILM scope paid-motion authorization (later, operator-named)

- Not this forensic’s next step. Do not run a real film.

UI cutover is a **later** phase and is not recommended as next.

---

## 26. DO NOT TOUCH

Observed: EP01, EP99, Canon, Visual Authority, `series_build`, `graph_json`, Film artifacts/bindings were not modified. No UI, adapter, or provider call in this forensic.

---

## 27. FINAL VERDICT

**NOT READY**

FAMIXA cannot reliably execute a complete film end-to-end through **one** authoritative Production OS.

Evidence:

1. CREATE_VIDEO is the only film-facing OS executor; even that is fixture on the desk and paid-Runway on TEST only (4A).
2. CREATE_IMAGE OS exists and the film CTA does not use it.
3. Voice, LipSync, Mix, Accept, Assembly, Final execute on graph/URL/IDB/legacy HTTP.
4. Assembly does not consume accepted artifactIds and does not produce a FINAL artifact.
5. FILM READY does not exist.
6. Acting cannot freeze one contract for visual and voice.
7. Preview and accept can succeed without OS bindings.
8. Staff/studio “Tạo video” still calls turbo.

4A proves the OS **can** own one real motion artifact on TEST. It does not prove a film.

---

## 28. EXACT NEXT STEP

**Production OS Complete Action Authority V1**

Make CREATE_IMAGE (already implemented), CREATE_VOICE, CREATE_LIPSYNC, MIX_VIDEO, ACCEPT, and ASSEMBLY execute only through the existing Production OS control plane, with parent identity = artifactId, using unpaid fixture/import adapters.

Do not run a film.  
Do not call Gemini / Runway / Veo / Fal / ElevenLabs.  
Do not modify EP01 / EP99 / Canon / UI.  
Do not benchmark providers.  
Do not micro-fix 4A gaps.

That phase removes the largest blocker: **two execution authorities**. Until it lands, a complete script cannot have deterministic lineage to a final film.

---

## 29. FINAL RULE

READ. TRACE. COMPARE. CLASSIFY.

No patch. No generation. No spend. No Film mutation.

END.
