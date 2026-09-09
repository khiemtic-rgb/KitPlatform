/** FAMIXA_SHOT_SMOOTHNESS_CONTRACT_V1 — coverage + editorial chips + last-frame gate + Mix polish. */

import { resolveTakeUrl } from './content-famixa-final-source';
import { shotRunOf, type FamixaSeriesShot, type SeriesPilotState } from './content-famixa-series';
import {
  dialogueFloorOf,
  isDirectorEditorial,
  performanceDurationOf,
  type ShotTiming,
  type TimingSource,
} from './famixa-shot-production-timing';

export const SMOOTHNESS_DOCUMENT_ID = 'FAMIXA_SHOT_SMOOTHNESS_CONTRACT_V1';

export type CoverageSize = 'WIDE' | 'MS' | 'MCU' | 'ECU' | 'OTS';
export type CameraPath = 'HOLD' | 'PUSH_IN' | 'TRACK';
export type CoverageBridge = 'CUT' | 'LAST_FRAME';
export type CoverageSource = 'DERIVED' | 'DIRECTOR';
export type EditorialPreset = 'FULL_TAKE' | 'DIALOGUE_FLOOR';

export type ShotCoverage = {
  size: CoverageSize;
  cameraPath: CameraPath;
  bridge: CoverageBridge;
  source?: CoverageSource;
};

export type EpisodeSmoothness = {
  grade?: boolean;
  colorMatch?: boolean;
  interpolate?: boolean;
};

export const CAMERA_LINE: Record<CameraPath, string> = {
  HOLD: 'Camera remains steady.',
  PUSH_IN: 'Camera eases in slightly.',
  TRACK: 'Camera tracks with the subject.',
};

const DIRECTED_RE = /bước|đặt tờ|đặt giấy|đi tới|walk|place the|sits down|stands up|nhận lấy/i;
const WALK_RE = /bước|đi tới|walk|track/i;
const SIT_RE = /ngồi|sofa|ghế|seated|sitting/i;
const DOOR_RE = /cửa phòng|đứng ở cửa|doorway/i;
const PHONE_RE = /điện thoại|phone/i;
const CLOSE_RE = /khóc|nước mắt|tear|ecu|cận/i;

export function shotBlobOf(shot: FamixaSeriesShot) {
  return `${shot.story || ''} ${shot.visual || ''} ${shot.motionPromptVi || ''} ${shot.beatText || ''}`;
}

export function hasDirectedMotion(shot: FamixaSeriesShot, action?: string) {
  return DIRECTED_RE.test(`${shotBlobOf(shot)} ${action || ''} ${shot.actingBeat?.before.action || ''}`);
}

export function deriveCoverage(
  shot: FamixaSeriesShot,
  opts?: { previous?: FamixaSeriesShot; transition?: string },
): ShotCoverage {
  if (shot.coverage?.source === 'DIRECTOR' && shot.coverage.size && shot.coverage.cameraPath) {
    return { ...shot.coverage, source: 'DIRECTOR' };
  }
  const blob = shotBlobOf(shot);
  const sitting = SIT_RE.test(blob);
  const doorway = !sitting && DOOR_RE.test(blob);
  const walk = WALK_RE.test(blob);
  const close = CLOSE_RE.test(blob) || PHONE_RE.test(blob);
  const size: CoverageSize = sitting || close ? (PHONE_RE.test(blob) ? 'MCU' : 'MCU') : doorway || walk ? 'MS' : 'MS';
  const cameraPath: CameraPath = walk ? 'TRACK' : 'HOLD';
  const trans = (opts?.transition || '').toUpperCase();
  const subjectShift = Boolean(
    opts?.previous?.characterIds?.length &&
      shot.characterIds?.length &&
      opts.previous.characterIds[0] !== shot.characterIds[0],
  );
  const prevSize = opts?.previous ? deriveCoverage(opts.previous).size : size;
  const sameFraming = prevSize === size && !subjectShift && trans === 'CONTINUOUS';
  const bridge: CoverageBridge =
    trans === 'REACTION' || trans === 'LOCATION_CHANGE' || !sameFraming ? 'CUT' : 'LAST_FRAME';
  return { size, cameraPath, bridge, source: 'DERIVED' };
}

export function coverageOf(
  state: SeriesPilotState,
  shot: FamixaSeriesShot,
  previous?: FamixaSeriesShot,
): ShotCoverage {
  const trans = shotRunOf(state, shot).transitionType;
  return deriveCoverage(shot, { previous, transition: trans });
}

export function cameraLineOf(coverage: ShotCoverage) {
  return CAMERA_LINE[coverage.cameraPath];
}

export function lastFrameEligible(opts: {
  coverage: ShotCoverage;
  previousTakeUrl?: string;
  confirmMotion?: boolean;
}) {
  return (
    opts.coverage.bridge === 'LAST_FRAME' &&
    Boolean(opts.previousTakeUrl?.trim()) &&
    opts.confirmMotion !== false
  );
}

export function previousShotOf(state: SeriesPilotState, shot: FamixaSeriesShot) {
  const pack = state.episode?.shots ?? [];
  const i = pack.findIndex((s) => s.id === shot.id);
  return i > 0 ? pack[i - 1] : undefined;
}

export function lastFrameFromUrlOf(state: SeriesPilotState, shot: FamixaSeriesShot) {
  const prev = previousShotOf(state, shot);
  if (!prev) return undefined;
  const coverage = coverageOf(state, shot, prev);
  const url = resolveTakeUrl(shotRunOf(state, prev));
  if (!lastFrameEligible({ coverage, previousTakeUrl: url })) return undefined;
  return url;
}

export function applyEditorialPreset(
  state: SeriesPilotState,
  shot: FamixaSeriesShot,
  preset: EditorialPreset,
): Pick<ShotTiming, 'productionDurationSec' | 'source' | 'performanceDurationSec'> {
  const performance = performanceDurationOf(state, shot);
  if (preset === 'FULL_TAKE') {
    return { productionDurationSec: performance, performanceDurationSec: performance, source: 'DEFAULT_TAKE' };
  }
  const floor = dialogueFloorOf(state, shot);
  const production = floor > 0 ? Math.min(floor, performance || floor) : performance;
  return {
    productionDurationSec: production,
    performanceDurationSec: performance,
    source: 'DIRECTOR' as TimingSource,
  };
}

export function editorialPresetOf(shot: FamixaSeriesShot): EditorialPreset {
  return isDirectorEditorial(shot) ? 'DIALOGUE_FLOOR' : 'FULL_TAKE';
}

export function smoothnessOf(state: SeriesPilotState): Required<EpisodeSmoothness> {
  return {
    grade: state.smoothness?.grade !== false,
    colorMatch: state.smoothness?.colorMatch !== false,
    interpolate: state.smoothness?.interpolate === true,
  };
}
