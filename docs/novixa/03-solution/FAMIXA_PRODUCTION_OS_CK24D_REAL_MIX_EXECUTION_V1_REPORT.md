# CK24D — REAL MIX EXECUTION V1

Date: 2026-09-23  
Mode: **ONE official SH02 COMPOSE_SEGMENTS execution**. No Veo / Fal / ElevenLabs. Remux path unchanged.

## 1. VERDICT

**PASS**

## 2. TARGET

- Build: `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f`
- Shot: `EP01-SC01-SH02`

## 3. FROZEN CONTRACT

- ContractVersion: `MIX_CONTRACT_V1`
- ContractHash: `6bf96bcabb04408d5ecefedb51ae08ac674dc4ccf0933b8552f554fcaac22ebb`
- OutputClock: `VIDEO_TAKE`
- OutputDuration: `8`
- DurationToleranceSec: `0`
- CompositionMode: `COMPOSE_SEGMENTS`
- Parents:
  - VIDEO_TAKE `b44e530b-5b59-422f-9b80-e058a3a639b5` SHA `21700e4f81c9e97422e99651effd4e33f9271539f7c546fb45f442dbda476f34` binding `ACCEPTED:VIDEO_TAKE` consume `BYTES`
  - LIPSYNC `b0b4f0f2-95b5-4839-b9bc-22c48028bb9e` SHA `725df139162db23c8489395ae46d0583561a47163b594111a9b686be56959c4e` binding `CURRENT:LIPSYNC` consume `BYTES`
- PictureSegments:
  1. Dest `0 → 2.5` Source LIPSYNC `b0b4…` Source `0 → 2.5`
  2. Dest `2.5 → 8` Source VIDEO_TAKE `b44e…` Source `2.5 → 8`
- AudioSegments:
  1. Dest `0 → 2.5` Source LIPSYNC embedded audio `b0b4…` Source `0 → 2.5`
  2. Dest `2.5 → 8` Source `SILENCE` duration `5.5`
- TimingHash: `e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855` (OS `ShotExecutionCompiler.Hash` of the MIX command Timing field)

## 4. FREEZE

- Snapshot ID: `023675c7-1d26-46a9-9736-809b2c669773`
- Snapshot Hash: `60c68fcf64e350909262eda5fc0eb0e740490019544b1fbd81327990bdd74cfa`
- Freeze Token: `ProductionMixFreeze.TokenV1` persisted on existing `production_task_input_snapshots.shot_contract`

`V1|MIX_CONTRACT_V1|COMPOSE_SEGMENTS|VIDEO_TAKE|8|0|e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855|6bf96bcabb04408d5ecefedb51ae08ac674dc4ccf0933b8552f554fcaac22ebb|P|2|VIDEO_TAKE|b44e530b5b59422f9b80e058a3a639b5|21700e4f81c9e97422e99651effd4e33f9271539f7c546fb45f442dbda476f34|ACCEPTED:VIDEO_TAKE|BYTES|LIPSYNC|b0b4f0f295b54839b9bc22c48028bb9e|725df139162db23c8489395ae46d0583561a47163b594111a9b686be56959c4e|CURRENT:LIPSYNC|BYTES|PIC|2|0|2.5|LIPSYNC|b0b4f0f295b54839b9bc22c48028bb9e|725df139162db23c8489395ae46d0583561a47163b594111a9b686be56959c4e|0|2.5|2.5|8|VIDEO_TAKE|b44e530b5b59422f9b80e058a3a639b5|21700e4f81c9e97422e99651effd4e33f9271539f7c546fb45f442dbda476f34|2.5|8|AUD|2|0|2.5|LIPSYNC|b0b4f0f295b54839b9bc22c48028bb9e|725df139162db23c8489395ae46d0583561a47163b594111a9b686be56959c4e|0|2.5|2.5|8|SILENCE|-|-|0|5.5`

## 5. EXECUTION

- Transport: `ProcessFfmpegMixTransport.ComposeSegmentsOnceAsync` from frozen `MIX_CONTRACT_V1` (`FfmpegMixComposeGraph`). Remux path not entered.
- Canonical graph: input0=VIDEO bytes, input1=LIPSYNC bytes; `[1:v]trim=0:2.5` + `[0:v]trim=2.5:8` concat picture; `[1:a]atrim=0:2.5` + `anullsrc d=5.5` concat audio; `-t 8` libx264/aac.
- Request ID: `58ecbf49-6e1d-4fc6-8fc7-4eb5954d3a49`
- Task ID: `549a3685-1eb5-4381-970b-b87aec953d2a`
- Attempt ID: `ff28d642-7659-4283-9894-f89bfbeb897c`
- Provider job: `ffmpeg-compose-ff28d642765942839894f89bfbeb897c`
- Execution count: compose **1**, remux **0**

## 6. OUTPUT ARTIFACT

- Artifact ID: `58297402-e252-45e8-8d33-c7414749a297`
- SHA256: `e73c85dbbffea0dadb815d115faf52648d8d9f5bb241527920d3efcee65be92b`
- Size: `1423715`
- MIME: `video/mp4`
- Duration: `8` (OS verified; ffprobe format/video `8.000000`, 192 frames @ 24fps)
- Storage: `os/ff28d642765942839894f89bfbeb897c`
- Parents: `[b44e530b-5b59-422f-9b80-e058a3a639b5, b0b4f0f2-95b5-4839-b9bc-22c48028bb9e]`
- Verification: `VERIFIED`
- Distinct from VIDEO SHA `21700e4f…` and LIPSYNC SHA `725df139…`

## 7. MEDIA FORENSICS

Picture:

- `0→2.5` = LIPSYNC. Frame MSE MIX vs LIPSYNC: t=0.10 **1.96**, t=1.20 **2.45**, t=2.40 **2.49**. MIX t=0.10 vs VIDEO t=0.10 MSE **7.00** (same scene, closed mouth on VIDEO, open/speaking mouth on MIX=LIPSYNC).
- `2.5→8` = VIDEO remainder. Frame MSE MIX vs VIDEO: t=2.60 **12.65**, t=5.00 **14.24**, t=7.90 **12.64**. MIX t=2.60 vs LIPSYNC t=2.40 MSE **235.62**. Visual: t=5.00 MIX and VIDEO both show the card-handoff pose; LIPSYNC does not contain that time.

Audio:

- Speech interval: MIX 0.00–2.50 WAV RMS **2502.84** (LIPSYNC full RMS **3558.36**). Audio stream exists.
- Remainder silence: MIX 2.50–8.00 WAV RMS **0.023** — not duplicated VOICE.
- Total: ffprobe format duration **8.000000**; video stream **8.000000** / 192 frames; OS `DurationSec=8`. AAC stream **7.984000** (encoder padding; OS clock is 8.000).

## 8. ACCEPTANCE

- CURRENT:MIX = `58297402-e252-45e8-8d33-c7414749a297`
- ACCEPTED:MIX = `58297402-e252-45e8-8d33-c7414749a297`
- VISIBLE:MIX = `58297402-e252-45e8-8d33-c7414749a297` (existing bindVisible)
- ACCEPTED:VIDEO_TAKE / ACCEPTED:VOICE / ACCEPTED:LIPSYNC unchanged

## 9. REPLAY

- First execution count: compose **1**
- Replay: `MixVideoAsync` same contract / idempotency `…:MIX_VIDEO:6bf96bca…` returned artifact `58297402-…`
- Second execution count: compose **0** this call; total compose remains **1**. No new permit, artifact, or binding.
- Negative evaluation (not executed on production): `MatchesFrozen` VIDEO SHA mutation → `HASH_CHANGED`

## 10. SAFETY

- Provider calls this execute: **0**
- Veo this execute: **0**
- Fal this execute: **0**
- ElevenLabs this execute: **0**
- FFmpeg compose: **1**
- FFmpeg remux: **0** for SH02
- CREATE_MIX permit consumed: **1** (`3f738f80-f2af-41d1-a6b0-4c7f07867c27`)
- MIX artifacts created: **1**
- MIX bindings created: CURRENT:MIX + ACCEPTED:MIX (+ VISIBLE:MIX)
- Graph mutations: none. No legacy IDB mutation. VIDEO / VOICE / LIPSYNC artifacts unchanged.

## 11. REGRESSION

- CK20 OfficialVeoVideoTakeAcceptance V1: PASS
- CK21A OfficialVideoAuthorityBridge V1 + live read: PASS (`ACCEPTED:VIDEO_TAKE` still `b44e530b-…`)
- CK22 Official SH02 CREATE_VOICE: PASS
- CK23B Official SH02 CREATE_LIPSYNC (current `b0b4…` / historical `32bec770-…` unused): PASS
- SH01 MixCanon remux suite + MixInputCutover: PASS

## 12. GAPS

- MIX freeze TimingHash is SHA256 of the empty MIX command Timing field (`e3b0c442…`), not the VIDEO generate timing hash (`01e81b44…`). OS used the existing compiler; the contract is still frozen and hashed.
- AAC audio stream is 7.984s vs container/OS 8.000s. Not an invented tolerance. Remainder is silence.
- Remainder picture is libx264 re-encode (MSE ~12–14 vs VIDEO), not byte-identical. Trim+visual+MSE vs LIPSYNC prove remainder, not remux of LIPSYNC as the whole shot.
- Generic `VISIBLE` / `ACCEPTED` now resolve to the MIX artifact via existing Accept/bindVisible. Named `ACCEPTED:VIDEO_TAKE` / `VOICE` / `LIPSYNC` were not rewritten.

## 13. NEXT STEP

CK24D proves the MIX vertical slice for official SH02: **VOICE → LIPSYNC → MIX**.

The next missing production-chain stage after MIX is existing OS **ASSEMBLE** (`ASSEMBLE` → `ASSEMBLY` / FilmAssembly), which already requires official film shots to consume a MIX artifact bound on generic `ACCEPTED`.

Do not invent a new MIX checkpoint.

HARD STOP.
