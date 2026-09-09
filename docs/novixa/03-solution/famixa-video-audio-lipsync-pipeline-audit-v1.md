# FAMIXA_VIDEO_AUDIO_LIPSYNC_PIPELINE_AUDIT_V1

**Status:** READ-ONLY AUDIT  
**Date:** 2026-09-04  
**Scope:** Famixa Series production path (staff + director) + KIT Video Engine I2V + TTS + Fal + FFmpeg  
**Guarantee:** Gemini calls = 0 · Video generation = 0 · TTS calls = 0 · Lip-sync calls = 0 · New media artifacts = 0 · Database mutation = 0 · Authority mutation = 0  

No code was modified. No provider was invoked. Side-effect on read: none observed.

---

## 1. Executive Summary

Famixa **does not generate talking video in one step**.

The live production path for EP/Shot (staff Video Studio, Famixa Series Turbo) is:

1. Gemini still → **approved keyframe** (identity + Visual Mode live here).
2. Runway **`gen4_turbo` image-to-video** animates that JPEG. Prompt is **motion-only**. Output is **mute**.
3. ElevenLabs TTS (if Voice step ran) produces MP3 **after or beside** I2V, not inside I2V.
4. Fal **audio-conditioned lipsync** is a **separate paid step**. Staff UI **never calls it**.
5. Assemble either overlays TTS on mute takes, or keeps Fal audio. Room/Foley/music/LUFS are **compiled in the client and ignored by the server**.

That architecture explains the six symptoms:

| Symptom | Evidence |
|---|---|
| Video quality kém | I2V = `gen4_turbo` 5\|10s + generic “blink and breathe” + no last frame + no audio cond + later `libx264 crf 20` re-encode |
| Ghép tiếng / timing kém | Line `startSec`/`endSec` from measured MP3 or `chars/12`; I2V locked to 5\|10; staff stitch used empty cues |
| Môi không khớp lời | I2V never sees audio. Staff mix is TTS overlay. Fal is unused on staff path. No phoneme/viseme |
| Character drift in video | Identity/VUA/CDL **not** in Runway payload; consistency depends on KF pixels + model |
| A/V lệch | Three clocks (I2V 5\|10, TTS duration, assemble speech-fit / Fal take-fit); `adelay` ms; concat `-c copy` fallback re-encode; no `-vsync`/`-shortest` |
| Sai reference / mode | I2V input is **scene KF JPEG**, not Character Studio sheet. VisualMode gates compile, then is **stripped** from `promptText` |

**Generation mode in production:** **C. Keyframe → Video** (implemented as **B. Image → Video**). Not T2V. Not character-reference-to-video.

**Lip-sync class:**  
- Staff EP path in use: **E. NONE** (mute take) or **D-adjacent overlay** (TTS on mute picture).  
- Director Fal path (exists, not staff-wired): **B. AUDIO-CONDITIONED LIP SYNC**.  
- **A. TRUE PHONEME/VISEME:** does not exist in this repo.

---

## 2. Current Architecture

Two I2V lanes share one provider client.

```
Famixa Series (staff + studio)
  ContentFamixaSeriesTab.sendTurbo
    → POST /content/series/turbo
    → ContentSeriesTurboService.StartAsync
    → ContentRunwayClient.CreateImageToVideoAsync
    → Runway POST v1/image_to_video

KIT Video Engine (director contracts)
  executeVideoGenerationExecution
    → VideoGenerationExecutionService.ExecuteAsync
    → RunwayVideoGenerationProvider.GenerateAsync
    → same ContentRunwayClient
```

Audio / lipsync / export live **only** on the Famixa Series graph, not inside Phase 06 ENGINE execute.

```
Voice Script (deriveVoiceScript)
  → ElevenLabs POST /v1/text-to-speech/{voiceId}   [optional; staff has no Voice tab]
  → Runway mute take
  → Fal fal-ai/sync-lipsync                        [director only]
  → ContentSeriesAssembleService (FFmpeg)          [director assembleCut]
    or recordAssembledCut (browser WebM)           [staff mixStaffEpisode / fallback]
```

---

## 3. Actual End-to-End Flow

```
SCRIPT
  ContentFamixaScriptDesk / parseEpisodeStory
  state.scriptLocked
        ↓
EPISODE
  episodeShots(state) · pack_content.series_build / series_pilot
        ↓
SCENE / SHOT
  content-famixa-scene-first · dialogueSegmentIds · applyDialogueMap
        ↓
VOICE (director / studio pane — NOT a staff tab)
  deriveVoiceScript → loadCueAudio → previewContentSeriesTts
  ContentElevenLabsClient.SynthesizeMp3Async
  IndexedDB kit-famixa-tts · voiceAssets[lineId].duration
        ↓
KEYFRAME
  ContentSeriesStillService / Gemini still
  Character Studio LOCKED refs + VUA compile (still path)
  kfIsApprovedStill → keyframeDataUrl
        ↓
CHARACTER REFERENCE
  Enters KF only (lockedStudioClaim / identity-anchor)
  NOT sent to Runway
        ↓
VIDEO GENERATION
  prepareRunwayKf (JPEG 1280×720)
  compileRunwayPromptV1 (motion only)
  ContentSeriesTurboService.StartAsync
  ContentRunwayClient.CreateImageToVideoAsync
  stampMuteTake
        ↓
LIP-SYNC (optional, director)
  startLipsync → StartLipsyncAsync → ContentFalClient.CreateLipsyncAsync
  stampFalFinal
        ↓
COMPOSITE / EXPORT
  assembleCut → assembleContentSeriesCut → ContentSeriesAssembleService.AssembleAsync
  OR mixStaffEpisode / EpisodeReel.stitch → recordAssembledCut
        ↓
FINAL MP4 or WebM (download; no auto-publish)
```

Staff EP99 path used in this conversation **stopped at mute I2V + mute stitch**, then gained **TTS overlay** (`mixStaffEpisode`). Fal was not called.

---

## 4. Video Generation Path

### Mode

| Mode | Used? | Evidence |
|---|---|---|
| A. Text → Video | No | No T2V endpoint |
| B. Image → Video | **Yes** | `POST v1/image_to_video` + `promptImage` |
| C. Keyframe → Video | **Yes** (same as B) | Source = approved `keyframeDataUrl` |
| D. Character reference → Video | **No** | No char refs in Runway JSON |
| E. Hybrid | Partial | KF pixels carry identity; prompt does not |
| F. Other | Wan I2V | `fal-ai/wan-i2v` if `engine=wan` — not staff default |

**Production EP/Shot (staff Tạo video):** Famixa Series Turbo, `engine=turbo` unless operator picks Wan.

### Components

| Step | File | Class / method | Input | Output | Provider |
|---|---|---|---|---|---|
| Staff click | `ContentFamixaBuildBoards.tsx` | `onCreate(shotId)` | shot id | — | — |
| Orchestrate | `ContentFamixaSeriesTab.tsx` | `startSceneTurbo` | `onlyIds?` | confirm modal | — |
| Send | `ContentFamixaSeriesTab.tsx` | `sendTurbo` | KF dataUrl, motion prompt, 5\|10s | taskId | HTTP |
| Client API | `content.api.ts` | `startContentSeriesTurbo` | `{clipId,prompt,imageDataUrl,seconds,ratio,engine,confirm:true}` | task | — |
| Server | `ContentSeriesTurboService.cs` | `StartAsync` | same + VUA gate | Runway task | — |
| Provider | `ContentRunwayClient.cs` | `CreateImageToVideoAsync` | JPEG data-URI, prompt, duration, ratio | task id | Runway |
| Poll | `ContentFamixaSeriesTab.tsx` | `getContentSeriesTurbo` | taskId | videoUrl | Runway |
| Stamp | `content-famixa-final-source.ts` | `stampMuteTake` | url, silent | `RUNWAY` / `RUNWAY_TTS` | — |

Alt ENGINE path: `VideoGenerationExecutionService.ExecuteAsync` → `RunwayVideoGenerationProvider.GenerateAsync`. Client `executeVideoGenerationExecution` posts without `?confirm=true` while server requires `confirm=true` — **possible dead UI path** (NOT DETERMINED whether another wrapper adds confirm).

---

## 5. Keyframe Pipeline

Canonical I2V visual input = **approved scene still**, not Character Studio sheet.

- Gate: `readyV2VideoShots` requires `kfIsApproved(run)` (`content-famixa-prod-v2.ts`).
- Approval: `kfIsApprovedStill` — pixels + (`kfApproved === true` **or** visual QA pass **or** legacy continuity ticks) (`content-famixa-scene-first.ts`).
- Human `kfApproved === true` wins over PENDING QA (staff path).
- Client normalize: `prepareRunwayKf` → JPEG 1280×720 or 720×1280.
- Server: `GuardRunwayDataUri` rejects URL/PNG/wrong size.
- ENGINE: `I2vPackageAsync` requires `InputImage == APPROVED_KEYFRAME`.

**HIGH RISK (model, not payload):** provider can still **reshape the face while animating**. Code cannot prevent that; it only prevents T2V and character-crop I2V.

---

## 6. Character Reference Pipeline

LOCKED Character Studio identity **does not travel in the video job**.

It enters at **still compile**:

- Client: `lockedStudioClaim(id, name, masterSha256)` vs `legacyPhotorealCanonClaim`.
- Server still: `ContentSeriesStillService` + `ProjectVisualModeAuthorityV1Rules.Preflight` (LOCKED identity-anchor).
- ENGINE execute stores Master/Dna/Prp SHA in the execution row — **not** in Runway JSON.

I2V payload is only:

```
{ model, promptImage, promptText?, duration, ratio }
```

If video is generated from a weak or drifted KF, I2V will animate the drift. Face / hair / age / wardrobe / proportions are **KF + Image QA hard fails**, not I2V constraints.

`RUNWAY_I2V_LAW_FAIL` **forbids** “preserve wardrobe / same wardrobe” language in the I2V prompt. Identity cannot be restated in text.

---

## 7. Visual Mode Pipeline

Project contract (`kit-video-visual-mode.ts` / `ProjectVisualModeAuthorityV1Rules.FamixaCurrent()`):

- `visualMode` = `3D_STYLIZED_REALISM`
- `videoRenderingMode` = `3D_STYLIZED_VIDEO`
- `photorealismCeiling` = low
- `referencePolicy` = `LOCKED_STUDIO_ONLY`

`VideoVisualIngressV1Rules.CompileVideo` reuses the **same** VisualUniverse snapshot compiler as stills, then appends a motion layer.

Then `RunwayI2vPrompt`:

- If `[FAMIXA VISUAL UNIVERSE AUTHORITY V1]` present, keep only the motion slice.
- `StripCallerStyleAuthority` removes VUA / PVS / photoreal tokens.
- Cap 1000 chars.
- Empty → `"Subtle body movement, blink and breathe. Camera remains steady."`

**ARCHITECTURAL GAP:** the video **provider** never receives VisualMode / VUA / PhotorealismCeiling. Inheritance is **gate + KF pixels + SHA**, not provider conditioning.

This is intentional (I2V law) and also a quality ceiling: Runway cannot be told “stay 3D stylized.”

---

## 8. Audio Pipeline

```
Dialogue text (Voice Script)
  → ElevenLabs MP3
  → measureAudioSec → voiceAssets.duration
  → (optional) Fal in
  → assemble voices[] adelay
  → AAC 48 kHz stereo
```

| Question | Answer |
|---|---|
| Audio before or after video? | **Intended before** (Full Voice lock). **Staff can I2V without Voice.** |
| Duration SoT? | **No single SoT.** I2V = 5\|10. Assemble speech-fit = measured TTS + preroll/gap/tail. Fal clips = take seconds. |
| Shot duration from? | `applyDialogueMap`: spoken → 10, silent → 5. `editSeconds` only on preview cut. |
| Dialogue timing from? | `buildAssembleTimeline` `startSec`/`endSec` from measured MP3 or `estimateSpokenSec(chars/12)` |
| Hard-coded duration? | **Yes** — I2V 5 or 10 only (`ContentSeriesTurboService` maps `>=8 → 10 else 5`) |
| Trim audio? | Fal `sync_mode` may cut/remap. FFmpeg `-t {dur}` + `atrim` |
| Pad silence? | `apad`, `anullsrc`, `SPEECH_PREROLL 0.2` / `TAIL 0.22`, `tpad=stop_duration=8` on video |
| Stretch audio? | Not in FFmpeg assemble. Fal `remap` **may** (provider-side; not inspected) |
| Stretch video? | No. Hold last frame via `tpad=clone` |
| Timebase mismatch? | Possible: I2V fps NOT DETERMINED FROM CODE; assemble forces `fps=30` + AAC 48 kHz |

Staff `EpisodeReel.stitch`: `cues: []`, `audioOf: async () => undefined`, **5s per clip hardcoded**.

---

## 9. TTS Pipeline

| Field | Value | File |
|---|---|---|
| Provider | ElevenLabs | `ContentElevenLabsClient.cs` |
| Endpoint | `POST v1/text-to-speech/{voiceId}` | same |
| Model | `eleven_v3` | L113 / L120 |
| Language | `language_code=vi` unless northern-lock (then omitted) | L109–124 |
| Normalization | `on` (vi) / `off` (northern) | same |
| Settings | stability, similarity_boost, style, speed, `use_speaker_boost: true` | L101–108 |
| Emotion tags | **Not sent.** `actingTtsPerformText` is spoken text only | `content-famixa-acting-law.ts` |
| Emotion effect | `actingTtsVoiceSettings` + character Voice Bible | same |
| Cache | IndexedDB `kit-famixa-tts` | `content-famixa-tts-store.ts` |
| Deterministic? | **No.** Same line can re-synth; cache key includes emotion+settings |

### Voice Canon

`voiceOf(state, characterId)` = `characters[].voiceId || roles[].voiceId`.

| Character | Graph id | Canonical ElevenLabs Voice ID in code |
|---|---|---|
| Minh | CHAR-001 | **GAP** — operator-assigned only |
| Nam | CHAR-002 | **GAP** |
| Linh | CHAR-003 | **GAP** |

Lanes exist (boy / father / mother) for **settings**, not for a locked Voice ID. Fallback: org `ContentVideoConfig.VoiceId`.

**GAP:** no immutable Canonical Voice asset per character.

---

## 10. Dialogue Timing

Famixa knows **line-level** timing on the assemble timeline:

```ts
AssembleCue { lineId, shotId, startSec, endSec, name, text }
```

Built in `buildAssembleTimeline` (`content-famixa-assemble.ts`). Exported as SRT.

| Timing | Exists? |
|---|---|
| DialogueStart / DialogueEnd (line) | **Yes** — assemble/SRT only |
| Duration (measured MP3) | **Yes** if TTS ran (`measureAudioSec`) |
| Word timing | **No** |
| Phoneme timing | **No** |
| Viseme timeline | **No** |

**LIP_SYNC_PRECISION_LIMITATION**

Line clock is **not** written into Runway or Fal. Fal receives one full MP3 + one mute MP4. Assemble uses `Math.round(startSec * 1000)` adelay.

---

## 11. Lip-sync Pipeline

### Does Famixa have real lip-sync?

**Yes, as an optional Fal job. Not on the staff finish path that produced the mute EP.**

| Layer | File | Method |
|---|---|---|
| UI | `ContentFamixaSeriesTab.tsx` | `startLipsync` |
| API | `content.api.ts` | `startContentSeriesLipsync` |
| Server | `ContentSeriesTurboService.cs` | `StartLipsyncAsync` |
| Provider | `ContentFalClient.cs` | `CreateLipsyncAsync` |

**Fal payload (default 1.9):**

```
POST fal-ai/sync-lipsync
{ video_url, audio_url, sync_mode, model: "lipsync-1.9.0-beta" }
```

Variants: `fal-ai/sync-lipsync/v3`, `fal-ai/latentsync`.  
`sync_mode`: `cut_off | silence | loop | bounce | remap`.  
UI default: **`remap`**. Server default if empty: **`cut_off`**.

Fal does **not** receive phonemes. Client does **not** read visemes back.

**Classification: B. AUDIO-CONDITIONED LIP SYNC**  
(Provider may infer visemes internally — **NOT DETERMINED FROM CODE**. Not A.)

Staff `mixStaffEpisode` copy: *“Miệng chưa khớp lời (chưa Fal).”*

I2V itself is **D. PROMPT-ONLY** mouth motion (“blink and breathe”) on a **mute** image — not speech.

---

## 12. Multi-character Dialogue

| Control | Behavior | File |
|---|---|---|
| Speaker of a line | `FamixaVoiceLine.characterId` / `name` | voice-script |
| Shot mapping | `dialogueSegmentIds` / `proposeDialogueMap` | dialogue-map |
| Two speakers one take | **`multiSpeakerBlock` → no Fal** | dialogue-map L355–359 |
| Face vs speaker | **`lipsyncSpeakerMismatch` → no Fal** | dialogue-map L334–353 |
| Several lines, same speaker | Fal uses **first line audio only** | SeriesTab startLipsync toast |
| Listener mouth closed | **No I2V constraint** | — |
| Active speaker timeline | Line cues only; **no viseme mute for others** | — |

**HIGH RISK** for Minh + Linh two-shot: I2V may move both mouths; Fal refuses two speakers; overlay TTS cannot close the listener’s mouth.

---

## 13. Audio Mixing

Client `compileMixCueSheet` (`content-famixa-mix.ts`) can emit:

- Room `room.night.dining`
- Foley (regex: footstep, paper, breath, …)
- Music + duck copy (−12 dB)
- `loudnorm: true` (−14 LUFS)

`assembleCut` **sends** `mix: assembleMixPayload(mixSheet)`.

Server `ContentSeriesAssembleRequest` = `{ FileStem, Clips, Aspect }` — **no Mix field**.

`ContentMixAssets.MasterArgs` (room −20 dB, music sidechain, `loudnorm=I=-14`) is **never called**.

**Limitation: flattened.** Per clip: TTS `amix` **or** Fal `[0:a]`. Then concat. One AAC track.

UI success toast can claim room / Foley / duck / LUFS that **did not run**.

---

## 14. A/V Sync

| Clock | Source |
|---|---|
| I2V length | 5 or 10 s (provider) |
| TTS length | MP3 metadata |
| Assemble clip | speech-fit (TTS + 0.2/0.1/0.22) or take-fit if Fal |
| FFmpeg | `-t {dur}` clamp 0.4–20; `adelay` ms; `fps=30` |
| Browser fallback | `performance.now()` vs `AudioContext` start |

Not found in assemble: `-shortest`, `-vsync`, `-async`, explicit output `-r`, Fal duration re-measure.

**Risks with code evidence:**

- TTS longer than 5/10 → `MISMATCH` / `voiceChainFrom` (10s chain), else blocked.
- Fal `remap` changes take length; assemble then uses `item.seconds` (5/10), not remapped length.
- Concat `-c copy` fail → full `libx264`+`aac` re-encode (drift + generation loss).
- `volume=2` on TTS (clipping possible; peak limiter only in unused `ContentMixAssets`).

FPS / PTS / DTS of **Runway source** — **NOT DETERMINED FROM CODE**.

---

## 15. FFmpeg Pipeline

**Per-clip** (`ContentSeriesAssembleService.MixArgs`):

```
-y [-ss SS] -i "{video}" [-i voice.mp3 ...] [-f lavfi -i anullsrc ...]
-filter_complex "
  [0:v]scale=...pad|crop,fps=30,setsar=1,format=yuv420p,tpad=stop_mode=clone:stop_duration=8[v];
  {keep Fal audio | anullsrc trim | adelay+amix+volume=2+apad}
"
-map "[v]" -map "[a]" -t {dur}
-c:v libx264 -preset veryfast -crf 20 -c:a aac -b:a 160k -ar 48000 -ac 2
```

**Concat:**

1. `-f concat -safe 0 -i list.txt -c copy cut.mp4`
2. Fallback: same concat + `libx264 veryfast crf 20` + `aac 160k 48k stereo`

**Browser fallback** (`recordAssembledCut`): canvas 1280×720, `captureStream(30)`, MediaRecorder vp8/vp9+opus, 4 Mbps, burned SRT. Staff mix uses this **only**.

**Quality:** I2V already compressed → **always** re-encoded to x264 crf 20 per part → maybe again on concat fallback. Unnecessary transcode: **yes**. Drift: **possible** on fallback and on browser record.

---

## 16. Provider Payloads

### Runway I2V (actual JSON)

```
POST https://api.dev.runwayml.com/v1/image_to_video
X-Runway-Version: 2024-11-06
Authorization: Bearer {key}

{
  "model": "gen4_turbo",
  "promptImage": "data:image/jpeg;base64,...",
  "promptText": "Subtle body movement, {acting}. Blink and breathe. Camera remains steady.",
  "duration": 5 | 10,
  "ratio": "1280:720" | "720:1280"
}
```

Not sent: lastFrame, audio, character refs, VisualMode, negativePrompt, identity SHA.

Client compiler (`compileRunwayPromptV1`) produces that motion sentence from **action keywords**, not from dialogue.

### ElevenLabs TTS

```
POST v1/text-to-speech/{voiceId}
{ text, model_id: "eleven_v3", language_code?: "vi", apply_text_normalization, voice_settings }
```

### Fal lipsync

```
{ video_url, audio_url, sync_mode, model?: "lipsync-1.9.0-beta" }
```

### Wan (alt)

```
fal-ai/wan-i2v { prompt, negative_prompt, image_url, num_frames, frames_per_second:16, resolution:"720p", aspect_ratio }
```

---

## 17. Current Quality Score

Scores are **from this architecture**, not from a new watch of pixels.

| Axis | Score | Evidence |
|---|---|---|
| Video visual quality | **4 / 10** | `gen4_turbo` + generic motion + 5s cap + crf 20 re-encode |
| Character consistency | **4 / 10** | Strong at KF QA; **zero** identity fields on I2V |
| Motion quality | **3 / 10** | “Subtle body movement, blink and breathe” |
| Camera quality | **3 / 10** | Three hardcoded camera strings; retry rotates them |
| Audio quality | **2 / 10** | Staff takes mute; mix stack unused |
| Voice quality | **5 / 10** | eleven_v3 + vi + Voice Bible **if** Voice Canon assigned |
| Dialogue timing | **4 / 10** | Line start/end only; heuristic fallback `chars/12` |
| Lip-sync | **1 / 10** staff · **5 / 10** if Fal used | Staff = none; Fal = B-class, no viseme SoT |
| Multi-character dialogue | **2 / 10** | Block or first-line-only; no listener mute |
| Audio mixing | **2 / 10** | Flatten; `ContentMixAssets` dead |
| A/V sync | **4 / 10** | Three clocks; no vsync/shortest |
| Final export quality | **3 / 10** staff WebM overlay · **5 / 10** director MP4+Fal | Double encode |

---

## 18. P0 Problems

1. **Staff production never lipsyncs.** I2V mute + stitch mute + overlay ≠ mouth sync.  
2. **No phoneme/viseme timeline.** `LIP_SYNC_PRECISION_LIMITATION`.  
3. **I2V has no audio.** Mouth during speech is model-guess or later Fal.  
4. **Staff single-shot I2V bypasses `voiceLocked`.** (`staffI2v` in `startSceneTurbo`.)  
5. **Multi-speaker / listener mouth.** HIGH RISK.  
6. **VisualMode / identity stripped from provider.** ARCHITECTURAL GAP for consistency under motion.

---

## 19. P1 Problems

1. I2V duration hard-coded 5\|10 vs TTS SoT.  
2. Client mix (room/Foley/duck/LUFS) **not applied**. UI can claim it ran.  
3. Fal uses **first line only** on a spoken shot.  
4. Character can drift during I2V (no lastFrame, no identity lock in prompt).  
5. Two I2V lanes (Series Turbo vs ENGINE); EP staff uses Turbo. ENGINE confirm flag may be missing.  
6. Concat re-encode quality loss.  
7. `RUNWAY_TTS` is preview-only for Final (`finalSourceBlockReason`) but staff finish treats clips as done.

---

## 20. P2 Problems

1. Camera control = 3 English phrases.  
2. No last-frame / interpolation pair.  
3. Browser MediaRecorder timing.  
4. Voice IDs not canonical constants.  
5. `tpad=8` clone hold.  
6. `volume=2` clip risk.  
7. Northern-lock drops `language_code=vi`.  
8. Wan path unused / different fps (16).

---

## 21. P3 Problems

1. Staff tabs omit Voice / Khớp môi.  
2. “Ghép có tiếng” vs Fal not distinguished in overview CTA.  
3. Toast over-claims mix.  
4. Staff yellow CTA can say “Sang hoàn thiện” on Xuất bản.

---

## 22. Root Cause Evidence

Do not treat these as pixel diagnoses of a specific take. They are **pipeline causes** traced in code.

| Symptom | Cause with file evidence |
|---|---|
| Mute video | Runway I2V has no audio; `stampMuteTake`; staff `audioOf => undefined` |
| Bad motion | `compileRunwayPromptV1` generic acting + blink |
| Mouth ≠ speech | I2V prompt-only; Fal not on staff path; no visemes |
| Drift | Identity only in KF JPEG; `RunwayI2vPrompt` strips style |
| Timing slip | 5\|10 vs measured TTS vs Fal remap vs `adelay` ms |
| Mix missing | `ContentSeriesAssembleRequest` has no Mix; `ContentMixAssets` unreferenced |

---

## 23. Recommended Target Architecture

**Recommend only. Do not implement in this audit.**

Evidence supports this sequence (already sketched in park rules; **not how staff runs today**):

```
SHOT
 ↓
CANONICAL KEYFRAME (LOCKED identity already in pixels)
 ↓
DIALOGUE TIMELINE (speaker + start/end; no invented lines)
 ↓
VOICE ASSET (canonical Voice ID per CHAR, measured duration = SoT)
 ↓
PHONEME/VISEME TIMELINE   ← missing today
 ↓
VIDEO MOTION (I2V still KF-only; duration ≥ speech)
 ↓
LIP-SYNC (audio-conditioned or viseme-driven; one speaker / take)
 ↓
AUDIO MIX (wire ContentMixAssets or drop the UI claim)
 ↓
FINAL COMPOSITE (one encode, fps + 48 kHz locked)
```

Do **not** send Character Studio sheets to I2V. Do **not** put VisualMode prose in Runway `promptText`. Keep authority on KF + SHA. Add **duration + speaker + Fal (or equivalent)** as gates on staff finish if Final means “talking picture.”

---

## 24. Recommended Fix Order

Director decides `FAMIXA_VIDEO_AUDIO_LIPSYNC_PIPELINE_FIX_V1`. Suggested order only:

1. Honest staff contract: mute take vs TTS overlay vs Fal Final.  
2. Wire Voice + measured duration before I2V on spoken shots (remove staff bypass or label it TEST).  
3. Apply or delete `ContentMixAssets` / client mix toast.  
4. Fal (or successor) as required Final for spoken shots; one speaker / take; first-line-only → voiceChain.  
5. Single duration SoT (speech) driving I2V 5/10 choice and assemble `-t`.  
6. Only then: viseme / lastFrame / motion compiler — if provider capability exists.

Do not touch Character Studio / DNA / PRP / CRP / PVS / VUA / CDL / Visual Mode Authority to “fix lipsync.”

---

## 25. Files inspected

**Client**

- `ContentFamixaSeriesTab.tsx`
- `ContentFamixaBuildBoards.tsx`
- `ContentFamixaSeriesWorkspace.tsx` / `ContentFamixaProductionOverview.tsx`
- `content-famixa-prod-v2.ts`
- `content-famixa-scene-first.ts`
- `content-famixa-series.ts`
- `content-famixa-visual-spec.ts`
- `content-famixa-kf-pipeline.ts`
- `content-runway-prompt-v1.ts`
- `content-runway-adapter.ts`
- `content-famixa-runway-pipe.ts`
- `content-famixa-still-ref.ts`
- `content-famixa-voice-script.ts`
- `content-famixa-dialogue-map.ts`
- `content-famixa-acting-law.ts`
- `content-famixa-performance.ts`
- `content-famixa-assemble.ts`
- `content-famixa-assemble-render.ts`
- `content-famixa-mix.ts`
- `content-famixa-tts-store.ts`
- `content-famixa-final-source.ts`
- `kit-video-production-ux.ts`
- `kit-video-visual-mode.ts`
- `kit-video-asset.ts`
- `client/admin/src/shared/api/content.api.ts`

**Server**

- `ContentSeriesTurboService.cs`
- `ContentRunwayClient.cs`
- `ContentFalClient.cs`
- `ContentElevenLabsClient.cs`
- `ContentSeriesAssembleService.cs`
- `ContentMixAssets.cs`
- `ContentSeriesPilotService.cs`
- `VideoVisualIngressV1Rules.cs`
- `ProjectVisualModeAuthorityV1Rules.cs`
- `SeriesStillVisualIngressV1Rules.cs`
- `VideoGenerationExecutionService.cs`
- `RunwayVideoGenerationProvider.cs`
- `KitVideoMotionService.cs`
- `ContentContracts.cs` (assemble / turbo DTOs)
- `ContentController.cs` (turbo / tts / lipsync / assemble actions — via grep)

**Policy**

- `.cursor/rules/content-park.mdc`

---

## 26. Files NOT modified

All of the above. Working tree was not edited for this audit. This markdown is the only new document.

---

## 27. Gemini count

**0**

---

## 28. Generation count

**0** (no I2V / still / ENGINE execute)

---

## 29. TTS count

**0**

---

## 30. Lip-sync count

**0**

---

## 31. Build status

Not run (audit-only). Last known staff UX smoke: `kit-video-production-ux.smoke.ts` **FAIL=1** (`12c staff no look path` — pre-existing, unrelated to this audit). Vite `:5173` + API `:5290` were restarted earlier the same day for an unrelated connection refusal.

---

## What must not be touched (reaffirmed)

Character Studio · Master · DNA · PRP · CRP · PVS · VUA · CDL · Visual Mode Authority · no regen Character / Calibration / Scene.

---

**STOP.** No `FAMIXA_VIDEO_AUDIO_LIPSYNC_PIPELINE_FIX_V1` until Director decides.
