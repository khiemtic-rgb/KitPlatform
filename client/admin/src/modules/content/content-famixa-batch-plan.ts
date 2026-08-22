/** Scene batch orchestration on the existing series graph. Does not invent story. */

import { actionNearlySame, kfIsApprovedStill } from './content-famixa-scene-first';
import {
  episodeShots,
  shotCharacterIds,
  shotHasValidAction,
  shotRunOf,
  studioShotCode,
  type FamixaSeriesShot,
  type SceneContinuityLock,
  type SeriesPilotState,
  type SeriesShotRun,
} from './content-famixa-series';

export type SceneKfMode = 'new' | 'reuse_previous' | 'reuse_baseline' | 'none';

export type ShotProdLane = 'scripted' | 'reuse' | 'hold' | 'skip' | 'locked';

export type SceneKfPlanRow = {
  shotId: string;
  code: string;
  mode: SceneKfMode;
  lane: ShotProdLane;
  eligible: boolean;
  sourceShotId?: string;
  sourceCode?: string;
  reason: string;
  hasKf: boolean;
  kfApproved: boolean;
  forceNew: boolean;
};

const NEW_FRAME = /close-?up|cận cảnh|chi tiết|insert|màn hình|tin nhắn|\bsms\b/i;
const PLACE_SHIFT = /bước vào|đi vào|vào lớp|ra khỏi|sang phòng|mở cửa vào|rời khỏi|vào nhà|ra đường/i;

export function sceneCodeOfShot(shot: FamixaSeriesShot) {
  const raw = shot.sceneId || shot.scene || shot.id;
  return raw.match(/SC\s*\d+/i)?.[0]?.replace(/\s+/g, '').toUpperCase() ?? '';
}

export function shotsInScene(shots: FamixaSeriesShot[], scene?: string) {
  const sc = (scene ?? '').match(/SC\s*\d+/i)?.[0]?.replace(/\s+/g, '').toUpperCase();
  const list = !sc ? shots : shots.filter((s) => sceneCodeOfShot(s) === sc);
  const seen = new Set<string>();
  return list.filter((s) => (seen.has(s.id) ? false : (seen.add(s.id), true)));
}

export function kfIsApproved(run: SeriesShotRun) {
  return kfIsApprovedStill(run);
}

function normPlace(s?: string) {
  return (s ?? '').replace(/\s+/g, ' ').trim().toLowerCase();
}

function visualCharIds(shot: FamixaSeriesShot) {
  return shotCharacterIds(shot).filter((id) => id !== 'CHAR-VO').sort();
}

function sameChars(a: FamixaSeriesShot, b: FamixaSeriesShot) {
  return visualCharIds(a).join() === visualCharIds(b).join();
}

function blobOf(shot: FamixaSeriesShot, action?: string) {
  return `${action || ''} ${shot.story || ''} ${shot.visual || ''} ${shot.location || ''}`;
}

export function continuityPlaceHint(
  lock: SceneContinuityLock,
  shot: FamixaSeriesShot,
  action?: string,
): { from: string; to: string } | undefined {
  const env = normPlace(lock.environment);
  const loc = normPlace(shot.location);
  const blob = blobOf(shot, action);
  if (!PLACE_SHIFT.test(blob)) return undefined;
  if (loc && env && loc !== env && !env.includes(loc) && !loc.includes(env.slice(0, 12))) {
    return { from: lock.environment, to: shot.location };
  }
  if (env && PLACE_SHIFT.test(blob) && !blob.toLowerCase().includes(env.slice(0, 10))) {
    return { from: lock.environment, to: (action || shot.story || shot.visual || '').slice(0, 80) };
  }
  return undefined;
}

export function buildSceneKfPlan(state: SeriesPilotState, sceneShots: FamixaSeriesShot[]): SceneKfPlanRow[] {
  const all = episodeShots(state);
  const rows: SceneKfPlanRow[] = [];
  for (let i = 0; i < sceneShots.length; i++) {
    const shot = sceneShots[i]!;
    const run = shotRunOf(state, shot);
    const action = (run.shotAction || shot.story || '').trim();
    const forceNew = Boolean(run.kfForceNew);
    const code = studioShotCode(shot, all);
    let mode: SceneKfMode = 'none';
    let lane: ShotProdLane = 'hold';
    let source: FamixaSeriesShot | undefined;
    let reason = 'HOLD — No Action';

    if (run.prodSkip) {
      lane = 'skip';
      reason = 'SKIP';
    } else if (run.status === 'approved') {
      lane = 'locked';
      mode = 'none';
      reason = 'LOCKED';
    } else if (!shotHasValidAction(shot, run)) {
      lane = 'hold';
      reason = 'HOLD — No Action';
    } else if (shot.voiceChainFrom && !forceNew) {
      source = sceneShots.find((s) => s.id === shot.voiceChainFrom) ?? all.find((s) => s.id === shot.voiceChainFrom);
      lane = 'reuse';
      mode = 'reuse_previous';
      reason = source
        ? `REUSE ${studioShotCode(source, all)} — nối thoại cùng KF`
        : 'REUSE host — nối thoại cùng KF';
    } else if (
      !forceNew &&
      i > 0 &&
      actionNearlySame(action, sceneShots[i - 1] ? (shotRunOf(state, sceneShots[i - 1]!).shotAction || sceneShots[i - 1]!.story) : '')
    ) {
      source = sceneShots[i - 1];
      lane = 'reuse';
      mode = 'reuse_previous';
      reason = source ? `REUSE ${studioShotCode(source, all)} — cùng Action` : 'REUSE — cùng Action';
    } else if (i === 0 || forceNew) {
      lane = 'scripted';
      mode = 'new';
      reason = forceNew ? 'Bạn chọn KF mới' : 'Shot đầu cảnh';
    } else if (NEW_FRAME.test(blobOf(shot, action))) {
      lane = 'scripted';
      mode = 'new';
      reason = 'Đổi khung / cận cảnh';
    } else if (
      PLACE_SHIFT.test(blobOf(shot, action)) ||
      (shot.location &&
        sceneShots[0]?.location &&
        normPlace(shot.location) &&
        normPlace(shot.location) !== normPlace(sceneShots[0].location))
    ) {
      lane = 'scripted';
      mode = 'new';
      reason = 'Đổi bối cảnh / vị trí';
    } else {
      const prevEligible = [...rows]
        .reverse()
        .find((r) => r.eligible && (r.lane === 'scripted' || r.lane === 'reuse' || r.lane === 'locked'));
      const prev = prevEligible ? sceneShots.find((s) => s.id === prevEligible.shotId) : undefined;
      const samePlace =
        Boolean(prev) &&
        sameChars(shot, prev!) &&
        (!shot.location || !prev!.location || normPlace(shot.location) === normPlace(prev!.location));
      if (samePlace && prev && prevEligible) {
        source =
          prevEligible.mode === 'new'
            ? prev
            : sceneShots.find((s) => s.id === prevEligible.sourceShotId) || prev;
        lane = 'scripted';
        mode = 'new';
        reason = `Kế thừa ${studioShotCode(source, all)} — vẽ Action mới, không copy khung`;
      } else {
        lane = 'scripted';
        mode = 'new';
        reason = 'Khác CHAR hoặc chỗ — KF mới';
      }
    }

    rows.push({
      shotId: shot.id,
      code,
      mode,
      lane,
      eligible: lane === 'scripted' || lane === 'reuse',
      sourceShotId: source?.id,
      sourceCode: source ? studioShotCode(source, all) : undefined,
      reason,
      hasKf: Boolean(run.keyframeDataUrl),
      kfApproved: kfIsApproved(run),
      forceNew,
    });
  }
  return rows;
}

export function copyShotKeyframe(
  state: SeriesPilotState,
  from: FamixaSeriesShot,
  to: FamixaSeriesShot,
): SeriesPilotState {
  const src = shotRunOf(state, from);
  const run = shotRunOf(state, to);
  if (!src.keyframeDataUrl) return state;
  return {
    ...state,
    runs: {
      ...state.runs,
      [to.id]: {
        ...run,
        keyframeDataUrl: src.keyframeDataUrl,
        keyframeFileName: run.keyframeFileName || src.keyframeFileName,
        keyframePath: run.keyframePath || src.keyframePath,
        keyframeInheritedFrom: from.id,
        kfForceNew: false,
      },
    },
    episode: state.episode
      ? {
          ...state.episode,
          shots: state.episode.shots.map((s) =>
            s.id === to.id ? { ...s, inheritFromShotId: from.id, previousShotId: s.previousShotId || from.id } : s,
          ),
        }
      : state.episode,
  };
}

export function applySceneKfReuses(
  state: SeriesPilotState,
  sceneShots: FamixaSeriesShot[],
  plan: SceneKfPlanRow[],
): SeriesPilotState {
  const byId = new Map(sceneShots.map((s) => [s.id, s]));
  let next = state;
  for (const row of plan) {
    const dst = byId.get(row.shotId);
    if (!dst) continue;
    if (row.lane === 'reuse' && row.sourceShotId) {
      const src = byId.get(row.sourceShotId);
      if (!src || !shotRunOf(next, src).keyframeDataUrl) continue;
      next = copyShotKeyframe(next, src, dst);
      continue;
    }
    const run = shotRunOf(next, dst);
    if (row.lane === 'scripted' && run.keyframeInheritedFrom && run.keyframeDataUrl) {
      next = {
        ...next,
        runs: {
          ...next.runs,
          [dst.id]: {
            ...run,
            keyframeDataUrl: undefined,
            keyframeInheritedFrom: undefined,
            status: run.status === 'approved' ? run.status : 'story_locked',
          },
        },
      };
    }
  }
  return next;
}

/** First NEW-KF shot in the scene plan — KF01 / file attach target. */
export function firstNewKfShot(sceneShots: FamixaSeriesShot[], plan: SceneKfPlanRow[]) {
  const id = plan.find((p) => p.lane === 'scripted' && p.mode === 'new')?.shotId ?? sceneShots[0]?.id;
  return sceneShots.find((s) => s.id === id);
}

export function sceneKfToGenerate(sceneShots: FamixaSeriesShot[], plan: SceneKfPlanRow[], state: SeriesPilotState) {
  return sceneShots.filter((s) => {
    const row = plan.find((p) => p.shotId === s.id);
    if (!row || row.lane !== 'scripted' || row.mode !== 'new') return false;
    if (!shotHasValidAction(s, shotRunOf(state, s))) return false;
    return !shotRunOf(state, s).keyframeDataUrl;
  });
}

export function productionQueue(plan: SceneKfPlanRow[]) {
  return plan.filter((p) => p.lane === 'scripted' || p.lane === 'reuse');
}

export function previewEligibleIds(plan: SceneKfPlanRow[]) {
  return new Set(
    plan.filter((p) => p.lane === 'scripted' || p.lane === 'reuse' || p.lane === 'locked').map((p) => p.shotId),
  );
}

export function laneLabel(row: SceneKfPlanRow) {
  if (row.lane === 'hold') return 'HOLD — No Action';
  if (row.lane === 'skip') return 'SKIP';
  if (row.lane === 'locked') return 'LOCKED';
  if (row.lane === 'reuse') return `REUSE ${row.sourceCode || ''}`.trim();
  return 'READY';
}

export function readySceneVideoShots(
  state: SeriesPilotState,
  sceneShots: FamixaSeriesShot[],
  lock: SceneContinuityLock,
) {
  const ready: FamixaSeriesShot[] = [];
  const blocked: FamixaSeriesShot[] = [];
  const plan = buildSceneKfPlan(state, sceneShots);
  for (const s of sceneShots) {
    const row = plan.find((p) => p.shotId === s.id);
    if (!row?.eligible) continue;
    const run = shotRunOf(state, s);
    if (run.status === 'approved' || run.previewUrl?.trim()) continue;
    const action = (run.shotAction || s.story || '').trim();
    if (kfIsApproved(run) && action && lock.locked) ready.push(s);
    else blocked.push(s);
  }
  return { ready, blocked };
}

export function modeLabel(row: SceneKfPlanRow) {
  if (row.lane === 'hold' || row.lane === 'skip') return laneLabel(row);
  if (row.mode === 'new') return 'KF mới';
  if (row.mode === 'reuse_baseline') return `Kế thừa ${row.sourceCode || 'KF đầu cảnh'}`;
  if (row.mode === 'reuse_previous') return `Reuse ${row.sourceCode || 'KF trước'}`;
  return laneLabel(row);
}
