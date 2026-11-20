# FAMIXA_FAL_V3_SPEAKER_TARGET_FORENSIC_V1_REPORT

**Suite:** `FAMIXA_FAL_V3_SPEAKER_TARGET_FORENSIC_V1`  
**Date:** 2026-09-16  
**Mode:** Code + tests only. **0 Fal / ElevenLabs / Veo / Runway.** No new output. No retry.

## VERDICT

**FAL_TARGETING_NOT_SUPPORTED**

Fal `fal-ai/sync-lipsync/v3` does **not** have a proven, reliable speaker-target HOW. FAMIXA now freezes **WHAT** (`speakerCharacterId`) and the adapter **does not emit** ASD / coordinates. A UI pin cannot make Linh the Fal face on this model.

Audio 4.24 → 4.00 was **not** Fal. It was KIT `MergeLipsyncVoices` `atrim=0:{motionDuration}` when the client sent `performanceDurationSec = frozen.motionDurationSec` (4.000).

---

## 1. UI FACE-SELECTION SOURCE

Desk (`ShotProductionWorkspace`):

| Button | Preset | Stored (before this suite) |
|---|---|---|
| **Chấm mặt phải (Linh)** | `lipsyncSpeakerSidePreset('right')` → `{x:0.66, y:0.38, width:1280, height:720}` | `run.lipsyncSpeaker` only |
| **Chấm mặt trái (Minh)** | `left` `{x:0.28, y:0.38, …}` | same |
| Click on video/keyframe | normalized `x/y` + pixel size | same |

**Not stored:** `characterId`, bbox, Fal index. Labels “Linh” / “Minh” were **UI copy only**.

Handler: `onPickLipsyncSpeaker` → `patchRun({ lipsyncSpeaker: pt })` → local graph / persist. Not Canon.

Now: `lipsyncSpeakerPickOf(side, characters)` / `stampLipsyncSpeakerPick` add **WHAT** `characterId` (CHAR-003 / CHAR-001) onto the same mark. No new face-picker.

---

## 2. speakerCharacterId FLOW

```
Chấm mặt phải (Linh)
  → lipsyncSpeakerPickOf('right', characters)
  → characterId CHAR-003  (name Linh / id CHAR-003)
  → run.lipsyncSpeaker.characterId
  → freezeLipSyncExecutionInput.speakerCharacterId
  → buildLipSyncProviderHowRequest.speakerCharacterId
  → mapFrozenSpeakerToFalV3How(...)
  → Fal HOW: video_url + audio_url + sync_mode only
     targeting = FAL_TARGETING_NOT_SUPPORTED
```

Spoken line `CHAR-003` is the freeze fallback if the mark has no id.

**Not** on Shot Contract / Canon: `falFaceX`, `falSpeakerIndex`, pixel coords as contract.

---

## 3. FROZEN SPEAKER TARGET

`LipSyncExecutionFrozenInput` now keeps:

- `speakerCharacterId` (WHAT)
- `speakerFaceRegion` `{x,y,width,height}` (existing normalized mark, optional)

After freeze:

- `assertFrozenSpeakerTargetUnchanged` rejects CHAR-001 overwrite
- HOW reads frozen id only; live `lipsyncSpeaker` cannot replace it
- Fal v3 HOW **does not** turn the region into payload fields

---

## 4. EXACT FAL TARGETING MECHANISM

Proven from in-repo contract forensic (`FAMIXA_FAL_V3_LIPSYNC_CONTRACT_FORENSIC_V1`, OpenAPI 2026-09-14) + `ContentFalClient.LipsyncPayload`. **No new Fal fetch.**

Documented / required v3 input:

- `video_url` (required)
- `audio_url` (required)
- `sync_mode` optional (`cut_off|loop|bounce|silence|remap`)

OpenAPI **also lists** `options.active_speaker_detection`:

| Field | Schema | Official Fal examples | Runtime |
|---|---|---|---|
| `auto_detect` | bool? | never shown | — |
| `v3` / `use_v2` | bool? | never shown | — |
| `frame_number` | int ≥ 0? | never shown | — |
| `coordinates` | **integer[2]** | never shown | Wave A floats rejected; Wave B/C ints still `value_error` on whole ASD object |
| `bounding_boxes` / `_url` | optional | never shown | never sent by FAMIXA |
| `face_image` | optional | never shown | never sent |

Six paid v3 jobs **with ASD produced no video**. Successful SH01 jobs (including `01a0a8d3`) used **video+audio only**. Fal then lip-synced **Minh**.

`ContentFalClient` only adds ASD when `speakerX/Y ∈ [0,1]`:

```
options.active_speaker_detection = {
  auto_detect: false,
  frame_number: 0,
  coordinates: PixelCoordinates(x,y,w,h)  // ints
}
```

That nest is **schema-present, runtime-unreliable**. Adapter must **not** emit it.

No invented fields (`speaker_index`, `target_face`, …).

---

## 5. WHY THE VALIDATED RUN HAD speakerPin=false

Loss is **not** “UI missing.” It is the **provider boundary**.

1. Desk **Khớp môi mới** → `startLipsync({ forceNew: true, remake: true })`.
2. `skipSpeakerPin = Boolean(forceNew) || dead-job` → **true**.
3. Confirm copy: *“chỉ gửi video + thoại — không chấm mặt.”*
4. `startProvider` passed `speaker: skipSpeakerPin ? undefined : speakerAtFreeze`.
5. `buildLipSyncProviderHowRequest` therefore had no `speakerX/Y`.
6. `LipsyncPayload` v3 without coords → `{video_url, audio_url, sync_mode}` only.
7. Fal chose a face. On SH01 that was **Minh (left)**.

Even a prior “Chấm mặt phải (Linh)” mark **did not enter the Fal body**.

This suite: SeriesTab no longer attaches speaker coords on the Fal POST. `skipSpeakerPin` still skips the *must-click* gate for dead/forceNew jobs; it is no longer the only drop.

---

## 6. EXACT AUDIO TRIM LOCATION

```
freeze.motionDurationSec = 4.000          // Take 55 physical
buildLipSyncProviderHowRequest
  performanceDurationSec = frozen.motionDurationSec   // 4.000
startContentSeriesLipsync({ performanceDurationSec: 4 })
ContentSeriesTurboService.StartLipsyncAsync
  padToPerformance = request.PerformanceDurationSec   // 4
  voiceRows.Count == 1 && padToPerformance > 0
    → MergeLipsyncVoices(..., 4)
      ffmpeg: atrim=0:4 , -t 4
```

**Not** Fal. **Not** Timing V3. **Not** editorial assemble. **Not** a new Voice.

Voice artifact stayed 4.24s / 69007 B / SHA `E0B3093F…`. The **upload** was the trimmed mix.

---

## 7. 4.24 → 4.00 EXPLANATION

Enforcement V1 set HOW duration to **frozen motion** so Fal would not pick take-56’s 6s clock. That value was reused as **audio pad/trim**.

Single locked Voice 4.24s + `atrim=0:4` → last ~0.24s dropped. Output AAC = 4.000s.

Fal v3 contract does **not** require audio duration = video duration (`sync_mode` exists; default FAMIXA `silence`).

---

## 8. MINIMAL CODE CHANGES

| Area | Change |
|---|---|
| `famixa-lipsync-speaker-target.ts` | WHAT: side/name → `speakerCharacterId`; freeze immutability |
| `famixa-fal-v3-lipsync-speaker-how.ts` | HOW: `FAL_TARGETING_NOT_SUPPORTED`; no ASD/coords |
| Freeze / HOW | `speakerCharacterId`, `voiceDurationSec`; v3 omits `speakerX/Y` |
| SeriesTab / Workspace | Stamp CHAR-003/001 on pick; freeze speaker; Fal POST without pin |
| `FamixaFalLipsyncAudioPrepV1` | Single voice = `DecodeAudio` full bytes; no `atrim` to motion 4s |
| Shot Timing V3 / editorial / Motion duration / Canon | **unchanged** |
| Historical `01a0a327` | **untouched** |

---

## 9. TESTS T1–T13

`famixa-fal-v3-speaker-target.smoke.ts` + `FamixaFalLipsyncAudioPrepV1Regression` — **PASS**.

| Test | Result |
|---|---|
| T1 UI Linh → CHAR-003 | PASS |
| T2 UI Minh → CHAR-001 | PASS |
| T3 speaker survives freeze | PASS |
| T4 speaker immutable after freeze | PASS |
| T5 provider HOW receives CHAR-003 | PASS |
| T6 adapter uses only documented Fal fields + targeting status | PASS |
| T7 unsupported Fal speaker fields never emitted | PASS |
| T8 Voice 4.24 preserved on HOW; server KeepFullVoiceBytes | PASS |
| T9 no atrim-to-4 for single voice | PASS |
| T10 Motion performance stays 4.00 | PASS |
| T11 Motion/Voice frozen ids unchanged | PASS |
| T12 historical LipSync task not referenced in new/freeze impl | PASS |
| T13 0 provider calls in these modules | PASS |

Also: freeze V2 T1–T15 PASS; enforcement T1–T13 PASS.

---

## 10. CAN FAL V3 RELIABLY TARGET LINH?

**No.**

**FAL_TARGETING_NOT_SUPPORTED**

Do not treat “Chấm mặt phải (Linh)” as an effective Fal speaker. Evaluate another provider/model if Linh-only sync is required.

---

## 11. REMAINING UNCERTAINTY

1. Literal Fal `detail.msg` for the six ASD rejects is still redacted in peek (prior forensic).
2. Whether `face_image` or per-frame `bounding_boxes` would work was **not** tested (no Fal). OpenAPI lists them; examples do not.
3. `auto_detect: true` is unproven and would still be Fal choosing the face — not CHAR-003.
4. Graph `voiceAssets.artifactId` can still be wiped by hydrate (unchanged this suite).

---

## 12. PROVIDER CALLS

```
FAL:          0
ELEVENLABS:   0
VEO:          0
RUNWAY:       0
NEW MOTION:   0
NEW VOICE:    0
NEW OUTPUT:   0
```

**STOP.**
