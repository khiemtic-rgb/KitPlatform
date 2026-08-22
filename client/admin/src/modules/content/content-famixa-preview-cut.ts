/** Preview Cut — Voice Script is timeline master. KIT maps; does not invent dialogue. */

import { kfIsApproved } from './content-famixa-batch-plan';
import { linesForShot } from './content-famixa-dialogue-map';
import {
  episodeShots,
  shotRunOf,
  studioShotCode,
  type FamixaSeriesShot,
  type SeriesPilotState,
} from './content-famixa-series';
import { deriveVoiceScript, estimateSpokenSec, type FamixaVoiceLine } from './content-famixa-voice-script';

export type PreviewCutStatus = 'ready' | 'need_voice' | 'need_kf' | 'need_video' | 'blocked';

export type PreviewCutItem = {
  shotId: string;
  code: string;
  seconds: number;
  silent: boolean;
  line?: Pick<FamixaVoiceLine, 'id' | 'characterId' | 'name' | 'text'>;
  lines: Pick<FamixaVoiceLine, 'id' | 'characterId' | 'name' | 'text'>[];
  voiceSec: number;
  hasVoiceFile: boolean;
  hasKf: boolean;
  kfApproved: boolean;
  hasVideo: boolean;
  durationIssue?: string;
  status: PreviewCutStatus;
  statusLabel: string;
};

export type PreviewCutPlan = {
  fromCode: string;
  toCode: string;
  items: PreviewCutItem[];
  extraLines: Pick<FamixaVoiceLine, 'id' | 'name' | 'text'>[];
  storyMissingKf: string[];
  motionMissingVideo: string[];
  durationBlocked: boolean;
  estimatedSec: number;
};

export function shotsInInclusiveRange(shots: FamixaSeriesShot[], fromId?: string, toId?: string) {
  if (!shots.length) return [];
  const a = Math.max(0, shots.findIndex((s) => s.id === fromId));
  const bRaw = shots.findIndex((s) => s.id === toId);
  const b = bRaw < 0 ? shots.length - 1 : bRaw;
  const lo = Math.min(a, b);
  const hi = Math.max(a, b);
  return shots.slice(lo, hi + 1);
}

function charsOf(text: string) {
  return text.replace(/\s+/g, '').length;
}

function pickLine(line: FamixaVoiceLine): Pick<FamixaVoiceLine, 'id' | 'characterId' | 'name' | 'text'> {
  return { id: line.id, characterId: line.characterId, name: line.name, text: line.text };
}

/** Explicit map first. Unmapped lines stay extra — never dumped onto the last Short. */
export function assignVoiceToShorts(state: SeriesPilotState, allShots: FamixaSeriesShot[]) {
  const script = deriveVoiceScript(state);
  const byShot = new Map<string, FamixaVoiceLine[]>();
  for (const shot of allShots) {
    byShot.set(shot.id, linesForShot(state, shot, script.lines));
  }
  const used = new Set([...byShot.values()].flat().map((l) => l.id));
  const extraLines = script.lines
    .filter((l) => !used.has(l.id))
    .map((l) => ({ id: l.id, name: l.name, text: l.text }));
  return { byShot, extraLines };
}

/** Map Voice Script onto the selected Short range. Does not invent dialogue. */
export function mapPreviewCut(
  state: SeriesPilotState,
  range: FamixaSeriesShot[],
  opts?: { hasVoiceFile?: (lineId: string) => boolean; voiceSecOf?: (lineId: string) => number },
): PreviewCutPlan {
  const pack = episodeShots(state);
  const all = pack.length ? pack : range;
  const { byShot, extraLines } = assignVoiceToShorts(state, all);
  const items: PreviewCutItem[] = [];

  for (const shot of range) {
    const run = shotRunOf(state, shot);
    const mapped = byShot.get(shot.id) ?? [];
    const line = mapped[0];
    const silent = mapped.length === 0;
    const hasVoiceFile = mapped.length === 0 || mapped.every((l) => Boolean(opts?.hasVoiceFile?.(l.id)));
    const voiceSec = mapped.reduce(
      (n, l) => n + (opts?.voiceSecOf?.(l.id) || estimateSpokenSec(charsOf(l.text))),
      0,
    );
    const i2v = Math.max(1, shot.seconds === 10 ? 10 : shot.seconds || 5);
    const cap = shot.editSeconds && shot.editSeconds > 0 ? shot.editSeconds : i2v;
    const durationIssue =
      !silent && voiceSec > i2v + 0.05 ? `VOICE ${voiceSec.toFixed(1)}s / I2V ${i2v.toFixed(1)}s` : undefined;
    const hasKf = Boolean(run.keyframeDataUrl);
    const hasVideo = Boolean(run.previewUrl?.trim());
    let status: PreviewCutStatus = 'ready';
    let statusLabel = silent ? 'Voice: NONE' : 'Ready';
    if (durationIssue) {
      status = 'blocked';
      statusLabel = durationIssue;
    } else if (!silent && !hasVoiceFile) {
      status = 'need_voice';
      statusLabel = 'TTS session';
    } else if (!hasKf) {
      status = 'need_kf';
      statusLabel = 'Cần hình';
    } else if (!hasVideo) {
      status = 'need_video';
      statusLabel = 'Cần video';
    }
    items.push({
      shotId: shot.id,
      code: studioShotCode(shot, pack.length ? pack : range),
      seconds: cap,
      silent,
      line: line ? pickLine(line) : undefined,
      lines: mapped.map(pickLine),
      voiceSec,
      hasVoiceFile,
      hasKf,
      kfApproved: kfIsApproved(run),
      hasVideo,
      durationIssue,
      status,
      statusLabel,
    });
  }

  const storyMissingKf = items.filter((i) => !i.hasKf).map((i) => i.shotId);
  const motionMissingVideo = items.filter((i) => !i.hasVideo && !i.durationIssue).map((i) => i.shotId);
  const estimatedSec = items.reduce((n, i) => n + i.seconds, 0);
  return {
    fromCode: items[0]?.code ?? '',
    toCode: items.at(-1)?.code ?? '',
    items,
    extraLines,
    storyMissingKf,
    motionMissingVideo,
    durationBlocked: items.some((i) => Boolean(i.durationIssue)),
    estimatedSec,
  };
}

/** Story Preview = KF sequence. Missing session TTS does not block watching pictures. */
export function storyPreviewReady(plan: PreviewCutPlan) {
  return plan.items.length > 0 && plan.items.every((i) => i.hasKf);
}

export function motionPreviewReady(plan: PreviewCutPlan) {
  return storyPreviewReady(plan) && !plan.durationBlocked && plan.items.every((i) => i.hasVideo);
}

/** Play / mux takes that already exist — missing KF or later Shorts do not block. */
export function existingMotionReady(plan: PreviewCutPlan) {
  return plan.items.some((i) => i.hasVideo);
}
