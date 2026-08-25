/** Famixa Production Workflow V2. Graph ≠ queue. Shorts come from Script Beats only. */

import { kfIsApproved, sceneCodeOfShot } from './content-famixa-batch-plan';
import { approveBlockReason, visualQaAllowsApprove } from './content-famixa-visual-spec';
import { shotsInInclusiveRange } from './content-famixa-preview-cut';
import { pickShots, shotProdStatus } from './content-famixa-scene-first';
import { classifyVideoPipe, dataUriHash, hasVerifiedTake, promptHashOf, sameKfAsInternalFail, type RunwayPipeRun } from './content-famixa-runway-pipe';
import {
  compileI2vPrompt,
  episodeShots,
  i2vActionOf,
  shotHasValidAction,
  shotRunOf,
  studioShotCode,
  type FamixaSeriesShot,
  type SeriesPilotState,
  type SeriesShotRun,
} from './content-famixa-series';
import { needsInheritanceReview } from './content-famixa-story-memory';
import { linesForShot, multiSpeakerBlock } from './content-famixa-dialogue-map';

export type ProdV2Step = 'script' | 'voice' | 'shorts' | 'image' | 'video' | 'preview' | 'final';

export type ShortSimpleStatus =
  | 'SCRIPT READY'
  | 'VOICE READY'
  | 'KF DRAFT'
  | 'KF READY'
  | 'VIDEO READY'
  | 'FINAL'
  | 'HOLD'
  | 'SKIP'
  | 'QA BLOCK'
  | 'QA REVIEW';

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
  if (prod === 'QA BLOCK') return 'QA BLOCK';
  if (prod === 'QA REVIEW') return 'QA REVIEW';
  if (prod === 'VIDEO APPROVED') return 'FINAL';
  if (prod === 'VIDEO READY') return hasVerifiedTake(run) ? 'VIDEO READY' : 'KF READY';
  if (prod === 'VIDEO QUEUED') return hasVerifiedTake(run) ? 'VIDEO READY' : 'KF READY';
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

export function isLockedTemplateKf(run: SeriesShotRun) {
  if (!run.keyframeDataUrl?.startsWith('data:image')) return false;
  if (run.kfApproved === true) return true;
  return isOperatorSuppliedKf(run);
}

/** First locked / operator still — later shots draw from this frame. */
export function visualLockShot(state: SeriesPilotState, shots: FamixaSeriesShot[]) {
  return (
    shots.find((s) => isLockedTemplateKf(shotRunOf(state, s))) ||
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
      if (isLockedTemplateKf(run) || isOperatorSuppliedKf(run)) return false;
      return true;
    })
    .map((s) => s.id);
}

export function shotI2vPromptHash(state: SeriesPilotState, shot: FamixaSeriesShot, run?: SeriesShotRun) {
  const row = run || shotRunOf(state, shot);
  const action = i2vActionOf(state, shot, row) || row.shotAction || shot.story || '';
  if (!action.trim()) return '';
  return promptHashOf(compileI2vPrompt(state, shot, action));
}

export function videoNeedIds(state: SeriesPilotState, shots: FamixaSeriesShot[]) {
  return shots.filter((s) => {
    const run = shotRunOf(state, s);
    if (!shotHasValidAction(s, run) || run.prodSkip) return false;
    if (run.status === 'approved' && run.previewUrl?.trim()) return false;
    if (!kfIsApproved(run)) return false;
    if (sameKfAsInternalFail(run, dataUriHash(run.keyframeDataUrl), shotI2vPromptHash(state, s, run))) return false;
    return !run.previewUrl?.trim();
  }).map((s) => s.id);
}

export function lipsyncQaReady(run: { shotQa?: { action?: boolean; continuity?: boolean; voiceFace?: boolean } }, spoken = true) {
  if (!run.shotQa?.action || !run.shotQa?.continuity) return false;
  if (spoken && !run.shotQa.voiceFace) return false;
  return true;
}

/** Takes with video + not yet Fal-lipsynced. TTS is checked at send. Two speakers = split, not Fal. */
export function lipsyncNeedIds(state: SeriesPilotState, shots: FamixaSeriesShot[]) {
  return shots.filter((s) => {
    const run = shotRunOf(state, s);
    if (!shotHasValidAction(s, run) || run.prodSkip || shotKeepsLipsync(run)) return false;
    const lines = linesForShot(state, s);
    if (Array.isArray(s.dialogueSegmentIds) && lines.length === 0) return false;
    if (multiSpeakerBlock(lines)) return false;
    return Boolean(run.previewUrl?.trim() || run.takeUrl?.trim());
  }).map((s) => s.id);
}

export function lipsyncInFlight(run: { lipsynced?: boolean; lipsyncStatus?: string; lipsyncTaskId?: string }) {
  if (run.lipsynced) return false;
  if (!run.lipsyncTaskId?.trim()) return false;
  const st = (run.lipsyncStatus || '').toUpperCase();
  return st === 'PENDING' || st === 'RUNNING' || st === 'PROCESSING' || st === 'IN_PROGRESS';
}

/** Paste from Fal Usage / Assets — never starts a new paid job. */
export function parseFalLipsyncRef(raw: string): { url?: string; taskId?: string } {
  const s = (raw || '').trim();
  if (!s) return {};
  if (/^https?:\/\//i.test(s) && (/fal\.media/i.test(s) || /\.(mp4|webm)(\?|#|$)/i.test(s))) {
    return { url: s };
  }
  const fromPath = s.match(/requests\/([^/?#]+)/i)?.[1];
  const id = (fromPath || s).replace(/^lipsync_(v3_|v1_|ls_)?/i, '').trim();
  if (id.length >= 8 && !/\s/.test(id)) return { taskId: `lipsync_v3_${id}` };
  return {};
}

/** Fal output, or preview after a successful lipsync. */
export function lipsyncVideoUrl(run?: {
  lipsynced?: boolean;
  lipsyncUrl?: string;
  previewUrl?: string;
}) {
  const dedicated = run?.lipsyncUrl?.trim();
  if (dedicated) return dedicated;
  if (run?.lipsynced) return run.previewUrl?.trim() || undefined;
  return undefined;
}

/** Assemble / play: Fal lipsync file wins, else raw take. */
export function takeVideoUrl(run?: {
  lipsynced?: boolean;
  lipsyncUrl?: string;
  previewUrl?: string;
  takeUrl?: string;
}) {
  return lipsyncVideoUrl(run) || run?.previewUrl?.trim() || run?.takeUrl?.trim() || undefined;
}

/** Flag may vanish after F5/merge — lipsyncUrl still means keep Fal audio. */
export function shotKeepsLipsync(run?: {
  lipsynced?: boolean;
  lipsyncUrl?: string;
}) {
  return Boolean(run?.lipsynced || run?.lipsyncUrl?.trim());
}

export type FalLipsyncModel = '1.9' | 'v3' | 'ls';
export type FalLipsyncSyncMode = 'cut_off' | 'silence' | 'loop' | 'bounce' | 'remap';
export type VideoSendOpts = { remake?: boolean };
export type LipsyncSendOpts = { remake?: boolean };

/** Fal 1.9 ≈ $0.70/min. v3 ≈ $8/min. LatentSync is per-clip (see estimate). */
export const FAL_LIPSYNC_USD_PER_MIN: Record<Exclude<FalLipsyncModel, 'ls'>, number> = { '1.9': 0.7, v3: 8 };

export const FAL_LIPSYNC_TIERS: { value: FalLipsyncModel; title: string; hint: string; rate: string }[] = [
  { value: 'v3', title: 'Chuẩn · đắt', hint: 'Fal v3 — miệng sát thoại nhất', rate: '~$8/phút (~$1.33/10s)' },
  { value: '1.9', title: 'Vừa', hint: 'Fal 1.9 — đủ dùng sản xuất', rate: '~$0.70/phút (~$0.12/10s)' },
  { value: 'ls', title: 'Rẻ', hint: 'LatentSync — test / tiết kiệm', rate: '~$0.20/clip (≤40s)' },
];

export function normalizeLipsyncModel(raw?: string): FalLipsyncModel {
  if (raw === 'v3') return 'v3';
  if (raw === 'ls' || raw === 'latentsync') return 'ls';
  return '1.9';
}

export function lipsyncTierOf(raw?: string) {
  const value = normalizeLipsyncModel(raw);
  return FAL_LIPSYNC_TIERS.find((t) => t.value === value) ?? FAL_LIPSYNC_TIERS[1]!;
}

export function normalizeLipsyncSyncMode(raw?: string): FalLipsyncSyncMode {
  return raw === 'cut_off' || raw === 'silence' || raw === 'loop' || raw === 'bounce' || raw === 'remap'
    ? raw
    : 'remap';
}

export function estimateFalLipsyncUsd(seconds: number, model: FalLipsyncModel = '1.9') {
  const kind = normalizeLipsyncModel(model);
  const sec = Math.max(5, Number(seconds) || 10);
  if (kind === 'ls') return sec <= 40 ? 0.2 : Math.round(sec * 0.005 * 100) / 100;
  return Math.round((sec / 60) * FAL_LIPSYNC_USD_PER_MIN[kind] * 100) / 100;
}

export function estimateFalLipsyncUsdForShots(shots: { seconds?: number }[], model: FalLipsyncModel = '1.9') {
  return Math.round(shots.reduce((n, s) => n + estimateFalLipsyncUsd(s.seconds ?? 10, model), 0) * 100) / 100;
}

/** Estimated / billed / Fal — per selected production shots. */
export function productionCostLedger(state: SeriesPilotState, shots: FamixaSeriesShot[]) {
  const billedRunway = shots.reduce((n, s) => n + inferRunwayBilled(shotRunOf(state, s), s.seconds), 0);
  const needI2v = videoNeedIds(state, shots);
  const estimatedRunway = billedRunway + needI2v.reduce((n, id) => {
    const s = shots.find((x) => x.id === id);
    return n + clampShortSeconds(s?.seconds ?? 5) * 5;
  }, 0);
  const needFal = lipsyncNeedIds(state, shots);
  const falDone = shots.filter((s) => shotKeepsLipsync(shotRunOf(state, s)));
  const falModel = normalizeLipsyncModel(state.lipsyncModel);
  const estimatedFalUsd = estimateFalLipsyncUsdForShots(
    [...needFal.map((id) => shots.find((s) => s.id === id)!).filter(Boolean), ...falDone],
    falModel,
  );
  const confirmedFalUsd = estimateFalLipsyncUsdForShots(falDone, falModel);
  return { billedRunway, estimatedRunway, estimatedFalUsd, confirmedFalUsd, needI2v: needI2v.length, needFal: needFal.length };
}

export function lipsyncTaskPrefix(model?: string) {
  const m = normalizeLipsyncModel(model);
  if (m === 'v3') return 'lipsync_v3_';
  if (m === 'ls') return 'lipsync_ls_';
  return 'lipsync_';
}

export function parseFalJobIdFromError(raw?: string) {
  const m = (raw || '').match(/Fal job ([0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12})/i);
  return m?.[1];
}

/** Dead poll (404/405) ≠ free retry. Timeout / still-running = Hỏi lại · 0$. New Confirm = new Fal charge. */
export function shouldResumeLipsync(run: {
  lipsyncTaskId?: string;
  lipsynced?: boolean;
  lipsyncStatus?: string;
  lipsyncError?: string;
}) {
  const err = run.lipsyncError || '';
  const recovered = parseFalJobIdFromError(err);
  if (run.lipsynced) return false;
  if (/404|405|504|downstream|quá tải|chưa có khớp môi/i.test(err)) return false;
  if (!run.lipsyncTaskId?.trim() && !recovered) return false;
  const st = (run.lipsyncStatus || '').toUpperCase();
  if (st === 'CANCELLED' || st === 'SUCCEEDED') return false;
  if (st === 'FAILED' && /quá \d+ phút|vẫn chạy|Hỏi lại|chưa lấy được file/i.test(err)) return true;
  if (st === 'FAILED') return false;
  return st === 'PENDING' || st === 'RUNNING' || st === 'PROCESSING' || st === 'IN_PROGRESS' || st === 'RETRY' || Boolean(recovered);
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
    if (run.previewUrl?.trim()) continue;
    const action = (run.shotAction || s.story || s.motionPromptVi || '').trim();
    if (!run.keyframeDataUrl) {
      blocked.push({ shot: s, reason: 'thiếu KF' });
      continue;
    }
    if (sameKfAsInternalFail(run, dataUriHash(run.keyframeDataUrl), shotI2vPromptHash(state, s, run)) && !shouldResumeTurboPoll(run)) {
      blocked.push({
        shot: s,
        reason: 'INTERNAL.BAD_OUTPUT — không gửi lại cùng KF + prompt. Sửa KF hoặc đổi prompt rồi Confirm 1 job.',
      });
      continue;
    }
    if (!kfIsApproved(run)) {
      const qa = run.visualQa;
      blocked.push({
        shot: s,
        reason: !qa
          ? 'KF DRAFT — duyệt trước I2V'
          : visualQaAllowsApprove(qa)
            ? `KF DRAFT — QA ${qa.total ?? 'PASS'} đã đạt. Bấm Duyệt KF rồi Confirm 1 job.`
            : approveBlockReason(qa) || 'KF DRAFT — duyệt trước I2V',
      });
      continue;
    }
    if (!i2vActionOf(state, s, run) && !action) {
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

/** New Runway POST only if the previous attempt never created a task. Never auto-retry after a created job. */
export function canRetryTurboStart(error: string, createdTask: boolean) {
  if (createdTask) return false;
  if (isTurboDailyQuota(error)) return false;
  return isTurboRateLimit(error);
}

export function isTurboRateLimit(error: string) {
  return /429|rate.?limit|too many|giới hạn tốc độ|throttl|daily-quota|hạn mức ngày/i.test(error);
}

export function isTurboDailyQuota(error: string) {
  return /daily-quota|hạn mức ngày|daily (generation |task )?limit|task limit has been reached|too many generations/i.test(error);
}

export function parseRetryAfterSec(error: string, fallback = 30) {
  const m = error.match(/429[^0-9]{0,24}(\d{1,3})/) || error.match(/retry-after[:\s]+(\d+)/i) || error.match(/đợi (\d+)s/i);
  if (m) return Math.min(600, Math.max(5, Number(m[1])));
  return fallback;
}

const RUNWAY_QUIET_KEY = 'kit.famixa.runwayQuietUntil';

export function loadRunwayQuietUntil() {
  try {
    return Number(sessionStorage.getItem(RUNWAY_QUIET_KEY) || 0);
  } catch {
    return 0;
  }
}

export function persistRunwayQuietUntil(until: number) {
  try {
    sessionStorage.setItem(RUNWAY_QUIET_KEY, String(until));
  } catch {
    /* quota */
  }
}

export function clearRunwayQuietUntil() {
  try {
    sessionStorage.removeItem(RUNWAY_QUIET_KEY);
  } catch {
    /* */
  }
}

export function runwayQuietRemainMin(until: number, now = Date.now()) {
  if (!until || until <= now) return 0;
  return Math.max(1, Math.ceil((until - now) / 60_000));
}

export function nextRunwayQuietUntil(opts: {
  now?: number;
  daily?: boolean;
  hits?: number;
  retryAfterSec?: number;
  prev?: number;
}) {
  const now = opts.now ?? Date.now();
  const add = opts.daily
    ? 20 * 60 * 1000
    : (opts.hits ?? 0) >= 3
      ? 30 * 60 * 1000
      : Math.max((opts.retryAfterSec ?? 30) * 1000, 60_000);
  return Math.max(opts.prev ?? 0, now + add);
}

export function turboInFlight(run: { turboStatus?: string; previewUrl?: string }) {
  if (run.previewUrl?.trim()) return false;
  const st = (run.turboStatus || '').toUpperCase();
  return st === 'PENDING' || st === 'RUNNING' || st === 'PROCESSING' || st === 'IN_PROGRESS';
}

/** 429 / throttle often means the old task is still on Runway — poll it, do not POST again. */
export function shouldResumeTurboPoll(run: {
  turboTaskId?: string;
  previewUrl?: string;
  turboStatus?: string;
  turboError?: string;
}) {
  if (!run.turboTaskId?.trim() || run.previewUrl?.trim()) return false;
  const st = (run.turboStatus || '').toUpperCase();
  if (st === 'SUCCEEDED') return false;
  if (st === 'PENDING' || st === 'RUNNING' || st === 'PROCESSING' || st === 'IN_PROGRESS' || st === 'THROTTLED' || st === 'RETRY') {
    return true;
  }
  if (isTurboRateLimit(run.turboError || '')) return true;
  return false;
}

/** Confirmed spend only. Task created / FAILED ≠ billed. 5 cr/s gen4_turbo. */
export function inferRunwayBilled(
  run: { runwayBilled?: number; runwaySpent?: number; turboTaskId?: string; previewUrl?: string; videoVerified?: boolean },
  seconds: number,
) {
  if (!run.previewUrl?.trim() && !hasVerifiedTake(run)) return 0;
  if (typeof run.runwayBilled === 'number' && run.runwayBilled > 0) return run.runwayBilled;
  if (typeof run.runwaySpent === 'number' && run.runwaySpent > 0) return run.runwaySpent;
  return clampShortSeconds(seconds) * 5;
}

export function runwayEstimatedCredits(seconds: number) {
  return clampShortSeconds(seconds) * 5;
}

export function runwayCostView(run: RunwayPipeRun, seconds: number) {
  const estimated = runwayEstimatedCredits(seconds);
  if (hasVerifiedTake(run) || run.previewUrl?.trim()) {
    const actual = inferRunwayBilled(run, seconds) || estimated;
    return { phase: 'ACTUAL' as const, estimated, actual, label: `Actual: ${actual} cr` };
  }
  if (
    run.videoPipe === 'INPUT_INVALID' ||
    (run.turboStatus || '').toUpperCase() === 'BLOCKED' ||
    /RUNWAY BLOCKED|KIT PRECHECK|Width —|Chưa đo được pixel/i.test(run.turboError || '')
  ) {
    return { phase: 'NONE' as const, estimated: 0, actual: undefined, label: 'KIT PRECHECK · 0 cr' };
  }
  const failed =
    classifyVideoPipe(run) === 'RUNWAY_FAILED' ||
    (run.turboStatus || '').toUpperCase() === 'FAILED';
  if (failed) {
    return {
      phase: 'REFUND_PENDING' as const,
      estimated,
      actual: undefined,
      label: `Estimated: ${estimated} cr · FAILED · REFUND PENDING`,
    };
  }
  if (run.turboTaskId?.trim()) {
    return { phase: 'PENDING' as const, estimated, actual: undefined, label: `Estimated: ${estimated} cr · PENDING` };
  }
  return { phase: 'NONE' as const, estimated: 0, actual: undefined, label: '' };
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
