# FAMIXA — CURRENT IMPLEMENTATION & UX READINESS REPORT

**Date:** 2026-09-04  
**Mode:** READ-ONLY. No code, no migration, no provider, no generate.  
**Scope:** Famixa Series Video Studio + KIT Video Engine surfaces that Staff/Director already see.  
**Code basis:** working tree after `FAMIXA_VIDEO_AUDIO_LIPSYNC_PIPELINE_FIX_V1` (not committed).

---

## Current Architecture

FAMIXA video production is **two lanes that share providers, not one engine**.

```
Lane A — Famixa Series (Staff Video Studio)
  series_build + series_pilot graph (JSON)
  ContentFamixaSeriesTab
  POST /content/series/still | turbo | tts | lipsync | assemble

Lane B — KIT Video Engine (Director / technical)
  video_project / video_production / video_shot_state / contracts / executions
  /content/video-engine/*
  Golden / SH01-01 style production shot
```

Staff episode work lives in **Lane A**. Lane B is mounted under Director mode as “Chi tiết hệ thống” and also appears on `/content/videos` when not on the Series tab (`ContentKitVideoEngineCard`).

There is **no first-class “Create Project”** in Video Studio. The project is implicit `FAMIXA`. Creating work creates a **series build** (one episode graph), not a new visual project.

### Hierarchy (actual)

| Layer | What exists | Where it lives |
|---|---|---|
| Project | Implicit `FAMIXA` + Project Visual Mode `3D_STYLIZED_REALISM` | PVS / Visual Mode Authority, not a staff form |
| Series | `FAMIXA` series code | `series_pilot` + `series_build` |
| Episode | `episode` on the graph (`EP99`, title, premise) | `SeriesPilotState.episode` |
| Scene | `FamixaSceneNode` + optional Scene Master | graph `scenes` / `sceneMasters` |
| Shot | `FamixaSeriesShot` | `episode.shots` |
| Shot run | `SeriesShotRun` (KF, take, Fal, QA) | `state.runs[shotId]` |
| Dialogue | Voice Script lines + `dialogueSegmentIds` | `lines` / shot map |
| Voice asset | duration + IndexedDB MP3 | `voiceAssets` + local blobs |
| Character (episode cast) | `FamixaCharacter` + `voiceId` | graph `characters` |
| Character (authority) | Master / DNA / PRP / CRP / Studio | `video_asset` + Character Studio APIs |

Shot ≠ Engine production shot. Staff SH01 on a build is not automatically `video-engine/production-shots/{guid}`.

---

## Character Studio

**Implemented and used.** Mounted on Staff tab **Nhân vật** via `ContentFamixaCharacterBoard` → `ContentFamixaCharacterStudio`.

What it does today:

- List / create / approve / lock character studio rows (`/content/character-studio/*` and related).
- Master revision, identity score, age/appearance consistency.
- Does **not** create episode shots or I2V.
- After bible ready, PVS / VUA / CDL / Calibration cards collapse under “Advanced / Calibration / Technical Details”.
- Staff still sees `ContentKitVideoImageGenerationExecutionCard` under the studio (“ảnh production Shot 01”).

Locked Minh Master / DNA / PRP are **not** edited from Staff. Director collapse “Tạo hình (đã xong)” still shows Master / DNA / Identity / Stress / Review cards as **read path**, labeled “Không sửa Master / DNA / PRP từ đây.”

---

## Visual Authority / Visual Universe

Working, locked, and **not** a staff production step.

| Authority | Status in current product | Staff sees |
|---|---|---|
| Project Visual Mode | `3D_STYLIZED_REALISM` | Badge on workspace header |
| PVS | Implemented | Advanced on Nhân vật |
| VUA | Implemented | Advanced on Nhân vật |
| CDL V1/V2 | Implemented | Advanced on Nhân vật |
| Calibration / Identity-conditioned | Implemented + separate pages `/content/visual-calibration/:packId` | Advanced + dedicated routes |
| Visual Universe | Locked; I2V must not redefine style | Stamped on KF; stripped from Runway prompt text |

Keyframe VisualMode is expected to match Project Visual Mode. Mismatch is a **preflight blocker** (`VISUAL_MODE_CONFLICT`) in the new contract. Runway still receives **motion-only** prompt + approved KF JPEG.

---

## Keyframe / Video / Voice / Lip-sync / Mix / Final

### Keyframe (Lane A)

- User: **Tạo ảnh** on Hình ảnh board (or director still path).
- API: `POST /content/series/still` (+ `POST /content/series/still-qa`).
- Service: `IContentSeriesStillService`.
- Provider: **Gemini** image.
- Artifact: JPEG in IndexedDB / `run.keyframeDataUrl` (stripped from server graph). `kfApproved` is operator.
- SoT: approved scene KF, **not** character sheet.

### Video (Lane A)

- User: **Tạo video** after KF approved. Confirm credit modal.
- API: `POST /content/series/turbo`, poll `GET /content/series/turbo/{taskId}`.
- Service: `ContentSeriesTurboService` → `ContentRunwayClient`.
- Provider: **Runway `gen4_turbo` I2V**.
- Artifact: mute MP4 (`takeUrl` / `previewUrl`). `I2V_IS_MUTE_TAKE = true`.
- Semantics: Generate **I2V take**, not Final.

Lane B equivalent: `POST /content/video-engine/shots/{id}/video-generation-execution/execute` after video contract approve. Same Runway client. Staff episode does not require this path.

### Voice / TTS

- User: tab **Thoại**; or listen/play on script; or generate when Fal/assemble finds missing MP3.
- API: `POST /content/series/tts` (`previewContentSeriesTts`), `GET /content/series/voices`.
- Service: `IContentSeriesPilotService.PreviewTtsAsync` → `ContentElevenLabsClient`.
- Provider: **ElevenLabs `eleven_v3`**, `language_code=vi`.
- Artifact: MP3 in memory + IndexedDB; `voiceAssets[lineId].duration`.
- Voice ID: operator-assigned on `characters[].voiceId`. Status `ASSIGNED` / `UNASSIGNED`. **`canonical: false`.**

Voice is a **Final gate**, not an I2V gate. Staff can create mute video before Voice is ready.

### Lip-sync

- User: **Create Lip-Sync** on Hoàn thiện, or director Fal controls in `ContentFamixaStudioView`.
- API: `POST /content/series/lipsync` (`voices[]` supported).
- Service: `ContentSeriesTurboService.StartLipsyncAsync` → `ContentFalClient`.
- Provider: **Fal** `fal-ai/sync-lipsync` (operator may pick v3 / latentsync).
- Artifact: `lipsyncUrl`, `lipsynced`, `finalSource=FAL`. Mute take is kept.
- Single speaker: all cues merged (`MergeLipsyncVoices`) — not first line only.
- Multi-speaker: **blocked**. `MULTI_SPEAKER_LIPSYNC_UNSUPPORTED`.

### Mix

- Canonical: `POST /content/series/assemble` with `mix` DTO. Server `ApplyCanonicalMix` → `ContentMixAssets` (room / music duck / sfx / loudnorm −14 LUFS). 48 kHz stereo AAC.
- Staff **Preview TTS** / stitch WebM: `mixStaffEpisode` / `recordAssembledCut` = **PREVIEW_ONLY**.
- There is **no dedicated Mix button** that only runs mix. Mix is a step inside assemble / preview.

### Final export

- Canonical Final: `assembleCut` → server FFmpeg MP4.
- Spoken shots: `finalSourceBlockReason` requires `FINAL_SOURCE=FAL`. Overlay TTS cannot finalize.
- Silent: I2V + mix allowed.
- Browser WebM = preview fallback, not canonical Final.

---

## Current Video Pipeline

Actual staff execution order (not the assumed KF → Video → Voice chain):

```
Create series_build
  → lock script
  → split scenes / shots (shotGraphLocked)
  → Character Studio lock (cast)
  → [Voice TTS may start after script+scenes]
  → Gemini KF → approve
  → Runway I2V mute → approve          ← Voice not required
  → Fal lipsync if spoken
  → server assemble (mix) if Final
  → Director Review / publish tab
```

| Stage | User action | API | Service | Provider | Artifact | Depends on |
|---|---|---|---|---|---|---|
| Script | Confirm kịch bản | `PUT series/pilot` or `PUT series/builds` | graph persist | — | `scriptLocked` | — |
| Scenes | Chia cảnh / khóa graph | same | graph | — | `shots[]`, `shotGraphLocked` | script |
| Cast | Studio approve/lock | character-studio + graph | Studio services | Gemini for refs (separate) | locked studio + `characters[]` | — |
| Voice | Play / generate TTS | `POST series/tts` | Pilot TTS | ElevenLabs | MP3 + `voiceAssets` | speaker + voiceId |
| KF | Tạo ảnh | `POST series/still` | Still | Gemini | KF JPEG + `kfApproved` | shot + identity lock (engine rules) |
| I2V | Tạo video + Confirm | `POST series/turbo` | Turbo | Runway | mute take | approved KF |
| Lip-sync | Create Lip-Sync + Confirm $ | `POST series/lipsync` | Turbo | Fal | lipsync MP4 | mute take + voice blobs + single speaker + QA ticks |
| Mix / Final | Ghép tập / assemble | `POST series/assemble` | Assemble | FFmpeg | MP4 | silent: take; spoken: Fal |
| Preview | Preview TTS / preview stitch | none (browser) | MediaRecorder | — | WebM | takes + optional TTS |

---

## Current Video Studio UI

**Route:** `/content/videos` → `ContentVideosPage`.

Outer tabs:

1. **Series Famixa — EP01** → `ContentFamixaSeriesTab` (this is Video Studio).
2. **Phase 1 — bảng sản xuất** → Lab (other brands). Not Famixa episode.
3. **Factory (góc brand)** → Creatomate jobs. Not this pipeline.

### Series list vs episode workspace

- List: `ContentFamixaSeriesDesk` — filter, search, **Tạo video mới** modal (title / premise / episode / optional script), open, delete.
- Open build: `ContentFamixaSeriesWorkspace`.

### Staff / Director toggle

- **Nhân viên** / **Kỹ thuật** on the header.
- Staff: 9 tabs + boards. Look path parked.
- Director: same tabs + Collapse **Tạo hình (đã xong)** + **Chi tiết hệ thống** (engine card stack + `ContentFamixaStudioView`).

### Staff tabs

| Tab | Component | Main CTAs |
|---|---|---|
| Tổng quan | `ContentFamixaProductionOverview` | Việc cần làm → jump tab |
| Kịch bản | `ContentFamixaScriptDesk` | lock script, listen TTS |
| Chia cảnh | `ContentFamixaBuildSceneList` | confirm split |
| Nhân vật | `ContentFamixaCharacterBoard` | Studio lock; Advanced authority cards |
| Thoại | `ContentFamixaBuildVoiceBoard` | display only (profiles + cue readiness) — generate still happens from script listen / Fal / assemble |
| Hình ảnh | `ContentFamixaBuildImageBoard` → ShotCatalog | Tạo ảnh, Duyệt, Reject |
| Video | `ContentFamixaBuildVideoBoard` → ShotCatalog | Tạo video, camera retry, Duyệt video |
| Hoàn thiện | `ContentFamixaBuildFinishBoard` | pipeline list, Create Lip-Sync, Preview TTS, Gửi Director Review |
| Xuất bản | `ContentFamixaBuildPublishBoard` | reel + preview; Final gated |

Scene-open view: `ContentKitVideoDirectorProductionWorkspace` (engine shot), not the series shot catalog.

### Cards / forms / modals (current)

- Confirm modals: I2V credit, Fal $, assemble, missing TTS resynth, preview TTS.
- ShotCatalog: search, filter (all/wait/review/done), paging.
- Voice board: no Voice-ID picker that invents IDs; shows NOT ASSIGNED.
- Director StudioView: engine, credits, Fal model/sync, timeline, assemble, QA ticks, many technical fields.
- Engine cards (Director only): Visual System, Master, Identity, Stress, DNA, PRP, Shot Contract, Prompt Compiler, Image/Video contract + execute + review, Golden Shot.

### Progress / status

- Workspace % from `deriveSeriesTrack` node.done.
- Overview “VIỆC CẦN LÀM” from `nextWorkCopy` (script → scenes → cast if images pending → images → video → voice → lipsync → finish).
- Per-shot pipeline marks on Hoàn thiện (FIX_V1).
- Track `finish.done` = videos ready; `publish.done` = `finalReady`.

### Error / retry

- I2V: confirm, resume poll, camera retry after INTERNAL.BAD_OUTPUT, do-not-blind-retry on same fingerprint.
- Fal: confirm $ , resume task id, remake = new paid job.
- Assemble: if spoken without Fal → `LIPSYNC_REQUIRED` error. Overlay path deprecated for Final.

### Hard / duplicated UX

- Lane A boards vs Lane B engine cards vs `ContentFamixaStudioView` vs Lab vs Factory — four “make video” mental models.
- **Thoại** tab is mostly status; actual TTS is on Kịch bản / Fal / assemble.
- Hoàn thiện + Xuất bản both show EpisodeReel + Preview TTS.
- Director still contains the old full studio (credits, engine picker, Fal model, timeline) after Staff already has boards.
- Nhân vật shows Studio **and** Image Generation Execution (engine Shot 01).
- Opening a scene jumps to **engine** workspace, not the series shot the staff just made.
- Header “Trạng thái: Hoàn thành” uses track.done, which can read complete when publish is still blocked.
- Technical strings still leak: MUTE_TAKE, FINAL_SOURCE, Fal $, Runway cr, sync_mode, Visual Mode enums on badges.

---

## Shot Lifecycle

**Assumed:** Draft → Keyframe → Video → Voice → Lip-sync → Mix → Final  

**Actual (Lane A graph + FIX_V1 gate):**

```
SHOT EXISTS (after scene split)
  → VISUAL_READY     approved canonical KF
  → VOICE_READY      all DialogueCues have speaker + voiceId + audio + duration
                     (empty cues = silent, voice not required)
  → VIDEO_READY      mute I2V take exists
  → LIPSYNC_READY    Fal READY, or NOT_REQUIRED if silent,
                     or UNSUPPORTED if multi-speaker
  → MIX_READY        server mix intent applied / silent take ready
  → FINAL_READY      gate — client cannot set this
  → DIRECTOR_REVIEW
  → APPROVED
```

I2V is allowed **before** Voice. Final is not.

`SeriesShotRun.status` is still the older story/approve enum (`story_locked`, `approved`, …), **not** the new production stage enum. Stage is **derived** by `videoProductionPreflight` / `shotProductionInputOf`.

`finalSource` on the run: `FAL` | `RUNWAY_TTS` | `RUNWAY` | `NONE`. `RUNWAY_TTS` = preview overlay, not Final.

---

## Current APIs (production-relevant)

### Keyframe / still

- `POST /api/content/series/still`
- `POST /api/content/series/still-qa`
- Engine: `.../image-generation-execution/preflight|execute|approve|reject`
- Engine: `.../image-director-review` + approve/reject
- `GET/POST .../image-generation-contract`

### Video

- `POST /api/content/series/turbo`
- `GET /api/content/series/turbo` · `GET /api/content/series/turbo/{taskId}`
- `POST /api/content/series/take-probe` · `POST /api/content/series/take-proxy`
- Engine: `.../video-contract` + validate/approve/reject
- Engine: `.../video-generation-execution/preflight|execute|approve|reject`

### Voice

- `GET /api/content/series/voices`
- `POST /api/content/series/tts`
- Graph persist: `GET/PUT /api/content/series/pilot`, `GET/PUT/DELETE /api/content/series/builds`

### Lip-sync

- `POST /api/content/series/lipsync`
- `GET /api/content/video-audio-lipsync-pipeline/regression` (tests only)

### Mix / Final

- `POST /api/content/series/assemble` (`clips` + `mix`)
- No `POST /shots/{id}/mix` or `/finalize`

### Character / authority (do not treat as episode production)

- `/api/content/series/characters*`
- `/api/content/character-studio*`
- `/api/content/project-visual-style*`
- `/api/content/video-engine/master-reference*`, `character-dna*`, `character-reference-pack*`, `visual-system*`, identity tests, stress, master-review, PRP, production-shots, contracts, compiler

---

## Current Data Model

**Lane A (episode) — JSON graph, no new tables this phase**

```
series_build
  └── SeriesPilotState
        ├── episode (title, premise, shots[])
        ├── scenes[] / sceneMasters{}
        ├── characters[]  (voiceId assigned, not canonical)
        ├── lines[] / voiceAssets{}
        ├── runs{shotId → SeriesShotRun}
        └── locks: scriptLocked, shotGraphLocked, castLocked, voiceLocked, previewApproved
```

```
FamixaSeriesShot ──1:1── SeriesShotRun
       │                      ├── keyframe* + kfApproved + visualMode
       │                      ├── takeUrl / previewUrl (MUTE)
       │                      ├── lipsyncUrl / lipsynced / finalSource
       │                      └── shotQa / turbo* / lipsync*
       └── dialogueSegmentIds[] → FamixaLine → FamixaVoiceAsset + IndexedDB MP3
```

**Lane B (engine) — SQL (existing migrations)**

`video_project`, `video_universe`, `video_production`, `video_shot_state`, `video_asset` (+ version/reference), `video_generation_job/attempt/provider_task`, `video_runway_take`, image/video contracts + executions + director reviews.

**Relationship Staff must not confuse:**  
Episode shot UUID in the graph ≠ Engine production shot GUID. Shared production note: “cảnh Minh trên Video Engine, không thuộc bản dựng đang mở.”

---

## Current Provider Integration

| Provider | What code actually does |
|---|---|
| **Gemini** | Series stills (`series/still`). Engine image execute. Character studio / master / calibration generate (separate). Vision QA. Not used for I2V or Fal. |
| **Runway** | `gen4_turbo` image-to-video only. `promptImage` = KF JPEG. `promptText` = motion. Duration 5 or 10. Mute take. Same client for Series turbo and Engine execute. |
| **ElevenLabs** | `eleven_v3` + `vi`. One spoken line per `POST series/tts`. Voice settings from acting law. No provider change this phase. |
| **Fal** | Upload take + audio; `fal-ai/sync-lipsync` (v3 / latentsync optional). Multi-speaker not sent. Confirm = paid. |
| **FFmpeg** | Merge multi-cue audio before Fal. Assemble concat + mix (dialogue/music/sfx/room, duck music, loudnorm). Canonical Final. |

Wan I2V exists on `ContentFalClient` but is **not** the Famixa staff I2V path.

---

## Current UX Problems

From the current UI/code, not a new architecture:

1. **Too many actions for one spoken shot:** lock script, map lines, assign voice, generate TTS, create KF, approve KF, confirm I2V, approve video, tick QA, confirm Fal $, assemble. Voice is a fifth tab that does not itself generate TTS.
2. **Technical leakage:** MUTE_TAKE, FINAL_SOURCE, Fal USD, Runway cr, sync_mode, Visual Mode, SHA/contracts in Director collapse, engine Shot 01 on Nhân vật.
3. **Unclear CTAs:** “Tạo video” = mute take. “Hoàn thiện” ≠ Final. “Preview TTS” looks like finish. “Gửi Director Review” disabled without explaining Fal. Outer tab still labeled “Series Famixa — EP01” for every episode.
4. **Fragmented workflow:** Desk → 9 tabs → scene opens Engine workspace → Director studio duplicate → Lab/Factory unused for this job → Calibration is another route.
5. **Can wrap, not rewrite:** Hoàn thiện pipeline list is already the right orchestration surface. Thoại + Kịch bản listen + Fal can be one Voice step in UX without new APIs.
6. **Can hide in Advanced:** Engine card stack, Golden Shot, PVS/VUA/CDL/Calibration, Fal model/sync, credit ledger, Visual System, identity tests.

---

## What Is Already Working

| Piece | Status |
|---|---|
| Series desk create/open/delete build | **working** |
| Script lock + scene/shot graph | **working** |
| Character Studio lock | **working** |
| Gemini series KF | **working** |
| Runway mute I2V + approve | **working** |
| ElevenLabs TTS + IndexedDB | **working** |
| Fal single-speaker + merge cues | **implemented / structurally working** (not live-proven this phase) |
| Server mix + duck | **implemented** (server path now used) |
| Final gate (no overlay Final) | **implemented** |
| Staff stage board | **implemented** (new) |
| Multi-speaker Fal | **known limitation** (block Final) |
| Phoneme / viseme | **not implemented** |
| Canonical Voice ID | **not implemented** (`canonical: false`) |
| Last frame I2V | **not implemented** |
| Dedicated Mix stage UI | **partial** (inside assemble only) |
| Dedicated `/shots/{id}/finalize` | **not implemented** (assemble + gate) |
| Engine Lane B episode production | **partial** — contracts/execute exist; Staff episode does not drive them |
| Lab / Factory for Famixa EP | **not this pipeline** |
| Scene 01 production KF | **known invalid** (`INVALID_REFERENCE_PIPELINE`) — historical, do not regen from UX |

---

## What MUST NOT Be Changed

UX redesign must not:

- Mutate Character Master / DNA / PRP / CRP / Studio lock
- Mutate PVS / VUA / CDL / Project Visual Mode / Visual Universe
- Regenerate Calibration or 24 character pixels
- Send character sheet / photoreal Canon to Runway
- Add a second visual compiler or style prose into I2V
- Change providers/models to “fix quality” (`gen4_turbo`, `eleven_v3`, Fal sync-lipsync)
- Treat mute I2V or TTS overlay as Final
- Invent Voice IDs
- Unlock multi-speaker by overlaying TTS
- Write Scene 01 as valid without a separate Director generate
- Add migrations for convenience
- Merge Content auth with Pharmacy / Family OS
- Let client set `FINAL_READY`

---

## Recommended UX Integration Points

Reuse; do not rewrite backend:

| UX step | Call |
|---|---|
| Episode list | `fetchContentSeriesBuilds` / `putContentSeriesBuild` |
| Persist graph | `putContentSeriesPilot` / build PUT |
| KF | `generateContentSeriesStill` + existing approve on `runs` |
| I2V | `startContentSeriesTurbo` + poll |
| Voice | `previewContentSeriesTts` + `fetchContentSeriesVoices` |
| Lip-sync | `startContentSeriesLipsync` (`voices[]`) |
| Final | `assembleContentSeriesCut` (`mix`) |
| Gate / copy | `videoProductionPreflight`, `staffStageCopy`, `episodeCanFinalize`, `finalSourceBlockReason` |
| Next action | `nextWorkCopy`, `deriveSeriesTrack` |
| Shot catalog | `ContentFamixaBuildImageBoard` / `VideoBoard` / `ShotCatalog` |
| Stage list | `ContentFamixaShotPipelineList` |
| Character lock display | `ContentFamixaCharacterStudio` (read + lock only) |

New UX can be a shell over these functions. `ContentFamixaSeriesTab` already owns the actions; it is the orchestration host.

---

## Technical Debt Relevant to UX

Only debt that blocks or confuses a new shell:

1. **`ContentFamixaSeriesTab.tsx` is the god host** (~7k lines): boards + director studio + assemble + Fal + TTS. New UX should wrap actions, not duplicate them.
2. **Two shot IDs** (graph vs engine). Scene-open currently mounts engine workspace — a redesign must pick one surface or staff will “lose” the episode shot.
3. **Thoại tab does not generate TTS** — orchestration gap, not a missing API.
4. **Stage enum is derived**, not stored on `SeriesShotRun.status` — UI must keep using preflight, not invent a second status column without a later persistence decision.
5. **Finish/Publish duplicate reel + preview.**
6. **Director collapse still exposes full engine + old studio** — will fight any “simple staff” IA if left visible by default.
7. **Outer Videos page** still mixes Series / Lab / Factory / Engine card.
8. **Pre-existing UX smoke 12c** (`không mở đường tạo hình`) — look-path copy is inconsistent; redesign should not re-open Look as a staff step.

Not UX-blocking: FFmpeg encode quality, Runway motion quality, missing phonemes, 5/10s provider cap.

---

## Current User Journey

There is no “Create Project”. Actual path:

1. `/content/videos` → Series tab → Desk.
2. **Tạo video mới** → `series_build` (episode code/title/premise/optional script).
3. **Kịch bản** → paste/edit → khóa.
4. **Chia cảnh** → shots + dialogue map → khóa graph.
5. **Nhân vật** → Character Studio (Minh already LOCKED in product). Assign `voiceId` if missing. Do not rebuild Master.
6. **Thoại** → see assignment / missing audio. Generate TTS via script listen (or later Fal/assemble).
7. **Hình ảnh** → Tạo ảnh (Gemini) → Duyệt ảnh per shot.
8. **Video** → Tạo video (Runway mute) → Confirm cr → Duyệt video.
9. **Hoàn thiện** → if spoken: Create Lip-Sync (Fal $) → wait. Preview TTS is not Final.
10. When `episodeCanFinalize` → **Gửi Director Review** / Xuất bản. Assemble MP4 is the file.

If the user opens a scene from the list, they may land on the **engine** shot workspace (Lane B), which is a different object.

---

## UX Redesign Readiness

**Yes — a Staff Video Studio UX redesign can start now**, as **frontend orchestration** over existing APIs and the FIX_V1 gate.

**Backend change required?** Not for the first UX pass.

**Minimum backend only if UX insists on:**

- Persisted stage column (optional; can keep deriving),
- Shot-scoped REST aliases (`/shots/{id}/lipsync`) — sugar, not required,
- Multi-speaker Fal — product limitation, not a UX blocker if the UI states it.

**Frontend-only:**

- One linear Staff shell (list → episode → shot strip).
- Bind buttons to existing `generateSceneKf` / `startSceneTurbo` / `startLipsync` / `assembleCut` / `loadCueAudio`.
- Hide Director/engine/calibration behind Advanced.
- Use `staffStageCopy` + `nextWorkCopy` as the only progress language.
- Do not add a new Final path.

---

## A. KEEP

- Lane A APIs: still, turbo, tts, lipsync, assemble, builds, pilot.
- FIX_V1 preflight / finalize / merge-cues / server mix.
- Mute I2V + Fal as the spoken path.
- Character Studio lock + Visual Mode `3D_STYLIZED_REALISM`.
- IndexedDB KF/TTS (do not upload dataUrls).
- Confirm-before-pay (Runway cr, Fal $).

## B. WRAP

- `ContentFamixaSeriesTab` action functions.
- ShotCatalog create/approve/retry.
- `ContentFamixaShotPipelineList` + `ContentFamixaBuildVoiceBoard`.
- `nextWorkCopy` / `deriveSeriesTrack`.
- Series desk create/open.

## C. SIMPLIFY

- 9 staff tabs → fewer steps that match real dependencies (Story → Cast → Pictures → Talking picture → Finish).
- Merge Thoại + Fal + assemble voice checks into one Voice/Lip-sync surface.
- One reel (Finish **or** Publish, not both).
- Staff copy: “Hình ảnh đã tạo” / “Chờ Lip-sync” — already started; apply everywhere (header “Hoàn thành”, Videos tab label).
- Hide engine cards, Golden, PVS/VUA/CDL, Fal model, credits from default Staff.

## D. CHANGE (only if UX cannot ship without it)

- Scene click currently opens Engine workspace — Staff scene should open the **series shot**, or the jump must be labeled “cảnh kỹ thuật”.
- Thoại tab should trigger existing TTS (`loadCueAudio`) if the redesign promises “make voice here”.
- Optional later: persist derived stage — **stop and declare schema** before migrating.

## E. DO NOT TOUCH

- Character Master / DNA / PRP / CRP / locked Studio identity
- PVS / VUA / CDL / Visual Universe / Visual Mode Authority
- Identity-conditioned Calibration + 24 character pixels
- Scene 01 `INVALID_REFERENCE_PIPELINE` (historical)
- Runway input rules (approved KF only, motion-only, no sheet)
- Provider/model choices this phase
- Finalization gate (no overlay Final, no multi-speaker fake)
- Pharmacy / Family OS / Local OS
- Database migrations
- Engine Golden SH01-01 as a staff episode substitute

---

**Conclusion:** The backend production contract is in place. UX redesign is an orchestration and hiding problem, not a missing-engine problem. Do not invent a third pipeline to make the studio feel simpler.
