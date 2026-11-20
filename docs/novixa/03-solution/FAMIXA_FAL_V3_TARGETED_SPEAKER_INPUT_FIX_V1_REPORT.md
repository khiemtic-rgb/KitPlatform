# FAMIXA_FAL_V3_TARGETED_SPEAKER_INPUT_FIX_V1_REPORT

**Suite:** `FAMIXA_FAL_V3_TARGETED_SPEAKER_INPUT_FIX_V1`  
**Date:** 2026-09-16  
**Mode:** Code + tests only. **0 Fal / ElevenLabs / Veo / Runway.** No generate. No retry. No new output.

This suite reverses the prior forensic verdict `FAL_TARGETING_NOT_SUPPORTED` for **payload construction only**. The next real request **would** include OpenAPI `options.active_speaker_detection`. This phase did **not** prove Fal accepts that object.

---

## 1. UI SOURCE

Desk (`ShotProductionWorkspace`):

| Control | WHAT written |
|---|---|
| **Chấm mặt phải (Linh)** | `lipsyncSpeakerPickOf('right')` → `{x:0.66, y:0.38, width:1280, height:720, characterId: CHAR-003, frameNumber: 0}` |
| **Chấm mặt trái (Minh)** | `left` `{x:0.28, y:0.38, …, characterId: CHAR-001, frameNumber: 0}` |
| Click on keyframe/video | `stampLipsyncSpeakerPick` — normalized `x/y` + source size; `characterId` from mark/side; `frameNumber` = deterministic mark frame **0** |

Handler: `applyLipsyncSpeakerPick` → `stampLipsyncSpeakerPick` → `run.lipsyncSpeaker`. Not Canon. Not Shot Contract.

Button labels “Linh” / “Minh” are **UI copy only**. Identity is `characterId`.

Click on video does **not** record `video.currentTime`. Preview overlay has no frame index.

After freeze: buttons disabled, overlay pick off, `canMutateLiveSpeakerTarget` false. Live UI cannot change the provider target.

---

## 2. speakerCharacterId

```
Chấm mặt phải (Linh)
  → lipsyncSpeakerPickOf('right', characters)
  → CHAR-003
  → run.lipsyncSpeaker.characterId
  → freezeLipSyncExecutionInput.speakerCharacterId
  → assertLipSyncSpeakerTargetReady (multi-face fail-closed)
  → buildLipSyncProviderHowRequest.speakerCharacterId
  → mapFrozenSpeakerToFalV3How (HOW only)
  → startContentSeriesLipsync({ speakerX, speakerY, speakerVideoWidth, speakerVideoHeight, speakerFrameNumber })
  → ContentFalClient.LipsyncPayload → options.active_speaker_detection
```

SH01 spoken line fallback remains `CHAR-003` if the mark has no id. Submit still requires a valid region on a 2-face shot.

**WHAT (Shot / freeze):** `speakerCharacterId` + optional `speakerFaceRegion` (normalized UI) + `speakerFrameNumber`.

**Not on WHAT / Canon:** `falFaceX`, `falFaceY`, `falSpeakerIndex`, Fal JSON.

---

## 3. FROZEN TARGET

`LipSyncExecutionFrozenInput` snapshots:

| Field | SH01 Linh example |
|---|---|
| `speakerCharacterId` | `CHAR-003` |
| `speakerFaceRegion` | `{ x: 0.66, y: 0.38, width: 1280, height: 720 }` |
| `speakerFrameNumber` | `0` |

`width`/`height` on the mark are **source-video size**, not a Fal bbox.

After freeze:

- HOW reads frozen region/id/frame only. Live `lipsyncSpeaker` / later CHAR-001 cannot replace it.
- `assertFrozenSpeakerTargetUnchanged` throws on CHAR-001 overwrite.
- UI pick is rejected (`Speaker đã freeze — không đổi target Fal.`).

---

## 4. frame_number

**Source:** `LIPSYNC_SPEAKER_MARK_FRAME_NUMBER = 0` — the existing deterministic marking frame (same default C# used when the pin had no frame).

**Not used:** `video.currentTime` after the fact. The UI never stored a click-frame index, so this suite **reuses the existing marking frame** rather than inventing one.

Would-be Fal field: `options.active_speaker_detection.frame_number = 0`.

---

## 5. NORMALIZED COORDINATES

Linh (right): `{ x: 0.66, y: 0.38 }` in UI 0..1 space.

Minh (left): `{ x: 0.28, y: 0.38 }`.

These stay on WHAT / freeze / API `speakerX`/`speakerY`. They are **not** sent to Fal as 0..1.

---

## 6. CONVERTED PIXEL COORDINATES

SH01 source: **1280 × 720**.

```
normalized (0.66, 0.38)
  × (1280, 720)
  → round
  → clamp last pixel
  → [845, 274]
```

Minh check: `(0.28, 0.38)` → `[358, 274]`.

Conversion lives in:

- TS `sourceVideoPixelCoordinates` / `falV3ActiveSpeakerDetectionOf` (adapter proof)
- C# `FamixaFalLipsyncSpeakerPinV1.PixelCoordinates` / `TryActiveSpeakerDetection` (actual queue JSON)

SeriesTab does **not** build Fal JSON.

---

## 7. EXACT FAL TARGETING OBJECT THAT WOULD BE EMITTED

Next `fal-ai/sync-lipsync/v3` body **would** be:

```json
{
  "video_url": "<Take 55 CDN after upload>",
  "audio_url": "<locked Voice CDN after upload>",
  "sync_mode": "silence",
  "options": {
    "active_speaker_detection": {
      "frame_number": 0,
      "coordinates": [845, 274]
    }
  }
}
```

| Slot | Identity |
|---|---|
| video | Take **55** — `mot:EP01-SC01-SH01:55` |
| audio | `vpa-20260915T161911Z-e0b3093f` (full bytes, 4.24s) |
| speaker WHAT | `CHAR-003` |
| ASD | `{ frame_number: 0, coordinates: [845, 274] }` |

Not emitted this phase: `bounding_boxes`, `bounding_boxes_url`, `face_image`, `auto_detect`, `v3:true`.

`sync_mode=silence` is the current FAMIXA default (`NormalizeSyncMode`). Fal OpenAPI default is `cut_off`. Both are enum-valid. OpenAPI does not define pad/trim math beyond the enum names; mismatch 4.00s video / 4.24s audio is left to that field.

---

## 8. AUDIO DURATION BEFORE / AFTER PREPARATION

| Stage | Duration | Bytes / SHA |
|---|---|---|
| Physical Voice (untouched) | **4.24s** | SHA `E0B3093F093D6B36DEFA1DF40E3B42EB2FC94B216C5DA697FAE6593178A1001C` |
| Freeze `voiceDurationSec` | **4.24** | artifact `vpa-20260915T161911Z-e0b3093f` |
| HOW `voiceDurationSec` | **4.24** | |
| Server prep (single voice) | **4.24** | `KeepFullVoiceBytes(1)` → `DecodeAudio` full |
| Motion / performance clock | **4.00s** | Take 55 — **not** an atrim |

Client may still send `performanceDurationSec=4` as a **selector**. It is **not** applied as `atrim` for a single LipSync voice.

---

## 9. CONFIRMATION NO atrim

`FamixaFalLipsyncAudioPrepV1.MergePadDurationSec(1, 4)` is **null**.

`ContentSeriesTurboService.StartLipsyncAsync` uses `KeepFullVoiceBytes` for one voice and **does not** call `MergeLipsyncVoices(..., 4)`.

`atrim=0:{prod}` remains only on the **multi-voice** merge path. SH01 LipSync is one voice.

Physical Voice file was not rewritten. No new TTS.

---

## 10. TESTS T1–T15

| # | Result | Evidence |
|---|---|---|
| T1 | **PASS** | Linh pick → `CHAR-003` |
| T2 | **PASS** | Minh pick → `CHAR-001` |
| T3 | **PASS** | freeze keeps `CHAR-003` |
| T4 | **PASS** | overwrite CHAR-001 throws; `canMutateLiveSpeakerTarget` false; HOW ignores live Minh |
| T5 | **PASS** | `0.66,0.38` × 1280×720 → `[845, 274]` |
| T6 | **PASS** | `frame_number` present; source = mark frame **0** |
| T7 | **PASS** | adapter emits `active_speaker_detection`; multi-face without target fail-closed |
| T8 | **PASS** | WHAT has no `falFaceX` / `falFaceY` / `falSpeakerIndex` |
| T9 | **PASS** | voice duration stays **4.24s** |
| T10 | **PASS** | no `atrim=0:4` on single LipSync voice |
| T11 | **PASS** | Motion duration stays **4.00s** |
| T12 | **PASS** | Motion identity stays `mot:EP01-SC01-SH01:55` (Take 55) |
| T13 | **PASS** | Voice identity stays `vpa-20260915T161911Z-e0b3093f` |
| T14 | **PASS** | historical `01a0a327` not referenced / not rewritten |
| T15 | **PASS** | zero provider calls in this suite |

Smoke: `famixa-fal-v3-targeted-speaker-input-fix.smoke.ts`.  
C# pin: `FamixaFalLipsyncSpeakerPinV1Regression` T6 Linh ASD `[845,274]` frame 0.

---

## 11. PROVIDER CALLS

| Provider | Calls |
|---|---|
| Fal | **0** |
| ElevenLabs | **0** |
| Veo | **0** |
| Runway | **0** |

No generate. No retry. Historical LipSync `01a0a327` unchanged. Take 55 and Voice `vpa-20260915T161911Z-e0b3093f` unchanged.

---

## 12. REMAINING UNCERTAINTY

1. **Fal may still reject ASD.** Six prior paid jobs with targeting fields failed validation. This suite only proves the **next** request would contain `{ frame_number: 0, coordinates: [845, 274] }`. It does **not** prove Linh is synced.
2. **frame_number = 0** is the existing deterministic mark frame, not a captured click-frame. If Linh’s face is not visible / not the intended face on frame 0, Fal can still miss.
3. **`sync_mode=silence` semantics** are enum-valid in the 2026-09-14 OpenAPI; exact pad-vs-trim behavior vs Fal default `cut_off` is not specified there.
4. **`speakerFaceRegion.width/height`** still carry source-video size (1280×720), not a normalized bbox. Adapter treats `width>1` as source pixels.
5. SH01 submit fail-closed still requires `speakerCharacterId ===` spoken line (`CHAR-003`) **and** a valid region. Minh can be picked (T2) but would not be sent on this 2-face Linh line.

**STOP.** Do not run Fal until a later mission explicitly authorizes one request.
