/** Famixa Production Workflow V2. Graph ≠ queue. Shorts come from Script Beats only. */

import { kfIsApproved, sceneCodeOfShot } from './content-famixa-batch-plan';
import { shotsInInclusiveRange } from './content-famixa-preview-cut';
import { pickShots, shotProdStatus } from './content-famixa-scene-first';
import {
  episodeShots,
  shotHasValidAction,
  shotRunOf,
  studioShotCode,
  type FamixaSeriesShot,
  type SeriesPilotState,
  type SeriesShotRun,
} from './content-famixa-series';
import { needsInheritanceReview } from './content-famixa-story-memory';

export type ProdV2Step = 'script' | 'voice' | 'shorts' | 'image' | 'video' | 'preview' | 'final';

export type ShortSimpleStatus =
  | 'SCRIPT READY'
  | 'VOICE READY'
  | 'KF DRAFT'
  | 'KF READY'
  | 'VIDEO READY'
  | 'FINAL'
  | 'HOLD'
  | 'SKIP';

export const PROD_V2_STEPS: { id: ProdV2Step; n: string; label: string }[] = [
  { id: 'script', n: '1', label: 'Kịch bản' },
  { id: 'voice', n: '2', label: 'Thoại' },
  { id: 'shorts', n: '3', label: 'Shorts' },
  { id: 'image', n: '4', label: 'Hình' },
  { id: 'video', n: '5', label: 'Video' },
  { id: 'preview', n: '6', label: 'Preview' },
  { id: 'final', n: '7', label: 'Final' },
];

/** Script-order shorts that may enter production. Empty SH never appear. */
export function productionShorts(state: SeriesPilotState) {
  return episodeShots(state).filter((s) => {
    const run = shotRunOf(state, s);
    if (run.prodSkip) return false;
    return shotHasValidAction(s, run);
  });
}

export function selectFirstN(shots: FamixaSeriesShot[], n: number) {
  if (!shots.length) return { fromId: undefined as string | undefined, toId: undefined as string | undefined };
  const k = Math.max(1, Math.min(n, shots.length));
  return { fromId: shots[0]!.id, toId: shots[k - 1]!.id };
}

export function selectPreset(shots: FamixaSeriesShot[], preset: 5 | 10 | 20 | 'all') {
  if (preset === 'all') return selectFirstN(shots, shots.length);
  return selectFirstN(shots, preset);
}

export function selectedShorts(shots: FamixaSeriesShot[], fromId?: string, toId?: string, pickIds?: string[]) {
  if (!shots.length) return [];
  if (pickIds?.length) return pickShots(shots, pickIds);
  return shotsInInclusiveRange(shots, fromId || shots[0]?.id, toId || shots.at(-1)?.id);
}

export function selectionSummary(shots: FamixaSeriesShot[], fromId?: string, toId?: string, pickIds?: string[]) {
  const sel = selectedShorts(shots, fromId, toId, pickIds);
  const sec = sel.reduce((n, s) => n + (s.editSeconds && s.editSeconds > 0 ? s.editSeconds : s.seconds === 10 ? 10 : 5), 0);
  const from = sel[0] ? studioShotCode(sel[0], shots) : '—';
  const to = sel.at(-1) ? studioShotCode(sel.at(-1)!, shots) : '—';
  return {
    count: sel.length,
    total: shots.length,
    sec,
    from,
    to,
    ids: sel.map((s) => s.id),
    shots: sel,
  };
}

export function clampShortSeconds(n: number): 5 | 10 {
  return n >= 8 ? 10 : 5;
}

export function setShortSeconds(state: SeriesPilotState, shotId: string, seconds: 5 | 10): SeriesPilotState {
  const ep = state.episode;
  if (!ep) return state;
  return {
    ...state,
    episode: {
      ...ep,
      shots: ep.shots.map((s) => (s.id === shotId ? { ...s, seconds, clock: `${seconds}s` } : s)),
    },
  };
}

export function shortSimpleStatus(state: SeriesPilotState, shot: FamixaSeriesShot): ShortSimpleStatus {
  const run = shotRunOf(state, shot);
  if (run.prodSkip) return 'SKIP';
  const prod = shotProdStatus(state, shot);
  if (prod === 'HOLD') return 'HOLD';
  if (prod === 'VIDEO APPROVED') return 'FINAL';
  if (prod === 'VIDEO READY' || prod === 'VIDEO QUEUED') return 'VIDEO READY';
  if (prod === 'KF APPROVED') return 'KF READY';
  if (prod === 'KF DRAFT') return 'KF DRAFT';
  if (state.voiceLocked) return 'VOICE READY';
  return 'SCRIPT READY';
}

export function kfNeedIds(state: SeriesPilotState, shots: FamixaSeriesShot[]) {
  return shots.filter((s) => {
    const run = shotRunOf(state, s);
    return shotHasValidAction(s, run) && !run.prodSkip && !run.keyframeDataUrl && run.status !== 'approved';
  }).map((s) => s.id);
}

export function isOperatorSuppliedKf(run: SeriesShotRun) {
  if (!run.keyframeDataUrl?.startsWith('data:image')) return false;
  if (run.keyframeInheritedFrom) return false;
  const name = (run.keyframeFileName || '').toLowerCase();
  if (/-canon\.(png|jpe?g|webp)$/.test(name) || /kf-.+-canon/.test(name)) return false;
  return Boolean(run.keyframeFileName || run.keyframePath);
}

/** First user-attached still in the range — later Shorts draw from this frame. */
export function visualLockShot(state: SeriesPilotState, shots: FamixaSeriesShot[]) {
  return (
    shots.find((s) => isOperatorSuppliedKf(shotRunOf(state, s))) ||
    shots.find((s) => shotRunOf(state, s).keyframeDataUrl?.startsWith('data:image'))
  );
}

/** Shorts after the template that still need a scene still (empty or leftover Canon sheet). */
export function kfFollowIds(state: SeriesPilotState, shots: FamixaSeriesShot[]) {
  const lock = visualLockShot(state, shots);
  return shots
    .filter((s) => {
      const run = shotRunOf(state, s);
      if (!shotHasValidAction(s, run) || run.prodSkip || run.status === 'approved') return false;
      if (lock && s.id === lock.id) return false;
      if (isOperatorSuppliedKf(run)) return false;
      return true;
    })
    .map((s) => s.id);
}

export function videoNeedIds(state: SeriesPilotState, shots: FamixaSeriesShot[]) {
  return shots.filter((s) => {
    const run = shotRunOf(state, s);
    if (!shotHasValidAction(s, run) || run.prodSkip) return false;
    if (run.status === 'approved') return false;
    if (!kfIsApproved(run)) return false;
    return !run.previewUrl?.trim();
  }).map((s) => s.id);
}

/** V2 I2V: script + voice + shot graph. Does not wait for 9:16 short lock. */
export function canWorkV2Scene(state: SeriesPilotState) {
  if (!state.scriptLocked) return false;
  if (!state.voiceLocked) return false;
  if (needsInheritanceReview(state)) return false;
  if ((state.episode?.shots.length ?? 0) > 0 && state.shotGraphLocked === false) return false;
  return true;
}

export function v2SceneBlockReason(state: SeriesPilotState): string | undefined {
  if (!state.scriptLocked) return 'Khóa kịch bản trước.';
  if (needsInheritanceReview(state)) return 'Duyệt kế thừa EP trước.';
  if (!state.voiceLocked) return 'Khóa Full Voice trước khi gửi Runway.';
  if (state.shotGraphLocked === false) return 'Duyệt cách chia Short rồi mới gửi video.';
  return undefined;
}

/** KF must be approved. Draft stills do not spend I2V credit. */
export function readyV2VideoShots(state: SeriesPilotState, shots: FamixaSeriesShot[]) {
  const ready: FamixaSeriesShot[] = [];
  const blocked: { shot: FamixaSeriesShot; reason: string }[] = [];
  for (const s of shots) {
    const run = shotRunOf(state, s);
    if (run.prodSkip || !shotHasValidAction(s, run)) {
      blocked.push({ shot: s, reason: 'HOLD / không có Action' });
      continue;
    }
    if (run.status === 'approved' || run.previewUrl?.trim()) continue;
    const action = (run.shotAction || s.story || s.motionPromptVi || '').trim();
    if (!run.keyframeDataUrl) {
      blocked.push({ shot: s, reason: 'thiếu KF' });
      continue;
    }
    if (!kfIsApproved(run)) {
      blocked.push({ shot: s, reason: 'KF DRAFT — duyệt trước I2V' });
      continue;
    }
    if (!action) {
      blocked.push({ shot: s, reason: 'thiếu Action' });
      continue;
    }
    ready.push(s);
  }
  return { ready, blocked };
}

export function videoCreditsFor(shots: FamixaSeriesShot[], creditsOf: (seconds: number) => number) {
  return shots.reduce((n, s) => n + creditsOf(s.seconds === 10 ? 10 : 5), 0);
}

export function canEnterProdStep(state: SeriesPilotState, step: ProdV2Step): string | undefined {
  if (step === 'script') return undefined;
  if (!state.scriptLocked && !state.voiceLocked) return 'Khóa kịch bản trước.';
  if (step === 'voice') return undefined;
  if (!state.voiceLocked) return 'Duyệt thoại (VOICE LOCKED) trước khi chia Short / tạo hình.';
  if (step === 'shorts') return undefined;
  if (state.shotGraphLocked === false) return 'Duyệt cách chia Short rồi mới tạo hình.';
  if (productionShorts(state).length === 0) return 'Không có Short hợp lệ từ kịch bản.';
  return undefined;
}

export function shortsByScene(shots: FamixaSeriesShot[]) {
  const out: { scene: string; shots: FamixaSeriesShot[] }[] = [];
  for (const s of shots) {
    const sc = sceneCodeOfShot(s) || 'SC';
    const last = out.at(-1);
    if (last && last.scene === sc) last.shots.push(s);
    else out.push({ scene: sc, shots: [s] });
  }
  return out;
}
