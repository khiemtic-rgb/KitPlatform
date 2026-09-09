# FAMIXA_SHOT_PRODUCTION_ORCHESTRATION_V1_HARDENING

Read-only verification after implementation. No live provider E2E.

## 1. Files changed

`ContentFamixaShotProduction/` — fingerprint, stamp, final IDB store, command/state, QA, card/workspace.

`content-famixa-series.ts` — optional `shotProduction` on existing JSON run (not a SQL stage column). Merge keeps it.

`ContentFamixaSeriesTab.tsx` — `applyProductionStamps` in persist; `readLive` from `stateRef`; assemble saves blob + stamp; watch opens assemble blob only; QA / video approve bind. Old tabs remain.

## 2. Orchestrator flow now

`produceShot` rebuilds snapshot via `readLive()` / `stateRef` after each adapter.

Free chain: ENSURE_VOICE → persist → refresh → ENSURE_PICTURE → WAIT_PICTURE_APPROVAL.

Stops (user owns): picture approve, Runway confirm, video review, QA ticks, Fal confirm, mix click.

Paid adapters run only after confirm flags or dedicated CTA that still opens existing `modal.confirm`.

Not one-click. Not unattended.

## 3. Snapshot / state

`buildShotProductionSnapshot` from graph + stamps.

Voice duration = sum `voiceAssets[].duration` (not text length).

`mixReady` = `assembleFp === current mix fp`. Not `hasLip`.

`finalReady` = approved KF + motion + (spoken: voice + lip + QA) + mix stamp. Overlay ≠ final.

Derived stage only. `SeriesShotRun.status` unchanged.

## 4. Invalidation matrix

Derived at read time (stored fp ≠ current input fp):

| Edit | Voice | KF | I2V | Lip | Mix |
|---|---|---|---|---|---|
| Dialogue | stale | keep | keep unless duration/acting in motion fp | stale | stale |
| Voice ID | stale | keep | keep | stale | stale |
| Acting | stale | visual review / kf fp includes acting | stale | stale | stale |
| Action | keep | stale | stale | stale | stale |
| Character ids vs stamp | BLOCK Character Studio | | | | |

`applyProductionStamps` restamps only when asset identity changes, not when text changes.

## 5. Fingerprint / stale

Hashes: dialogue+voiceId+acting; action+blocking+camera+emotion+cast; motion←kf+action+acting+voiceDur; lipsync←voice+motion; mix←those.

Missing stamp on legacy assets ≠ stale (old workflow). After first generate, stamps exist.

Stale voice ⇒ not READY ⇒ old lip/final not READY.

Stale KF ⇒ not approved for I2V.

## 6. QA gate

Card shows existing `shotQa` checkboxes. No default true. Fal command is `LIPSYNC_QA_REQUIRED` until action + continuity + voiceFace.

## 7. Mix / final

Lip-sync ≠ mix.

Assemble writes IndexedDB blob + `assembleFp` on the run.

Reload: mix/final from `assembleFp`, not `useState`.

Watch: assemble blob only. Never labels take/lipsync as Final.

## 8. Retry

Buttons scoped: voice / picture / motion / lipsync / mix.

`retryScope` unchanged (one stage). Lip/mix retry do not call voice/KF/I2V.

Blind retry: `sameFingerprintBlindRetry` + existing `sameFailedInput` in turbo. No auto paid retry.

## 9. Tests

`famixa-shot-production-orchestration.smoke.ts` — 32 numbered cases + async confirm/refresh. No provider.

Also re-run FIX_V1 smoke (must still pass; `shotProductionInputOf.mixReady` unchanged for episode gate).

## 10. Known gaps

- Episode `VOICE LOCKED` still required by `generateSceneKf`.
- Picture preflight may still send user to Hình ảnh tab.
- `generateSceneKf` still fire-and-forget until promise resolves; continuation uses refresh + effect.
- Video review is an orch gate (`videoApproved`) on this surface only.
- Live Gemini/Runway/ElevenLabs/Fal not run.
- UX smoke 12c still pre-existing.

## 11. One-shot manual acceptance

Ready to run **manually** (Director authorizes paid calls). Not claimed live-passed.

CHAR-001 Minh · "Con xin lỗi bố."

1. Voice assigned → Tạo thoại → duration shown.  
2. Tạo hình → duyệt hình.  
3. Tạo chuyển động → existing Runway confirm.  
4. Duyệt chuyển động.  
5. Tick 3 QA.  
6. Tạo Lip-sync → existing Fal confirm.  
7. Hoàn thiện → download + IDB.  
8. Xem video → assemble blob, not take/Fal.  
9. Đổi thoại → Voice/Lip/Final stale; KF may remain.

## 12. UX Studio can start?

**Yes, on top of this orchestrator** — as the next phase (`FAMIXA_VIDEO_STUDIO_UX_V1`).

Do not treat this card as the finished Studio. Do not claim one-click. Do not claim Mix/Final from lip-sync alone.
