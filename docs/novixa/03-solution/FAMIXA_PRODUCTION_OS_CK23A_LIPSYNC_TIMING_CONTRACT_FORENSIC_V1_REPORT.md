# FAMIXA PRODUCTION OS — CK23A LIPSYNC TIMING CONTRACT FORENSIC V1

Date: 2026-09-23  
Mode: READ-ONLY. No Fal Start. No CREATE_*. No MIX. No snapshot/binding/artifact mutation.

CK23 remains: EXECUTION = PASS · QUALITY = FAIL · MIX = BLOCKED.

---

## 1. CURRENT LIPSYNC REQUEST CONTRACT

Official SH02 path (CK23), not the desk turbo `FalLipSyncProvider`.

```
ProductionCreateVideoService.CreateLipsyncAsync
  file: ProductionCreateVideoService.Actions.cs
  DurationSec = motion.DurationSec  →  8
  ShotContract = ProductionLipsyncFreeze.Token(...)
  OpenFrozenAsync → ProductionTaskInputSnapshot
  FalLipSyncExecutionAdapter.ExecuteFrozenAsync
    file: FalLipSyncExecutionAdapter.cs
  HttpFalLipsyncTransport.ExecuteOnceAsync
    DTO in: FalLipsyncTransportRequest
    file: IFalLipsyncTransport.cs / HttpFalLipsyncTransport.cs
  ContentFalClient.ExecuteProductionLipsyncOnceAsync
    UploadAsync(video) + UploadAsync(audio)
    CreateLipsyncAsync(..., syncMode: "silence", modelKind: "v3")
    LipsyncPayload → JSON
  download / persist Fal mp4
  MaterializeMediaAsync writes executed.Bytes unchanged
```

### Domain objects

| Object | File | Timing-relevant fields |
|---|---|---|
| `ShotExecutionInput` | `ShotExecutionInput.cs` | `DurationSec`, `Timing` (string). No VoiceStart / SpeechStart / AlignmentMode |
| `ProductionTaskInputSnapshot` | `ProductionEntities.cs` | `DurationSec`, `TimingHash`, `ShotContract`, `ParentArtifactIds` |
| `ProductionLipsyncFreeze` | `ProductionLipsyncFreeze.cs` | `line\|speaker\|videoId\|voiceId\|x\|y\|w\|h\|frame` — **no seconds** |
| `FalLipsyncTransportRequest` | `IFalLipsyncTransport.cs` | `VideoDurationSec` (decimal) — **not** a Fal JSON field |
| `FamixaLipSyncStartRequest` | `FamixaProviderOrchestrationV1Rules.cs` | Desk turbo only: `SyncMode`, `SpeakerDurationSec`, `SpeakerFrameCount`. **Not used by CK23 official adapter** |

CK23 frozen snapshot `71b2bc55-…`:

- ProviderId `fal` · ModelId `sync-lipsync-v3`
- DurationSec **8**
- ShotContract `line-SC01-CHAR-001-2|CHAR-001|b44e530b5b59422f9b80e058a3a639b5|0fde86c2250f4303bda04913a6dbd1e4|0.28|0.38|1280|720|0`
- Parents: VIDEO `b44e530b-…` + VOICE `0fde86c2-…`

`DurationSec = 8` is the **video clock**. It does not define speech alignment.

---

## 2. CURRENT FAL REQUEST

Hard-coded in `ExecuteProductionLipsyncOnceAsync` (`ContentFalClient.cs`):

- endpoint `fal-ai/sync-lipsync/v3`
- `sync_mode` literal `"silence"` (not read from snapshot)
- `kind` `"v3"`

Serialized by `LipsyncPayload` when pin resolves (`TryActiveSpeakerDetection` true):

```json
{
  "video_url": "<Fal CDN after UploadAsync of VIDEO_TAKE bytes>",
  "audio_url": "<Fal CDN after UploadAsync of VOICE bytes>",
  "sync_mode": "silence",
  "options": {
    "active_speaker_detection": {
      "auto_detect": false,
      "bounding_boxes": [[278, 184, 438, 364], … × 192]
    }
  }
}
```

Box = `FaceBoundingBox(0.28, 0.38, 1280, 720)`. Count = `FrameCountFromTake(videoBytes, 8)` = **192**.

**Literal HTTP body for job `01a0cd8b-…` is not stored.** Reconstruction is from current C# serialize contract. CDN URLs: UNKNOWN (not logged).

`JsonOpts`: `DefaultIgnoreCondition = WhenWritingNull`. Null properties are omitted.

---

## 3. ALL EXPLICIT + IMPLICIT TIMING PARAMETERS

### Sent (explicit)

| Parameter | Value | Source |
|---|---|---|
| `video_url` | upload of 8.000s mute VIDEO | storage bytes, no ffmpeg |
| `audio_url` | upload of 2.480s VOICE | storage bytes, no ffmpeg |
| `sync_mode` | `silence` | hard-coded in `ExecuteProductionLipsyncOnceAsync` |
| `options.active_speaker_detection.auto_detect` | `false` | pin present |
| `options.active_speaker_detection.bounding_boxes` | 192 × `[278,184,438,364]` | pin + take frame count |

### Used in C# but **not** serialized to Fal JSON

| Name | Value | Role |
|---|---|---|
| `FalLipsyncTransportRequest.VideoDurationSec` | 8 | fallback for `FrameCountFromTake` only |
| `speakerDurationSec` argument | 8 | unused when `speakerFrameCount=192` |
| Snapshot `DurationSec` | 8 | identity/cost clock, not a Fal field |

### OpenAPI fields listed in-repo (2026-09-14 capture) that this request **omits**

Do not infer Fal defaults except where that capture states one.

| Field | In OpenAPI | Sent? | Documented default in-repo |
|---|---|---|---|
| `sync_mode` | yes | **yes = silence** | Fal default if omitted = `cut_off` (not applicable; we send silence) |
| `options.sync_mode` | yes | no | UNKNOWN FROM REPOSITORY |
| `options.model_mode` | yes | no | UNKNOWN FROM REPOSITORY |
| `options.prompt` | yes | no | UNKNOWN FROM REPOSITORY |
| `options.temperature` | yes | no | marked ignored by sync-3 in that capture |
| `options.occlusion_detection_enabled` | yes | no | marked ignored by sync-3 in that capture |
| ASD `v3` / `use_v2` / `frame_number` / `coordinates` / `bounding_boxes_url` / `face_image` | yes | no | UNKNOWN FROM REPOSITORY |

### Parameters **not present** on the 2026-09-14 Fal v3 OpenAPI capture

`offset`, `start`, `start_time`, `end`, `end_time`, `trim`, `pad`, `adelay`, `duration`, `audio_start`, `alignment`.  
DTO `LipsyncPayload` cannot represent them. **Do not invent them.**

---

## 4. CURRENT DOMAIN TIMING CONTRACT

Inspected fields vs the asked names:

| Name | Present? |
|---|---|
| VideoStartSec | **No** |
| VideoDurationSec | **No** as a named field. Snapshot `DurationSec=8` is the video clock |
| VoiceStartSec | **No** |
| VoiceDurationSec | **No** (Voice artifact DurationSec 2.534 lives on the VOICE row, not the LIPSYNC snapshot) |
| SpeechStartSec | **No** |
| SpeechEndSec | **No** |
| LipSyncStartSec | **No** |
| LipSyncEndSec | **No** |
| AlignmentMode | **No** |
| AudioPaddingMode | **No** |

`ShotContract` is identity + pin, not a timeline.

**The current contract is insufficient.** `DurationSec = 8` says how long the picture clock is. It does not say where speech must sit on that clock.

---

## 5. WHERE THE +1.74s OFFSET IS CREATED

Classification of the **LIPSYNC artifact audio** (not Director preview):

| | Created there? | Evidence |
|---|---|---|
| **A. Famixa before provider** | **No** | Voice RMS starts 0.10; `KeepFullVoiceBytes(1)`; raw `files.Read`; no atrim/apad |
| **B. Provider because of request/config** | **Yes — location** | Output audio 1.839→4.32; length 2.48s = Voice; lead 1.84s + tail 3.69s appear first in Fal mp4 |
| **C. Response handling** | **No** | `TryDownloadOutputAsync` / recover copies bytes; Fal cache SHA = artifact SHA `9d8e6949…` |
| **D. Artifact mux/materialization** | **No** | `MaterializeMediaAsync` writes `executed.Bytes` |
| **E. Preview overlay only** | **No** for this offset | Offset is on `32bec770-…` / `fal/9f1aa23b….mp4` independently |

**Answer: B.**

Whether `sync_mode=silence` **caused** the 1.84s *lead* (vs trailing-only pad, vs visual align) is **UNKNOWN FROM REPOSITORY**. Enum name is sent; pad math is not specified in-repo. Famixa code did **not** write a 1.74s delay into the Voice bytes.

Director CK21A overlay remains a **separate** defect. It does not create this artifact offset.

---

## 6. WHETHER FAL ACTUALLY MODIFIED MOUTH PIXELS

| Check | Result |
|---|---|
| Full-frame PSNR LIPSYNC vs VIDEO (0–7.5s / 0.5s) | **49.2–51.9** |
| Mouth-ROI MAE vs VIDEO same t | **< 0.6** until paper-handoff 6.75+ |
| Open mouth at 3.0–4.0 | **Identical on VIDEO** (Veo gasp) |
| Closed mouth at 0–2.5 (during early Fal audio) | **Identical on VIDEO** |
| Frame count | 192 = 192 |

Residual PSNR ~50 is consistent with **re-encode**, not a viseme pass.

Fal returned a muxed mp4 (picture ≈ source + new audio track). **Independent viseme drive is not observed.**  
Do not call this lip-sync success.

---

## 7. PROVIDER DOCUMENTATION EVIDENCE

Sources **in this repository** (no new network):

| Source | What it states about timing |
|---|---|
| `FAMIXA_FAL_V3_LIPSYNC_CONTRACT_FORENSIC_V1_REPORT.md` (OpenAPI 2026-09-14) | `sync_mode` enum `cut_off\|loop\|bounce\|silence\|remap`; **default `cut_off` if omitted**. No pad/trim math |
| Same report | Fal llms.txt examples = `video_url` + `audio_url` (+ optional `sync_mode`) only |
| `FAMIXA_FAL_V3_TARGETED_SPEAKER_INPUT_FIX_V1_REPORT.md` §12.3 | “`sync_mode=silence` semantics … exact pad-vs-trim behavior vs Fal default `cut_off` is **not specified**” |
| `FamixaFalLipsyncAudioPrepV1.cs` comment | “Fal v3 documented input is video_url + audio_url — **no duration trim field**” |
| `famixa-video-audio-lipsync-pipeline-audit-v1.md` | Lists the enum. Desk UI default `remap`; older server default if empty `cut_off`. **No definition of silence** |
| `FAMIXA_REAL_FILM_RUN_V1_EP01_SH01_LIPSYNC_REPORT.md` | Internal gloss: `silence` = “no trim” — **not a Fal quote** |
| CK23 Usage `json_input` | **Not in workspace** |

No generated Fal OpenAPI/SDK file lives in the repo.

---

## 8. UNKNOWN / NOT PROVEN

1. Literal Fal `json_input` / `json_output` for `lipsync_v3_01a0cd8b-934c-7d33-9bb2-c3d9d6d574c2`.
2. What Fal **means** by `silence` (lead pad vs tail pad vs match-longer vs align-to-motion).
3. What `cut_off` / `loop` / `bounce` / `remap` would have done to this 8s / 2.48s pair.
4. Whether omitted `options.*` changed alignment.
5. Whether 192 full-take `bounding_boxes` affected placement.
6. Whether muxed audio is Fal’s “authoritative” product or a convenience mux — **UNKNOWN FROM REPOSITORY**. Output file is what we stored; visemes were not returned as a side channel.
7. Whether a later Fal schema added `offset` / `start_time` after 2026-09-14.

External docs that would need a **human read** (not a paid Start): current `fal-ai/sync-lipsync/v3` OpenAPI field descriptions, llms.txt, and Usage JSON for this request_id.

---

## 9. POSSIBLE FIX OPTIONS

Not implemented. Not selected.

### OPTION A — Fix request/configuration

- Change: e.g. send `cut_off` (or another enum value) instead of hard-coded `silence`; or stop repeating boxes for all 192 frames.
- Unchanged: domain snapshot shape; Voice/Video artifacts.
- Risks: `cut_off` math UNKNOWN; may shorten output to ~2.48s or still fail visemes.
- New artifact: **yes**. Snapshot schema: **no** if only transport constant changes.
- CK23 artifact: **evidence only**.

### OPTION B — Normalize/prepare voice timing before provider

- Change: pad/shift Voice bytes so speech sits at a chosen start before upload.
- Unchanged: Fal model id; VIDEO take.
- Risks: invents timing Famixa does not currently own; still no viseme guarantee.
- New artifact: **yes**. Snapshot schema: **no** unless the pad becomes a frozen field.
- CK23 artifact: **evidence only**.

### OPTION C — Change LipSync provider/model

- Change: different Fal model or another vendor.
- Unchanged: VIDEO/VOICE parents if rebound.
- Risks: new contract, cost, quality unknown.
- New artifact: **yes**. Snapshot: provider/model freeze would differ (not a SQL schema change by itself).
- CK23 artifact: **evidence only**.

### OPTION D — Change LipSync contract to carry timeline alignment

- Change: add explicit SpeechStart/End or AlignmentMode on the domain snapshot (new fields or token).
- Unchanged: existing CK23 row.
- Risks: schema/token design; still needs a provider that honors it.
- New artifact: **yes** for a new run. Snapshot schema: **likely yes** if columns/token change.
- CK23 artifact: **evidence only**.

### OPTION E — Adapter semantics, domain contract stable

- Change: adapter maps current `DurationSec=8` + Voice length to a documented Fal mode without new snapshot columns.
- Unchanged: `ProductionTaskInputSnapshot` shape.
- Risks: hidden policy; same UNKNOWN Fal math.
- New artifact: **yes**. Schema: **no**.
- CK23 artifact: **evidence only**.

No option is objectively mandatory from in-repo Fal docs. **B is not required to explain the offset** (Famixa did not pad). A/E need Fal’s silence/cut_off text before they are more than guesses. D is the gap in **our** contract regardless of vendor.

---

## 10. RECOMMENDED NEXT FORENSIC STEP

Human-only, no Start:

1. Fal Dashboard → Usage → request `01a0cd8b-934c-7d33-9bb2-c3d9d6d574c2` → save `json_input` + `json_output` (redact CDN if needed).
2. Read **current** Fal OpenAPI / llms.txt **descriptions** of `sync_mode` values (not a queue POST).
3. After that, decide A vs E vs “vendor cannot start-align.” Do not MIX. Do not CREATE_LIPSYNC until that text exists.

---

## SAFETY

Fal Start = 0 this checkpoint. ElevenLabs / Vertex / MIX = 0. CK23 bytes/bindings unchanged.

---

## VERDICT

**INCONCLUSIVE**

- Offset **location** is established: **B (provider output)**, not Famixa A/C/D, not preview E.
- Domain timing contract is established as **insufficient** (`DurationSec=8` only).
- Provider **semantics** of `silence` / alignment / omitted options: **UNKNOWN FROM REPOSITORY**.

Not FAIL: Famixa code did not write +1.74s into the Voice or the stored LIPSYNC bytes after download.

Do not call the artifact good.  
CK23 EXECUTION = PASS · QUALITY = FAIL · MIX = BLOCKED.

STOP.
