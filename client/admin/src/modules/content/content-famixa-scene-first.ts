/** Scene-first / Continuity-first. Script is SoT. Does not invent story. */

import { inferActingDirection } from './content-famixa-acting-law';
import { linesForShot } from './content-famixa-dialogue-map';
import { estimateSpokenSec, deriveVoiceScript } from './content-famixa-voice-script';
import type { AssembleTimeline } from './content-famixa-assemble';
import {
  episodeShots,
  lockFromGraph,
  shotHasValidAction,
  shotRunOf,
  type FamixaSeriesShot,
  type SeriesPilotState,
  type SeriesShotRun,
} from './content-famixa-series';

export type SceneMaster = {
  sceneId: string;
  title: string;
  location: string;
  time: string;
  lighting: string;
  characters: string;
  wardrobe: string;
  props: string;
  environment: string;
  camera: string;
  mood: string;
  continuityRules: string;
  locked: boolean;
  sourceShotId?: string;
};

export type ShotProdStatus =
  | 'HOLD'
  | 'READY'
  | 'KF DRAFT'
  | 'KF APPROVED'
  | 'VIDEO QUEUED'
  | 'VIDEO READY'
  | 'VIDEO APPROVED';

export const PROD_GATES = [
  { id: 'script', label: 'Script Approved' },
  { id: 'voice', label: 'Voice Approved' },
  { id: 'shotPlan', label: 'Shot Plan Approved' },
  { id: 'sceneMaster', label: 'Scene Master Approved' },
  { id: 'kf', label: 'KF Approved' },
  { id: 'video', label: 'Video Approved' },
  { id: 'preview', label: 'Preview Approved' },
] as const;

export type ProdGateId = (typeof PROD_GATES)[number]['id'];

export const SCENE_CONTINUITY_RULE =
  'Keep the same people, faces, hair, wardrobe, age, room, time, lighting, and props. Only Action, pose, and camera may change.';

const PROP_RE = /bài kiểm tra|tờ giấy|điện thoại|cơm|nồi|cặp|bàn ăn/i;

export function sceneCodeOf(raw?: string) {
  return raw?.match(/SC\s*\d+/i)?.[0]?.replace(/\s+/g, '').toUpperCase() ?? '';
}

export function sceneIdOfShot(shot: FamixaSeriesShot) {
  return sceneCodeOf(shot.sceneId) || sceneCodeOf(shot.scene) || sceneCodeOf(shot.id) || 'SC';
}

function clip(s: string, n: number) {
  return s.replace(/\s+/g, ' ').trim().slice(0, n);
}

function inferTime(blob: string) {
  if (/buổi tối|đêm|\btối\b/i.test(blob)) return 'evening';
  if (/chiều/i.test(blob)) return 'afternoon';
  if (/sáng|buổi sáng/i.test(blob)) return 'morning';
  return '';
}

function inferLighting(blob: string, time: string) {
  if (/ấm|warm/i.test(blob)) return time === 'evening' ? 'warm indoor evening' : 'warm indoor';
  if (time === 'evening') return 'warm indoor evening';
  return '';
}

function propsFrom(blob: string) {
  const found = blob.match(new RegExp(PROP_RE.source, 'gi')) ?? [];
  return [...new Set(found.map((s) => s.toLowerCase()))].join(', ');
}

/** Fill Scene Master from Script / graph only. Empty stays empty. */
export function deriveSceneMaster(state: SeriesPilotState, sceneId: string): SceneMaster {
  const sc = sceneCodeOf(sceneId) || sceneId;
  const node = (state.scenes ?? []).find((s) => sceneCodeOf(s.id) === sc);
  const lock = lockFromGraph(
    state,
    episodeShots(state).find((s) => sceneIdOfShot(s) === sc),
  );
  const shots = episodeShots(state).filter((s) => sceneIdOfShot(s) === sc);
  const blob = [node?.environment, node?.content, node?.title, lock.environment, ...shots.map((s) => s.story)]
    .filter(Boolean)
    .join(' ');
  const time = inferTime(blob);
  return {
    sceneId: sc || 'SC',
    title: node?.title || lock.scene.replace(/^SC\d+\s*[—–-]\s*/i, '') || '',
    location: clip(node?.environment || lock.environment || shots[0]?.location || '', 160),
    time,
    lighting: inferLighting(blob, time) || clip(node?.performance ? '' : '', 80),
    characters: clip(lock.characters, 200),
    wardrobe: clip(lock.wardrobe, 200),
    props: propsFrom(blob),
    environment: clip(node?.environment || lock.environment, 200),
    camera: clip(node?.camera || lock.camera, 120),
    mood: clip(node?.performance || lock.performance, 160),
    continuityRules: SCENE_CONTINUITY_RULE,
    locked: Boolean(state.sceneMasters?.[sc]?.locked || (lock.locked && sceneCodeOf(lock.scene) === sc)),
    sourceShotId: state.sceneMasters?.[sc]?.sourceShotId || lock.sourceShotId,
  };
}

export function sceneMasterOf(state: SeriesPilotState, sceneId: string): SceneMaster {
  const derived = deriveSceneMaster(state, sceneId);
  const stored = state.sceneMasters?.[derived.sceneId];
  if (!stored) return derived;
  return {
    ...derived,
    ...stored,
    sceneId: derived.sceneId,
    continuityRules: stored.continuityRules || SCENE_CONTINUITY_RULE,
    locked: Boolean(stored.locked),
  };
}

export function upsertSceneMaster(state: SeriesPilotState, patch: Partial<SceneMaster> & { sceneId: string }): SeriesPilotState {
  const cur = sceneMasterOf(state, patch.sceneId);
  const next = { ...cur, ...patch, sceneId: cur.sceneId };
  return {
    ...state,
    sceneMasters: { ...state.sceneMasters, [next.sceneId]: next },
  };
}

export function lockSceneMaster(state: SeriesPilotState, sceneId: string): SeriesPilotState {
  const master = sceneMasterOf(state, sceneId);
  return upsertSceneMaster(state, { ...master, locked: true });
}

export function unlockSceneMaster(state: SeriesPilotState, sceneId: string): SeriesPilotState {
  return upsertSceneMaster(state, { sceneId, locked: false });
}

export function sceneMasterLocked(state: SeriesPilotState, sceneId: string) {
  return sceneMasterOf(state, sceneId).locked === true;
}

export function mastersForShots(state: SeriesPilotState, shots: FamixaSeriesShot[]) {
  const ids = [...new Set(shots.map(sceneIdOfShot))];
  return ids.map((id) => sceneMasterOf(state, id));
}

export function allSelectedMastersLocked(state: SeriesPilotState, shots: FamixaSeriesShot[]) {
  const masters = mastersForShots(state, shots);
  return masters.length > 0 && masters.every((m) => m.locked);
}

export function normAction(raw?: string) {
  return (raw ?? '').replace(/\s+/g, ' ').trim().toLowerCase();
}

/** Same beat / same pose — REUSE. Different Action — new still. */
export function actionNearlySame(a?: string, b?: string) {
  const x = normAction(a);
  const y = normAction(b);
  if (!x || !y || x.length < 8 || y.length < 8) return false;
  if (x === y) return true;
  const shorter = x.length <= y.length ? x : y;
  const longer = x.length > y.length ? x : y;
  return longer.includes(shorter) && shorter.length / longer.length >= 0.85;
}

export function isCanonSheetName(name?: string) {
  const n = (name || '').toLowerCase();
  return /-canon\.(png|jpe?g|webp)$/.test(n) || /kf-.+-canon/.test(n);
}

export function kfHasPixels(run: SeriesShotRun) {
  return Boolean(run.keyframeDataUrl?.startsWith('data:image'));
}

/** Explicit duyệt. Legacy: continuity ticks count as approved. */
export function kfIsApprovedStill(run: SeriesShotRun) {
  if (!kfHasPixels(run)) return false;
  if (run.kfApproved === true) return true;
  if (run.kfApproved === false) return false;
  return Boolean(run.continuity && Object.values(run.continuity).some(Boolean));
}

/** Immediate previous production still in the same Scene — not a white prompt. */
export function previousSceneKf(state: SeriesPilotState, shot: FamixaSeriesShot, queue: FamixaSeriesShot[]) {
  const scene = sceneIdOfShot(shot);
  const i = queue.findIndex((s) => s.id === shot.id);
  for (let k = (i < 0 ? queue.length : i) - 1; k >= 0; k--) {
    const prev = queue[k]!;
    if (sceneIdOfShot(prev) !== scene) continue;
    const run = shotRunOf(state, prev);
    if (run.prodSkip || !shotHasValidAction(prev, run)) continue;
    if (kfHasPixels(run)) return { shot: prev, run };
  }
  return undefined;
}

export function sequentialKfIds(state: SeriesPilotState, shots: FamixaSeriesShot[]) {
  return shots
    .filter((s) => {
      const run = shotRunOf(state, s);
      if (run.prodSkip || !shotHasValidAction(s, run)) return false;
      if (run.status === 'approved' || run.kfApproved) return false;
      if (s.voiceChainFrom && !run.kfForceNew) return false;
      return true;
    })
    .map((s) => s.id);
}

export function planEditSeconds(voiceSec: number, pauseSec = 0.2, actionSec = 0) {
  const spoken = voiceSec > 0.2 ? voiceSec + pauseSec : 0;
  const raw = Math.max(spoken, actionSec, spoken > 0 ? 1.2 : 1);
  return Math.round(raw * 10) / 10;
}

/** Runway gen4_turbo only accepts 5 or 10. Assemble trims to edit. */
export function i2vSecondsForEdit(editSec: number): 5 | 10 {
  return editSec > 5.5 ? 10 : 5;
}

export function applyEditDurations(
  state: SeriesPilotState,
  shots = episodeShots(state),
  voiceSecOf?: (lineId: string) => number,
): SeriesPilotState {
  const ep = state.episode;
  if (!ep) return state;
  const script = deriveVoiceScript(state);
  return {
    ...state,
    episode: {
      ...ep,
      shots: ep.shots.map((s) => {
        if (!shots.some((q) => q.id === s.id)) return s;
        const run = shotRunOf(state, s);
        if (!shotHasValidAction(s, run) || run.prodSkip) return s;
        const lines = linesForShot(state, s, script.lines);
        let voice = 0;
        let pause = 0.2;
        for (const line of lines) {
          voice += voiceSecOf?.(line.id) || state.voiceAssets?.[line.id]?.duration || estimateSpokenSec(line.text.replace(/\s+/g, '').length);
          pause = Math.max(pause, inferActingDirection({ text: line.text, name: line.name, action: s.story }).pauseSec);
        }
        const editSeconds = planEditSeconds(voice, pause, lines.length ? 0 : 2);
        const seconds = i2vSecondsForEdit(editSeconds);
        return { ...s, editSeconds, seconds, clock: `${seconds}s` };
      }),
    },
  };
}

export function shotProdStatus(state: SeriesPilotState, shot: FamixaSeriesShot): ShotProdStatus {
  const run = shotRunOf(state, shot);
  if (run.prodSkip) return 'HOLD';
  if (!shotHasValidAction(shot, run)) return 'HOLD';
  if (run.status === 'approved' || state.sceneLocked) return 'VIDEO APPROVED';
  if (run.turboStatus === 'PENDING' || run.turboStatus === 'RUNNING') return 'VIDEO QUEUED';
  if (run.previewUrl?.trim()) return 'VIDEO READY';
  if (kfIsApprovedStill(run)) return 'KF APPROVED';
  if (kfHasPixels(run)) return 'KF DRAFT';
  return 'READY';
}

export function pickShots(shots: FamixaSeriesShot[], ids?: string[]) {
  if (!ids?.length) return shots;
  const set = new Set(ids);
  return shots.filter((s) => set.has(s.id));
}

export function prodGateState(state: SeriesPilotState, shots: FamixaSeriesShot[]) {
  const pack = shots.filter((s) => shotHasValidAction(s, shotRunOf(state, s)) && !shotRunOf(state, s).prodSkip);
  const kfOk = pack.length > 0 && pack.every((s) => kfIsApprovedStill(shotRunOf(state, s)) || s.voiceChainFrom);
  const videoOk = pack.length > 0 && pack.every((s) => Boolean(shotRunOf(state, s).previewUrl?.trim()) || s.voiceChainFrom);
  return {
    script: Boolean(state.scriptLocked),
    voice: Boolean(state.voiceLocked),
    shotPlan: state.shotGraphLocked === true,
    sceneMaster: allSelectedMastersLocked(state, pack),
    kf: kfOk,
    video: videoOk,
    preview: Boolean(state.previewApproved) && videoOk,
  } as Record<ProdGateId, boolean>;
}

/** Full episode Final — all 7 gates. */
export function fullEpisodeBlockReason(state: SeriesPilotState, shots: FamixaSeriesShot[]) {
  const g = prodGateState(state, shots);
  const miss = PROD_GATES.filter((row) => !g[row.id]).map((row) => row.label);
  return miss.length ? `Chưa đủ gate: ${miss.join(' → ')}` : undefined;
}

/** Preview range: Script + Voice + Shot Plan. Scene Master before KF. KF+Voice before video. */
export function previewRangeBlockReason(state: SeriesPilotState, shots: FamixaSeriesShot[], kind: 'kf' | 'video' | 'assemble') {
  if (!state.scriptLocked && !state.voiceLocked) return 'Khóa kịch bản trước.';
  if (!state.voiceLocked) return 'Duyệt thoại trước.';
  if (state.shotGraphLocked === false) return 'Duyệt cách chia Shot trước.';
  if (kind === 'kf' && !allSelectedMastersLocked(state, shots)) return 'Khóa Scene Master trước khi tạo hình.';
  if (kind === 'video') {
    const miss = shots.filter((s) => {
      const run = shotRunOf(state, s);
      return shotHasValidAction(s, run) && !run.prodSkip && !kfIsApprovedStill(run);
    });
    if (miss.length) return `Duyệt KF trước I2V: ${miss.length} shot còn DRAFT.`;
  }
  return undefined;
}

export type TimelineLane = {
  id: 'video' | 'voice' | 'sfx' | 'music' | 'subtitle';
  label: string;
  spans: { startSec: number; endSec: number; label: string }[];
};

export function buildTimelineLanes(tl: AssembleTimeline): TimelineLane[] {
  return [
    {
      id: 'video',
      label: 'VIDEO',
      spans: tl.clips.map((c) => ({ startSec: c.startSec, endSec: c.startSec + c.seconds, label: c.code })),
    },
    {
      id: 'voice',
      label: 'VOICE',
      spans: tl.cues.map((c) => ({ startSec: c.startSec, endSec: c.endSec, label: c.name || c.code })),
    },
    {
      id: 'sfx',
      label: 'SFX',
      spans: tl.totalSec > 0 ? [{ startSec: 0, endSec: tl.totalSec, label: 'room tone' }] : [],
    },
    { id: 'music', label: 'MUSIC', spans: [] },
    {
      id: 'subtitle',
      label: 'SUBTITLE',
      spans: tl.cues.map((c) => ({ startSec: c.startSec, endSec: c.endSec, label: c.text.slice(0, 24) })),
    },
  ];
}

export function continueScenePrompt(master: SceneMaster, prevCode: string | undefined, action: string) {
  const who = master.characters || 'the same people';
  const place = master.location || master.environment || 'the same room';
  const light = master.lighting || 'the same lighting';
  const clothes = master.wardrobe || 'the same wardrobe';
  const from = prevCode ? `from ${prevCode}` : 'from the attached previous keyframe';
  return [
    `Continue the exact same scene ${from}.`,
    `Preserve character identity (${who}), wardrobe (${clothes}), location (${place}), lighting (${light}), props (${master.props || 'same props'}), and time (${master.time || 'same time'}).`,
    master.camera ? `Camera language: ${master.camera}.` : '',
    `Only change the action: ${action}.`,
    SCENE_CONTINUITY_RULE,
  ]
    .filter(Boolean)
    .join(' ');
}
