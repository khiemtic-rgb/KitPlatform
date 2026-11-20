# FAMIXA_SH01_LIPSYNC_QUALITY_FORENSIC_V1_REPORT

**Mode:** READ-ONLY forensic. No Fal / ElevenLabs / Veo / Runway. No generate. No graph write. No Final. No approve.

**Verdict:** We did **not** put Take 55 + Voice artifact `vpa-20260915T161911Z-e0b3093f` + a proven Linh pin into Fal. The playing 6s LipSync is Fal applied onto **Take 56 LATE** (6.000s, 144 frames). Linh’s mouth in the output mostly follows that source take, not the speech. Exact Fal `json_input` URLs/bytes are **UNKNOWN** (no stored Usage dump).

---

## 1. Actual Fal input

Locked output (desk preview):

| Field | Value |
|---|---|
| taskId | `lipsync_v3_01a0a327-ec7b-79b2-9f02-192781f6946f` |
| provider | Fal |
| model (from prefix + prior poll) | `fal-ai/sync-lipsync/v3` |
| physical | `App_Data/kit-video-i2v/fal/06a032f474624bca2d0b609572b6d545.mp4` |
| duration / codec | **6.000s** · h264 1280×720 @ **24 fps · 144 frames** · aac 48 kHz stereo **6.000s** |
| byteLength / SHA256 | 3743506 · `55515811DB64741B6E4B3CB3AAA2085BE2D8450B118295ADB3064E661AA676BA` |
| persist mtime | 2026-09-15 **10:43:43** local |

### A. Video input (what Fal actually warped)

No CDN `video_url` is stored. Physical compare of output frames vs local Veo files:

| t | PSNR avg vs Take 55 | PSNR avg vs Take 56 |
|---|---|---|
| 0s | 45.19 | **50.07** |
| 1s | 26.56 | **45.25** |
| 2s | 26.60 | **46.97** |
| 3s | 22.52 | **47.37** |
| 4s | — | **46.39** |
| 5s | n/a (Take 55 ends at 4s) | **46.35** |

Take 55 = `d006770ca0f75a00fb8442567cad5471.mp4` · **4.000s · 96 frames** · mute.  
Take 56 = `7e8e90b4f823be0cb99f9e3e7eac0fea.mp4` · **6.000s · 144 frames** · mute.

Fal output frame count = Take 56 frame count (144), not Take 55 (96). At 2s the acting matches Take 56 (Linh looking down) and **does not** match Take 55 (Minh looking up, Linh facing him).

**Fal video input = Take 56 physical MP4. Not Take 55.**  
Upload URL after `_fal.UploadAsync`: **UNKNOWN**.

Graph freeze `mot:EP01-SC01-SH01:55` is the **intended** bind, not the file Fal received.

### B. Audio input

Locked Voice artifact `vpa-20260915T161911Z-e0b3093f`:

| Field | Value |
|---|---|
| path (recovery copy) | `.tmp/sh01-voice-recovery-v1/tts.mp3` |
| bytes / SHA256 | 69007 · `E0B3093F093D6B36DEFA1DF40E3B42EB2FC94B216C5DA697FAE6593178A1001C` |
| format | mp3 · 44100 Hz · **mono** · **4.240s** |
| created | 2026-09-15 **23:19:11** local (`161911Z` in artifactId) |

Fal job persisted **10:43** the same day — **~12.5 h earlier**. This artifact **did not exist** when Fal ran.

Recovery already recorded a prior Chrome blob SHA `741D5543…` ≠ `E0B3093F…` (same 69007 B is coincidence).

Fal output audio envelope (−45 dB): silence **0.00–0.70s**, speech **0.70–4.93s** (≈ **4.23s**), silence **4.93–6.02s**. Speech length matches a 4.24s line, not the artifact identity.

**Audio bytes/URL Fal received: UNKNOWN.**  
**Same as `vpa-20260915T161911Z-e0b3093f`: no.**

### C–G. Model / payload / speaker / URLs

No local `json_input` / Usage dump for `01a0a327`.

| Field | Evidence | Value |
|---|---|---|
| model | task prefix `lipsync_v3_` | `fal-ai/sync-lipsync/v3` |
| `video_url` / `audio_url` | Fal CDN after upload | **UNKNOWN** |
| `sync_mode` | C# default if omitted = `silence`; Fal schema default `cut_off` | **UNKNOWN** (not dumped) |
| `options.active_speaker_detection` | Six earlier v3 jobs **with ASD failed validation**; this job **produced mp4** | **UNKNOWN**; consistent with **ASD omitted** |
| face bbox / target | only sent if `speakerX/Y` in [0,1] | **UNKNOWN** |
| `auto_detect` / `coordinates` | — | **UNKNOWN** |

`ContentFalClient.LipsyncPayload` v3 without pin:

```
{ video_url, audio_url, sync_mode }
```

With pin: `options.active_speaker_detection = { auto_detect:false, frame_number:0, coordinates:[px,py] }`.

`skipSpeakerPin` exists on **later** desk code (`forceNew` / dead Fal). **Not proven** for this request_id.

---

## 2. Motion forensic

### Claimed bind — Take 55

`mot:EP01-SC01-SH01:55` · `d006770ca0f75a00fb8442567cad5471.mp4`

| Field | Value |
|---|---|
| duration | 4.000s |
| resolution / fps | 1280×720 · 24 · 96 frames |
| audio | **none** |
| SHA256 | `834C116A2DDFF1E6DAA09D705D5BCA2A7E6E902B3058AE5395286AA075B9BA50` |

**Faces (visual, t=0s):** **2** — Minh left, Linh right. Expected speaker = Linh (CHAR-003).

| Linh (Take 55, t=0) | |
|---|---|
| position | right half; head ≈ x 0.58–0.78, y 0.08–0.42 |
| relative size | larger than Minh (closer) |
| angle | 3/4, looking left toward Minh |
| occlusion | none |
| mouth | visible, slightly open — usable for LipSync |

At **t=2s Take 55** Linh still faces Minh (mouth visible). Minh looks up, mouth slightly open.

### Video Fal actually used — Take 56 LATE

`7e8e90b4f823be0cb99f9e3e7eac0fea.mp4` · 6.000s · 144 frames · mute · SHA256 `7EF2ECAD89EAFA3FB079546FB5DF74D917EBB8C1A2B6905E773F48A8753A0596`

Same two-shot. During the **speech window** Linh **looks down (t=2–3s)** then **profile (t=4–5s)**. Mouth is small / edge-on. Minh’s mouth is already open in the **source** at 3s and 4s (Veo acting, not Fal inventing it).

Fal did **not** receive a clear single target face. Two faces, and the speaker’s mouth is often not frontal on the clip that was sent.

---

## 3. Voice forensic

| Field | Locked artifact | Fal output audio |
|---|---|---|
| duration | 4.240s | 6.000s (speech ≈ 4.23s) |
| sample rate | 44100 mono | 48000 stereo |
| leading silence | none at −45 dB | **0.70s** |
| trailing silence | none (file ends) | **1.09s** (4.93–6.02) |
| identity | `vpa-20260915T161911Z-e0b3093f` | **UNKNOWN** |

Dialogue text (graph / Recovery, not re-ASR’d):  
`Về rồi hả con? Sao cái mặt tiu nghỉu như mất sổ gạo thế kia?`

Speech on the 6s output sits at **[0.70, 4.93]** with pad after. Not flush to t=0.

---

## 4. Output forensic

Desk `<video>` is this Fal file (prior binding mission: blob size 3743506, duration 6). **G. OUTPUT_PLAYBACK is not the quality bug.**

Video body/camera = Take 56. Audio track present and intelligible. Mouth of Linh does **not** open/close with that audio (see §8).

---

## 5. Face target

| Question | Answer |
|---|---|
| Did the payload name Linh? | **UNKNOWN** — no dump |
| Does v3 require a pin for 2 faces? | Contract forensic: official Fal examples are `video_url`+`audio_url` only; ASD on this endpoint was **rejected** on earlier jobs |
| Was Linh the only moving mouth? | **No.** Minh’s open mouth at 3s/4s is **already in Take 56**. Fal kept it. |
| Did Fal drive Linh visemes? | At 2s and 3s (mid-speech) Linh’s mouth is **closed / slight smile**, same as source 56. **No independent speech mouth.** |

Preset `lipsyncSpeakerSidePreset('right')` = `{x:0.66, y:0.38}` → pixels `[845, 274]` on 1280×720 — **reconstruction only**. Not proven sent on `01a0a327`.

---

## 6. Temporal alignment

| Stream | Seconds | Frames |
|---|---|---|
| Voice artifact (now) | 4.24 | — |
| Take 55 (claimed) | 4.00 | 96 |
| Take 56 (actual video in) | 6.00 | 144 |
| Fal out | 6.00 | 144 |

- Fal video in: **6.00s** (Take 56).  
- Fal audio in: **UNKNOWN** length; output speech ≈ 4.23s + 0.70s lead + 1.09s tail ⇒ **padded to 6s**.  
- Current merge path can `apad`/`-t` to `performanceDurationSec` (last non-late take duration, else 6 if 56 were used as clock). **Whether that exact merge ran on this job: UNKNOWN.**  
- Stretch/fit: output video frame count = source 56. **No evidence of time-stretching the picture.** Audio was fitted into 6s.  
- Take 55 vs 4.24s voice would have been a 0.24s overrun; that pair was **not** the pair Fal rendered.

Pre-roll **0.70s** ≠ current default `speechStartSec` 0.20. Origin **UNKNOWN**.

---

## 7. Payload

Reconstructed **capability** of `ContentSeriesTurboService.StartLipsyncAsync` (read code, not this job’s HTTP):

1. Fetch take bytes (`FetchTakeForLipsyncAsync` — Veo FileId if `takeTaskId` is stored).  
2. Merge/pad voices if `PerformanceDurationSec > 0` or multiple cues.  
3. `UploadAsync` video + audio → Fal CDN.  
4. `CreateLipsyncAsync` one queue POST.

**This job’s literal JSON: UNKNOWN.**  
Most likely envelope for a SUCCESS v3 after ASD failures: `{ video_url, audio_url, sync_mode }` **without** ASD.

Mechanism that would select Take 56: graph `takeUrl` / `previewUrl` / `acceptedTake` 56 / `turboTaskId` of the LATE Veo op — `FetchTakeForLipsyncAsync` reads that FileId. Freeze `mot:55` was **not** what got uploaded.

---

## 8. Frame evidence

Extracts: `.tmp/sh01-lipsync-quality-forensic/fal_{0-5}s.jpg` vs `t55_*` / `t56_*`.

| t | Audio | Linh mouth (Fal) | vs Take 56 | Minh |
|---|---|---|---|---|
| 0s | silence | slight open | same blocking as 56 | closed |
| 1s | speech | slight open | ≈ 56 | closed |
| 2s | speech | **closed, looking down** | **same as 56** (not 55) | closed |
| 3s | speech | **closed smile, looking down** | **same as 56** | **open (already on 56)** |
| 4s | speech | **closed, profile** | **same as 56** | open (already on 56) |
| 5s | silence | closed profile | same as 56 | slight open |

Linh mouth **does** change a little with source acting, **not** continuously with consonants/vowels. No face-drift / melt artifacts visible on these stills.

---

## 9. Audio evidence

Fal wav (−35 dB): extra micro-gap 1.84–2.03 (phrase boundary). At −45 dB that gap disappears → one speech block **0.70–4.93**.

Voice mp3: no silence at −45 dB (continuous 4.24s).

Dialogue placement on the 6s output: **[0.70, 4.93]**. Tail pad **[4.93, 6.00]**.

---

## 10. Root cause classification

| Code | Status |
|---|---|
| **A. WRONG_VIDEO_INPUT** | **YES** vs claimed Take 55. Fal video = Take 56 LATE. |
| **B. WRONG_AUDIO_INPUT** | **YES vs locked `vpa-…e0b3093f`** (artifact after the job). Bytes Fal used: **UNKNOWN**. Envelope fits *a* 4.24s line + 6s pad. |
| **C. WRONG_FACE_TARGET** | **UNPROVEN pin.** Two faces. Linh not viseme-driven. Minh open mouth is **source 56**, not a proven Fal retarget. |
| **D. TEMPORAL_ALIGNMENT** | **YES on the rendered pair:** 4.23s speech inside 6s picture; 0.70s lead; Linh down/profile during speech. |
| **E. FAL_MODEL_LIMITATION** | **Not isolated.** A/C/D still open. Remaining after A: even on 56, Linh mouth ≈ source, not audio. |
| **F. PAYLOAD_CONFIGURATION** | **Likely no ASD** (success after ASD validation failures). `sync_mode` **UNKNOWN**. |
| **G. OUTPUT_PLAYBACK** | **No.** Desk plays this file. |
| **H. UNKNOWN** | Exact CDN URLs, `sync_mode`, speaker coords, audio SHA Fal uploaded. |

**Question asked:** *Did we send the right video + right voice + right speaker?*

| Input | Sent to Fal? |
|---|---|
| Take 55 (`mot:…:55`) | **No** — Take 56 went. |
| Voice `vpa-20260915T161911Z-e0b3093f` | **No / unprovable** — artifact later; SHA not in the job. |
| Linh as target | **Not proven.** Payload pin UNKNOWN. |

If those three had been true, leftover risk would be Linh’s 3/4 + downward + profile on a 2-face 6s take, plus v3 without a working ASD. That is **not** the first failure. The first failure is **wrong picture file**.

---

## 11. Whether retry is technically justified

**Yes, as a technical statement only** — this phase does **not** retry and does **not** change model/payload.

A later send would be a **different job** only if it uploaded Take 55 bytes + the locked Voice bytes + a documented Linh pin (or a 1-face crop). This report does not authorize that POST.

---

## 12. Remaining uncertainty

1. Literal Fal `json_input` / `json_output` for `01a0a327`.  
2. SHA of the audio that was uploaded (pre-Recovery TTS).  
3. Whether `sync_mode` was `silence` or `cut_off`.  
4. Whether any speaker coordinates were present.  
5. Why lead is 0.70s vs current 0.20s `speechStartSec`.  
6. How much of Linh’s still mouth is “v3 didn’t retarget” vs “source 56 already closed + profile.”

---

## Safety

| Provider / write | Count |
|---|---|
| Fal / ElevenLabs / Veo / Runway | **0** |
| Graph writes | **0** |
| Production media writes | **0** |

Frame/audio extracts only under `.tmp/sh01-lipsync-quality-forensic/` (scratch). Production `fal/` and `veo/` files not modified.

---

## STOP
