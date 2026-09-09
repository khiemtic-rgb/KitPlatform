# FAMIXA_VIDEO_AUDIO_LIPSYNC_PIPELINE_FIX_V1_REPORT

**Status:** IMPLEMENTED — waiting Director review  
**Date:** 2026-09-04  
**Document:** FAMIXA_VIDEO_AUDIO_LIPSYNC_PIPELINE_FIX_V1  
**Suite:** FAMIXA_VIDEO_AUDIO_LIPSYNC_PIPELINE_FIX_V1_REGRESSION  
**Based on:** FAMIXA_VIDEO_AUDIO_LIPSYNC_PIPELINE_AUDIT_V1  

**Guarantee this phase:** Gemini = 0 · Runway = 0 · ElevenLabs = 0 · Fal = 0 · Generation = 0 · Migration = 0 · Authority mutation = 0 · Commit = 0 · PR = 0  

**Verdict:** PASS WITH GAPS

---

## 1. Current pipeline before

Staff Video Studio:

```
Canonical KF
  → Runway gen4_turbo I2V (MUTE_TAKE)
  → optional TTS overlay in the browser
  → staff called that “hoàn tất / Final”
```

Fal `fal-ai/sync-lipsync` existed on the director/studio path but Staff never entered it as a required stage. Server assemble ignored the client mix payload. Stitch used a hard 5-second clip. Dialogue shots could finalize from raw I2V.

## 2. Pipeline after

Canonical production contract (Staff + Director share it):

```
SCRIPT → EPISODE → SCENE → SHOT
  → SHOT VISUAL CONTRACT
  → CANONICAL KEYFRAME          Visual SoT
  → DIALOGUE TIMELINE           Dialogue SoT
  → CANONICAL VOICE ASSETS      Voice SoT + timing SoT
  → VIDEO I2V (MUTE_TAKE)       VideoTake
  → LIP-SYNC (Fal)              LipSync SoT when spoken
  → AUDIO MIX (server FFmpeg)   Mix
  → FINAL COMPOSITE             Final SoT
  → DIRECTOR REVIEW
  → APPROVED
```

Shot kinds:

| Kind | Path |
|---|---|
| SILENT_SHOT | KF → I2V → Mix → Final allowed |
| SINGLE_SPEAKER_SHOT | KF → I2V mute → Voice → Fal → Mix → Final |
| MULTI_SPEAKER_SHOT | VIDEO_READY possible; FINAL blocked (`MULTI_SPEAKER_LIPSYNC_UNSUPPORTED`) |
| AMBIENCE_ONLY_SHOT | treated as silent for lip-sync |

`VIDEO_READY ≠ FINAL_READY` when the shot has dialogue.

## 3. State machine

```
DRAFT → VISUAL_READY → VOICE_READY → VIDEO_READY
  → LIPSYNC_READY → MIX_READY → FINAL_READY
  → DIRECTOR_REVIEW → APPROVED
```

Error states: `BLOCKED` · `FAILED` · `REVIEW_REQUIRED`

Spoken + mute take without Fal → stage `BLOCKED` with `LIPSYNC_REQUIRED`.

## 4. Dialogue contract

`DialogueCue`: ShotId, DialogueId, SpeakerCharacterId, Text, VoiceId, StartSec, EndSec, AudioAssetId, AudioDurationSec, Status.

A shot is no longer one text blob. Missing speaker → `SPEAKER_REQUIRED`. Same-speaker cues are merged with timing (`mergeSameSpeakerCues` / `MergeLipsyncVoices`); discarded list must stay empty.

## 5. Voice contract

`CharacterVoiceProfile`: CharacterId, VoiceId?, status `UNASSIGNED` | `ASSIGNED`, `canonical: false`.

No Voice ID is invented. UI shows `NOT ASSIGNED` or `Voice {id} — assigned, not canonical`.

VoiceReady for a spoken shot = every cue has speaker + VoiceId + AudioAsset + Duration > 0.2s.

Staff tab **Thoại** is a real stage. Voice is required for Final, not for creating a mute I2V take.

## 6. VideoTake contract

I2V remains `runway` / `gen4_turbo`. Output is `MUTE_TAKE`. Runway audio is never a source.

Duration for spoken shots is `i2vDurationForDialogue(lastEnd)` → 5 or 10 from dialogue + tail (0.22s). Provider cap 10s is a warning (`DIALOGUE_EXCEEDS_I2V_CAP`), not a silent trim through speech.

`POST /content/series/turbo` semantics: **Generate I2V Take**, not Generate Final Video.

## 7. LipSync contract

`LipSyncArtifact`: provider `fal`, endpoint `fal-ai/sync-lipsync`.

Inputs: mute I2V take + voice asset(s). Multiple same-speaker cues are merged with `adelay` before Fal. First-line-only discard is removed.

Retry LipSync does not touch KF, I2V, character, Visual Mode, PVS, VUA, CDL.

## 8. Mix contract

Client sends `ContentSeriesAssembleMixDto` intent (room / music / loudnorm / sfx). Server `ApplyCanonicalMix` → `ContentMixAssets.MasterArgs`.

Canonical audio: 48 kHz · stereo · AAC. Dialogue linear gain = 1 (no `volume=2`). Music ducking exists (`sidechaincompress` on the music bed). Mix beds are still procedural FFmpeg stems, not operator-uploaded dialogue/music/SFX/ambience files.

`mixStaffEpisode` and browser stitch = `PREVIEW_ONLY` / `NOT_ELIGIBLE_FOR_FINAL`.

## 9. Finalization gate

| Shot | Allow Final |
|---|---|
| Silent | Canonical KF + mute take + mix |
| Single speaker | KF + mute take + VoiceReady + LipSyncReady + MixReady + `FINAL_SOURCE=LIPSYNC_VIDEO` |
| Multi speaker | BLOCK (`MULTI_SPEAKER_LIPSYNC_UNSUPPORTED`) |
| TTS overlay / client mix only | BLOCK (`PREVIEW_ONLY`, `NOT_ELIGIBLE_FOR_FINAL`) |
| Legacy photoreal / VisualMode mismatch | BLOCK |

`assembleCut` (server MP4) now refuses spoken items without `FINAL_SOURCE=FAL` via `finalSourceBlockReason`. Overlay TTS cannot mint a Final file.

Client cannot set `FINAL_READY`. Only `videoProductionPreflight` / `VideoAudioLipsyncPipelineV1Rules.Preflight` can.

## 10. Staff workflow

Tabs: Tổng quan → Kịch bản → Chia cảnh → Nhân vật → **Thoại** → Hình ảnh → Video → Hoàn thiện → Xuất bản.

Hoàn thiện lists per-shot:

VISUAL · VOICE · VIDEO (Hình ảnh đã tạo / take câm) · LIP-SYNC · MIX · FINAL

Actions: **Create Lip-Sync** when required. Multi-speaker: **NOT SUPPORTED FOR MULTI-SPEAKER**. Publish stays locked until the episode gate passes. Finish is reachable after I2V so staff can see blockers.

Copy: never “Video hoàn tất” for a spoken mute take. Use “Video hình ảnh đã tạo” / “Chờ Lip-sync”.

## 11. Director workflow

Same preflight / finalize / mix / Fal merge contracts. Director UI may differ; backend production contract does not. Engine I2V lane is still mute `gen4_turbo` on the approved KF. This phase does not add a second visual compiler and does not send character sheets to Runway.

## 12. Multi-speaker behavior

Unsupported. `MULTI_SPEAKER_LIPSYNC_SUPPORTED = false`. `LISTENER_MOUTH_CONTROL = false`.

FINAL blocked. UI states the limitation. No fake mouth sync. No TTS overlay as lipsync.

## 13. Retry behavior

| Retry | KF | I2V | Voice | LipSync | Mix | Character / mode |
|---|---|---|---|---|---|---|
| lipsync | no | no | no | yes | no | no |
| mix | no | no | no | no | yes | no |
| i2v | no | yes | no | no | no | no |
| voice | no | no | yes | no | no | no |

## 14. FFmpeg changes

- Concat: `-fflags +genpts`; `-c copy` then re-encode fallback (`libx264` + AAC 48k stereo) if copy fails.
- Dialogue gain from `DialogueLinearGain` (1), not `volume=2`.
- `tpad` stop clone reduced 8s → 1s.
- Server mix always runs after concat (`ApplyCanonicalMix` or `MasterFallbackArgs`).
- Same-speaker Fal audio merged with `adelay` + `amix` before upload.
- Silent `anullsrc` remains only for true silent clips, not as a spoken-shot fallback.
- Stitch seconds follow take duration / dialogue end, not a global 5s pad.

## 15. A/V sync changes

Each preflight row stores `VideoDurationSec`, `AudioDurationSec`, `FinalDurationSec`, `AvSync` = PASS | WARNING | FAIL (delta ≤0.12 / ≤0.4). Spoken duration must cover last cue + tail. Voice is not stretched to fit a 5s take.

Limitation: no phoneme/viseme timeline; A/V status is duration metadata, not a mouth-accuracy score.

## 16. Old path deprecation

| Path | Role |
|---|---|
| I2V → TTS overlay → Final | `DEPRECATED` + `NOT_ELIGIBLE_FOR_FINAL` |
| `assembleNeedTtsOverlay` | PREVIEW_ONLY detector; canonical assemble blocks these items |
| `mixStaffEpisode` / MediaRecorder WebM | `PREVIEW_ONLY` |
| Canonical Final | `SERVER_FFMPEG` after Fal when spoken |

Code is kept for preview. It is not on the finalize path.

## 17. APIs added / modified

| API | Change |
|---|---|
| `GET /api/content/video-audio-lipsync-pipeline/regression` | **New.** In-process VA-01…30. `generate=false`, all provider flags false. |
| `POST /api/content/series/turbo` | Unchanged route. Semantics = Generate I2V Take. |
| `POST /api/content/series/lipsync` | Accepts `voices[]`. Server merges same-speaker cues. |
| `POST /api/content/series/assemble` | Executes `request.Mix` on the server. |

No new `/content/shots/{id}/voice|lipsync|mix|finalize` routes. Existing routes plus the gate are enough.

## 18. Files created

- `client/admin/src/modules/content/famixa-video-audio-lipsync-pipeline.ts`
- `client/admin/src/modules/content/famixa-video-audio-lipsync-pipeline-fix.smoke.ts`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/VideoAudioLipsyncPipelineV1Rules.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/VideoAudioLipsyncPipelineV1Regression.cs`
- `docs/novixa/03-solution/famixa-video-audio-lipsync-pipeline-fix-v1.md` (this report)

## 19. Files modified

- `client/admin/src/modules/content/ContentFamixaSeriesTab.tsx`
- `client/admin/src/modules/content/ContentFamixaBuildBoards.tsx`
- `client/admin/src/modules/content/ContentFamixaSeriesWorkspace.tsx`
- `client/admin/src/modules/content/kit-video-production-ux.ts`
- `client/admin/src/modules/content/kit-video-production-ux.smoke.ts`
- `client/admin/src/modules/content/kit-video-production-os.smoke.ts`
- `client/admin/src/modules/content/content-famixa-assemble.ts`
- `client/admin/src/shared/api/content.api.ts`
- `src/KitPlatform.Api/Controllers/Content/ContentController.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Application/ContentContracts.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesAssembleService.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/ContentSeriesTurboService.cs`
- `src/Packs/Content/KitPlatform.Packs.Content.Infrastructure/VideoGenerationExecutionService.cs` (register suite)

## 20. Files deleted

None.

## 21. Regression result

`FAMIXA_VIDEO_AUDIO_LIPSYNC_PIPELINE_FIX_V1_REGRESSION` — **PASS FAIL=0**

VA-01…VA-30 covered in C# `VideoAudioLipsyncPipelineV1Regression.Run()` (in-process, no HTTP, no providers).

## 22. TypeScript smoke result

```
npx tsx src/modules/content/famixa-video-audio-lipsync-pipeline-fix.smoke.ts
FAMIXA_VIDEO_AUDIO_LIPSYNC_PIPELINE_FIX_V1 PASS FAIL=0 (no provider)
```

Executed with local `tsx` (Node v24.18.0). Runtime PASS, not compile-only.

## 23. Build result

| Project | Errors | Warnings |
|---|---|---|
| Content.Application | 0 | 0 |
| Content.Infrastructure | 0 | Pre-existing: NU1903 Newtonsoft.Json 10.0.3; CS4014 `CustomerPushRepository.cs` (not this change) |
| API | 0 (verified via alternate output ` -o`) | 58 pre-existing (NU1903, Survey SKPaint CS0618, `Program.cs` CS8602). **No new warnings in pipeline files.** |
| Admin `tsc --noEmit` | Pre-existing phase05/06 `Buffer`/`process` and unrelated shot/workspace TS2551/TS6133 | **Zero errors in pipeline files.** |

Default `src/KitPlatform.Api/bin/Debug` copy was locked by running `KitPlatform.Api` (pid 17204). Compile was verified to a separate output directory so the live process was not killed.

## 24. Existing regression result

C# (in-process):

| Suite | Result |
|---|---|
| FAMIXA_PROJECT_VISUAL_STYLE_V1_REGRESSION | PASS |
| FAMIXA_VISUAL_UNIVERSE_AUTHORITY_V1_REGRESSION | PASS |
| FAMIXA_CHARACTER_DESIGN_LANGUAGE_V1_REGRESSION | PASS |
| FAMIXA_CHARACTER_DESIGN_LANGUAGE_V2_REGRESSION | PASS |
| FAMIXA_VISUAL_MODE_AUTHORITY_V1_REGRESSION | PASS |
| FAMIXA_CHARACTER_STUDIO_V1_REGRESSION | PASS |
| FAMIXA_CHARACTER_STUDIO_IDENTITY_LOCK_V1_REGRESSION | PASS |
| FAMIXA_VISUAL_CALIBRATION_PACK_V1_REGRESSION | PASS |
| FAMIXA_IDENTITY_CONDITIONED_CALIBRATION_V1_REGRESSION | PASS |
| FAMIXA_VISUAL_FOUNDATION_FINALIZATION_V1_REGRESSION | PASS |
| FAMIXA_CHARACTER_FIRST_MASTER_VISUAL_INGRESS_V1_REGRESSION | PASS |
| FAMIXA_SERIES_STILL_VISUAL_INGRESS_V1_REGRESSION | PASS |
| FAMIXA_VIDEO_VISUAL_INGRESS_V1_REGRESSION | PASS |
| FAMIXA_VIDEO_AUDIO_LIPSYNC_PIPELINE_FIX_V1_REGRESSION | PASS |

TypeScript smokes for the same surfaces: all PASS FAIL=0.

`kit-video-production-os.smoke.ts`: PASS (staff tabs now include Thoại).

`kit-video-production-ux.smoke.ts`: **12c FAIL pre-existing** — `ContentFamixaBuildBoards.tsx` never contained `không mở đường tạo hình`. Not introduced by this pipeline. 12f/12q/12r PASS.

## 25–29. Provider / generation counts

| Provider | Calls this phase |
|---|---|
| Gemini | 0 |
| Runway | 0 |
| ElevenLabs | 0 |
| Fal | 0 |
| Generation (image/video/TTS/lipsync) | 0 |

Tests use in-memory contracts only.

## 30. Database mutation

None. No migration. Persistence stays on the existing series graph / assemble / lipsync task model.

`DATABASE_SCHEMA_CHANGE_REQUIRED` was **not** raised.

## 31. Authority SHA before / after

Authority rule files were **not edited**. File SHA256 is therefore unchanged (before = after):

| Authority | File SHA256 |
|---|---|
| Minh Master lock | `65f78ada9a27dd1d2d7aa037b2ff5fce5f68e6eebde8f3d4d07207ea5cc2aa7d` |
| DNA | `a610fabd0b204349ca9fd99240f352fd8b67efd5305174e5480a729d92a80099` |
| PVS | `ec3a7dfa30aaba8917a9ffcd9da56e042597d75e7bf096ef6c5c6ae42ba478e9` |
| VUA | `bcf31d8cab3def9ea93220c1f59e9d91ecd6cf362ad10eb9254a48b1386c855a` |
| CDL V1 | `cbee05449aa5609d56ab9ef54b4a594343b92640c78bd2c42047e4c39bd5c488` |
| CDL V2 | `2062ddffccf0d63338f8513f4721fde96da10cd9a02cd1b3af4eba6efa66bc65` |
| Visual Mode | `ce2d02ae72befc7abd9a194e48576349b3eb53d020bd58427ae5dc5f6f755ab9` |
| Character Studio | `813b41cdd5ae7bb80489c14db075ad1be00ce594cc62088aa2a27a5b03c2182e` |

`ProjectVisualMode` remains `3D_STYLIZED_REALISM`. Character Studio identity lock was not written. Scene 01 `INVALID_REFERENCE_PIPELINE` was not regenerated. 24 character pixels were not touched.

## 32. Remaining gaps

1. Multi-speaker lip-sync unsupported (intentional this phase).
2. No phoneme / viseme timeline.
3. Listener mouth control unsupported (provider limitation).
4. Last frame unused (`LAST_FRAME_USED = false`).
5. Voice IDs remain operator-assigned; `canonical: false`.
6. Mix stems are procedural FFmpeg beds, not a full uploaded Dialogue/Music/SFX/Ambience library.
7. I2V still only 5 / 10 seconds at the provider. Overflow is a warning, not a multi-take stitcher.
8. Staff I2V may still run before Voice (mute take). Final stays blocked until Voice + Fal.
9. Dedicated shot-scoped REST (`/shots/{id}/voice|lipsync|mix|finalize`) not added.
10. MediaRecorder WebM remains as preview fallback if server assemble fails for silent/preview work.
11. Runway motion quality is unchanged (`gen4_turbo`). Not a quality phase.

## 33. Recommended next phase

1. Multi-speaker lip-sync strategy (segmented Fal or provider that accepts a speaker timeline) — or keep the block and productize the limitation.
2. Canonical Character Voice assignment (still human-picked, then `canonical: true`).
3. Provider-quality pass: last frame, motion, duration >10s via chained takes — without rewriting Visual Mode or identity.
4. Persist LipSyncArtifact / VideoTake / VoiceAsset as first-class rows **only if** graph JSON is no longer auditable enough (`DATABASE_SCHEMA_CHANGE_REQUIRED` then).
5. Live Director-authorized generation of Scene 01 after this contract is accepted — not before.

---

## Acceptance map

| # | Criterion | Result |
|---|---|---|
| 1 | Silent KF → I2V → Final | ALLOW |
| 2 | Single speaker KF → mute I2V → Voice → Fal → Mix → Final | ALLOW (structure) |
| 3 | Single speaker without Voice | BLOCK |
| 4 | Single speaker without LipSync | BLOCK |
| 5 | TTS overlay, no Fal | BLOCK FINAL |
| 6 | Multi-speaker unsupported | BLOCK FINAL |
| 7 | Old staff overlay path | NOT_ELIGIBLE_FOR_FINAL |
| 8 | Retry LipSync does not regen KF/I2V | PASS |
| 9 | Retry Mix does not regen KF/I2V/LipSync | PASS |
| 10 | VisualMode `3D_STYLIZED_REALISM` | unchanged |
| 11 | Character Studio LOCKED | unchanged |
| 12 | No authority mutation | PASS |
| 13 | No external provider in tests | PASS |

---

## FINAL VERDICT

**PASS WITH GAPS**

Canonical workflow is implemented. False Final from mute I2V + TTS overlay is blocked. Single-speaker Fal path is structurally correct (mute take + merged voice → Fal → server mix → Final). Server mix actually runs. Retry semantics do not regenerate KF/I2V. Regression and TypeScript smoke PASS. Authority files unchanged. Tests made zero provider calls.

Gaps that keep this from PASS: multi-speaker lip-sync remains unsupported (allowed by the brief), plus the provider/timing limitations listed in §32.

STOP. No production generate. No Gemini / Runway / ElevenLabs / Fal. No Scene 01 regen. No Character / Calibration regen. No approve / lock / promote. No migration. No commit. No push. No PR.

Wait for Director review.
