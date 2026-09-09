/** FAMIXA_SHOT_TIMING_CONTRACT_V3 — performance-first, editorial-trim-last. */

import { shotRunOf, type FamixaSeriesShot, type SeriesPilotState } from './content-famixa-series';

export const DOCUMENT_ID = 'FAMIXA_SHOT_TIMING_CONTRACT_V3';
/** Same constants as famixa-video-audio-lipsync-pipeline. Do not invent new defaults. */
export const SPEECH_LEAD_IN = 0.2;
export const SPEECH_TAIL = 0.22;
export const INTER_CUE_GAP_SEC = 0.1;
export const PROVIDER_MAP_5 = 5.5;
export const PROVIDER_MAP_10 = 10.05;
export const ASSEMBLE_SHORT_FAIL_SEC = 0.12;
export const ASSEMBLE_APAD_MAX_SEC = 0.05;
export const TIMING_EPS = 0.021;

/** Compat helper only. Duration authority must not read shot id. */
export const SH01_TIMING_V2_ID = 'FAMIXA_SH01_TIMING_V2';
export const SH01_TIMING_V2_PRODUCTION_SEC = 3.4;
export const SH01_TIMING_V2_SHOT_ID = 'EP99-SC01-SH01';

export type TimingSource = 'DEFAULT_TAKE' | 'DIRECTOR';

export type ShotTiming = {
  leadInSec: number;
  tailSec: number;
  reactionSec: number;
  interCueGapSec: number;
  voiceDurationSec: number;
  /** Editorial keep-in-film. Default = performance. Director override only when source=DIRECTOR. */
  productionDurationSec: number;
  providerDurationSec: 5 | 10;
  speechStartSec: number;
  speechEndSec: number;
  performanceDurationSec?: number;
  source?: TimingSource;
};

export type ShotActingBeat = {
  before: { action?: string; body?: string; prop?: string; gaze?: string; room?: string; holdSec: number };
  during: { speech: boolean; emotion?: string; intensity?: number; pace?: string };
  after: { action?: string; gaze?: string; holdSec: number };
};

export function roundCentisecond(n: number) {
  return Math.round(Math.max(0, n) * 100) / 100;
}

export function mapProviderDuration(productionSec: number): 5 | 10 | 'BLOCK' {
  const p = Number(productionSec) || 0;
  if (p <= 0) return 'BLOCK';
  if (p <= PROVIDER_MAP_5) return 5;
  if (p <= PROVIDER_MAP_10) return 10;
  return 'BLOCK';
}

export function computeShotTiming(opts: {
  voiceDurations: number[];
  leadInSec?: number;
  tailSec?: number;
  reactionSec?: number;
  interCueGapSec?: number;
}): ShotTiming {
  const voices = opts.voiceDurations.map((d) => Math.max(0, Number(d) || 0)).filter((d) => d > 0);
  const voiceDurationSec = roundCentisecond(voices.reduce((n, d) => n + d, 0));
  const leadInSec = roundCentisecond(opts.leadInSec ?? SPEECH_LEAD_IN);
  const tailSec = roundCentisecond(opts.tailSec ?? SPEECH_TAIL);
  const reactionSec = roundCentisecond(opts.reactionSec ?? 0);
  const interCueGapSec = roundCentisecond(opts.interCueGapSec ?? INTER_CUE_GAP_SEC);
  const gaps = Math.max(0, voices.length - 1) * interCueGapSec;
  const productionDurationSec = roundCentisecond(leadInSec + voiceDurationSec + gaps + tailSec + reactionSec);
  const mapped = mapProviderDuration(productionDurationSec);
  const providerDurationSec: 5 | 10 = mapped === 'BLOCK' || mapped === 10 ? 10 : 5;
  const speechStartSec = leadInSec;
  const speechEndSec = roundCentisecond(leadInSec + voiceDurationSec + gaps);
  return {
    leadInSec,
    tailSec,
    reactionSec,
    interCueGapSec,
    voiceDurationSec,
    productionDurationSec,
    providerDurationSec,
    speechStartSec,
    speechEndSec,
  };
}

export function measuredVoiceDurations(
  state: SeriesPilotState,
  shot: FamixaSeriesShot,
  voiceSecOf?: (lineId: string) => number,
  lineIds?: string[],
) {
  const ids = lineIds ?? shot.dialogueSegmentIds ?? [];
  return ids.map((id) => {
    const measured = voiceSecOf?.(id) ?? state.voiceAssets?.[id]?.duration;
    return Number(measured) || 0;
  });
}

export function shotHasMeasuredVoice(
  state: SeriesPilotState,
  shot: FamixaSeriesShot,
  voiceSecOf?: (lineId: string) => number,
  lineIds?: string[],
) {
  return measuredVoiceDurations(state, shot, voiceSecOf, lineIds).some((d) => d > 0.2);
}

/** Compat predicate. Duration authority must not call this. */
export function isSh01TimingV2Shot(shot: FamixaSeriesShot) {
  return shot.id === SH01_TIMING_V2_SHOT_ID;
}

/** True when a number is only lead + voice + tail (legacy speech clock). */
export function isDerivedSpeechClock(timing: ShotTiming) {
  const expected = roundCentisecond(timing.leadInSec + timing.voiceDurationSec + timing.tailSec);
  return (timing.reactionSec || 0) < 0.02 && Math.abs(timing.productionDurationSec - expected) < TIMING_EPS;
}

/** Compat stretch. Not duration authority. */
export function withTargetProductionDuration(timing: ShotTiming, targetSec: number): ShotTiming {
  const target = roundCentisecond(targetSec);
  if (mapProviderDuration(target) === 'BLOCK') return timing;
  const gapBudget = roundCentisecond(Math.max(0, timing.speechEndSec - timing.speechStartSec - timing.voiceDurationSec));
  const reactionSec = roundCentisecond(
    Math.max(0, target - timing.leadInSec - timing.voiceDurationSec - gapBudget - timing.tailSec),
  );
  return computeShotTiming({
    voiceDurations: timing.voiceDurationSec > 0 ? [timing.voiceDurationSec] : [],
    leadInSec: timing.leadInSec,
    tailSec: timing.tailSec,
    reactionSec,
    interCueGapSec: timing.interCueGapSec,
  });
}

/** Compat helper. Duration readers must not apply this by shot id. */
export function applySh01TimingV2(timing: ShotTiming): ShotTiming {
  if (!isDerivedSpeechClock(timing)) return timing;
  return withTargetProductionDuration(timing, SH01_TIMING_V2_PRODUCTION_SEC);
}

export function afterHoldSecOf(shot: FamixaSeriesShot) {
  const hold = Number(shot.actingBeat?.after?.holdSec);
  return hold > 0 ? roundCentisecond(hold) : 0;
}

export function leadInSecOf(shot: FamixaSeriesShot) {
  const lead = Number(shot.actingBeat?.before?.holdSec);
  return lead > 0 ? roundCentisecond(lead) : SPEECH_LEAD_IN;
}

export function isDirectorEditorial(shot: FamixaSeriesShot) {
  return shot.timing?.source === 'DIRECTOR' && Number(shot.timing.productionDurationSec) > 0;
}

function successTakeDurationSec(state: SeriesPilotState, shot: FamixaSeriesShot) {
  const attempts = shotRunOf(state, shot).runwayAttempts ?? [];
  for (let i = attempts.length - 1; i >= 0; i--) {
    const row = attempts[i];
    const status = (row?.status || '').toUpperCase();
    const dur = Number(row?.duration) || Number(row?.exactRequest?.duration);
    if ((status === 'SUCCEEDED' || status === 'SUCCESS') && dur > 0) return roundCentisecond(dur);
  }
  return 0;
}

export function providerDurationOf(state: SeriesPilotState, shot: FamixaSeriesShot, _voiceSecOf?: (lineId: string) => number): 5 | 10 {
  const take = successTakeDurationSec(state, shot);
  if (take > 0) return take > PROVIDER_MAP_5 ? 10 : 5;
  const raw = Number(shot.seconds);
  if (raw === 10) return 10;
  if (raw === 5) return 5;
  return 5;
}

/** Measured take, else stored performance, else provider 5|10. Voice is not authority. */
export function performanceDurationOf(state: SeriesPilotState, shot: FamixaSeriesShot, _voiceSecOf?: (lineId: string) => number) {
  const take = successTakeDurationSec(state, shot);
  if (take > 0) return take;
  const stored = Number(shot.timing?.performanceDurationSec);
  if (stored > 0) return roundCentisecond(stored);
  return providerDurationOf(state, shot);
}

export function dialogueEndOf(state: SeriesPilotState, shot: FamixaSeriesShot, voiceSecOf?: (lineId: string) => number) {
  const voices = measuredVoiceDurations(state, shot, voiceSecOf).filter((d) => d > 0);
  const voiceDurationSec = roundCentisecond(voices.reduce((n, d) => n + d, 0));
  if (voiceDurationSec <= 0 && Number(shot.timing?.voiceDurationSec) > 0) {
    return roundCentisecond(leadInSecOf(shot) + Number(shot.timing?.voiceDurationSec));
  }
  if (voiceDurationSec <= 0) return 0;
  const gaps = Math.max(0, voices.length - 1) * INTER_CUE_GAP_SEC;
  return roundCentisecond(leadInSecOf(shot) + voiceDurationSec + gaps);
}

/** Voice + holds create the floor. They do not set default production. */
export function dialogueFloorOf(state: SeriesPilotState, shot: FamixaSeriesShot, voiceSecOf?: (lineId: string) => number) {
  const end = dialogueEndOf(state, shot, voiceSecOf);
  if (end <= 0) return 0;
  const after = afterHoldSecOf(shot);
  return roundCentisecond(end + (after > 0 ? after : SPEECH_TAIL));
}

/** Speech-trim export: keep dialogue + tail. Silent shots keep the full I2V cap. */
export function speechCutPlayableSec(
  state: SeriesPilotState,
  shot: FamixaSeriesShot,
  cap: number,
  voiceSecOf?: (lineId: string) => number,
) {
  const max = Math.min(20, Math.max(0.4, cap));
  const floor = dialogueFloorOf(state, shot, voiceSecOf);
  if (floor <= 0) return max;
  return Math.min(max, Math.max(0.4, floor));
}

/** Director override if source=DIRECTOR; otherwise performance. Ignores legacy voice-clock stamps. */
export function editorialDurationOf(state: SeriesPilotState, shot: FamixaSeriesShot, voiceSecOf?: (lineId: string) => number) {
  if (isDirectorEditorial(shot)) return roundCentisecond(Number(shot.timing!.productionDurationSec));
  return performanceDurationOf(state, shot, voiceSecOf);
}

export function productionDurationOf(state: SeriesPilotState, shot: FamixaSeriesShot, voiceSecOf?: (lineId: string) => number) {
  return editorialDurationOf(state, shot, voiceSecOf);
}

export function falInputDurationOf(state: SeriesPilotState, shot: FamixaSeriesShot, voiceSecOf?: (lineId: string) => number) {
  return performanceDurationOf(state, shot, voiceSecOf);
}

export function assembleDurationOf(state: SeriesPilotState, shot: FamixaSeriesShot, voiceSecOf?: (lineId: string) => number) {
  return editorialDurationOf(state, shot, voiceSecOf);
}

export const TIMING_BLOCK = {
  PRODUCTION_SHORTER_THAN_DIALOGUE: 'PRODUCTION_SHORTER_THAN_DIALOGUE',
  PRODUCTION_LONGER_THAN_PERFORMANCE: 'PRODUCTION_LONGER_THAN_PERFORMANCE',
  PRODUCTION_BELOW_FLOOR: 'PRODUCTION_BELOW_FLOOR',
} as const;

export function timingDurationBlockReason(state: SeriesPilotState, shot: FamixaSeriesShot, voiceSecOf?: (lineId: string) => number) {
  const production = editorialDurationOf(state, shot, voiceSecOf);
  const performance = performanceDurationOf(state, shot, voiceSecOf);
  const dialogueEnd = dialogueEndOf(state, shot, voiceSecOf);
  const floor = dialogueFloorOf(state, shot, voiceSecOf);
  if (production <= 0) return TIMING_BLOCK.PRODUCTION_SHORTER_THAN_DIALOGUE;
  if (dialogueEnd > 0 && production + TIMING_EPS < dialogueEnd) return TIMING_BLOCK.PRODUCTION_SHORTER_THAN_DIALOGUE;
  if (isDirectorEditorial(shot) && floor > 0 && production + TIMING_EPS < floor) return TIMING_BLOCK.PRODUCTION_BELOW_FLOOR;
  if (performance > 0 && production - TIMING_EPS > performance) return TIMING_BLOCK.PRODUCTION_LONGER_THAN_PERFORMANCE;
  return undefined;
}

export function buildV3ShotTiming(state: SeriesPilotState, shot: FamixaSeriesShot, voiceSecOf?: (lineId: string) => number): ShotTiming {
  const voices = measuredVoiceDurations(state, shot, voiceSecOf);
  const afterHold = afterHoldSecOf(shot);
  const computed = computeShotTiming({
    voiceDurations: voices,
    leadInSec: leadInSecOf(shot),
    tailSec: afterHold > 0 && afterHold <= SPEECH_TAIL + TIMING_EPS ? afterHold : SPEECH_TAIL,
  });
  const performance = performanceDurationOf(state, shot, voiceSecOf);
  const production = editorialDurationOf(state, shot, voiceSecOf);
  const provider = providerDurationOf(state, shot, voiceSecOf);
  return {
    ...computed,
    performanceDurationSec: performance,
    productionDurationSec: production,
    providerDurationSec: provider,
    source: isDirectorEditorial(shot) ? 'DIRECTOR' : 'DEFAULT_TAKE',
    reactionSec: 0,
  };
}

export function shotTimingOf(state: SeriesPilotState, shot: FamixaSeriesShot, voiceSecOf?: (lineId: string) => number): ShotTiming | undefined {
  return buildV3ShotTiming(state, shot, voiceSecOf);
}

export function deriveActingBeat(shot: FamixaSeriesShot, timing: ShotTiming, performance?: { emotion?: string; intensity?: number; pace?: string }): ShotActingBeat {
  const blob = `${shot.story || ''} ${shot.visual || ''} ${shot.motionPromptVi || ''} ${shot.beatText || ''}`;
  const paper = /tờ giấy|cầm giấy|cầm tờ|sheet of paper/i.test(blob);
  const sitting = /ngồi|sofa|ghế|seated|sitting/i.test(blob);
  const doorway = !sitting && /cửa phòng|đứng ở cửa|doorway/i.test(blob);
  const phone = /điện thoại|phone/i.test(blob);
  const noLookUp = /không ngẩng|does not look up|doesn't look up/i.test(blob);
  const lookMom = !sitting && /nhìn mẹ|toward (?:his )?mother|toward linh/i.test(blob);
  const action = sitting
    ? 'Sitting on the sofa.'
    : doorway
      ? 'Standing at the living-room doorway.'
      : (shot.story || shot.visual || '').trim() || undefined;
  const gaze = phone
    ? 'Keeps looking at the phone and does not look at the camera.'
    : noLookUp
      ? 'Does not look up; not at the camera.'
      : lookMom
        ? 'He looks toward his mother, not the camera.'
        : 'not at the camera';
  return {
    before: {
      action,
      body: sitting ? 'Remains seated and makes only a small natural movement.' : undefined,
      prop: paper ? 'He holds a sheet of paper with both hands.' : phone ? 'Holds and looks at a phone.' : undefined,
      gaze,
      room: sitting ? 'The living room remains still; only the speaker makes subtle movement.' : undefined,
      holdSec: timing.leadInSec,
    },
    during: {
      speech: timing.voiceDurationSec > 0.2,
      emotion: performance?.emotion,
      intensity: performance?.intensity,
      pace: performance?.pace,
    },
    after: {
      action: lookMom ? 'holds his gaze' : undefined,
      gaze: lookMom ? 'toward his mother' : undefined,
      holdSec: timing.tailSec,
    },
  };
}

export function applyTimingToShot(shot: FamixaSeriesShot, timing: ShotTiming, performance?: { emotion?: string; intensity?: number; pace?: string }): FamixaSeriesShot {
  return {
    ...shot,
    timing: {
      ...timing,
      source: timing.source ?? (isDirectorEditorial(shot) ? 'DIRECTOR' : 'DEFAULT_TAKE'),
    },
    actingBeat: shot.actingBeat ?? deriveActingBeat(shot, timing, performance),
    editSeconds: timing.productionDurationSec,
    seconds: timing.providerDurationSec,
    clock: `${timing.providerDurationSec}s`,
  };
}
