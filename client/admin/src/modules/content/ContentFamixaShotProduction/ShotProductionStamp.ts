import { linesForShot } from '../content-famixa-dialogue-map';
import { episodeShots, shotRunOf, type FamixaSeriesShot, type SeriesPilotState, type SeriesShotRun } from '../content-famixa-series';
import {
  keyframeInputFingerprint,
  lipsyncInputFingerprint,
  mixInputFingerprint,
  motionInputFingerprint,
  voiceInputFingerprint,
  type ProductionInputFps,
} from './ShotProductionFingerprint';
import { shotProductionIntentOf } from './ShotProductionIntent';
import { editorialDurationOf, performanceDurationOf } from '../famixa-shot-production-timing';

export type ShotProductionStamp = {
  voiceFp?: string;
  kfFp?: string;
  motionFp?: string;
  lipsyncFp?: string;
  assembleFp?: string;
  assembleFileName?: string;
  characterIds?: string[];
};

export function voiceDurationSecOf(state: SeriesPilotState, shot: FamixaSeriesShot) {
  const lines = linesForShot(state, shot);
  return lines.reduce((n, line) => n + Number(state.voiceAssets?.[line.id]?.duration || 0), 0);
}

export function voiceIdentityOf(state: SeriesPilotState, shot: FamixaSeriesShot) {
  return linesForShot(state, shot)
    .map((line) => `${line.id}:${Number(state.voiceAssets?.[line.id]?.duration || 0)}`)
    .join(',');
}

export function kfIdentityOf(run?: SeriesShotRun) {
  return `${run?.keyframeFileName || ''}|${run?.keyframePath || ''}|${run?.keyframeDataUrl ? '1' : '0'}`;
}

export function motionIdentityOf(run?: SeriesShotRun) {
  return (run?.takeUrl || run?.previewUrl || '').trim();
}

export function lipsyncIdentityOf(run?: SeriesShotRun) {
  return (run?.lipsyncUrl || '').trim();
}

export function computeInputFingerprints(state: SeriesPilotState, shot: FamixaSeriesShot): ProductionInputFps {
  const intent = shotProductionIntentOf(state, shot);
  const lines = linesForShot(state, shot);
  const voice = voiceInputFingerprint({
    shotId: shot.id,
    dialogue: lines.map((l) => ({ id: l.id, text: l.text, voiceId: l.voiceId })),
    emotion: intent.emotion,
    acting: intent.acting,
  });
  const keyframe = keyframeInputFingerprint({
    shotId: shot.id,
    action: intent.action,
    blocking: intent.blocking,
    camera: intent.camera,
    emotion: intent.emotion,
    acting: intent.acting,
    characterIds: intent.characterIds,
  });
  const performanceDurationSec = performanceDurationOf(state, shot);
  const editorialDurationSec = editorialDurationOf(state, shot);
  const motion = motionInputFingerprint({
    keyframeFp: keyframe,
    action: intent.action,
    acting: intent.acting,
    voiceDurationSec: voiceDurationSecOf(state, shot),
    productionDurationSec: editorialDurationSec,
  });
  const lipsync = lipsyncInputFingerprint({ voiceFp: voice, motionFp: motion, performanceDurationSec });
  return {
    voice,
    keyframe,
    motion,
    lipsync,
    mix: mixInputFingerprint({ lipsyncFp: lipsync, voiceFp: voice, motionFp: motion, productionDurationSec: editorialDurationSec }),
    character: intent.characterIds.join(','),
  };
}

/** Stamp fps only when the underlying asset identity changes. Edits do not restamp. */
export function applyProductionStamps(prev: SeriesPilotState, next: SeriesPilotState): SeriesPilotState {
  const shots = episodeShots(next);
  if (!shots.length) return next;
  let changed = false;
  const runs = { ...next.runs };
  for (const shot of shots) {
    const before = shotRunOf(prev, shot);
    const after = shotRunOf(next, shot);
    const fps = computeInputFingerprints(next, shot);
    const stamp: ShotProductionStamp = { ...(after.shotProduction ?? {}) };
    let dirty = false;
    if (voiceIdentityOf(next, shot) !== voiceIdentityOf(prev, shot) && voiceDurationSecOf(next, shot) > 0.2) {
      stamp.voiceFp = fps.voice;
      dirty = true;
    }
    if (kfIdentityOf(after) !== kfIdentityOf(before) && (after.keyframeDataUrl || after.keyframeFileName || after.keyframePath)) {
      stamp.kfFp = fps.keyframe;
      stamp.characterIds = shotProductionIntentOf(next, shot).characterIds;
      dirty = true;
    }
    if (motionIdentityOf(after) !== motionIdentityOf(before) && motionIdentityOf(after)) {
      stamp.motionFp = fps.motion;
      dirty = true;
    }
    if (lipsyncIdentityOf(after) !== lipsyncIdentityOf(before) && lipsyncIdentityOf(after)) {
      stamp.lipsyncFp = fps.lipsync;
      dirty = true;
    }
    if (!dirty) continue;
    runs[shot.id] = { ...after, shotProduction: stamp };
    changed = true;
  }
  return changed ? { ...next, runs } : next;
}

export function stampAssemble(run: SeriesShotRun, assembleFp: string, fileName: string): SeriesShotRun {
  return {
    ...run,
    shotProduction: {
      ...(run.shotProduction ?? {}),
      assembleFp,
      assembleFileName: fileName,
    },
  };
}
