/** Continuity chain on the existing shot graph. Does not invent story. */

import { actingI2vBrief, inferActingDirection } from './content-famixa-acting-law';
import { englishI2vMotion, I2V_VI_RE } from './content-famixa-i2v-en';
import {
  lockFromGraph,
  shotRunOf,
  studioShotCode,
  type FamixaSeriesShot,
  type SceneContinuityLock,
  type SeriesPilotState,
} from './content-famixa-series';

export type TransitionType =
  | 'CONTINUOUS'
  | 'CUT_ON_ACTION'
  | 'REACTION'
  | 'STATIC_CUT'
  | 'LOCATION_CHANGE';

export type ShotBeatState = {
  character: string;
  wardrobe: string;
  location: string;
  lighting: string;
  time: string;
  pose: string;
  facing: string;
  prop: string;
  camera: string;
};

export type TransitionCheck = {
  id: keyof ShotBeatState;
  ok: boolean;
  from: string;
  to: string;
};

export type ContinuityLink = {
  shotId: string;
  code: string;
  previousShotId?: string;
  nextShotId?: string;
  transitionType: TransitionType;
  start: ShotBeatState;
  action: string;
  end: ShotBeatState;
  risks: TransitionCheck[];
  blocked: boolean;
};

const PLACE_SHIFT = /bước vào|đi vào|vào lớp|ra khỏi|sang phòng|mở cửa vào|rời khỏi|vào nhà|ra đường|buổi tối/i;
const PROP_RE = /bài kiểm tra|tờ giấy|điện thoại|cơm|nồi|cặp/i;
const POSE_RE = /chạy|bước|đứng|khựng|cười|nhìn|ngồi|đưa|nhận/i;

function clip(s: string, n = 80) {
  return s.replace(/\s+/g, ' ').trim().slice(0, n);
}

function actionOf(state: SeriesPilotState, shot: FamixaSeriesShot) {
  const run = shotRunOf(state, shot);
  return clip(run.shotAction || shot.story || shot.motionPromptVi || '', 200);
}

function timeOf(blob: string, lock: SceneContinuityLock) {
  if (/buổi tối|đêm|tối/i.test(blob) || /đêm|tối/i.test(lock.environment)) return 'evening';
  if (/chiều/i.test(blob)) return 'afternoon';
  return 'day';
}

function lightingOf(time: string, lock: SceneContinuityLock) {
  if (/ấm|warm/i.test(lock.environment)) return time === 'evening' ? 'warm indoor evening' : 'warm afternoon';
  return time === 'evening' ? 'warm evening light' : 'late-afternoon light';
}

function propOf(blob: string) {
  const m = blob.match(PROP_RE);
  return m ? m[0] : '';
}

function poseOf(blob: string, fallback: string) {
  const m = blob.match(POSE_RE);
  return m ? clip(blob, 72) : fallback;
}

export function deriveStartState(
  state: SeriesPilotState,
  shot: FamixaSeriesShot,
  prevEnd?: ShotBeatState,
): ShotBeatState {
  const lock = lockFromGraph(state, shot);
  const action = actionOf(state, shot);
  const blob = `${action} ${shot.location || ''} ${lock.environment || ''}`;
  const time = timeOf(blob, lock);
  if (prevEnd && !PLACE_SHIFT.test(action)) {
    return {
      ...prevEnd,
      pose: poseOf(action, prevEnd.pose),
      facing: prevEnd.facing,
      prop: propOf(action) || prevEnd.prop,
    };
  }
  return {
    character: clip(lock.characters || shot.characters?.join(', ') || '', 80),
    wardrobe: clip(lock.wardrobe || '', 80),
    location: clip(shot.location || lock.environment || '', 80),
    lighting: lightingOf(time, lock),
    time,
    pose: poseOf(action, clip(lock.position || 'standing', 72)),
    facing: 'not at camera',
    prop: propOf(action),
    camera: clip(lock.camera || 'eye-level 35mm medium shot', 80),
  };
}

export function deriveEndState(state: SeriesPilotState, shot: FamixaSeriesShot, start: ShotBeatState): ShotBeatState {
  const action = actionOf(state, shot);
  return {
    ...start,
    pose: poseOf(action, start.pose),
    prop: propOf(action) || start.prop,
    facing: /nhìn|quay/i.test(action) ? clip(action, 60) : start.facing,
  };
}

export function inferTransitionType(shot: FamixaSeriesShot, action: string, prev?: FamixaSeriesShot): TransitionType {
  const blob = `${action} ${shot.story || ''} ${shot.location || ''}`;
  if (PLACE_SHIFT.test(blob)) {
    if (prev && shot.location && prev.location && shot.location !== prev.location) return 'LOCATION_CHANGE';
    if (/ra khỏi|vào nhà|sang phòng/i.test(blob)) return 'LOCATION_CHANGE';
  }
  if (/đưa|nhận lấy|nhận /i.test(blob)) return 'CUT_ON_ACTION';
  if (/khựng|nhìn mẹ|im lặng/i.test(blob)) return 'REACTION';
  return 'CONTINUOUS';
}

export function checkTransition(prevEnd: ShotBeatState, start: ShotBeatState): TransitionCheck[] {
  const keys: (keyof ShotBeatState)[] = [
    'character',
    'wardrobe',
    'location',
    'lighting',
    'pose',
    'facing',
    'prop',
    'camera',
  ];
  return keys.map((id) => {
    const from = (prevEnd[id] || '').toLowerCase();
    const to = (start[id] || '').toLowerCase();
    const ok =
      !from ||
      !to ||
      from === to ||
      from.includes(to.slice(0, 12)) ||
      to.includes(from.slice(0, 12));
    return { id, ok, from: prevEnd[id], to: start[id] };
  });
}

export function buildContinuityChain(state: SeriesPilotState, shots: FamixaSeriesShot[]): ContinuityLink[] {
  const links: ContinuityLink[] = [];
  let prevEnd: ShotBeatState | undefined;
  for (let i = 0; i < shots.length; i++) {
    const shot = shots[i]!;
    const prev = i > 0 ? shots[i - 1] : undefined;
    const action = actionOf(state, shot);
    const start = deriveStartState(state, shot, prevEnd);
    const end = deriveEndState(state, shot, start);
    const transitionType = shotRunOf(state, shot).transitionType || inferTransitionType(shot, action, prev);
    const risks = prevEnd && transitionType === 'CONTINUOUS' ? checkTransition(prevEnd, start) : [];
    const blocked = risks.some((r) => !r.ok && (r.id === 'wardrobe' || r.id === 'location' || r.id === 'character'));
    links.push({
      shotId: shot.id,
      code: studioShotCode(shot, shots),
      previousShotId: prev?.id,
      nextShotId: shots[i + 1]?.id,
      transitionType,
      start,
      action,
      end,
      risks,
      blocked,
    });
    prevEnd = end;
  }
  return links;
}

export function applyContinuityChain(state: SeriesPilotState, shots: FamixaSeriesShot[]): SeriesPilotState {
  const links = buildContinuityChain(state, shots);
  const runs: SeriesPilotState['runs'] = { ...state.runs };
  for (const link of links) {
    const shot = shots.find((s) => s.id === link.shotId);
    if (!shot) continue;
    const run = shotRunOf(state, shot);
    runs[shot.id] = {
      ...run,
      startState: run.startState ?? link.start,
      endState: run.endState ?? link.end,
      transitionType: run.transitionType ?? link.transitionType,
    };
  }
  const byId = new Map(links.map((l) => [l.shotId, l]));
  return {
    ...state,
    runs,
    episode: state.episode
      ? {
          ...state.episode,
          shots: state.episode.shots.map((s) => {
            const link = byId.get(s.id);
            if (!link) return s;
            return {
              ...s,
              previousShotId: s.previousShotId || link.previousShotId,
              nextShotId: s.nextShotId || link.nextShotId,
            };
          }),
        }
      : state.episode,
  };
}

function stateLine(label: string, s: ShotBeatState) {
  return [
    `${label}:`,
    s.character && `People: ${s.character}.`,
    s.wardrobe && `Clothes: ${s.wardrobe}.`,
    s.location && `Place: ${s.location}.`,
    s.lighting && `Light: ${s.lighting}.`,
    s.pose && `Pose: ${s.pose}.`,
    s.prop && `Prop: ${s.prop}.`,
    s.camera && `Camera: ${s.camera}. Keep this camera unless the action requires a change.`,
  ]
    .filter(Boolean)
    .join(' ');
}

function toEnglishState(raw: string) {
  if (!raw) return '';
  if (!I2V_VI_RE.test(raw)) return raw;
  return englishI2vMotion(raw, 5).slice(0, 220);
}

/** I2V: START + ACTION + END. UI Action stays Vietnamese. */
export function compileContinuityI2v(link: ContinuityLink, seconds: number) {
  const motion = englishI2vMotion(link.action, seconds);
  const start = toEnglishState(
    [link.start.location, link.start.pose, link.start.prop, link.start.lighting].filter(Boolean).join('. '),
  );
  const end = toEnglishState([link.end.pose, link.end.prop, link.end.facing].filter(Boolean).join('. '));
  const text = [
    'Cinematic live-action. The attached still is the exact first frame.',
    start ? `START STATE: ${start}` : stateLine('START', link.start),
    motion,
    actingI2vBrief(inferActingDirection({ action: link.action })),
    end ? `END STATE: ${end}` : stateLine('END', link.end),
    'Do not reset wardrobe, place, lighting, or faces. Continue from the start state. No look at camera. No text.',
    `${seconds >= 8 ? 10 : 5} seconds.`,
  ]
    .filter(Boolean)
    .join(' ');
  return text.length <= 900 ? text : text.slice(0, 900);
}

export function previousApprovedKf(
  state: SeriesPilotState,
  shot: FamixaSeriesShot,
  queue: FamixaSeriesShot[],
) {
  const i = queue.findIndex((s) => s.id === shot.id);
  for (let k = i - 1; k >= 0; k--) {
    const prev = queue[k]!;
    const run = shotRunOf(state, prev);
    if (run.keyframeDataUrl?.startsWith('data:image')) return { shot: prev, run };
  }
  return undefined;
}
