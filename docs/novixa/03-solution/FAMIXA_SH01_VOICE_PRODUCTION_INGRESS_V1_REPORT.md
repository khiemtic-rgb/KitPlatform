# FAMIXA SH01 VOICE PRODUCTION INGRESS V1

Date: 2026-09-21  
Mode: FORENSIC → IMPLEMENT → TEST → LIVE VERIFY  
Scope: EP01 `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` / SC01 / `EP01-SC01-SH01`  
Safety: Provider calls = 0. AI calls = 0. Media generation = 0. VIDEO_TAKE immutable.

---

## 1. VERDICT

**PASS**

OUTCOME A. A locked official-film spoken line already existed. It was materialized into Production OS without rewriting text, without calling ElevenLabs, and without creating CURRENT:VOICE.

CREATE_VOICE is now **available = true**.

STOP. Do not call ElevenLabs. Do not start LipSync or Mix.

---

## 2. Canonical dialogue source

Official EP01 `graph_json` with `scriptLocked=true`, `voiceLocked=true`, `shotGraphLocked=true`, `storyReviewed=true`.

| Source | Text | Authoritative? |
|---|---|---|
| `graph_json.lines[id=line-SC01-CHAR-003-1]` | `Về rồi hả con? Sao cái mặt tiu nghỉu như mất sổ gạo thế kia?` | **YES** — locked script line |
| `episode.shots[EP01-SC01-SH01].dialogueSegmentIds` | `["line-SC01-CHAR-003-1"]` | **YES** — shot binding |
| `episode.shots[].beatText` | same line prefixed `Linh:` | corroboration |
| `roles.role-CHAR-003.line` / `characters.CHAR-003.line` | same exact text | corroboration |
| `runs.EP01-SC01-SH01.shotAction` | stage direction only (no spoken line) | Story only, already ingested |
| PerformanceSnapshot `f596c3eb-…` | `lines: []` | parent WHAT, empty dialogue |
| Director UI / TEST `LINE-${shot.id}` | placeholder | **NO** |
| kit-famixa-tts / localStorage / IDB | cache | **NO** |

UI text alone was not used. The locked graph line is the same string the UI displays.

---

## 3. Materialized identities

| Field | Value |
|---|---|
| Exact lineId | `line-SC01-CHAR-003-1` |
| Exact text | `Về rồi hả con? Sao cái mặt tiu nghỉu như mất sổ gạo thế kia?` |
| SpeakerCharacterId | `CHAR-003` (Linh / Mẹ) — SPEAKER; CHAR-001 remains LISTENER |
| VoiceId | `kOIUKJ9E0DPndqTtvJm5` (Anlee) |
| Voice provider | **elevenlabs** |
| Voice model | **eleven_v3** |
| ProductionAiSelection | `28b8579e-ded7-438e-938c-d71a8367a539` ACTIVE VOICE EXPLICIT |
| Selection hash | `SELECTION_HASH_V1:e47a571c7552e34b1a3b562d88b6a8287967c99f7a05c84bf943a7f16969c47f` |
| PerformanceSnapshot | `7dbaa094-55ff-4eea-8413-8e8ce2cbf254` |
| Performance hash | `PERFORMANCE_HASH_V1:da743747582b6e9eedd5c704265bf9c65d437eba1d45a1652dece2cd9d282493` |
| Parent snapshot (unchanged) | `f596c3eb-1dd4-4a3e-9077-f7fd40776116` `lines: []` |
| CREATE_VOICE permit | `0b90c754-558d-4b09-a6f1-687320858c5f` **ISSUED** unconsumed |
| Permit hash | `PERMIT_HASH_V1:e00ab49e3485f0302c090fa340c9f7fc6886f75c2afcf0115bbcb7171212759e` |
| CREATE_VOICE available | **true** |
| blockReason | none |

Historical voice bytes `vpa-20260915T161911Z-e0b3093f` were used as **identity evidence only**. They did not become CURRENT:VOICE.

---

## 4. VIDEO_TAKE unchanged

| Field | Value |
|---|---|
| artifactId | `b22370a9-6ddd-4b04-a281-c7498cc926ae` |
| SHA256 | `8fa96f9b0bc229891d2e59c7763cc1b122d2afe493da124d4da7883333e66296` |
| storageKey | `os/a1673a1e54d344d69031cec3ed6c44b3` |
| duration | 5.042s |
| size | 1529738 |
| VIDEO selection | `9b0dceee-ba53-4cc5-8139-ee9ba58a2011` runway / gen4_turbo unchanged |

---

## 5. Graphs

| | before | after |
|---|---|---|
| EP01 | `415fedf306ed23d1ace94a794400b440` | `415fedf306ed23d1ace94a794400b440` |
| EP99 | `c7cd2516627332c2648c1509395e4a64` | `c7cd2516627332c2648c1509395e4a64` |

---

## 6. Provider / media

| | |
|---|---|
| Provider calls | **0** |
| ElevenLabs | 0 |
| Runway this phase | 0 (`startCount=0`) |
| Fal / Gemini / Veo / Wan | 0 |
| Media generation | **0** |
| CURRENT:VOICE | **none** |
| LipSync / Mix | not started |

---

## 7. Tests

| Suite | Result |
|---|---|
| `FAMIXA_SH01_VOICE_PRODUCTION_INGRESS_V1` C# | **PASS FAIL=0** |
| `FAMIXA_REAL_FILM_RUN_V1_VOICE` C# | **PASS FAIL=0** |
| `famixa-sh01-voice-production-ingress.smoke.ts` | **PASS FAIL=0 (no provider)** |

Duplicate live POST returned the same Snapshot / Selection / Permit ids.

---

## 8. T01–T26

| ID | Result |
|---|---|
| T01 canonical dialogue source found | **PASS** |
| T02 exact dialogue preserved | **PASS** |
| T03 canonical lineId | **PASS** |
| T04 canonical speaker | **PASS** CHAR-003 |
| T05 approved VoiceId | **PASS** `kOIUKJ9E0DPndqTtvJm5` |
| T06 ElevenLabs selected | **PASS** |
| T07 approved model selected | **PASS** eleven_v3 |
| T08 VOICE ProductionAiSelection persisted | **PASS** |
| T09 PerformanceSnapshot versioning correct | **PASS** new id; parent immutable |
| T10 Story unchanged | **PASS** |
| T11 ActingBeat unchanged | **PASS** |
| T12 VisualContract unchanged | **PASS** |
| T13 Keyframe unchanged | **PASS** `1f4c26e0-…` |
| T14 5.0s timing unchanged | **PASS** |
| T15 one-shot CREATE_VOICE permit issued | **PASS** |
| T16 permit not consumed | **PASS** |
| T17 CREATE_VOICE readiness | **PASS** available=true |
| T18 reload preserves readiness | **PASS** |
| T19 duplicate ingress idempotent | **PASS** |
| T20 cross-shot blocked | **PASS** SH02 empty |
| T21 cross-build blocked | **PASS** EP99 empty |
| T22 no provider call | **PASS** |
| T23 no media generation | **PASS** |
| T24 VIDEO_TAKE unchanged | **PASS** |
| T25 EP01 graph unchanged | **PASS** |
| T26 EP99 unchanged | **PASS** |

---

## 9. Remaining gaps

- CREATE_VOICE execute is still fail-closed (`OFFICIAL_FILM_FORBIDDEN` before adapter Start). No ElevenLabs adapter is wired.
- Director UI still sends `CHAR-TEST` / `VOICE-TEST` on click. GET readiness is OS-resolved; a later execute mission must send the canonical line/VoiceId, not TEST identities.
- Historical physical voice is not CURRENT. The next phase must generate a new ProductionArtifact VOICE.

---

## 10. NEXT ACTION

**STOP.**

CREATE_VOICE is READY. Do not call ElevenLabs in this phase. Do not start LipSync. Do not start Mix. Do not modify UI.
