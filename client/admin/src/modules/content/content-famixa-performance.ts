/** Performance lock on existing dialogue / CHAR / Scene. Does not invent beats or hug/reset. */

import {
  actingLabel,
  inferActingDirection,
  lockLinePerformance,
  resolveLinePerformance,
  type ActingDirection,
  type LinePerformance,
} from './content-famixa-acting-law';
import { sceneCodeOfShot } from './content-famixa-batch-plan';
import { linesForShot } from './content-famixa-dialogue-map';
import type { FamixaCharacter, FamixaSeriesShot, SeriesPilotState } from './content-famixa-series';

export type VoiceBible = {
  ageImpression?: string;
  tone?: string;
  habit?: string;
  formality?: string;
};

export function linePerformanceOf(state: SeriesPilotState, lineId: string): LinePerformance | undefined {
  for (const sc of state.scenes ?? []) {
    const hit = (sc.dialogue ?? []).find((d) => d.id === lineId);
    if (hit?.performance) return hit.performance;
  }
  return undefined;
}

export function stampDialoguePerformances(state: SeriesPilotState): SeriesPilotState {
  const scenes = (state.scenes ?? []).map((sc) => ({
    ...sc,
    dialogue: (sc.dialogue ?? []).map((d) => {
      if (d.performance?.locked) return d;
      const dir = inferActingDirection({ text: d.text, characterId: d.characterId, emotion: d.emotion });
      return { ...d, performance: lockLinePerformance(dir) };
    }),
  }));
  return { ...state, scenes };
}

export function patchDialoguePerformance(
  state: SeriesPilotState,
  lineId: string,
  patch: Partial<LinePerformance>,
): SeriesPilotState {
  const scenes = (state.scenes ?? []).map((sc) => ({
    ...sc,
    dialogue: (sc.dialogue ?? []).map((d) => {
      if (d.id !== lineId) return d;
      const base: ActingDirection = d.performance?.locked
        ? resolveLinePerformance({ text: d.text, characterId: d.characterId, performance: d.performance })
        : inferActingDirection({ text: d.text, characterId: d.characterId, emotion: d.emotion });
      const emotion = patch.emotion ?? base.emotion;
      const intensity = patch.intensity ?? base.intensity;
      const next = lockLinePerformance({
        ...base,
        emotion,
        intensity,
        pace: patch.pace ?? base.pace,
        volume: patch.volume ?? base.volume,
        pauseSec: patch.pauseSec ?? base.pauseSec,
        label: actingLabel(emotion, intensity),
      }, { emphasis: patch.emphasis ?? d.performance?.emphasis });
      return { ...d, performance: next };
    }),
  }));
  return { ...state, scenes };
}

export function mergeKeepDialoguePerformance(
  rem?: { id: string; performance?: LinePerformance }[],
  old?: { id: string; performance?: LinePerformance }[],
) {
  const byOld = new Map((old ?? []).map((d) => [d.id, d.performance]));
  return (rem ?? []).map((d) => ({
    ...d,
    performance: d.performance?.locked ? d.performance : byOld.get(d.id) ?? d.performance,
  }));
}

export function isChildFromBible(ch?: Pick<FamixaCharacter, 'voiceBible'>) {
  const raw = (ch?.voiceBible?.ageImpression || '').trim();
  const n = Number(raw);
  if (n > 0 && n <= 14) return true;
  return /11|trẻ|youth|child|kid/i.test(raw);
}

const HOPE = /vui|hy vọng|hope|excited|uneasy|háo/i;
const HURT = /tổn|hurt|hụt|im lặng|silent|bực|annoyed|aftertaste/i;

/** Operator-filled Scene arc. KIT warns — does not write the next emotion. */
export function emotionArcJumpWarning(
  master: { emotionPrev?: string; emotionNow?: string; emotionNext?: string },
  hasReactionShot: boolean,
) {
  const now = (master.emotionNow || '').trim();
  const next = (master.emotionNext || '').trim();
  if (!now || !next) return undefined;
  const jump = (HOPE.test(now) && HURT.test(next)) || (HURT.test(now) && HOPE.test(next));
  if (!jump) return undefined;
  if (hasReactionShot) return undefined;
  return `Emotion arc ${now} → ${next} chưa có shot im/reaction. KIT không bịa beat.`;
}

/** 3+ spoken shots in a row — film needs pause / look / reaction. */
export function consecutiveDialogueWarning(state: SeriesPilotState, shots: FamixaSeriesShot[]) {
  const streaks: string[][] = [];
  let cur: string[] = [];
  for (const s of shots) {
    const spoken = linesForShot(state, s).length > 0;
    if (spoken) {
      cur.push(s.shot || s.id);
      continue;
    }
    if (cur.length >= 3) streaks.push(cur);
    cur = [];
  }
  if (cur.length >= 3) streaks.push(cur);
  if (!streaks.length) return undefined;
  return `${streaks.map((r) => r.join('→')).join('; ')} thoại liền — thêm reaction/pause. Không bắt nhân vật nói liên tục.`;
}

export function sceneHasReactionShot(state: SeriesPilotState, shots: FamixaSeriesShot[], sceneId: string) {
  const sc = (sceneId || '').replace(/\s+/g, '').toUpperCase();
  return shots.some((s) => (sceneCodeOfShot(s) || '').toUpperCase() === sc && linesForShot(state, s).length === 0);
}

export function patchVoiceBible(
  state: SeriesPilotState,
  characterId: string,
  patch: Partial<VoiceBible>,
): SeriesPilotState {
  const characters = (state.characters ?? []).map((c) =>
    c.id === characterId ? { ...c, voiceBible: { ...c.voiceBible, ...patch } } : c,
  );
  return { ...state, characters };
}
