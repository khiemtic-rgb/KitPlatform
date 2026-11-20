# FAMIXA PRODUCTION OS — CK23A LIPSYNC TIMING FORENSIC V1

Date: 2026-09-23  
Mode: READ-ONLY FORENSIC  
No MIX. No Fal Start. No CREATE_LIPSYNC. No artifact / snapshot / binding mutation.

Canonical: Build `4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f` · Shot `EP01-SC01-SH02`

---

## 1. ACTUAL VOICE TIMELINE

| Field | Value |
|---|---|
| ArtifactId | `0fde86c2-250f-4303-bda0-4913a6dbd1e4` |
| SHA256 | `bd348ffb84c5588335be647fc82dd37139b795d244dfdde0ea9e551ee3f6f298` |
| Bytes / MIME | 40586 · `audio/mpeg` (mp3 44100 Hz mono 128 kbps, stored as `.mp4` name) |
| DB DurationSec | 2.534 |
| ffprobe duration | **2.480s** |
| Line | `line-SC01-CHAR-001-2` · CHAR-001 · «(lý nhí) Dạ… nay con làm bài không tốt.» |

Decoded PCM (−45 dB, 50 ms hop):

| Mark | Seconds |
|---|---|
| VOICE_START (first energy ≥ −45 dB) | **0.10** |
| Speech span (−45 dB) | **0.25 → 2.50** |
| VOICE_END | **2.45** (file ends 2.48) |
| Peak | 2.25s · −11.45 dB |

Leading silence: none at −45 dB (only a 0.12s −35 dB dip 0.15–0.27). **OS did not prepend/append silence to the Voice artifact.**

---

## 2. ACTUAL MOUTH TIMELINE

LIPSYNC picture = VIDEO_TAKE picture. PSNR LIPSYNC vs VIDEO is **49.2–51.9** at every 0.5s sample (0–7.5s). Mouth-ROI MAE vs source is **< 0.6** until the paper-handoff (6.75+). Fal did **not** invent independent visemes.

Open-mouth frames are already on the mute Veo take:

| t | Mouth (VIDEO = LIPSYNC) |
|---|---|
| 0.0–2.5 | closed (reading the paper) |
| 3.0 | first visible opening |
| 3.25–4.0 | open gasp (Veo acting) |
| 6.5–7.5 | closed / looking down; paper taken (largest face-ROI motion at **6.75**, not speech visemes) |

| Mark | Seconds |
|---|---|
| MOUTH_START (first open) | **3.00** |
| MOUTH_ACTIVE | **3.00 → ~4.0** |
| MOUTH_END (gasp) | **~4.0** |
| Late acting (not speech) | **6.5 → 7.75** |

LIPSYNC muxed audio (not the Director overlay):

| Mark | Seconds |
|---|---|
| Digital silence | **0.00 → 1.839** |
| Speech (−45 dB) | **1.90 → 4.30** (span 2.05–4.35) |
| Trailing silence | **4.32 → 8.01** |
| Peak | 4.10s · −11.41 dB |

LIPSYNC video: 8.000s · h264 1280×720 @ 24 fps · **192 frames** (same as VIDEO).  
LIPSYNC audio: aac 44100 Hz mono · 8.000s.  
VIDEO: **mute** (no audio stream).

---

## 3. MEASURED OFFSET

| Comparison | Offset |
|---|---|
| LIPSYNC audio start − VOICE start | **1.839 − 0.10 = 1.74s** (`LIPSYNC_OFFSET_SEC`) |
| LIPSYNC speech length | 4.32 − 1.84 = **2.48s** (= Voice ffprobe length) |
| First open mouth − VOICE start | **3.00 − 0.10 = 2.90s** |
| First open mouth − LIPSYNC audio start | **3.00 − 1.84 = 1.16s** |

Fal copied the 2.48s Voice into the 8s output **after 1.84s of leading silence**, then **3.69s trailing silence**. It did not stretch the Voice to 8s.

Mouth open is Veo acting at ~3.0–4.0, not a Fal viseme track. It only overlaps the last ~1s of Fal audio. At 2.5s (mid Fal-audio) the mouth is still closed.

Director Play video (see § UI) does **not** play this Fal audio. It plays Voice from t=0 over mute VIDEO. That is why a listener hears speech immediately and sees the gasp later.

---

## 4. EXACT FAL INPUT

Trace (code + frozen snapshot + physical SHA). No Fal Usage GET this checkpoint.

```
CreateLipsyncAsync
  parents = ACCEPTED:VIDEO_TAKE + ACCEPTED/CURRENT:VOICE
  DurationSec = motion.DurationSec = 8
  ShotContract = line-SC01-CHAR-001-2|CHAR-001|<videoN>|<voiceN>|0.28|0.38|1280|720|0
  → FalLipSyncExecutionAdapter.ExecuteFrozenAsync
      VideoBytes = files.Read(os-veo-out/5a72a533ca30466e99b0357fa47bcdd8)
      VoiceBytes = files.Read(os-voice/9bd531131c244f2b84c25c392b1fb94d)
  → HttpFalLipsyncTransport.ExecuteOnceAsync
  → ContentFalClient.ExecuteProductionLipsyncOnceAsync
      UploadAsync(video, video/mp4, take.mp4)
      UploadAsync(audio, audio/mpeg, voice.mp3)
      CreateLipsyncAsync(..., sync_mode="silence", kind="v3")
  → POST fal-ai/sync-lipsync/v3
```

| Input | Artifact | SHA | Duration | Frames |
|---|---|---|---|---|
| video_url bytes | `b44e530b-…` | `21700e4f…476f34` | 8.000s mute | 192 |
| audio_url bytes | `0fde86c2-…` | `bd348ffb…f298` | 2.480s mp3 | — |

Snapshot `71b2bc55-fa5b-4dc1-aec8-99441ecb7b0e` · hash `e1d10cf3…cca5` · provider `fal` / `sync-lipsync-v3` · DurationSec **8**.

Pin (CK23 lock, T3 Minh left): **0.28, 0.38 @ 1280×720 frame 0**.

Serialized v3 body (CDN URLs not stored; shape from `LipsyncPayload`):

```json
{
  "video_url": "<fal upload of VIDEO_TAKE bytes>",
  "audio_url": "<fal upload of VOICE bytes>",
  "sync_mode": "silence",
  "options": {
    "active_speaker_detection": {
      "auto_detect": false,
      "bounding_boxes": [[278, 184, 438, 364], … × 192]
    }
  }
}
```

Box from `FamixaFalLipsyncSpeakerPinV1.FaceBoundingBox(0.28, 0.38, 1280, 720)` repeated `FrameCountFromTake` = **192**.  
No `offset`, `start`, `end`, `trim`, `adelay`, or duration field. Fal job `lipsync_v3_01a0cd8b-934c-7d33-9bb2-c3d9d6d574c2`. Local Fal cache SHA = LIPSYNC SHA (`fal/9f1aa23b08a16a6dcefc887f4a20fad8.mp4`).

---

## 5. EXACT PREPROCESSING

| Step | Happened? |
|---|---|
| ffmpeg atrim / apad / adelay on Voice | **No.** `FamixaFalLipsyncAudioPrepV1.KeepFullVoiceBytes(1)` → no pad. Production OS reads storage bytes. |
| Voice silence insert / normalize | **No.** Artifact RMS starts at t≈0.10. |
| Video trim / retime | **No.** 8.000s / 192 frames in = 8.000s / 192 frames out. |
| OS mux Voice onto VIDEO before Fal | **No.** VIDEO is mute; two separate uploads. |
| Snapshot timing fields | DurationSec=8 (video clock). No VOICE_START / offset stored. |

---

## 6. ROOT CAUSE

Two stacked facts. Do not collapse them.

### A. Director Play video is not the LIPSYNC file

`GetPreviewAsync` → `OfficialVideoAuthorityBridgeV1.ResolveAsync` prefers **ACCEPTED:VIDEO_TAKE** whenever it exists (CK21A).  
`osPreviewUrl` therefore hydrates the mute 8s Veo take.

`ShotProductionWorkspace` then:

- `osVisible` → `previewBind.lipsyncUrl = undefined`, kind = **motion**
- `muteTakeVoiceOverlay('motion', osVoiceUrl)` → **true**
- `<video muted>` + hidden `<audio src=VOICE>` with `audio.currentTime = video.currentTime`
- Caption: **«thoại phát cùng Play video (miệng chưa khớp)»**

Listener hears Voice **0.10–2.45**. Picture is mute Veo. Mouth opens at **3.0** (source gasp). That is the reported quality observation.

`VISIBLE` and `VISIBLE:LIPSYNC` are both `32bec770-…`. Preview authority ignores them while ACCEPTED:VIDEO_TAKE is present.

### B. Fal output is also not a start-aligned viseme take

Independently of the UI:

1. Fal placed Voice after **1.84s** leading silence (`sync_mode=silence` + 8s picture vs 2.48s audio).
2. Fal did **not** retarget the mouth (PSNR ~50 vs source; open mouth at 3.0–4.0 is already on VIDEO).
3. Snapshot DurationSec is the **video** clock (8), not the Voice clock (2.48). OS sent that as `speakerDurationSec` only to size 192 boxes. It did not send an audio offset. The 1.84s lead is **provider placement**, not an OS pad.

A–J:

| | Result |
|---|---|
| A Voice prepend/append | **No** on the artifact |
| B Audio preprocess duration/start | **No** |
| C Video trim/retime before Fal | **No** |
| D Wrong Fal files | **No** — SHA-locked parents |
| E Fal param that parks dialogue late | **`sync_mode=silence`** kept 8s and inserted **1.84s lead** (not a named `offset`) |
| F Voice clock vs video duration | Snapshot DurationSec = **8** (video). Output length follows video |
| G last-frame / end pad / OS offset | **No OS logic.** Fal trailing pad 3.69s + lead 1.84s |
| H Snapshot timing | DurationSec + pin token only |
| I requested vs provider vs output | Requested start-of-file Voice; output audio at 1.84; picture unchanged |
| J Fal behavior vs OS timing | **Both.** OS did not shift audio; Fal did. OS preview hides Fal audio |

---

## 7. WHETHER ROOT CAUSE IS

| Bucket | Status |
|---|---|
| OS timing | **Partial** — DurationSec anchored to 8s video; no start-of-shot audio contract |
| Audio preprocessing | **No** |
| Provider request | Correct bytes + `silence` + 192-frame boxes. No offset field exists to send |
| Provider behavior | **Yes** — 1.84s lead + no visemes |
| UI preview only | **Yes for the Play-video observation.** Overlay Voice @ 0 on mute VIDEO. **Not** sufficient to call the LIPSYNC file good |

---

## 8. RECOMMENDED FIX

Do **not** apply in this checkpoint. Do **not** MIX. Do **not** Start Fal.

1. **Preview authority:** once `CURRENT:LIPSYNC` / `VISIBLE:LIPSYNC` exists, Director must play that file **without** Voice overlay. CK21A ACCEPTED:VIDEO_TAKE stays the video-take authority; it must not own the lipsync player.
2. **Do not treat `32bec770-…` as mix-ready.** Picture = Veo acting. Fal audio ≠ start-of-shot. Mouth ≠ Voice.
3. A later lipsync (new CK only) would need a start-aligned Fal contract (e.g. `cut_off` to Voice length, or proven start pad) and a viseme check (face PSNR must drop vs source during speech). That is not authorized here.

---

## UI PREVIEW (requested)

| Player | Source |
|---|---|
| `<video>` | GET `/preview` media = **ACCEPTED:VIDEO_TAKE** `b44e530b-…` (mute 8s) |
| Overlay `<audio>` | CURRENT:VOICE `0fde86c2-…` from t=0 synced to video clock |
| LIPSYNC file audio | **Not played** on Play video |
| Caption | «Video · thoại phát cùng Play video (miệng chưa khớp)» |

Do not use that composition as Fal output evidence.

---

## PRODUCTION SAFETY

| Action | Count |
|---|---|
| Fal / ElevenLabs / Vertex / MIX POST | **0** |
| Artifact / snapshot / binding writes | **0** |
| Permit consume | **0** |

Scratch only: `.tmp/ck23a-lipsync-timing-forensic/` (frames + wav + RMS). Canonical files untouched.

Bindings still:

| Role | Artifact |
|---|---|
| ACCEPTED:VIDEO_TAKE | `b44e530b-5b59-422f-9b80-e058a3a639b5` |
| ACCEPTED:VOICE / CURRENT:VOICE | `0fde86c2-250f-4303-bda0-4913a6dbd1e4` |
| ACCEPTED:LIPSYNC / CURRENT:LIPSYNC / VISIBLE:LIPSYNC / VISIBLE | `32bec770-9706-4aad-bc21-8db3d4f11d4c` |

---

## VERDICT

**CK23 QUALITY = FAIL**  
**CK24 = BLOCKED**

LIPSYNC artifact existence is not enough. Mouth is Veo acting, late vs Voice. Director Play video is a Voice overlay on the mute take, not the Fal mux.

STOP. Do not start MIX / CK24.
