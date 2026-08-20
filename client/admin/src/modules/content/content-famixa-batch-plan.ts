/** Scene batch orchestration on the existing series graph. Does not invent story. */

import {
  shotCharacterIds,
  shotRunOf,
  studioShotCode,
  type FamixaSeriesShot,
  type SceneContinuityLock,
  type SeriesPilotState,
  type SeriesShotRun,
} from './content-famixa-series';

export type SceneKfMode = 'new' | 'reuse_previous' | 'reuse_baseline';

export type SceneKfPlanRow = {
  shotId: string;
  code: string;
  mode: SceneKfMode;
  sourceShotId?: string;
  sourceCode?: string;
  reason: string;
  hasKf: boolean;
  kfApproved: boolean;
  forceNew: boolean;
};

const NEW_FRAME =
  /close-?up|cận cảnh|chi tiết|insert|bài kiểm tra|màn hình|tin nhắn|\bsms\b|điện thoại|tay cầm/i;
const PLACE_SHIFT = /bước vào|đi vào|vào lớp|ra khỏi|sang phòng|mở cửa vào|rời khỏi|vào nhà|ra đường/i;

export function sceneCodeOfShot(shot: FamixaSeriesShot) {
  const raw = shot.sceneId || shot.scene || shot.id;
  return raw.match(/SC\s*\d+/i)?.[0]?.replace(/\s+/g, '').toUpperCase() ?? '';
}

export function shotsInScene(shots: FamixaSeriesShot[], scene?: string) {
  const sc = (scene ?? '').match(/SC\s*\d+/i)?.[0]?.replace(/\s+/g, '').toUpperCase();
  if (!sc) return shots;
  return shots.filter((s) => sceneCodeOfShot(s) === sc);
}

export function kfIsApproved(run: SeriesShotRun) {
  return Boolean(run.keyframeDataUrl) && run.status !== 'story_locked';
}

function normPlace(s?: string) {
  return (s ?? '').replace(/\s+/g, ' ').trim().toLowerCase();
}

function sameChars(a: FamixaSeriesShot, b: FamixaSeriesShot) {
  return [...shotCharacterIds(a)].sort().join() === [...shotCharacterIds(b)].sort().join();
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
  const baseline = sceneShots[0];
  const rows: SceneKfPlanRow[] = [];
  for (let i = 0; i < sceneShots.length; i++) {
    const shot = sceneShots[i]!;
    const run = shotRunOf(state, shot);
    const action = (run.shotAction || shot.story || '').trim();
    const forceNew = Boolean(run.kfForceNew);
    let mode: SceneKfMode = 'new';
    let source: FamixaSeriesShot | undefined;
    let reason = 'Shot đầu cảnh';

    if (i === 0 || forceNew) {
      mode = 'new';
      reason = forceNew ? 'Bạn chọn KF mới' : 'Shot đầu cảnh';
    } else if (NEW_FRAME.test(blobOf(shot, action))) {
      mode = 'new';
      reason = 'Đổi khung / cận cảnh';
    } else if (
      PLACE_SHIFT.test(blobOf(shot, action)) ||
      (shot.location &&
        baseline?.location &&
        normPlace(shot.location) &&
        normPlace(shot.location) !== normPlace(baseline.location))
    ) {
      mode = 'new';
      reason = 'Đổi bối cảnh / vị trí';
    } else {
      const prev = sceneShots[i - 1];
      const prevRow = rows[i - 1];
      const samePlace =
        Boolean(prev) &&
        sameChars(shot, prev!) &&
        (!shot.location || !prev!.location || normPlace(shot.location) === normPlace(prev!.location));
      if (samePlace && prev && prevRow) {
        source =
          prevRow.mode === 'new'
            ? prev
            : sceneShots.find((s) => s.id === prevRow.sourceShotId) || baseline || prev;
        mode = source.id === baseline?.id ? 'reuse_baseline' : 'reuse_previous';
        reason = 'Cùng chỗ · cùng CHAR';
      } else {
        mode = 'new';
        reason = 'Khác CHAR hoặc chỗ — KF mới';
      }
    }

    rows.push({
      shotId: shot.id,
      code: studioShotCode(shot),
      mode,
      sourceShotId: source?.id,
      sourceCode: source ? studioShotCode(source) : undefined,
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
    if (row.mode === 'new' || !row.sourceShotId) continue;
    const dst = byId.get(row.shotId);
    const src = byId.get(row.sourceShotId);
    if (!dst || !src) continue;
    if (!shotRunOf(next, src).keyframeDataUrl) continue;
    next = copyShotKeyframe(next, src, dst);
  }
  return next;
}

export function sceneKfToGenerate(sceneShots: FamixaSeriesShot[], plan: SceneKfPlanRow[], state: SeriesPilotState) {
  return sceneShots.filter((s) => {
    const row = plan.find((p) => p.shotId === s.id);
    if (!row || row.mode !== 'new') return false;
    return !shotRunOf(state, s).keyframeDataUrl;
  });
}

export function readySceneVideoShots(
  state: SeriesPilotState,
  sceneShots: FamixaSeriesShot[],
  lock: SceneContinuityLock,
) {
  const ready: FamixaSeriesShot[] = [];
  const blocked: FamixaSeriesShot[] = [];
  for (const s of sceneShots) {
    const run = shotRunOf(state, s);
    if (run.status === 'approved' || run.previewUrl?.trim()) continue;
    const action = (run.shotAction || s.story || '').trim();
    if (kfIsApproved(run) && action && lock.locked) ready.push(s);
    else blocked.push(s);
  }
  return { ready, blocked };
}

export function modeLabel(row: SceneKfPlanRow) {
  if (row.mode === 'new') return 'KF mới';
  if (row.mode === 'reuse_baseline') return `Kế thừa ${row.sourceCode || 'KF đầu cảnh'}`;
  return `Reuse ${row.sourceCode || 'KF trước'}`;
}
