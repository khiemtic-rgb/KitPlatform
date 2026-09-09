# FAMIXA_SHOT_PRODUCTION_ORCHESTRATION_V1

Status: **IMPLEMENTED (orchestration MVP, not live provider E2E)**  
Date: 2026-09-04  
Lane: FAMIXA Series / Lane A only  
Paid generate during coding: **not run**

Shot is the production unit. The orchestrator sequences existing Lane A actions. It does not create a third pipeline, a new authority, or a provider rewrite.

---

## A. Files changed

- `client/admin/src/modules/content/ContentFamixaSeriesTab.tsx` — thin bind only (overview panel + adapters). Old Staff tabs / Director / Advanced remain.
- `client/admin/src/modules/content/famixa-video-audio-lipsync-pipeline.ts` — `shotProductionInputOf` already extracted here (no new persisted columns).
- `docs/novixa/03-solution/famixa-shot-production-orchestration-v1.md` — this report.

No migration. No C# provider change. No Character / PVS / VUA / CDL / Visual Mode / Calibration change.

## B. New components

`client/admin/src/modules/content/ContentFamixaShotProduction/`

| File | Role |
|---|---|
| `ShotProductionIntent.ts` | `ShotProductionIntent` (orchestration only, not authority) |
| `ShotProductionState.ts` | derived `ShotProductionStage` |
| `ShotProductionOrchestrator.ts` | `produceShot` / `nextShotProductionCommand` |
| `ShotProductionActions.ts` | retry scope + conservative invalidation |
| `ShotProductionErrors.ts` | staff copy |
| `ShotProductionVoice.ts` | reuse `loadCueAudio` / existing TTS |
| `ShotProductionAssemble.ts` | one-shot body for existing assemble API |
| `ShotProductionCard.tsx` | one Staff CTA |
| `ShotProductionProgress.tsx` | Voice / Picture / Video / Lip-sync / Final |
| `ShotProductionWorkspace.tsx` | isolated Staff surface |
| `index.ts` | barrel |

Feature flag: `localStorage.famixa-shot-orch !== '0'` (default on). Set `famixa-shot-orch=0` to hide the new panel.

## C. Existing functions reused

- `shotProductionInputOf`, `videoProductionPreflight`, `episodeCanFinalize`, `characterVoiceProfile`
- `i2vDurationForDialogue`, `dialogueExceedsI2vCap`, `retryTouches`, `mergeSameSpeakerCues`
- `linesForShot`, `multiSpeakerBlock`, `shotRunOf`
- `loadCueAudio` → `previewContentSeriesTts`
- `generateSceneKf` / existing KF approve / reject
- `startSceneTurbo` (existing Runway confirm)
- `startLipsync` (existing Fal confirm)
- `assembleContentSeriesCut` / `assembleMixPayload` flags (room / music / loudnorm)
- `nextWorkCopy`, `deriveSeriesTrack`, `staffStageCopy` (old boards unchanged)

## D. API calls reused

Unchanged Lane A:

- `POST /content/series/still`
- `POST /content/series/turbo`
- `POST /content/series/tts`
- `POST /content/series/lipsync`
- `POST /content/series/assemble`

No new endpoint. Orchestrator files do not call providers.

## E. State transitions

Derived only (`ShotProductionStage`). `SeriesShotRun.status` is not replaced.

Spoken single-speaker:

`DRAFT → VOICE_REQUIRED → VISUAL_REQUIRED (WAIT_APPROVAL) → VIDEO_REQUIRED (CONFIRM_MOTION) → LIPSYNC_REQUIRED (CONFIRM_LIPSYNC) → ENSURE_MIX → FINAL_READY / WATCH`

Silent:

`VISUAL_REQUIRED → WAIT_APPROVAL → VIDEO_REQUIRED → MIX / WATCH`

Blocks (no workaround):

- `VOICE_NOT_ASSIGNED`
- `MULTI_SPEAKER_LIPSYNC_UNSUPPORTED`
- `NEEDS_VISUAL_REVIEW` (rejected KF)
- `DURATION_MISMATCH` (voice longer than provider 5/10)
- `DO_NOT_BLIND_RETRY`
- Character / Visual Mode edits from a shot

Client does not set `FINAL_READY`. Gate remains `videoProductionPreflight` / `episodeCanFinalize`.

## F. Retry behavior

Stage-scoped via `retryScope` → existing `retryTouches`:

| Retry | Regenerates | Does not touch |
|---|---|---|
| Voice | voice | KF, I2V, lip-sync, mix |
| Picture | KF | voice, I2V |
| Motion | I2V | voice, KF |
| Lip-sync | lip-sync | voice, KF, I2V |
| Mix | mix | voice, KF, I2V, lip-sync |

Same fail fingerprint → `DO_NOT_BLIND_RETRY`.

## G. Invalidation behavior (conservative)

| Edit | Invalidate |
|---|---|
| Dialogue text | Voice + Lip-sync. KF only if visual intent later changes (not auto). |
| Voice ID | Voice + Lip-sync. Not KF. |
| Emotion / acting | Voice + Motion + Lip-sync. Not KF in this pass. |
| Action / blocking | Picture + Motion. Voice stays. |
| Character identity | BLOCK `CHARACTER_AUTHORITY_REQUIRED` |
| Visual Mode | BLOCK `VISUAL_MODE_AUTHORITY_REQUIRED` |

## H. Credit confirmation

Orchestrator emits `CONFIRM_MOTION` / `CONFIRM_LIPSYNC` and **does not** call paid adapters until Staff confirms.

UI copy:

- `Đang chờ xác nhận tạo video`
- `Đang chờ xác nhận Lip-sync`

Paid click then calls existing `startSceneTurbo` / `startLipsync`, which still own the confirm modals. No bypass.

## I. Test results

Command (local, no provider):

```
.\node_modules\.bin\tsx src\modules\content\famixa-shot-production-orchestration.smoke.ts
.\node_modules\.bin\tsx src\modules\content\famixa-video-audio-lipsync-pipeline-fix.smoke.ts
```

Covered: silent; spoken single-speaker; missing Voice ID; missing audio; multi-speaker block; KF rejected; I2V pending; I2V failed; Lip-sync failed; Mix gate; Final gate; retry invalidation; no blind retry; confirmation required; voice reuse; no invented Voice ID; TTS overlay not Final; old tabs still present.

Also re-ran `famixa-video-audio-lipsync-pipeline-fix.smoke.ts` and `kit-video-production-os.smoke.ts` — PASS.

`kit-video-production-ux.smoke.ts` still fails **12c** (`không mở đường tạo hình` is not in boards). Pre-existing. Not invented here.

**Live Gemini / Runway / ElevenLabs / Fal E2E was not run.** Do not treat this as provider-success.

## J. Known limitations

1. Existing `generateSceneKf` still requires episode `VOICE LOCKED`. One-click from an unlocked episode will create shot voice then stop at the current picture gate. Orchestrator does not fake-lock voice.
2. Continuity reference is previous approved KF only, and only as input the current KF path already accepts. Invalid Scene 01 reference is not used as canonical.
3. I2V duration remains provider 5 / 10. Longer voice → `DURATION_MISMATCH`, no silent trim/stretch.
4. Multi-speaker stays BLOCK. No TTS overlay Final.
5. Episode assemble path is unchanged. One-shot mix builds one clip for the same assemble API. Spoken mix without Fal is refused.
6. Old Staff / Director / Advanced / Engine UI is still the fallback.
7. `ShotProductionIntent` is not persisted and is not an authority.

## K. Manual test steps (Director)

Do not generate paid video unless authorized.

1. Login `KIT_MKT` / `admin` / `Admin@123` → Videos → Series Famixa → open a build with CHAR-001 Minh, one spoken cue, Voice ID already assigned.
2. Confirm Tổng quan still has the old production overview.
3. Below it, open **Sản xuất cảnh** / SHOT card.
4. Hide new UI: DevTools `localStorage.setItem('famixa-shot-orch','0')` then reload. Old Thoại / Hình ảnh / Video / Hoàn thiện tabs must still work.
5. Dry-run Staff CTAs only (cancel paid confirms):
   - Empty shot → **Tạo Shot** (voice then picture, then **Xem & duyệt hình**).
   - After approve → **Tạo video** shows `Đang chờ xác nhận tạo video` / existing Runway confirm. Cancel = 0 cr.
   - After mute take (if already present) → **Tạo Lip-sync** shows `Đang chờ xác nhận Lip-sync` / existing Fal confirm. Cancel = 0 Fal.
   - After Fal → **Hoàn thiện** (server assemble) then **Xem video**.
6. Multi-speaker shot (if any) must show the staff block, not a workaround.
7. Retry buttons must not offer unrelated paid stages.

Stop and wait for Director review. No commit / PR / deploy unless asked.
