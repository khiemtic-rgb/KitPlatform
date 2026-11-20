# FAMIXA REAL FILM RUN V1 — EP01 SH01 — REAL LIPSYNC ONE SHOT

Date: 2026-09-21  
Mode: REAL PRODUCTION EXECUTION · ONE SHOT · ONE PAID PROVIDER CALL  
Scope: EP01 `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` / SC01 / `EP01-SC01-SH01`

## Verdict
FAILED

Failure boundary: **Download / FAILED_IDENTITY** after exactly one Fal Start.

Fal accepted queue request `01a0c347-d35d-7c71-8cf5-12ba5141f0bc`. Production OS froze CURRENT:VIDEO_TAKE + CURRENT:VOICE, consumed the CREATE_LIPSYNC permit, and called Fal once. Physical MP4 bytes were not returned into Production OS storage. No LIPSYNC artifact was verified. CURRENT:LIPSYNC and VISIBLE:LIPSYNC were not bound.

No second Fal Start. No MIX. No FINAL. No VIDEO/VOICE regeneration.

## Scope
EP01 / SC01 / SH01

## Provider
Fal

## Model
sync-lipsync-v3

## Generation
Fal Start Count: 1  
Fal Retry Count: 0  
Fal Job ID: `01a0c347-d35d-7c71-8cf5-12ba5141f0bc` (queue request id in API log; **not persisted** on Attempt)

## Input Video
ArtifactId: `b22370a9-6ddd-4b04-a281-c7498cc926ae`  
SHA256: `8fa96f9b0bc229891d2e59c7763cc1b122d2afe493da124d4da7883333e66296`  
Duration: 5.042s  
Role: VIDEO_TAKE · VERIFIED · CURRENT · physical SHA match on disk (`os/a1673a1e54d344d69031cec3ed6c44b3`)

## Input Voice
ArtifactId: `bf8d0827-21ff-4daa-83b6-116bc3324ab0`  
SHA256: `948bb91adb6d10edd1fe0024b91090537699ddec79905cae9e8f956f1b4dbfd2`  
Duration: 4.232s  
LineId: `line-SC01-CHAR-003-1`  
Speaker: CHAR-003  
VoiceId: `kOIUKJ9E0DPndqTtvJm5`  
Role: VOICE · VERIFIED · CURRENT · physical SHA match on disk (`os-voice/e3c1abdad0274dd395b848134ac0b44c`)

## Speaker Target
Character: CHAR-003 (Linh / Mẹ)  
Target method: existing Fal v3 active-speaker bounding box from `FamixaFalLipsyncSpeakerPinV1`  
Target resolved: YES  
Target payload summary: pin x=0.63 y=0.25 @ 1280×720, frame 0; `auto_detect=false`; sync_mode=`silence` (no trim)

## Production
RequestId: `bfe960a3-e23a-4727-aa8b-25c1cf2502c9`  
TaskId: `5778814a-e617-4131-a9d4-18d2b518340d`  
InputSnapshotId: `26636cbe-e395-4a81-85b7-79e2323dfc9d`  
InputSnapshotHash: `834c082275914e8a28ee36bc80c819182812bf121b502229cfbe7b75528f3b13`  
AttemptId: `2db32efb-4ac8-4d2c-9297-11d234b135e1` (STARTED; providerJobId empty)  
Permit: `255b0c57-dfb5-4034-812c-41e125bed819` CONSUMED once before Start  
Frozen ShotContract: `line-SC01-CHAR-003-1|CHAR-003|b22370a96ddd4b04a281c7498cc926ae|bf8d082721ff4daa83b6116bc3324ab0|0.63|0.25|1280|720|0`

## Output
ArtifactId: (none)  
Role: (none)  
SHA256: (none)  
StorageKey: (none)  
MIME: (none)  
Duration: (none)  
Verification: FAILED_IDENTITY — no physical MP4 entered canonical production storage

## Binding
CURRENT:LIPSYNC: (none)  
VISIBLE:LIPSYNC: (none)

## Reload Verification
FAIL — nothing to reload; VIDEO/VOICE IDs unchanged after GET state

## Idempotency
Fal generation count: 1  
Duplicate replay behavior: same idempotency key returns IN_FLIGHT_REPLAY / does not Start Fal again. A prior empty-story POST (`famixa-real-film-run-v1-ep01-sh01-lipsync-oneshot`) created a CREATED task with **no Attempt and no Fal call**.

## Isolation
EP01 graph unchanged: YES (`415fedf306ed23d1ace94a794400b440`)  
EP99 graph unchanged: YES (`c7cd2516627332c2648c1509395e4a64`)

## Provider Calls
Fal: 1  
Runway: 0  
ElevenLabs: 0  
Gemini: 0  
Veo: 0  
Wan: 0

## Legacy Authority
URL used: NO  
IDB used: NO  
localStorage used: NO  
legacy resolver used: NO

## Safety
Retry: NO  
Fallback: NO  
Provider switch: NO  
Video regenerated: NO  
Voice regenerated: NO

CURRENT:VIDEO_TAKE remains `b22370a9-6ddd-4b04-a281-c7498cc926ae`  
CURRENT:VOICE remains `bf8d0827-21ff-4daa-83b6-116bc3324ab0`

## Remaining Gaps
- Physical Fal bytes were not materialized as ProductionArtifact(LIPSYNC) after the one Start (`FAILED_IDENTITY`).
- Attempt `2db32efb-4ac8-4d2c-9297-11d234b135e1` has no persisted `providerJobId`; job id exists only in API logs.
- No CURRENT:LIPSYNC / VISIBLE:LIPSYNC.
- Empty-story first POST left an unused IN_PROGRESS CREATE_LIPSYNC request with no provider call.

STOP. No MIX. No second Fal Start.
