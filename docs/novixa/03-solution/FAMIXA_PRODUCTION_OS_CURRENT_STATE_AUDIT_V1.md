# FAMIXA PRODUCTION OS
# CURRENT STATE AUDIT V1

Date: 2026-09-23  
Mode: READ-ONLY FORENSIC AUDIT  
Safety: no code, no migration, no seed, no provider call, no Start, no Poll, no snapshot/permit/artifact/graph mutation.

Every claim is classified: **[PROVEN]** / **[PARTIAL]** / **[PLANNED]** / **[MISSING]** / **[UNKNOWN]**.  
If a file/class/table/test was not found: **NO EVIDENCE FOUND**.

---

## 1. Executive Summary

Famixa is not “an app that calls Veo.” It is also not yet a complete Video Production OS.

What exists today is a **real Production OS kernel** (Request → Task → immutable InputSnapshot → Attempt → Artifact → Binding → Events) plus a **second, older Series/graph/IDB plane** that still holds some Director-visible state.

On official film `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`:

| Shot | What is production-proven on Production OS | What is not |
|---|---|---|
| EP01-SC01-SH02 | CREATE_VIDEO google/veo-3.1 through freeze → permit → real Vertex Start → Poll → verified VIDEO_TAKE → **ACCEPTED:VIDEO_TAKE** (CK18–CK20) | Voice, LipSync, Mix, Final |
| EP01-SC01-SH01 | CREATE_VOICE ElevenLabs once; Fal LipSync recovered into OS; FFmpeg MIX + ASSEMBLE remux | Character-consistent multi-shot episode; clean Director-only path; SH01 motion is **not** the CK18–CK20 VIDEO_TAKE |

**CK20 is already complete.** It does not need to be redone. It must not be expanded into Voice/LipSync/Mix.

The largest gap to an AI-native Video Production OS is not another motion checkpoint. It is a **single official-shot vertical slice after VIDEO_TAKE** (Voice → LipSync → Mix → Accept) that consumes the CK20 artifact by identity, plus **one character-identity authority** that actually reaches the motion/voice providers without the legacy graph/IDB plane.

---

## 2. Architecture Map

### Layers found

| # | Layer | Exists? | Where | Completeness | Provider-coupled? | Production evidence |
|---|---|---|---|---|---|---|
| 1 | Creative domain (Story/Acting/Visual) | [PROVEN] | `ProductionEp01Sh0{1,2}IngressCanon.cs`, `content-famixa-series.ts`, `PerformanceContractEntities.cs` | Official ingress for SC01 SH01/SH02 only | No (strings + hashes) | Official freeze hashes on SH02 snapshot `fde661bb-…` |
| 2 | Character system | [PROVEN] stack / [PARTIAL] OS consumption | `famixa_character` (mig 330), `video_master_reference`, `video_character_dna`, `CharacterIdentityGovernanceRules.cs`, Character Studio | CHAR-001 canon + governance proven; OS Veo start does not send master SHA as structured identity | Image path talks Gemini; OS motion talks compiled prompt + keyframe bytes | CHAR-001 locked in SH01/SH02 VisualContract JSON |
| 3 | Story / Scene / Shot | [PARTIAL] | `series_build.graph_json` (315); `ProductionScope`; `video_production_shot` (356); `video_production_shot_contract` (358) | Two shot models; OS uses Build+ShotCode, not shot UUID as authority | No | Official build `4ef164d6-…` |
| 4 | Director workspace | [PARTIAL] | `ContentFamixaShotProduction/*`, `production-os-action-authority.ts`, `ContentProductionController` | CTAs remapped to OS; leftovers + engineer checkpoints remain | Labels hide Vertex; official CK path did not | UI cutover report 2026-09-18 |
| 5 | Media contracts | [PROVEN] | `ProductionTaskInputSnapshot`, `ProductionActions`, `ProductionArtifactRoles`, `ShotDependencyGraph` | Generic roles IMAGE/VIDEO_TAKE/VOICE/LIPSYNC/MIX/ASSEMBLY | Snapshot stores ProviderId/ModelId, not Veo DTOs | Schema 368 |
| 6 | Execution engine | [PROVEN] | `ProductionWorkExecutionEngine.cs`, `ProductionExecutionService.cs` | Freeze, attempt, verify, bind, accept | HOW adapters injected | CK18–CK20 + TEST fixture vertical |
| 7 | Provider adapter layer | [PARTIAL] | `IMotionExecutionAdapter`, `IVoiceExecutionAdapter`, `ILipSyncExecutionAdapter`, `IMixExecutionAdapter`; **separate** `IFamixaMotionProvider` / `IProductionProvider` | Three stacks; live official motion = Veo transport | Adapter layer is provider-specific; domain snapshot is not | Veo official SH02; Runway TEST/4A; ElevenLabs SH01; Fal SH01 recover |
| 8 | Artifact system | [PROVEN] | `production_artifacts`, `IProductionArtifactFileStore`, `DirectoryProductionArtifactFileStore` | Identity + SHA + role + parents | Storage folders (`os-veo-out`) are HOW | SH02 VIDEO_TAKE `b44e530b-…` |
| 9 | Provenance / audit | [PROVEN] | `production_task_events`, event types in `ProductionWorkIds.cs` | Full for OS tasks; incomplete across planes | Payloads carry provider/model, not secrets | CK19/CK20 events |
| 10 | Queue / job / retry / recovery | [PARTIAL] | Attempt + ledger (`OfficialVeoStartLedger`) + Poll/Recover on adapters | Veo Start ledger + Poll recover proven; no general job queue | Veo ledger is Veo-specific | CK18 ledger; CK19 recover; Fal recover report |
| 11 | Storage | [PROVEN] | Postgres `pack_content.*`; `App_Data/kit-video-i2v/` | Durable bytes + DB rows | Folder names leak HOW | Physical SH02 mp4 unchanged after CK20 |
| 12 | UI | [PARTIAL] | Admin Vite Famixa studio | Director desk exists; two authority planes | Some surfaces still mention takes/Fal | UI cutover + later forensics |
| 13 | Configuration | [PROVEN] | Veo Join/HTTP composition, cost policy, selections table | Official VIDEO selection EXPLICIT | Vertex project/location in Veo HOW | Selection `c52b646f-…` |
| 14 | Testing | [PROVEN] | CK5–CK20 21/21; many fixture verticals; smokes | Strong offline; live gated by env | Official live is Veo-shaped | 2026-09-23 suite |
| 15 | Episode orchestration | [PARTIAL] | `production_film_assemblies` + `ASSEMBLE` | One-shot remux proven; multi-shot concat **NO EVIDENCE FOUND** as real episode master | FFmpeg HOW | SH01 assemble report; TEST fixture assembly |

### Dual plane (critical)

| Plane | Authority | Status |
|---|---|---|
| Production OS | `production_*` tables, artifact GUID, SHA, bindings | [PROVEN] kernel |
| Legacy Series | `series_build.graph_json`, `mot:shot:n`, IDB `kit-famixa-tts`, Fal FileId, `ShotProductionFinalStore` | [PROVEN] still present; classified LEGACY in cutover reports |

Do not treat “graph has a take” as “OS has an authoritative VIDEO_TAKE.”

---

## 3. Capability Matrix

| DOMAIN | PROVEN | PARTIAL | PLANNED | MISSING |
|---|---|---|---|---|
| Creative | Official SH01/SH02 Story/ActingBeat/VisualContract freeze | SC02/SH03 OS ingress | — | OS Story table |
| Character | CHAR-001 canon, master/DNA/PRP, governance, age | Continuity across official shots as pixels | World types in `ProductionOsRules` | Character-conditioned provider API; CHAR-002+ OS film |
| World | — | `WorldLocationReference` types only | World in `ProductionOsRules.cs` | Wired world → snapshot |
| Shot | Scope + contract + snapshot + dependency graph | Two shot IDs (OS vs `video_production_shot`) | — | Unified shot identity |
| Image | OS import/fixture keyframe; Kit Video Gemini stills | Two image pipelines | — | OS paid image adapter on official film |
| Motion | Veo official SH02 CK18–CK20; Runway TEST/4A | Duration map 10s→8s Veo-specific | Wan catalog/legacy Fal | Kling OS adapter; multi-provider official SH02 |
| Voice | ElevenLabs official SH01; fixture TEST | SH02 voice | — | SH02 official CREATE_VOICE |
| LipSync | Fal recover official SH01; fixture TEST | Quality/speaker; dual-plane preview | — | SH02 LipSync from CK20 VIDEO_TAKE |
| Mix | FFmpeg remux official SH01; fixture TEST | No music/SFX/levels | — | Timeline mix; SH02 mix |
| Final | Binding `FINAL`; one-shot ASSEMBLY remux | “Final = MIX bytes” | — | Episode master concat |
| Artifact | Roles + SHA + verify + parents | Cross-plane IDs | — | Single identity across graph/OS |
| Provenance | OS events for official tasks | FINAL→CHARACTER walk incomplete | — | Full episode provenance |
| Provider abstraction | `I*ExecutionAdapter` + frozen ProviderId/ModelId | Three provider stacks | Router catalog Wan | One adapter registry |
| Execution | Freeze/permit/attempt/verify/bind | Official path Veo-specialized | — | Generic live host (not OfficialVeo*) |
| Recovery | Veo poll recover; Fal job recover; replay | Process-kill not always live-proven | — | Universal recover host |
| Director UX | OS CTAs mapped | Engineer CK path; leftover graph | — | Director-complete official film |
| Episode orchestration | 1-shot assemble | TEST 1-shot fixture film | — | Multi-shot EP01 |
| Future AI readiness | Generic snapshot + adapters | I2V-shaped fields | First/last, V2V types | Those capabilities in snapshot |

---

## 4. Creative System

### Domain models [PROVEN]

- `ProductionScope` — `ProductionEntities.cs` (`BuildId`, `EpisodeCode`, `SceneCode`, `ShotCode`)
- `ProductionTaskInputSnapshot` — Story, ActingBeat, VisualContract, ShotContract, hashes, DurationSec, KeyframeArtifactId
- `ProductionEp01Sh01IngressCanon` / `ProductionEp01Sh02IngressCanon`
- Client `FamixaSeriesShot` — `content-famixa-series.ts`
- Performance: `PerformanceSnapshot`, `PerformanceLine`, `CharacterInShot` — `PerformanceContractEntities.cs`

### Database [PROVEN]

- `pack_content.series_build.graph_json` (mig 315) — legacy graph
- `pack_content.video_production_shot` (356) + immutable `video_production_shot_contract` (358)
- `pack_content.production_task_input_snapshots` + `trg_prod_snapshot_immutable` (368)
- `pack_content.production_scope_anchor` (370)

### API [PROVEN]

- `ContentProductionController` — `api/content/production` (requests, shot state, keyframes, actions)
- Legacy `ContentController` `/api/production/shots/{guid}/contract`

### UI [PROVEN]

- `ContentFamixaShotProduction/ShotProductionWorkspace.tsx`
- `ContentFamixaSeriesTab.tsx` (legacy still refused for OS CTAs)

### Versioning / freeze [PROVEN]

- Snapshot immutability trigger
- `OfficialCreateVideoFreezeV1` — idempotent freeze; drift = `FREEZE_MISMATCH`
- Shot contract trigger `trg_video_prod_shot_contract_immutable`

### Regenerate without changing canonical intent [PROVEN for OS motion]

New Task/Attempt allowed; snapshot hash must rematch. Official SH02 snapshot `fde661bb-…` / hash `56f8259e…` was not mutated by CK18–CK20.

### Story → Scene → Shot dependency [PARTIAL]

- Client graph: scene beats → shot `beatId` [PROVEN]
- OS: per-shot Story/Acting/Visual only; **no Story aggregate table** [MISSING]
- SH03 / SC02 Production OS canon ingress: **NO EVIDENCE FOUND** in backend canon classes

### Provenance [PROVEN] on OS snapshots via hashes + ingress JSON; [PARTIAL] vs series graph.

---

## 5. Character System

Famixa has a **character canon**, not only a reference image. It does **not** yet have a character-conditioned motion contract that providers must honor as structured identity.

| Item | Status | Evidence |
|---|---|---|
| Character entity | [PROVEN] | `famixa_character` + `famixa_character_version` (330); seed CHAR-001… |
| Character ID | [PROVEN] | `CharacterIdentityGovernanceRules.NormalizeCharacterId` → `CHAR-\d{3}` |
| Version / era | [PROVEN] | `famixa_character_version.era`, `is_current_canon` |
| Master reference | [PROVEN] | `video_master_reference` (352/344); `KitVideoCharacterDnaRules.MasterCode` |
| Table `video_character_master` | [MISSING] | NO EVIDENCE FOUND — use `video_master_reference` |
| Face / hair / body / wardrobe | [PROVEN] | `canon_json.visualDna` / `wardrobe` (330); CRP `WARDROBE_REFERENCE` |
| Age | [PROVEN] | `CharacterAgeConsistencyV1Rules`, `CharacterAgeGateV1Rules` |
| Voice identity | [PARTIAL] | `canon_json.voiceDna`; OS voice uses frozen voiceId on SH01, not full voice bible |
| Personality / behavior | [PROVEN] in canon | 330 `personalityDna` / `behaviorDna` |
| Acting style | [PARTIAL] | Shot ActingBeat, not a character acting-style entity |
| Expression / pose / negative | [PROVEN] at shot/DNA | `ProductionShotContractRules.ShotCharacterFields`; `KitVideoCharacterDnaRules.ForbiddenVariation` |
| Reference assets (CRP) | [PROVEN] | FRONT / THREE_QUARTER / SIDE / FULL_BODY |
| Consistency rules | [PROVEN] | 330 `continuityRules`; governance `CONTINUITY_GATE` |
| Character → shot binding | [PROVEN] | VisualContract JSON `primary`/`secondary` id; `video_production_shot.character_id` |
| Character → provider translation | [PARTIAL] | Image: prompt compiler + Gemini. Motion OS: `Story + ActingBeat + VisualContract` + keyframe bytes (`VeoMotionPrompt.FromFrozen`) — **not** master SHA on the wire |
| Continuity SH01/SH02 | [PARTIAL] | Same CHAR-001 id [PROVEN]. Different keyframe pixels [PROVEN] (`ProductionEp01Sh02ExistingCanonicalIngressV1Regression` T15). Veo does not lock face identity |
| Continuity SC02 / SH03 / EP | [MISSING] OS | No OS canon ingress |

### Can CHAR-001 stay the same person across SH01 / SH02 / SH03 / SC02 / EP01?

- **As an ID and canon record:** [PROVEN] yes for EP01 SC01 SH01/SH02.
- **As pixels Veo will regenerate:** [MISSING] — no character-lock API in official motion path.
- **As SH03/SC02 OS work:** [MISSING] backend canon.

Missing layer: **Character → Motion provider contract** (structured identity, not only prose in VisualContract).

---

## 6. Image / Keyframe

Two pipelines:

1. **Kit Video Gemini stills** — `ImageGenerationExecutionService`, `GeminiImageGenerationProvider`, mig 362+ [PROVEN] for that path.
2. **Production OS CREATE_IMAGE** — `IImageExecutionAdapter` = `ImportImageAdapter` / `FixtureImageAdapter` only [PROVEN]. Paid/network blocked in `CreateImageAsync`.

Official SH02 keyframe:

- ArtifactId `91daaf4b-5550-4cbc-b9f9-0bd0be556cef` (`OfficialCreateVideoFreezeV1.OfficialKeyframeId`)
- Bound `CURRENT:KEYFRAME`
- Freeze requires KeyframeArtifactId + byte SHA match [PROVEN]
- **Immutable execution input:** yes, as artifact GUID + SHA on snapshot. Not a mutable URL (`ProductionIdentityRules.ForbidUrlIdentity`).

Handoff Image → Motion [PROVEN]: snapshot `KeyframeArtifactId` + `KeyframeHash`; Veo adapter refuses missing/mismatched bytes.

OS does **not** generate official-film keyframes from Character Studio in the CK18–CK20 path. Those pixels were imported.

---

## 7. Motion Engine

### Classification

**B. Provider-agnostic abstraction, one official-film production provider (Veo 3.1), plus a second proven adapter (Runway) on TEST / older SH01 — not multi-provider production on the same official SH02 execution.**

Not A (pure Veo app): `IMotionExecutionAdapter` + frozen ProviderId/ModelId exist.  
Not C: official SH02 was not executed on Runway/Kling/Wan.

### Capability matrix (CK5–CK20 + prior)

| Capability | Status | Evidence | Production-ready? |
|---|---|---|---|
| Freeze | [PROVEN] | `OfficialCreateVideoFreezeV1`, CK12A, snapshot `fde661bb-…` | YES (official SH02) |
| Provider binding | [PROVEN] | Selection `c52b646f-…` EXPLICIT google/veo-3.1 | YES |
| Cost policy | [PROVEN] | `ProductionAuthoritativeVeo31CostPolicyV1` + regressions | YES for Veo 3.1 quote; not a general cost OS |
| Permit issue/consume | [PROVEN] | Permit `97240e2f-…` consumed CK18 only | YES |
| Paid network gate | [PROVEN] | OfficialVeo paid-network authorization | YES on official path |
| Authenticator | [PROVEN] | `OfficialVeoProductionAuthenticatorV1`; Join not persisted | YES |
| HTTP composition | [PROVEN] | `OfficialVeoHttpCompositionV1` | YES (Veo HOW) |
| Execution authorization | [PROVEN] | AUTH_HASH `b69ed488…` | YES |
| Real Vertex Start | [PROVEN] | CK18: 1 `predictLongRunning` | YES (one-shot) |
| Operation identity | [PROVEN] | `…/operations/633ea037-…` | YES |
| Poll | [PROVEN] | CK19: 1 `fetchPredictOperation` | YES |
| Recover | [PROVEN] | CK19 second run Poll=0, same artifact | YES (terminal replay) |
| Output materialization | [PROVEN] | VIDEO_TAKE `b44e530b-…` SHA `21700e4f…` 1,437,239 B | YES |
| Artifact verify | [PROVEN] | `OfficialVeoOutputBytes.IsSupportedVideo` | YES (container, not semantics) |
| Acceptance | [PROVEN] | CK20 `ACCEPTED:VIDEO_TAKE` + `GetAuthoritativeVideoTake` | YES |
| Replay / idempotency | [PROVEN] | CK18 Start replay; CK19/CK20 accept replay | YES |
| Failure fail-closed | [PROVEN] | CK20 tamper A–T; CK19 negative matrix | YES (deterministic) |
| Runway adapter | [PROVEN] adapter + TEST/4A live | `RunwayMotionExecutionAdapter`; CK4A; Real Runway V2 | YES on TEST; not official SH02 |
| Kling | [MISSING] | NO EVIDENCE FOUND of OS Kling adapter | NO |
| Wan | [PLANNED]/legacy | `WanMotionProvider` on Fal legacy stack; catalog in `FamixaProviderRouterV1` | NO as OS `IMotionExecutionAdapter` |
| Future provider | [PARTIAL] | New adapter + catalog + selection; snapshot fields stay generic | Possible without rewriting Story/Character tables |

Official SH02 duration: OS 10s / Vertex wire 8s (`VeoMotionDurationMapping`) — Veo HOW leak, not domain Story.

---

## 8. Provider Architecture

### Stacks (three, not one)

| Stack | Types | Live use |
|---|---|---|
| Production OS adapters | `IMotionExecutionAdapter`, `IVoiceExecutionAdapter`, `ILipSyncExecutionAdapter`, `IMixExecutionAdapter` | Official SH02 Veo; SH01 ElevenLabs/Fal/FFmpeg; TEST fixtures |
| Series Famixa providers | `IFamixaMotionProvider`, `IFamixaVoiceProvider`, `FamixaProviderRouterV1` | Legacy series turbo/lipsync/Wan-on-Fal |
| Catalog mocks | `IProductionProvider` MOCK_A/B | Compile/catalog tests only |

### Does core domain depend on Google/Vertex/Veo/Runway/Kling?

- `ProductionTaskInputSnapshot` / `ProductionArtifact` / `ProductionActions`: **no vendor types** [PROVEN]
- Official live hosts `OfficialVeo*` **do** encode Vertex operation, AUTH_HASH, Veo duration [PROVEN] — HOW/checkpoint layer
- Adding a provider **without** editing Story/Scene/Shot/Character/Artifact/Snapshot **records** is possible if the new engine fits current snapshot fields (I2V: prompt + keyframe + duration + aspect).
- Adding first/last-frame, video-to-video, multi-ref **requires snapshot field extension** (additive). That is a media-contract change, not a Story rewrite.

### Coupling to call out

- `VeoVertexOperationId`, `HttpVeoMotionTransport`, `OfficialVeoStartLedger` — Veo HOW
- `IVeoMotionTransport` name is Veo-specific even though the motion **interface** is `IMotionExecutionAdapter`
- Duration 10→8 is Veo 3.1 policy, stored as OS 10 on snapshot

---

## 9. Voice

| Topic | Status | Evidence |
|---|---|---|
| Voice contract / freeze | [PROVEN] | `ProductionVoiceFreeze`, CREATE_VOICE action, snapshot VoiceHash |
| Adapter | [PROVEN] | `ElevenLabsVoiceExecutionAdapter` + `FixtureVoiceAdapter` |
| Official SH01 real TTS | [PROVEN] | `FAMIXA_REAL_FILM_RUN_V2_EP01_SH01_VOICE_REPORT.md` — ElevenLabs = 1, VOICE VERIFIED, CURRENT:VOICE |
| Official SH02 voice | [MISSING] | CK20 explicitly did not run Voice |
| TEST fixture voice | [PROVEN] | CREATE_VOICE cutover + full-film vertical (fixture-voice-v1) |
| Character voice bible → OS | [PARTIAL] | Canon has voiceDna; SH01 used frozen voiceId, not full personality TTS tags |
| Recovery / replay | [PROVEN] on SH01 V2 | Replay did not call ElevenLabs again |
| IDB leftover | [PROVEN] leftover | `kit-famixa-tts` still exists; cutover says not OS authority |

Providers: ElevenLabs [PROVEN] adapter + one official shot. F5 [MISSING] OS adapter. Fixture [PROVEN] TEST.

---

## 10. LipSync

Contract: VIDEO_TAKE + VOICE → LIPSYNC via `ParentArtifactIds` (≥2) [PROVEN] in `FalLipSyncExecutionAdapter` / `FixtureLipSyncAdapter`.

| Topic | Status | Evidence |
|---|---|---|
| Adapter | [PROVEN] | `FalLipSyncExecutionAdapter`, `IFalLipsyncTransport` |
| Official SH01 first Start | [PARTIAL] | V1 report **FAILED** at download; job existed |
| Official SH01 recover | [PROVEN] | Recover report PASS — LIPSYNC VERIFIED, CURRENT+VISIBLE bound; Fal Start stayed 1 |
| Quality / speaker | [PARTIAL] | V2 quality **PARTIAL** — wrong character mouth on a **graph** freeze (`mot:…`, IDB voice), not CK20 VIDEO_TAKE |
| Official SH02 | [MISSING] | No OS LipSync on SH02 |
| Fixture TEST | [PROVEN] | Vertical run TEST-SC01-SH01 |

Production-ready as **OS recover/materialize** on SH01: yes.  
Production-ready as **Director-quality character-correct LipSync**: no.  
Production-ready as **SH02 consumer of CK20 VIDEO_TAKE**: no.

---

## 11. Mix / Audio

| Topic | Status | Evidence |
|---|---|---|
| MIX_VIDEO from CURRENT:LIPSYNC | [PROVEN] | Mix input cutover; `FfmpegMixExecutionAdapter` |
| Official SH01 FFmpeg remux | [PROVEN] | `FAMIXA_REAL_FILM_RUN_V1_EP01_SH01_MIX_REPORT.md` PASS; stream-copy; replay +0 |
| Music / SFX / ambience / levels / timeline | [MISSING] | NO EVIDENCE FOUND beyond remux of lipsync AV |
| Master audio separate from MIX | [MISSING] | MIX **is** the shot AV remux |
| Fixture mix | [PROVEN] | TEST vertical |

Implementation is real FFmpeg remux, not a design-only mix desk.

---

## 12. Final

| Topic | Status | Evidence |
|---|---|---|
| Binding role FINAL | [PROVEN] | `ProductionBindingRoles.Final` |
| Artifact role FINAL | [MISSING] | Roles are KEYFRAME/VIDEO_TAKE/VOICE/LIPSYNC/MIX/**ASSEMBLY** — no FINAL artifact role |
| ASSEMBLE | [PROVEN] | `FfmpegAssemblyExecutionAdapter`; official SH01 one-shot remux of MIX; TEST fixture assembly |
| FilmAssembly rows | [PROVEN] | `production_film_assemblies` / `_shots` (368) |
| Scene/Episode/Master concat | [MISSING] | Official assemble shots.Count == 1 in adapter; “No FINAL claim” in vertical-run report |
| Export formats | [PARTIAL] | MP4 stream-copy only |

There is no episode master renderer. ASSEMBLY of one MIX is not an episode.

---

## 13. Artifact System

Roles in `ProductionArtifactRoles`: KEYFRAME, VIDEO_TAKE, VOICE, LIPSYNC, MIX, ASSEMBLY.

| Field | Status |
|---|---|
| ArtifactId, Role, Hash, Size, MIME, StorageKey | [PROVEN] |
| Task / Attempt / Snapshot hash / Parents | [PROVEN] |
| Verification VERIFIED | [PROVEN] |
| Acceptance | [PROVEN] SH02 `ACCEPTED:VIDEO_TAKE`; SH01 ACCEPT MIX / ACCEPTED |
| Version as new artifact + new task | [PROVEN] pattern; not a version column |
| Provider credentials on artifact | [PROVEN] absent |

### Trace FINAL → … → CHARACTER

| Link | Status |
|---|---|
| ASSEMBLY → MIX parent | [PROVEN] SH01 assemble freeze |
| MIX → LIPSYNC parent | [PROVEN] SH01 |
| LIPSYNC → VIDEO_TAKE + VOICE parents | [PROVEN] adapter + recover |
| VIDEO_TAKE → snapshot → shot | [PROVEN] SH02 CK20 |
| Snapshot → CHAR-001 in VisualContract | [PROVEN] string id |
| VIDEO_TAKE → master SHA / CRP | [MISSING] |
| SH02 VIDEO_TAKE → SH02 VOICE | [MISSING] |

CK20 lookup `GetAuthoritativeVideoTake(build, shot)` is the downstream contract for SH02 motion output.

---

## 14. Provenance

Question: “Video này được tạo từ cái gì?”

| Walk | Status |
|---|---|
| Official SH02 VIDEO_TAKE → task/attempt/snapshot/operation/selection/permit/AUTH_HASH | [PROVEN] FULL for that hop (CK18–CK20 events) |
| Official SH01 MIX → LipSync → Voice + Video | [PROVEN] PARTIAL (OS rows exist; SH01 video ≠ CK20 SH02 take) |
| MIX → Character master | [MISSING] |
| Across planes (graph take 55 → OS artifact) | [PARTIAL] historically confused |

Overall: **PARTIAL**.

---

## 15. Reliability

| Concern | Fake / deterministic | Real provider |
|---|---|---|
| Replay same Start | CK18 regression START_REPLAY | CK18 live Start lifetime = 1 |
| Poll recover | CK19 regression | CK19 live Poll=1 then 0 |
| Accept idempotent | CK20 A–T + replay | CK20 live replay same artifact |
| Permit one-shot | closed permit store tests | CK18 consume 1; CK19/20 issue/consume 0 |
| Fal recover | adapter RecoverFrozenAsync | SH01 recover PASS |
| Process kill mid-poll | documented UNVERIFIED in 4A | [UNKNOWN] for CK18/19 kill |
| Duplicate VIDEO_TAKE | CK20 fail-closed AMBIGUOUS | Live count = 1 |

---

## 16. Director UX

Files: `ShotProductionWorkspace.tsx`, `ShotProductionCta.ts`, `production-os-action-authority.ts`, `ContentFamixaStudioView.tsx`, Character Studio.

Mapped Director verbs [PROVEN]: Tạo hình, Tạo video, Nạp thoại, Khớp môi, Hoàn thiện, Dùng kết quả, Lắp tập → Production Actions.

Can a non-engineer Director finish a **real official** video without knowing Vertex / OAuth / Operation / Permit / SnapshotHash?

**No.** [PROVEN] friction:

1. Official SH02 CREATE_VIDEO was executed as engineer checkpoints (CK18–CK20), not a single desk click.
2. Dual plane: preview historically played wrong take / cross-shot Fal (`FAMIXA_WORK_CONTROL_AND_EXECUTION_AUDIT_V1_REPORT`).
3. REGENERATE backend `ACTION_REMAP_FORBIDDEN` (UI cutover).
4. Character stills still `/series/still` (LEGACY).
5. Official SH02 has no Voice CTA completion on the CK20 artifact.
6. Permit/cost/auth are correct OS controls but are not fully hidden behind “Tạo video” on the official Veo path.

TEST fixture desk is closer to a complete click-path and is **not** real media.

---

## 17. Future AI Readiness

Current media contract is **image-to-video + frozen text + duration + aspect + parent artifact GUIDs**.

| Future capability | Fits today? |
|---|---|
| Text → Video | [PARTIAL] — Story exists; official path requires keyframe bytes (`MissingKeyframe`) |
| Image → Video | [PROVEN] — the official SH02 path |
| Reference → Video | [PARTIAL] — one keyframe, not multi-ref |
| Character → Video | [MISSING] structured; [PARTIAL] prose + pixels |
| Video → Video | [MISSING] snapshot field |
| Motion transfer / first-last frame | [MISSING] |
| Audio-aware generation | [MISSING] (`GenerateAudio: false` on Veo start) |
| Long-form | [MISSING] — Veo HOW caps duration |
| Scene/world consistency | [PLANNED] types only |
| Provider-specific extras | Belong in adapter HOW, not Story |

The contract is **not** only Veo 3.1, but official execution and duration policy **are** Veo-shaped. Extensibility = new adapter + optional snapshot fields. Do not bake Vertex operation IDs into Story.

---

## 18. What Famixa Can Actually Do Today

Only items with source + test and/or live report:

1. Freeze official EP01-SC01-SH02 CREATE_VIDEO against canonical Story/Acting/Visual/keyframe/timing and refuse drift.
2. Bind EXPLICIT google/veo-3.1 selection and consume one execution permit.
3. Start exactly one real Vertex `predictLongRunning` and persist operation identity (CK18).
4. Poll that operation once, materialize verified VIDEO_TAKE bytes, SHA `21700e4f…`, 1,437,239 bytes (CK19).
5. Accept that artifact as `ACCEPTED:VIDEO_TAKE` and look it up by build+shot; replay without Start/Poll (CK20).
6. Fail-closed on missing/ambiguous/tampered VIDEO_TAKE (CK20 matrix).
7. Import an official keyframe as immutable KEYFRAME artifact (not URL).
8. Run CREATE_VIDEO on TEST with Runway (CK4A / Real Runway V2) through OS Start/Poll/artifact.
9. Run CREATE_VOICE once on official SH01 via ElevenLabs; persist VOICE VERIFIED + CURRENT:VOICE.
10. Recover one official SH01 Fal LipSync job into a VERIFIED LIPSYNC artifact.
11. Remux official SH01 LIPSYNC → MIX and MIX → ASSEMBLY with server FFmpeg (stream-copy).
12. Run a **fixture-only** TEST film Story → Keyframe → Video → Voice → LipSync → Mix → Accept → Assemble.
13. Store CHAR-001 canon, master/DNA/PRP, and gate image prompts through identity governance.
14. Map Director CTAs to Production Actions and refuse legacy `/series/turbo` / `/series/lipsync` on OS shots.
15. Keep production events without OAuth/PEM/SA JSON (CK18–CK20 secret scan 0).

---

## 19. What Famixa Cannot Do Today

Only if evidence shows absence or failure:

- Cannot produce a complete official episode end-to-end from Story to episode master.
- Cannot run official SH02 Voice / LipSync / Mix / Final on the CK20 VIDEO_TAKE (not started).
- Cannot guarantee Veo will regenerate CHAR-001’s face across SH01/SH02/SH03.
- Cannot run official SH03 or SC02 through Production OS canon ingress.
- Cannot treat Series graph / IDB as OS authority (and they still exist).
- Cannot route official SH02 CREATE_VIDEO across Veo/Runway/Wan/Kling as replaceable engines in one live host.
- Cannot generate official-film keyframes on the OS CREATE_IMAGE path (import/fixture only).
- Cannot mix music/SFX/ambience or set a mix timeline.
- Cannot emit a FINAL artifact role or multi-shot episode master.
- Cannot hide Vertex/permit/snapshot from the official Veo Director path.
- Cannot claim LipSync speaker/character quality as production-accepted (SH01 V2 PARTIAL).
- Kling Production OS adapter: **NO EVIDENCE FOUND**.

---

## 20. Critical Gaps

Dependency order (not a quality ranking):

1. **One authority plane** — Director and downstream must read OS artifact GUIDs, not graph/IDB/latest URL.
2. **Consume CK20 VIDEO_TAKE** — SH02 Voice must take `GetAuthoritativeVideoTake` as immutable parent. Without this, SH02 motion is a dead-end.
3. **Official SH02 CREATE_VOICE** — same freeze/permit/artifact rules as SH01 V2, bound to SH02 snapshot + character voice id.
4. **Official SH02 LipSync** — VIDEO_TAKE `b44e530b-…` + new VOICE; Fal (or successor) behind `ILipSyncExecutionAdapter`; accept only after speaker/identity check.
5. **Character → provider identity** — master/DNA must constrain motion/voice HOW, or CHAR-001 continuity remains prose.
6. **Shot Accept → Mix → one-shot Assembly** on SH02 — closes the shot, not the episode.
7. **Director click-path** for the official slice — one desk flow, no CK host, no leftover preview plane.
8. **Single provider registry** — stop adding `OfficialVeo*` hosts; execute through generic OS + adapter.
9. **Episode assemble** — only after ≥2 accepted shot MIX/ASSEMBLY artifacts.
10. **Keyframe generation on OS** — only after Voice/LipSync on existing official pixels, or SH02 stays import-only.

---

## 21. What Not To Build

- **Do not redo CK20.** Acceptance exists. More acceptance metadata is not a capability.
- **Do not start CK21 motion infra** (another Start/Poll/Vertex host).
- **Do not add Kling/Wan OS adapters** before SH02 Voice uses the existing VIDEO_TAKE.
- **Do not create a second artifact table** or parallel “AuthoritativeVideoTake” store — binding `ACCEPTED:VIDEO_TAKE` is enough.
- **Do not rebuild Character Studio / CRP** to unblock SH02 Voice.
- **Do not concatenate an episode** from one shot.
- **Do not promote `IProductionProvider` MOCK_A/B** into the live path.
- **Do not keep writing OfficialVeo-only live hosts** for Voice/LipSync — that leaks Veo into the next stages.
- **Do not migrate official film to delete LEGACY graph in the same step as first SH02 Voice** unless Voice is blocked by it; prefer ignore-and-read-OS.
- **Do not add a video transcoder** for acceptance/verification.

---

## 22. Recommended Next Path

### Does CK20 need to be done now?

**No. CK20 is already `CK20_COMPLETE`.**  
Artifact `b44e530b-5b59-422f-9b80-e058a3a639b5` is VERIFIED + ACCEPTED.  
Do not reopen CK20. Do not fold Voice into a “CK20b.”

### At most three phases (vertical production slice)

**Phase 1 — Official SH02 Voice on the accepted VIDEO_TAKE**  
CREATE_VOICE for EP01-SC01-SH02. Freeze voice line + voiceId. One paid TTS or an explicit fixture decision. Bind CURRENT:VOICE. Parent/context names ArtifactId `b44e530b-…` as immutable motion input (do not regenerate video). Same closed rules: no Start video, no Poll video, no refreeze of the video snapshot.

**Phase 2 — Official SH02 LipSync + Mix**  
CREATE_LIPSYNC from that VIDEO_TAKE + VOICE. Recover/materialize like SH01. Then MIX_VIDEO FFmpeg remux. ACCEPT the MIX. No episode concat.

**Phase 3 — Director cutover for that slice**  
Desk “Nạp thoại / Khớp môi / Hoàn thiện / Dùng kết quả” on SH02 reads OS bindings only. Hide Vertex/permit. Then stop. Episode assemble waits until a second accepted shot exists.

Each phase must produce a durable artifact a Director can play. No infrastructure-only phase.

---

## 23. Final Architectural Verdict

1. Famixa today is a **Production OS kernel with one official-film motion shot accepted, a partial second-shot (SH01) voice/lipsync/mix chain, a fixture-complete TEST film, and a leftover Series/graph plane**.
2. Production-proven: official SH02 Veo freeze→Start→Poll→VIDEO_TAKE→ACCEPTED; official SH01 ElevenLabs voice, Fal lipsync recover, FFmpeg mix/assemble; TEST Runway video; CHAR-001 canon/governance; OS work/artifact/event model.
3. Foundation-only: provider-agnostic adapter interfaces, Wan/Kling catalogs, world types, episode assembly, OS image generation, Director-complete official film, character-conditioned motion.
4. The largest gap to a complete Video Production OS is **not more Veo checkpoints** — it is **one official SH02 vertical slice after the accepted VIDEO_TAKE**, on a single artifact authority, with character identity actually reaching Voice/LipSync.
5. Next step: **leave CK20 closed; execute official SH02 CREATE_VOICE against ArtifactId `b44e530b-5b59-422f-9b80-e058a3a639b5`.**

---

## Explicit close

This audit did not modify code, schema, or data.  
It did not call Vertex, Veo, Runway, Fal, or ElevenLabs.  
CK20 remains `CK20_COMPLETE`.  
The next production capability must be requested explicitly.
