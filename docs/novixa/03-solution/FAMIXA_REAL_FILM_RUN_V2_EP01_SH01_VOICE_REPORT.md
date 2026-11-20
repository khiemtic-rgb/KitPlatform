# FAMIXA REAL FILM RUN V2 — EP01 SH01 — ELEVENLABS ONE SHOT

Date: 2026-09-21  
Mode: IMPLEMENT → TEST → LIVE PAID ONE-SHOT → RELOAD VERIFY  
Scope: EP01 `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` / SC01 / `EP01-SC01-SH01`  
Safety: ElevenLabs = 1. All other AI = 0. No LipSync / Mix / Assembly / Final. VIDEO_TAKE immutable.

---

## 1. VERDICT

**PASS**

CREATE_VOICE executed once on official EP01 SH01. Production OS bound the frozen canonical inputs (not Director `CHAR-TEST` / `VOICE-TEST`). Permit `0b90c754-558d-4b09-a6f1-687320858c5f` was consumed immediately before the single ElevenLabs Start. Physical MPEG bytes from that Attempt are now ProductionArtifact VOICE **VERIFIED** and **CURRENT:VOICE**.

Replay of the same request did not call ElevenLabs again. Reload preserves the same artifact.

**STOP.** Do not start LipSync. Do not start Mix. Do not regenerate Video. Do not modify UI.

---

## 2. Identities

| Field | Value |
|---|---|
| RequestId | `72844d99-16d1-4092-a986-f783d6a2eb54` |
| TaskId | `b1cb1046-61ee-4882-bf1d-fd608e0ed4a4` |
| InputSnapshotId | `08fd452b-b26e-460f-bbc3-c029bd034d3f` |
| InputSnapshotHash | `5cb1917adb391ebfa89e2db18b2243eac28c0a1c56ca024c9adf296e71c454c9` |
| AttemptId | `e3c1abda-d027-4dd3-95b8-48134ac0b44c` |
| provider | elevenlabs |
| model | eleven_v3 |
| VoiceId | `kOIUKJ9E0DPndqTtvJm5` |
| lineId | `line-SC01-CHAR-003-1` |
| speakerCharacterId | CHAR-003 |
| exact text | `Về rồi hả con? Sao cái mặt tiu nghỉu như mất sổ gạo thế kia?` |
| provider request/job identity | `JnddJIWW3ry1RbkhZveL` |
| ElevenLabs generation count | **1** |
| physical audio size | 67753 |
| MIME | `audio/mpeg` |
| duration | 4.232s |
| SHA256 | `948bb91adb6d10edd1fe0024b91090537699ddec79905cae9e8f956f1b4dbfd2` |
| storageKey | `os-voice/e3c1abdad0274dd395b848134ac0b44c` |
| ProductionArtifactId | `bf8d0827-21ff-4daa-83b6-116bc3324ab0` |
| CURRENT:VOICE binding | `bf8d0827-21ff-4daa-83b6-116bc3324ab0` |
| CREATE_VOICE permit | `0b90c754-558d-4b09-a6f1-687320858c5f` **CONSUMED** once |
| ProductionAiSelection | `28b8579e-ded7-438e-938c-d71a8367a539` |
| PerformanceSnapshot | `7dbaa094-55ff-4eea-8413-8e8ce2cbf254` immutable |

InputSnapshot ShotContract (no TEST identities):

`line-SC01-CHAR-003-1|CHAR-003|kOIUKJ9E0DPndqTtvJm5|Về rồi hả con? Sao cái mặt tiu nghỉu như mất sổ gạo thế kia?`

---

## 3. Reload / idempotency / isolation

| Check | Result |
|---|---|
| reload verification | **PASS** — same artifactId / SHA / storageKey / MIME / duration / lineId / provider / model; media 67753 bytes still available |
| idempotency verification | **PASS** — replay returned the same RequestId / TaskId / AttemptId / ArtifactId; process ElevenLabs start after reload = 0 |
| historical audio isolation | **PASS** — `vpa-20260915T161911Z-e0b3093f` is not CURRENT:VOICE |
| VIDEO_TAKE unchanged | **PASS** — `b22370a9-6ddd-4b04-a281-c7498cc926ae` SHA `8fa96f9b0bc229891d2e59c7763cc1b122d2afe493da124d4da7883333e66296` 5.042s |

### EP01 graph

| | before | after |
|---|---|---|
| md5 | `415fedf306ed23d1ace94a794400b440` | `415fedf306ed23d1ace94a794400b440` |

### EP99 graph

| | before | after |
|---|---|---|
| md5 | `c7cd2516627332c2648c1509395e4a64` | `c7cd2516627332c2648c1509395e4a64` |

---

## 4. Provider / media

| | |
|---|---|
| ElevenLabs | **1** (`JnddJIWW3ry1RbkhZveL`) |
| Runway this phase | 0 |
| Fal / Gemini / Veo / Wan | 0 |
| LipSync / Mix / Assembly / Final | not started |
| VIDEO_TAKE | unchanged |
| CURRENT:VOICE | `bf8d0827-21ff-4daa-83b6-116bc3324ab0` |

---

## 5. Tests

| Suite | Result |
|---|---|
| `FAMIXA_REAL_FILM_RUN_V2_VOICE` C# | **PASS FAIL=0** |
| `FAMIXA_REAL_FILM_RUN_V1_VOICE` C# | **PASS FAIL=0** |
| `FAMIXA_SH01_VOICE_PRODUCTION_INGRESS_V1` C# | **PASS FAIL=0** |
| `FAMIXA_PRODUCTION_OS_CREATE_VOICE_CUTOVER_V1` C# | **PASS FAIL=0** |
| `famixa-real-film-run-v2-voice.smoke.ts` | **PASS** |

---

## 6. T01–T30

| ID | Result |
|---|---|
| T01 canonical line resolved | **PASS** `line-SC01-CHAR-003-1` |
| T02 exact text frozen | **PASS** |
| T03 speaker = CHAR-003 | **PASS** |
| T04 VoiceId = kOIUKJ9E0DPndqTtvJm5 | **PASS** |
| T05 provider = elevenlabs | **PASS** |
| T06 model = eleven_v3 | **PASS** |
| T07 PerformanceSnapshot frozen | **PASS** `7dbaa094-…` |
| T08 InputSnapshot immutable | **PASS** no CHAR-TEST / VOICE-TEST |
| T09 TEST identity rejected | **PASS** UI TEST is not execution authority; OS bind used |
| T10 permit scope correct | **PASS** EP01 / EP01-SC01-SH01 / CREATE_VOICE / elevenlabs / eleven_v3 / VoiceId |
| T11 permit consumed exactly once | **PASS** |
| T12 exactly one ElevenLabs generation | **PASS** |
| T13 no retry | **PASS** no second HTTP Start |
| T14 physical audio verified | **PASS** 67753 MPEG |
| T15 SHA recorded | **PASS** |
| T16 duration recorded | **PASS** 4.232s |
| T17 ProductionArtifact VOICE created | **PASS** |
| T18 artifact VERIFIED | **PASS** |
| T19 CURRENT:VOICE bound | **PASS** |
| T20 reload preserves artifact | **PASS** |
| T21 replay does not call provider again | **PASS** |
| T22 historical audio does not become CURRENT | **PASS** |
| T23 cross-shot blocked | **PASS** SH02 voice bindings = 0 |
| T24 cross-build blocked | **PASS** |
| T25 VIDEO_TAKE unchanged | **PASS** |
| T26 EP01 graph unchanged | **PASS** |
| T27 EP99 unchanged | **PASS** |
| T28 no Runway call | **PASS** |
| T29 no Fal call | **PASS** |
| T30 no other AI provider | **PASS** |

---

## 7. Failure evidence (recovered, no second generation)

The first live POST consumed the permit and completed ElevenLabs Start (`JnddJIWW3ry1RbkhZveL`). Physical bytes were written to `os-voice/e3c1abdad0274dd395b848134ac0b44c` (67753). Verify then returned `FAILED_VERIFICATION` because VOICE verify accepted only fixture WAV.

That was a verifier gap, not a second generation. MPEG verify was added. The same Request / Task / Attempt / bytes were verified and bound. Process ElevenLabs start after reload remained 0. Lifetime generation count remains **1**.

---

## 8. NEXT ACTION

**STOP.**

CURRENT:VOICE is established. Do not start LipSync. Do not start Mix. Do not modify UI. Do not regenerate Video.
