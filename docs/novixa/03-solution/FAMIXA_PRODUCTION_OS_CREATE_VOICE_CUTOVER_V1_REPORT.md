# FAMIXA PRODUCTION OS — CREATE_VOICE CUTOVER V1

Date: 2026-09-19  
Mode: FORENSIC → IMPLEMENT → TEST → LIVE VERIFY  
Scope: Authority cutover for CREATE_VOICE / CURRENT:VOICE / voice resolution on **OS-controlled shots**.  
Safety: provider calls this phase = 0. TTS calls = 0. Media generation this phase = 0. EP01 / EP99 not written.

Every claim is classified: FACT / IMPLEMENTED / TESTED / LIVE_VERIFIED / ASSUMED / UNVERIFIED / RECOMMENDED.

---

## 1. EXECUTIVE VERDICT

**PASS_WITH_GAPS**

Production OS now owns Voice the same way it owns Video on OS-controlled shots:

USER INTENT → ProductionRequest → CREATE_VOICE → ProductionTask → immutable InputSnapshot → Provider/Model freeze → Attempt → FixtureVoiceAdapter → physical bytes → Verify → ProductionArtifact(role=VOICE) → CURRENT:VOICE → Shot State → GET /voice

Canonical resolver:

`ResolveCurrentVoiceArtifact(buildId, shotCode, lineId)`

= ProductionBinding `CURRENT:VOICE` → exact `artifactId` → ProductionArtifact → `storageKey` + SHA.

It does **not** resolve latest voice, voiceId alone, IndexedDB, or provider URL. Mismatch of build / shot / line **fail-closes**.

`kit-famixa-tts` / IndexedDB remains as **CACHE / RECOVERY / LEGACY**. It is not Production OS authority. Official-film Director still reads IDB via `resolveLineAudio` for non-OS assemble/display. That leftover is the main gap, same dual-plane pattern as the preview cutover.

CREATE_VOICE execution already existed. This phase cut over **resolution authority** and exposed GET `/voice`. It did not generate new audio and did not call ElevenLabs / F5 / Fal / Runway / Veo / Gemini / Wan.

---

## 2. SAFETY / SCOPE

| Rule | Result | Class |
|---|---|---|
| Real provider calls | 0 | FACT / TESTED |
| ElevenLabs / F5-TTS / Fal / Runway / Veo / Gemini / Wan | not invoked | FACT |
| New audio generation | 0 this phase | FACT |
| FFmpeg / MIX / ASSEMBLY / FINAL | not run | FACT |
| Official film media | not written | LIVE_VERIFIED |
| Schema migration | none | FACT |
| New orchestrator | none | FACT |
| Director redesign / label change | none | FACT |
| Timing V3 / Performance / Visual Authority / Canon | not modified | FACT |

---

## 3. FORENSIC VOICE PATH

Traced before authority code change.

| Path | Route | Class |
|---|---|---|
| OS UI CREATE_VOICE | SeriesTab `executeProductionOsAction` → POST `/production/requests` action=CREATE_VOICE → `CreateVoiceAsync` → FixtureVoiceAdapter → `MaterializeMediaAsync(voice)` → `BindNamedProtectedAsync(CURRENT:VOICE)` | AUTHORITY (OS) |
| OS GET state VOICE node | GET `/shots/{build}/{shot}/state` → `GetShotStateAsync` → `CURRENT:VOICE` binding | AUTHORITY (OS) |
| OS CREATE_LIPSYNC | `productionNodeArtifactId(VOICE)` / GET `/voice` → `VoiceArtifactId` parent | AUTHORITY (OS) |
| EvaluateCreateLipsync fill | if claim missing, fill from `CURRENT:VOICE` | AUTHORITY (OS) |
| `ResolveVisiblePreviewAsync` | VISIBLE only; never VOICE | AUTHORITY preview (visual) |
| `kit-famixa-tts` IndexedDB | `loadTtsBlobAny` / `famixa-voice-physical-idb-store` | CACHE / RECOVERY |
| `resolveLineAudio` | official film hydrate / assemble / lipsync-ready | LEGACY AUTHORITY (official film only) |
| `VoicePhysicalArtifactIdentity` | artifactId+buildId+lineId+voiceId client key | LEGACY / COMPATIBILITY |
| graph `audioUrl` / `voiceUrl` / `voiceAsset` / `ttsAsset` / `lineAudio` | series graph fields | LEGACY / NON-AUTHORITY for OS |
| ElevenLabs / F5 clients | not on CREATE_VOICE OS path | BYPASS if used as authority; unused this phase |

FACT: official-film voice resolution treated IndexedDB as authority. That is the hole this phase closes for **Production OS execution**, not for official-film Director display.

---

## 4. AUTHORITY BEFORE

| Voice Concern | Current Authority | Target Authority | Risk |
|---|---|---|---|
| Voice Task | Production OS CREATE_VOICE | Production OS | Low |
| Voice Attempt | ProductionAttempt (sync TTS, job id nullable) | same | Low |
| Voice Provider | FixtureVoiceAdapter frozen on Task/Snapshot | same; no silent fallback | Low |
| Voice Model | `fixture-voice-v1` frozen | same | Low |
| VoiceId | hashed into Snapshot.VoiceHash (`LineId\|Speaker\|VoiceId\|Story`) | same; not identity alone | Med if treated as artifact id |
| LineId | VoiceRequirements + PerformancePayloadJson | same; no new column | Med |
| Voice Artifact | ProductionArtifact role=VOICE on OS execute | ProductionArtifact | Low |
| Physical Audio | server `storageKey` after materialize | server physical | Low |
| Voice Binding | CURRENT:VOICE via protected bind | CURRENT:VOICE | Low |
| Voice Preview | N/A (VOICE is not VISIBLE) | CURRENT:VOICE for downstream | Low |
| Voice State | GET state VOICE node | GET state + GET /voice | Med — no line-verified resolver |
| LipSync Input | OS: VoiceArtifactId; official film: IDB blob | Voice ProductionArtifactId | HIGH if IDB used on OS |
| Official film resolve | `kit-famixa-tts` | CACHE only | HIGH |

---

## 5. AUTHORITY AFTER

| Voice Concern | Authority | Class |
|---|---|---|
| Voice Task / Attempt / Snapshot | Production OS | IMPLEMENTED / TESTED |
| Provider / Model | Frozen on Task + InputSnapshot | IMPLEMENTED / TESTED / LIVE_VERIFIED (TEST SH02 snapshot `fixture` / `fixture-voice-v1`) |
| Voice Artifact | ProductionArtifact role=VOICE | IMPLEMENTED / TESTED / LIVE_VERIFIED |
| Physical bytes | `storageKey` + SHA256 | IMPLEMENTED / TESTED / LIVE_VERIFIED |
| Current voice | ProductionBinding `CURRENT:VOICE` | IMPLEMENTED / TESTED / LIVE_VERIFIED |
| Line-verified resolve | `ResolveCurrentVoiceArtifact` | IMPLEMENTED / TESTED |
| IndexedDB | CACHE / RECOVERY | IMPLEMENTED / TESTED |
| Legacy graph voice URLs | cannot bind OS | IMPLEMENTED / TESTED |
| Official film Director IDB | still LEGACY display/assemble | FACT / leftover |

---

## 6. CREATE_VOICE ACTION CONTRACT

FACT / IMPLEMENTED / TESTED

Action: `CREATE_VOICE`  
CTA: `Tạo giọng nói` / `Nạp thoại`

Required inputs (existing, not invented):

- LineText
- VoiceId
- LineId
- CharacterId
- PerformanceSnapshot (non-official-film)

Success output: verified ProductionArtifact role=`VOICE` belonging to Attempt + InputSnapshot; `CURRENT:VOICE` names exact artifactId.

Forbidden fallbacks now include:

- CREATE_IMAGE / CREATE_VIDEO / CREATE_LIPSYNC
- `indexedDB.kit-famixa-tts`
- `latest voice`
- `voiceId alone`
- `provider URL`
- `audioUrl` / `voiceUrl`

Invalidation: existing ShotDependencyGraph. Story/Acting → Voice → LipSync → Mix → Accepted → Assembly.

---

## 7. INPUT SNAPSHOT

FACT / IMPLEMENTED / TESTED

`FreezeInputAsync` still freezes Story (line text), VoiceHash, ProviderId, ModelId, PerformanceSnapshotId/Hash/PayloadJson, SnapshotHash.

LineId is **not** a snapshot column. Reused: `PerformancePayloadJson` `"lineId"` + `VoiceHash = Hash(LineId|Speaker|VoiceId|Story)`. No SCHEMA_GAP. No migration.

After freeze, adapter receives the snapshot only. No re-read of picker / localStorage.

---

## 8. PROVIDER / MODEL FREEZE

FACT / IMPLEMENTED / TESTED / LIVE_VERIFIED (TEST SH02)

Task + Snapshot store `provider_id=fixture`, `model_id=fixture-voice-v1`.

No ElevenLabs → F5 fallback. Provider change = new Task (`NewTaskForProviderChangeAsync` existing).

---

## 9. ATTEMPT

FACT / IMPLEMENTED / TESTED / LIVE_VERIFIED

Synchronous fixture TTS. Attempt exists. `ProviderJobId` is the fixture storage token, not an invented vendor job.

Live TEST-SC01-SH02: attempt `333a0a3a-26f4-4a6c-87f5-afabdf43a138`.

---

## 10. PHYSICAL AUDIO

FACT / IMPLEMENTED / TESTED / LIVE_VERIFIED

Preferred authority: existing `IProductionArtifactFileStore` + `ProductionStoragePolicy.VoiceFolder`.

Live storageKey:

`os-test/aaaaaaaa-0000-4000-8000-000000000371/voice/333a0a3a26f44a6c87f5afabdf43a138`

MIME `audio/wav`, 80044 bytes, duration 5s. Not a provider URL. Not an IndexedDB blob.

---

## 11. PRODUCTION ARTIFACT

FACT / IMPLEMENTED / TESTED / LIVE_VERIFIED

| Field | Live TEST-SC01-SH02 |
|---|---|
| artifactId | `3a753477-fef0-4326-8423-a5d7c316be37` |
| role | VOICE |
| status | VERIFIED |
| taskId | `d515c1c1-46de-47a0-8731-28184deaf8d9` CREATE_VOICE SUCCEEDED |
| attemptId | `333a0a3a-26f4-4a6c-87f5-afabdf43a138` |
| inputSnapshotHash | `37e02952d09a7cd843b2be8af2de294d86bfa48a7655c448dfa2f94032c749d0` |
| byteSha256 | `df8e2fc5672e801443fa0c83fce714e0259cd597f2d55d3342adbb707c78fe93` |
| storageKey | `os-test/.../voice/333a0a3a…` |
| mime | audio/wav |
| duration | 5 |
| build | `bbbbbbbb-0000-4000-8000-000000000371` |
| shot | TEST-SC01-SH02 |

---

## 12. CURRENT:VOICE BINDING

FACT / IMPLEMENTED / TESTED / LIVE_VERIFIED

Role: `CURRENT:VOICE` (existing `CurrentFor(VOICE)`). No VISIBLE:VOICE. VOICE does not steal CURRENT VIDEO_TAKE.

Bind path: `TryBindNamedProtectedAsync` — same late-lineage guard as video.

Live: one active `CURRENT:VOICE` on TEST-SC01-SH02 → artifact `3a753477-…`.

---

## 13. INDEXEDDB AUTHORITY CUTOVER

IMPLEMENTED / TESTED

| Rule | Result |
|---|---|
| IDB is not Production OS authority | resolver source has no `kit-famixa-tts` / IndexedDB |
| IDB may remain | stores kept; comments mark CACHE / RECOVERY |
| IDB presence cannot override OS | resolver has no IDB input; returns bound artifact A |
| IDB absence does not destroy OS | server state is bindings + artifacts |
| Official OS execute must not fall back to IDB | CREATE_LIPSYNC uses GET /voice + OS VOICE node; never `loadTtsBlobAny` |

Mandatory scenario (in-memory): CURRENT:VOICE = A; unbound newer artifact C exists; resolver returns A. IndexedDB is not consulted.

---

## 14. LEGACY GRAPH CUTOVER

IMPLEMENTED / TESTED

`LegacySeriesGraphAdapter.NonAuthoritativeFields` now includes `audioUrl`, `voiceUrl`, `voiceAsset`, `ttsAsset`, `lineAudio`, `indexedDB.kit-famixa-tts`.

`MayDriveNewBinding(*)` remains false. Graph cannot write ProductionBinding.

`resolveLineAudio` classified LEGACY / CACHE / RECOVERY. Not deleted.

---

## 15. VOICE RESOLUTION

IMPLEMENTED / TESTED

Canonical:

`ProductionWorkExecutionEngine.ResolveCurrentVoiceArtifactAsync(buildId, shotCode, lineId)`

Steps:

1. Require non-empty lineId; else empty `MISSING_DEPENDENCY`
2. Read active `CURRENT:VOICE` only
3. Require artifact role=VOICE, VERIFIED, storageKey, SHA, storageKey not a URL
4. Require task scope build+shot match; else `CROSS_SHOT_FORBIDDEN`
5. Require snapshot lineId (from PerformancePayloadJson) equals requested lineId; else `PERFORMANCE_LINE_MISMATCH`
6. Return exact artifactId + lineage

GET: `/api/content/production/shots/{build}/{shot}/voice?lineId=`  
GET media: `.../voice/media?lineId=` (existing file store; no generation)

Client: `getProductionCurrentVoice`, `resolveCurrentVoiceArtifactId` (OS VOICE node only).

Historical live TEST snapshot has **empty** `performance_payload_json`. Line-verified GET on that row would fail-closed. GET state VOICE node still names the binding. Class: LIVE_VERIFIED for binding/artifact; line-verified GET on that historical row = fail-closed by design, not live-exercised (API process not rebuilt).

---

## 16. VOICE → LIPSYNC CONTRACT

IMPLEMENTED / TESTED (no LipSync migration, no Fal)

CREATE_LIPSYNC already requires `VoiceArtifactId` parent, not URL / IDB / voiceId.

EvaluateCreateLipsync fills missing claim from `CURRENT:VOICE`.

OS UI now prefers GET `/voice` then OS state node. Never IndexedDB.

Downstream contract exposed: `ProductionVoiceArtifactDto.artifactId` + GET state VOICE node.

---

## 17. INVALIDATION

FACT / TESTED

Existing `ShotDependencyGraph` / `InvalidateDownstreamAsync`. No new engine.

- Story change unbinds `CURRENT:VOICE` and downstream
- Voice change unbinds `CURRENT:LIPSYNC` and Mix+
- Old voice artifact remains immutable

---

## 18. REGENERATION

FACT / TESTED

New idempotency key → new Request / Task / Attempt / Artifact. Prior artifact SHA unchanged. CURRENT:VOICE moves only by new bind.

---

## 19. LATE RESULT

FACT / TESTED

Older CREATE_VOICE task completed after newer CURRENT:VOICE cannot `BindNamedProtectedAsync`. Reason: `LATE_RESULT_NOT_CURRENT`. Same guard as video. No voice-specific guard.

---

## 20. CROSS-SCOPE

FACT / TESTED

Planted CURRENT:VOICE pointing at another shot/build/official EP01 artifact throws `CROSS_SHOT_FORBIDDEN` (or `CROSS_SCOPE_FORBIDDEN` when scope store is present). Empty resolve if no binding.

---

## 21. TEST MATRIX T01–T42

Suite: `FAMIXA_PRODUCTION_OS_CREATE_VOICE_CUTOVER_V1`  
xUnit: `ProductionCreateVoiceCutoverV1Tests` PASS  
Smoke: `famixa-create-voice-cutover.smoke.ts` PASS FAIL=0  
Related: preview-authority suite still PASS

| ID | Result | Class |
|---|---|---|
| T01 Action Contract resolves | PASS | TESTED |
| T02 missing required input rejected | PASS | TESTED |
| T03 Voice Task created | PASS | TESTED |
| T04 InputSnapshot created | PASS | TESTED |
| T05 Provider/model frozen | PASS | TESTED + LIVE_VERIFIED (SH02) |
| T06 VoiceId frozen | PASS | TESTED + LIVE_VERIFIED (VoiceHash present) |
| T07 LineId frozen | PASS | TESTED |
| T08 Script/text frozen | PASS | TESTED |
| T09 Fixture provider through OS | PASS | TESTED |
| T10 Physical audio exists | PASS | TESTED + LIVE_VERIFIED |
| T11 Audio MIME verified | PASS | TESTED + LIVE_VERIFIED (`audio/wav`) |
| T12 Audio duration verified | PASS | TESTED + LIVE_VERIFIED (5s) |
| T13 SHA256 verified | PASS | TESTED + LIVE_VERIFIED |
| T14 role = VOICE | PASS | TESTED + LIVE_VERIFIED |
| T15 lineage Task | PASS | TESTED + LIVE_VERIFIED |
| T16 lineage Attempt | PASS | TESTED + LIVE_VERIFIED |
| T17 lineage InputSnapshot | PASS | TESTED + LIVE_VERIFIED |
| T18 CURRENT:VOICE created | PASS | TESTED + LIVE_VERIFIED |
| T19 resolver exact artifactId | PASS | TESTED |
| T20 not latest voice | PASS | TESTED |
| T21 not voiceId alone | PASS | TESTED |
| T22 not IndexedDB | PASS | TESTED |
| T23 not provider URL | PASS | TESTED |
| T24 cross-shot rejected | PASS | TESTED |
| T25 cross-build rejected | PASS | TESTED |
| T26 cross-scope rejected | PASS | TESTED |
| T27 old artifact immutable | PASS | TESTED |
| T28 regeneration new Task | PASS | TESTED |
| T29 late cannot steal CURRENT:VOICE | PASS | TESTED |
| T30 story invalidates voice | PASS | TESTED |
| T31 voice invalidates LipSync | PASS | TESTED |
| T32 no voice → downstream missing | PASS | TESTED |
| T33 reload preserves CURRENT:VOICE | PASS | TESTED + LIVE_VERIFIED (DB still bound) |
| T34 IDB absence does not destroy OS | PASS | TESTED |
| T35 IDB cannot override OS | PASS | TESTED |
| T36 legacy graph URL cannot override | PASS | TESTED |
| T37 provider calls = 0 | PASS | FACT |
| T38 media generation = 0 this phase | PASS | FACT |
| T39 EP01 untouched | PASS | LIVE_VERIFIED |
| T40 EP99 untouched | PASS | LIVE_VERIFIED |
| T41 no new orchestrator | PASS | TESTED (source) |
| T42 no competing voice state machine | PASS | TESTED (source) |

---

## 22. LIVE VERIFICATION

No POST CREATE_VOICE. No TTS. No provider start. Existing TEST fixture only.

| Check | Result | Class |
|---|---|---|
| DB CURRENT:VOICE TEST-SC01-SH02 | artifact `3a753477-…` | LIVE_VERIFIED |
| Artifact SHA / storageKey / mime / duration | see §11 | LIVE_VERIFIED |
| Task CREATE_VOICE SUCCEEDED fixture | yes | LIVE_VERIFIED |
| GET /state VOICE node | API process not reachable this session (`localhost:5290` connect fail) | UNVERIFIED |
| GET /voice | implemented; running API not rebuilt with this endpoint | IMPLEMENTED / TESTED / UNVERIFIED |
| Reload | binding still present after this phase (read-only) | LIVE_VERIFIED |
| IndexedDB control | server resolver has no IDB | TESTED; not live-browser | 
| Historical snapshot payload | `performance_payload_json` length 0 on this TEST voice | FACT — line-verified GET would fail-closed on that row |

Never convert IMPLEMENTED → LIVE_VERIFIED for GET /voice.

---

## 23. EP01 SAFETY CHECK

| Field | Value |
|---|---|
| series_build id | `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` |
| title | Tập 01 |
| updated_at | 2026-09-18 23:01:02.129634+07 |
| graph_json md5 | `415fedf306ed23d1ace94a794400b440` |
| production_tasks | 0 |
| active bindings | 0 |

Unchanged this phase. LIVE_VERIFIED.

---

## 24. EP99 SAFETY CHECK

| Field | Value |
|---|---|
| series_build id | `77262ec0-74ad-459d-9128-f09bb553a5dd` |
| title | TEST BATCH 3 SHORT |
| updated_at | 2026-09-07 16:52:54.467199+07 |
| graph_json md5 | `c7cd2516627332c2648c1509395e4a64` |
| production_tasks | 0 |
| active bindings | 0 |

Unchanged this phase. LIVE_VERIFIED.

---

## 25. REMAINING GAPS

1. Official-film Director still uses `resolveLineAudio` → `kit-famixa-tts` for display/assemble. LEGACY plane. Not deleted. RECOMMENDED next only if a later phase migrates official-film voice **display** — not required to keep OS authority.
2. GET `/voice` not LIVE_VERIFIED. Local API was down / not rebuilt. UNVERIFIED.
3. Live TEST-SC01-SH02 voice snapshot has empty PerformancePayloadJson. Line-verified resolver fail-closes on that historical row. New OS CREATE_VOICE freezes payload. ASSUMED that a new fixture voice would pass live GET /voice; not proven.
4. UI CREATE_VOICE still sends `LINE-${shot.id}` while TEST performance fixture uses `L1`. OS lipsync keeps CURRENT:VOICE node if GET line mismatches. RECOMMENDED: align OS UI lineId with frozen Performance line — separate phase, no semantics change here.

---

## 26. NEXT SINGLE PHASE

**CREATE_LIPSYNC input cutover to exact VOICE + VIDEO_TAKE artifactIds on the live API** (still fixture / 0 Fal), after API rebuild so GET `/voice` can be LIVE_VERIFIED.

Do not start official-film Preview migration, Mix, Assembly, Final, or vendor TTS in that phase.

---

## 27. STOP

This phase is closed.

Production OS owns Voice execution state and CURRENT:VOICE physical identity on OS-controlled shots. IndexedDB is cache. Official-film IDB display remains leftover.

No commit. No deploy. No provider calls. No official film writes.
