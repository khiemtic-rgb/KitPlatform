# FAMIXA PRODUCTION OS — PERFORMANCE CONSUMPTION CUTOVER V1
PHASE C REPORT

Date: 2026-09-18  
Mode: IMPLEMENT + TEST  
Scope: TEST only  

Provider calls: 0  
Media generation: 0  
EP01 / EP99: untouched  

Every statement is classified: FACT / IMPLEMENTED / TESTED / LIVE VERIFIED / ASSUMED / UNVERIFIED / RECOMMENDED.

Do not read this as Real Provider Bridge.  
Do not read this as media generation.  
Do not read this as Director UX work.

---

## Forensic path (before / after)

```
USER ACTION
  → ACTION CONTRACT (RequiredInputs)
  → GET Evaluate* / POST Create* (ONE evaluator)
  → REQUEST
  → TASK
  → INPUT SNAPSHOT (PerformanceSnapshotId + Hash + PayloadJson)
  → PERFORMANCE SNAPSHOT (immutable WHAT)
  → COMPILER (ActingBeat DERIVED from frozen Draft)
  → FIXTURE adapter (HOW)
  → ARTIFACT
  → BINDING
```

Official-film in-memory suites that still freeze CREATE_VIDEO on EP01 UUIDs stay on the Phase B optional-bind path. TEST scopes fail closed without a PerformanceSnapshot. **FACT**

---

## 1. CREATE_VIDEO cutover

RequiredInputs are now:

`ApprovedKeyframe, Story, ActingBeat, VisualContract, Timing, FrozenProviderModel, PerformanceSnapshot`

ActingBeat remains as compatibility. PerformanceSnapshot is authoritative WHAT. **IMPLEMENTED**

GET `EvaluateCreateVideoAsync` and POST `CreateVideoAsync` both call `CreateVideoActionContractGate.Evaluate` then `BindCreateVideoSnapshotAsync`. **IMPLEMENTED**

Fail-closed when:

- snapshot missing
- SnapshotId unknown
- PerformanceHash invalid / mismatch
- payload hash mismatch
- wrong shot / wrong build
- official film bind attempted

No fallback to ActingHash, fingerprints, latest graph, localStorage, SeriesShotRun, acceptedTake, or previewUrl. **IMPLEMENTED** **TESTED** (T01, T03–T06, T32)

Freeze copies `performanceSnapshotId`, `performanceHash`, `performancePayloadJson`. After INPUT_FROZEN the identity does not change when a later draft is frozen. **IMPLEMENTED** **TESTED** (T07, T08, T24, T29)

Compiler: `ShotExecutionCompiler` still emits the same prompt language. When a snapshot is bound, `ActingBeat` is overwritten by `PerformanceActingProjection.FromDraft` (DERIVED). Graph `ShotActingBeat` cannot override the frozen WHAT. **IMPLEMENTED** **TESTED** (T04 in CREATE_VIDEO TEST; T08)

---

## 2. CREATE_VOICE cutover

RequiredInputs are now:

`LineText, VoiceId, LineId, CharacterId, PerformanceSnapshot`

POST `CreateVoiceAsync` calls `EvaluateCreateVoiceAsync` (same evaluator as GET). **IMPLEMENTED**

Exact line match against frozen Draft:

- `lineId`
- `characterId`
- spoken `text`
- canonical `textHash`
- `delivery.locked == true`

No first-line, fuzzy text, or emotion fallback. **IMPLEMENTED** **TESTED** (T09–T14)

VoiceId remains HOW. It is not part of PerformanceHash. **FACT** **TESTED** (T15)

---

## 3. CREATE_LIPSYNC cutover

RequiredInputs now include `PerformanceSnapshot` in addition to `MotionArtifactId`, `VoiceArtifactId`, `FrozenProviderModel`. **IMPLEMENTED**

Physical parents remain Motion + Voice artifact IDs. Shared WHAT is `PerformanceHash` (plus buildId / shotCode via parent scope checks). **IMPLEMENTED**

Invariant:

`Motion.PerformanceHash == Voice.PerformanceHash == LipSync.PerformanceHash`

Mismatch → `PERFORMANCE_HASH_MISMATCH`. Same line text or same character is not enough. **IMPLEMENTED** **TESTED** (T17–T20)

Multi-speaker policy was not redesigned. **FACT**

---

## 4. Performance identity propagation

Every TEST CREATE_VIDEO / CREATE_VOICE / CREATE_LIPSYNC that passes Phase C writes:

- PerformanceSnapshotId
- PerformanceHash
- PerformancePayloadJson

onto the immutable `ProductionTaskInputSnapshot`. **IMPLEMENTED** **TESTED** (T02, T07, T10, T17)

Provider/model change (`NewTaskForProviderChangeAsync`) forwards the frozen PerformanceSnapshotId + Hash. **IMPLEMENTED** **TESTED** (T21)

---

## 5. Acting invalidation

`ClassifyInputDrift` now treats `PerformanceHash` change as `ACTING_CHANGED` before ActingHash. **IMPLEMENTED**

`InvalidateDownstreamAsync(Acting)` already supersedes VIDEO, VOICE, LIPSYNC, MIX via the existing graph. No second invalidation engine. **FACT** **TESTED** (T16)

Old artifacts stay immutable. New P002 requires new tasks. **TESTED** (T16)

---

## 6. Timing behavior

Timing V3 was not redesigned. Timing change changes TimingHash, not PerformanceHash. **FACT** **TESTED** (T22)

PerformanceHash does not contain provider duration bins. **FACT**

---

## 7. Visual Authority behavior

Visual Authority was not rebuilt. Visual change changes VisualHash, not PerformanceHash. **FACT** **TESTED** (T23)

---

## 8. Provider / model behavior

Provider/model is HOW. `runway/gen4_turbo` → `google/veo-3.1` keeps PerformanceSnapshotId and PerformanceHash. **IMPLEMENTED** **TESTED** (T21)

No new PerformanceSnapshot is created because HOW changed. **FACT**

---

## 9. Legacy isolation

| Field / path | Classification |
|---|---|
| PerformanceSnapshot / PerformanceHash / PayloadJson | AUTHORITY |
| ActingBeat string on InputSnapshot after bind | DERIVED (projection from frozen Draft) |
| ActingBeat / ActingHash on CREATE_VIDEO RequiredInputs | COMPATIBILITY (still listed; cannot satisfy snapshot) |
| VoiceHash (`lineId\|speaker\|voiceId\|LineText`) | HOW / execution identity |
| LinePerformance graph read | FORBIDDEN FALLBACK for CREATE_VOICE |
| actingBeatFingerprint / latest snapshot / localStorage / acceptedTake / previewUrl | FORBIDDEN FALLBACK |
| Official-film in-memory CREATE_VIDEO without snapshot | LEGACY READ (EP01 UUID work-execution / pipeline / cutover suites only) |

**IMPLEMENTED**

---

## 10. Action Contract GET/POST parity

One evaluator per action:

- CREATE_VIDEO: `CreateVideoActionContractGate` + `BindCreateVideoSnapshotAsync`
- CREATE_VOICE: `EvaluateCreateVoiceAsync`
- CREATE_LIPSYNC: `EvaluateCreateLipsyncAsync`

GET `/actions` (`GetShotStateAsync` → `BuildActions`) uses those verdicts. POST throws the same `BlockReason`. **IMPLEMENTED** **TESTED** (T31)

---

## 11. Tests T01–T32

`PerformanceConsumptionCutoverV1Regression` **PASS**. **TESTED**

| Id | Result |
|---|---|
| T01 CREATE_VIDEO no snapshot | FAIL closed |
| T02 CREATE_VIDEO valid P001 | READY / executed (fixture) |
| T03 wrong hash | FAIL |
| T04 wrong shot | FAIL |
| T05 wrong build | FAIL |
| T06 ActingHash only | FAIL |
| T07 freeze preserves P001 | PASS |
| T08 graph mutation after freeze | remains P001 |
| T09 CREATE_VOICE no snapshot | FAIL |
| T10 CREATE_VOICE P001 + L001 | PASS |
| T11–T13 wrong line / character / text | FAIL |
| T14 unlocked line | cannot freeze |
| T15 VoiceId change | PerformanceHash same, VoiceHash changes |
| T16 P001 → P002 acting invalidate | VIDEO + VOICE stale, artifacts immutable |
| T17 LipSync same hash | READY |
| T18–T20 different hash / same line / same character | FAIL |
| T21 provider change | PerformanceHash unchanged |
| T22 timing change | PerformanceHash unchanged |
| T23 visual change | PerformanceHash unchanged |
| T24 snapshot immutability | SNAPSHOT_IMMUTABLE |
| T25–T27 cross-shot / build / test-scope | FAIL |
| T28 official film | FAIL |
| T29 reload | exact SnapshotId + Hash |
| T30 duplicate idempotency | same request/task |
| T31 GET == POST | identical decision |
| T32 legacy fallback | FAIL |

xUnit: `Performance_consumption_cutover_v1_T01_through_T32` **PASS**. **TESTED**

---

## 12. Regression

Existing suites **PASS** (xUnit `ProductionPipelineV1Tests`, 14/14):

- ProductionTestCreateVideoV1Regression
- ProductionCompleteActionAuthorityV1Regression
- ProductionCreateVideoActionContractV1Regression
- ProductionWorkExecutionV1Regression
- ProductionPipelineV1Regression
- ProductionCreateVideoV1Regression
- ProductionScopeAnchor / Test scope / Test keyframe
- ProductionRunwayBridgeV1Regression (dry-run, 0 HTTP)
- PerformanceContractV1Regression (Phase A)
- PerformanceSnapshotBindingV1Regression (Phase B)

TS:

- `famixa-production-os-ui-cutover.smoke.ts` **PASS**
- `famixa-shared-performance-contract.smoke.ts` **PASS**
- `famixa-performance-snapshot-binding.smoke.ts` **PASS**
- `famixa-performance-consumption-cutover.smoke.ts` **PASS**

**TESTED**

TEST fixtures that execute CREATE_VIDEO / CREATE_VOICE / CREATE_LIPSYNC on non-official builds now attach a TEST PerformanceSnapshot. That is the cutover, not an unrelated patch. **FACT**

---

## 13. Database changes

None. Phase B columns on `production_task_input_snapshot` are used. No second PerformanceSnapshot table. No Phase A table rewrite. No EP01/EP99 backfill. **FACT**

---

## 14. HTTP / live verification

No live admin session, no POST `/production/requests` against `:5290`, no browser CTA walk. **UNVERIFIED**  
Not **LIVE VERIFIED**.

---

## 15. Provider calls

0. Fixture / dry-run adapters only. No Gemini, Runway HTTP, Veo, Wan, ElevenLabs, or Fal. **FACT**

---

## 16. Media generation

0 new production media. Fixture MP4/WAV bytes in in-memory tests only. No new keyframes, video, voice, lip-sync, or mix on disk for official film. **FACT**

---

## 17. EP01 / EP99 safety

- Performance draft freeze on official film → `OFFICIAL_FILM_FORBIDDEN` **TESTED** (T28)
- Bind snapshot onto official film scope → `OFFICIAL_FILM_FORBIDDEN` **TESTED** (T28)
- No production task/artifact/binding created on official builds in this phase **FACT**
- Official EP01 `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` and EP99 `77262ec0-74ad-459d-9128-f09bb553a5dd` IDs unchanged **FACT**

`ActionRequiresSnapshot` is false for official film UUIDs so historical in-memory work-execution / pipeline / create-video-cutover suites keep passing without a PerformanceSnapshot. Those suites do not write official film rows. **FACT**

---

## 18. Remaining gaps

1. Live GET/POST parity on `:5290` + Vite `:5173` was not executed. **UNVERIFIED**
2. Director UI still does not mint or send a PerformanceSnapshot. TEST UI will show the existing blocked state for CREATE_VIDEO / CREATE_VOICE until a snapshot is supplied. That is required Phase C behavior, not a UI bug. **ASSUMED** from code; **UNVERIFIED** in browser
3. Official-film CREATE_* remains on the pre-cutover required-input set (no PerformanceSnapshot). Phase C does not activate consumption on EP01/EP99. **FACT**
4. ActingBeat compatibility string is still compiled into the HOW prompt after deterministic projection. Prompt language was not redesigned. **FACT**
5. LipSync speaker targeting / Fal quality was not touched. **FACT**

---

## 19. Exact next phase

**STOP.** Phase C is complete.

The next phase in the shared-performance program is Real Provider Bridge (paid/network HOW execution against a frozen PerformanceSnapshot). Do not start it from this report.

Do not generate media.  
Do not benchmark Runway/Veo.  
Do not fix LipSync quality.  
Do not modify Director UX.

---

## Files changed (this phase)

| File | Role |
|---|---|
| `ProductionActionContracts.cs` | require PerformanceSnapshot on VIDEO/VOICE/LIPSYNC |
| `CreateVideoActionContractGate.cs` | GET required-input + claim fields |
| `ProductionPerformanceConsumption.cs` | one evaluator: bind, voice line, parent hash, acting projection |
| `ProductionTestPerformanceFixture.cs` | TEST snapshot seed |
| `ProductionWorkExecutionEngine.cs` | require + derive at freeze |
| `ProductionStateMachines.cs` | PerformanceHash → ACTING_CHANGED |
| `ProductionExecutionService.cs` | GET/POST EvaluateCreateVoice / EvaluateCreateLipsync |
| `IProductionExecutionService.cs` | new evaluate methods |
| `ProductionCreateVideoService.cs` / `.Actions.cs` | consume verdict; preserve Performance* |
| `ProductionWorkIds.cs` | MISSING_PERFORMANCE_SNAPSHOT, PERFORMANCE_LINE_MISMATCH, PERFORMANCE_HASH_MISMATCH |
| `PerformanceSnapshotBinding.cs` | BindResult includes Draft |
| `PerformanceConsumptionCutoverV1Regression.cs` | T01–T32 |
| Phase A/B assertions + TEST fixtures | cutover, not unrelated systems |
| `famixa-performance-consumption-cutover.smoke.ts` | static proof |
| this report | forensic record |

No Director CTA, no adapter, no Timing V3, no Canon, no VA, no new SQL. **FACT**
