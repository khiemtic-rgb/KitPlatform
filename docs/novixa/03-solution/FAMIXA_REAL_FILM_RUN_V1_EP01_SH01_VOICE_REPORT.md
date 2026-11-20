# FAMIXA REAL FILM RUN V1 — EP01 SH01 — REAL VOICE ONE SHOT

Date: 2026-09-20  
Mode: IMPLEMENT → TEST → LIVE PAID ONE-SHOT → RELOAD VERIFY  
Scope: EP01 `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` / SC01 / `EP01-SC01-SH01`  
Safety: Exactly one real ElevenLabs generation was allowed. Actual generation count = **0**. All other AI providers = 0. VIDEO_TAKE immutable. No LipSync / Mix / Assembly / Final.

---

## 1. VERDICT

**BLOCKED**

CREATE_VOICE did **not** call ElevenLabs.

Production OS is the only voice-input authority. On official EP01 SH01 the canonical spoken line, speaker, and VoiceId **cannot be resolved deterministically**. The live PerformanceSnapshot has zero lines. There is no VOICE `ProductionAiSelection`, no approved VoiceId, and no CREATE_VOICE permit.

The mission forbids inventing dialogue, rewriting the story, modifying ActingBeat / PerformanceSnapshot, guessing a VoiceId, falling back to another TTS provider, or using kit-famixa-tts / localStorage / IndexedDB / `resolveLineAudio` as execution authority.

STOP. No provider Start. No CURRENT:VOICE.

---

## 2. Precise blocker

Live PerformanceSnapshot `f596c3eb-1dd4-4a3e-9077-f7fd40776116`  
hash `PERFORMANCE_HASH_V1:863ff9a9fc9d2757f9f02f9e4e48308402d969323953cfe9a197fa38106655f3`

| OS field | Live value |
|---|---|
| `lines` | `[]` |
| `dialogueSegmentIds` | `[]` |
| CHAR-003 | IN_FRAME / PRIMARY / **LISTENER** / `lineIds: []` |
| CHAR-001 | IN_FRAME / SECONDARY / **LISTENER** / `lineIds: []` |
| VOICE `ProductionAiSelection` | **none** (only VIDEO `runway` / `gen4_turbo`) |
| CREATE_VOICE permit | **none** (only consumed CREATE_VIDEO) |
| VOICE artifact / CURRENT:VOICE | **none** |

Director graph dialogue («Về rồi hả con?…») is **not** Production OS authority. UI still sends TEST identities `LINE-${shot.id}` / `CHAR-TEST` / `VOICE-TEST`. Those are rejected on official film (`MISSING_DEPENDENCY`).

After INPUT cannot be frozen:

GET `/actions` with no snapshot claim → `CREATE_VOICE.available=false` `MISSING_PERFORMANCE_SNAPSHOT`  
GET `/actions` with the live snapshot id → `CREATE_VOICE.available=false` `MISSING_DEPENDENCY` missing `LineText`, `VoiceId`, `LineId`, `CharacterId`

---

## 3. Locked IDs (VOICE not created)

| Field | Value |
|---|---|
| RequestId | **none** |
| TaskId | **none** |
| InputSnapshotId | **none** |
| InputSnapshotHash | **none** |
| AttemptId | **none** |
| provider | **none** (expected ElevenLabs — not selected in OS) |
| model | **none** (expected eleven_v3 — not selected in OS) |
| VoiceId | **unresolved** |
| lineId | **unresolved** |
| speakerCharacterId | **unresolved** |
| provider request/job identity | **none** |
| provider call count | **0** |
| physical audio size | **none** |
| MIME | **none** |
| duration | **none** |
| SHA256 | **none** |
| storageKey | **none** |
| ProductionArtifactId | **none** |
| CURRENT:VOICE binding | **none** |

---

## 4. Immutable VIDEO_TAKE (untouched)

| Field | Live after this phase |
|---|---|
| artifactId | `b22370a9-6ddd-4b04-a281-c7498cc926ae` |
| SHA256 | `8fa96f9b0bc229891d2e59c7763cc1b122d2afe493da124d4da7883333e66296` |
| duration | 5.042s |
| MIME | `video/mp4` |
| size | 1529738 bytes |
| storageKey | `os/a1673a1e54d344d69031cec3ed6c44b3` |
| provider / model | runway / gen4_turbo |
| binding | CURRENT:VIDEO_TAKE + VISIBLE:VIDEO_TAKE |
| physical bytes | same SHA / same size on disk |

---

## 5. Implementation (fail-closed only)

Official-film CREATE_VOICE now stops before any adapter Start:

1. `VOICE-TEST` / `CHAR-TEST` → `MISSING_DEPENDENCY`
2. Official film without PerformanceSnapshot claim → `MISSING_PERFORMANCE_SNAPSHOT`
3. Snapshot with empty / unmatched lines → `PERFORMANCE_LINE_MISMATCH`
4. Even if evaluate were to pass, official film still throws `OFFICIAL_FILM_FORBIDDEN` so `FixtureVoiceAdapter` cannot become CURRENT:VOICE
5. DI remains `IVoiceExecutionAdapter = FixtureVoiceAdapter`. No ElevenLabs Production OS adapter was wired.

TEST fixture path is unchanged. No UI change. No snapshot mutation. No graph write.

---

## 6. Tests

| Suite | Result |
|---|---|
| `FAMIXA_REAL_FILM_RUN_V1_VOICE` C# | **PASS FAIL=0** |
| `famixa-real-film-run-v1-voice.smoke.ts` | **PASS FAIL=0 (no provider)** |

Voice adapter `ExecuteCount = 0`. Runway `CreateCalls = 0`.

---

## 7. Reload verification

API rebuilt and restarted after the fail-closed gate. GET Production OS state (`KIT_MKT` admin, `:5290`):

| Check | Result |
|---|---|
| CURRENT:VOICE | **absent** |
| VIDEO_TAKE artifactId / SHA / storageKey / bytes / duration | unchanged |
| speaker / line binding | still unresolved (`lines: []`) |
| SH02 VoiceArtifactId | null |
| EP99 VoiceArtifactId | null |
| CREATE_LIPSYNC | `MISSING_VOICE_ARTIFACT` |
| MIX_VIDEO | `MISSING_LIPSYNC_ARTIFACT` |
| Runway HTTP audit (new process) | start=0 poll=0 download=0 total=0 |

Idempotency: no CREATE_VOICE Request/Task/Attempt exists, so replay cannot create a second provider call. Provider generation count remains 0.

---

## 8. EP01 graph

| | |
|---|---|
| before | `415fedf306ed23d1ace94a794400b440` |
| after | `415fedf306ed23d1ace94a794400b440` |
| unchanged | **true** |

---

## 9. EP99

| | |
|---|---|
| before | `c7cd2516627332c2648c1509395e4a64` |
| after | `c7cd2516627332c2648c1509395e4a64` |
| unchanged | **true** |

No EP99 rows written.

---

## 10. All provider calls

| Provider | Count | Notes |
|---|---|---|
| ElevenLabs | **0** | no Start; no adapter wired |
| Runway | **0** this phase | existing VIDEO_TAKE job `acc096dd-9fe4-42fd-bbc1-d1db1b6deaf4` not replayed |
| Fal | **0** | LipSync not started |
| Gemini / Veo / Wan / Mix / Assembly / Final | **0** | |

---

## 11. Failure evidence

Not a provider failure. Pre-Start authority failure:

- Canonical line: **unresolved** (`lines: []`)
- Canonical speaker: **unresolved** (both characters LISTENER, empty `lineIds`)
- Canonical VoiceId: **unresolved** (no VOICE selection)
- ElevenLabs / eleven_v3: **not selected** in Production OS
- TEST identities: rejected
- Fixture TTS: forbidden on official film
- No automatic retry
- No VoiceId / provider switch

---

## 12. T01–T25

| ID | Result | Evidence |
|---|---|---|
| T01 canonical line resolved | **BLOCKED** | snapshot `lines: []` |
| T02 canonical speaker resolved | **BLOCKED** | CHAR-003 and CHAR-001 LISTENER, `lineIds: []` |
| T03 canonical VoiceId resolved | **BLOCKED** | no VOICE selection |
| T04 ElevenLabs selected | **BLOCKED** | only VIDEO `runway` / `gen4_turbo` |
| T05 exact text frozen | **NOT_REACHED** | no InputSnapshot |
| T06 input snapshot immutable | **NOT_REACHED** | no InputSnapshot |
| T07 provider/model frozen | **NOT_REACHED** | no InputSnapshot |
| T08 exactly one ElevenLabs generation | **PASS** | count = 0, correct STOP |
| T09 no retry | **PASS** | no second task |
| T10 physical audio verified | **NOT_REACHED** | no bytes |
| T11 SHA recorded | **NOT_REACHED** | no artifact |
| T12 duration recorded | **NOT_REACHED** | no artifact |
| T13 ProductionArtifact VOICE created | **PASS** | none created |
| T14 artifact VERIFIED | **NOT_REACHED** | no artifact |
| T15 CURRENT:VOICE bound | **PASS** | none bound |
| T16 reload preserves CURRENT:VOICE | **PASS** | still none after restart |
| T17 replay does not create second provider call | **PASS** | no Request exists; count stays 0 |
| T18 legacy audio cannot override CURRENT | **PASS** | TEST identities rejected; no CURRENT |
| T19 cross-shot protection | **PASS** | SH02 VoiceArtifactId null |
| T20 cross-build protection | **PASS** | EP99 VoiceArtifactId null |
| T21 no Runway call | **PASS** | audit 0; VIDEO_TAKE unchanged |
| T22 no Fal call | **PASS** | LipSync not started |
| T23 no other AI provider | **PASS** | |
| T24 EP01 graph unchanged | **PASS** | `415fedf306ed23d1ace94a794400b440` |
| T25 EP99 unchanged | **PASS** | `c7cd2516627332c2648c1509395e4a64` |

---

## 13. Safety (untouched)

- Locked VIDEO_TAKE
- KEYFRAME `1f4c26e0-35aa-4395-94e7-09d2b1b764b6`
- PerformanceSnapshot payload
- ActingBeat
- EP01 graph
- EP99
- kit-famixa-tts
- legacy voice URLs
- Director display / localStorage
- SeriesShotRun
- LipSync / Mix / Assembly / Final
- UI

---

## 14. NEXT ACTION

**STOP.**

Do not start LipSync.  
Do not start Mix.  
Do not modify UI.  
Do not regenerate VIDEO or KEYFRAME.  
Do not call ElevenLabs.  
Do not invent a line, speaker, or VoiceId into the snapshot.

A later mission must first materialize the canonical SH01 spoken line + speaker + already-approved VoiceId into Production OS (PerformanceSnapshot + VOICE selection + one-shot permit) before CREATE_VOICE can run.
