/** Famixa final cut: timeline + Vietnamese SRT. Does not invent dialogue. */

import { resolveLinePerformance } from './content-famixa-acting-law';
import { estimateSpokenSec } from './content-famixa-voice-script';
import { studioShotCode, type FamixaSeriesShot } from './content-famixa-series';
import type { PreviewCutPlan } from './content-famixa-preview-cut';
import { buildTimelineLanes, type TimelineLane } from './content-famixa-scene-first';

export type AssembleCue = {
  lineId: string;
  shotId: string;
  code: string;
  name: string;
  text: string;
  startSec: number;
  endSec: number;
};

export type AssembleClip = {
  shotId: string;
  code: string;
  startSec: number;
  seconds: number;
  videoUrl?: string;
  useVideoAudio?: boolean;
  /** I2V missing — hold approved KF. Not a skipped beat. */
  holdStill?: boolean;
  cues: AssembleCue[];
};

export type AssembleTimeline = {
  clips: AssembleClip[];
  cues: AssembleCue[];
  totalSec: number;
  missingVideo: string[];
  spokenWithoutFile: string[];
  lanes: TimelineLane[];
};

function charsOf(text: string) {
  return text.replace(/\s+/g, '').length;
}

function srtStamp(sec: number) {
  const ms = Math.max(0, Math.round(sec * 1000));
  const h = Math.floor(ms / 3_600_000);
  const m = Math.floor((ms % 3_600_000) / 60_000);
  const s = Math.floor((ms % 60_000) / 1000);
  const milli = ms % 1000;
  return `${String(h).padStart(2, '0')}:${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')},${String(milli).padStart(3, '0')}`;
}

export function takeDownloadName(shot: FamixaSeriesShot, pack: FamixaSeriesShot[]) {
  const code = studioShotCode(shot, pack).replace(/\s+/g, '');
  return `FAMIXA_${code}.mp4`;
}

export function lipsyncDownloadName(shot: FamixaSeriesShot, pack: FamixaSeriesShot[]) {
  const code = studioShotCode(shot, pack).replace(/\s+/g, '');
  return `FAMIXA_${code}-lipsync.mp4`;
}

export function assembleFileStem(plan: PreviewCutPlan, episode?: string, title?: string) {
  const ep = (episode || 'EP01').replace(/\s+/g, '');
  const slug = (title || 'cut')
    .normalize('NFD')
    .replace(/[\u0300-\u036f]/g, '')
    .replace(/đ/g, 'd')
    .replace(/Đ/g, 'D')
    .replace(/[^\w]+/g, '-')
    .replace(/^-|-$/g, '')
    .toLowerCase()
    .slice(0, 40);
  return `famixa-${ep}-${slug || 'cut'}-${plan.fromCode}-${plan.toCode}`.replace(/\s+/g, '');
}

const SPEECH_PREROLL = 0.2;
const SPEECH_GAP = 0.1;
const SPEECH_TAIL = 0.22;

function cueDurSec(line: { id: string; text: string }, itemSec: number, voiceSecOf?: (id: string) => number) {
  const measured = voiceSecOf?.(line.id);
  if (measured && measured > 0.2 && measured < 30) return measured;
  return Math.max(0.35, line.text ? estimateSpokenSec(charsOf(line.text)) : itemSec);
}

/** Map selected Shorts → concat timeline. `speech` cuts dead air so dialogue sits on the take. */
export function buildAssembleTimeline(
  plan: PreviewCutPlan,
  opts?: {
    hasVoiceFile?: (lineId: string) => boolean;
    voiceSecOf?: (lineId: string) => number;
    fit?: 'take' | 'speech';
  },
): AssembleTimeline {
  const fit = opts?.fit ?? 'speech';
  const clips: AssembleClip[] = [];
  const cues: AssembleCue[] = [];
  const missingVideo: string[] = [];
  const spokenWithoutFile: string[] = [];
  let abs = 0;
  for (const item of plan.items) {
    const keepFal = Boolean(item.hasLipsyncFile);
    const itemFit = keepFal ? 'take' : fit;
    const lines = item.lines.length ? item.lines : item.line ? [item.line] : [];
    const local: AssembleCue[] = [];
    let t = lines.length && itemFit === 'speech' ? SPEECH_PREROLL : 0;
    for (const line of lines) {
      const dur = cueDurSec(line, item.seconds, opts?.voiceSecOf);
      const pause = resolveLinePerformance({
        text: line.text,
        name: line.name,
        performance: 'performance' in line ? line.performance : undefined,
      }).pauseSec;
      const cue: AssembleCue = {
        lineId: line.id,
        shotId: item.shotId,
        code: item.code,
        name: line.name,
        text: line.text,
        startSec: abs + t,
        endSec: abs + t + dur,
      };
      local.push(cue);
      cues.push(cue);
      t += dur + (itemFit === 'speech' ? Math.max(SPEECH_GAP, pause) : 0);
      if (opts?.hasVoiceFile && !opts.hasVoiceFile(line.id)) spokenWithoutFile.push(item.code);
    }
    const voiceEnd = lines.length ? t - (itemFit === 'speech' ? SPEECH_GAP : 0) : 0;
    const seconds =
      itemFit === 'speech'
        ? Math.max(voiceEnd + SPEECH_TAIL, lines.length ? SPEECH_PREROLL + 0.4 : Math.min(item.seconds, 1))
        : Math.max(item.seconds, voiceEnd);
    if (!item.hasVideo) missingVideo.push(item.code);
    clips.push({
      shotId: item.shotId,
      code: item.code,
      startSec: abs,
      seconds,
      useVideoAudio: keepFal,
      holdStill: !item.hasVideo && item.hasKf,
      cues: local,
    });
    abs += seconds;
  }
  const spokenWithoutFileUniq = [...new Set(spokenWithoutFile)];
  const base = {
    clips,
    cues,
    totalSec: abs,
    missingVideo,
    spokenWithoutFile: spokenWithoutFileUniq,
    lanes: [] as TimelineLane[],
  };
  return { ...base, lanes: buildTimelineLanes(base) };
}

export function formatSrt(cues: AssembleCue[]) {
  return cues
    .map((c, i) => {
      const body = c.name ? `${c.name}: ${c.text}` : c.text;
      return `${i + 1}\n${srtStamp(c.startSec)} --> ${srtStamp(c.endSec)}\n${body}`;
    })
    .join('\n\n');
}

export function rangeTakesReady(plan: PreviewCutPlan) {
  return plan.items.length > 0 && plan.items.every((i) => i.hasVideo);
}

/** Test-assemble: at least one existing take — do not wait for missing KF/Runway. */
export function existingTakesReady(plan: PreviewCutPlan) {
  return plan.items.some((i) => i.hasVideo);
}

export function planWithExistingTakes(plan: PreviewCutPlan): PreviewCutPlan {
  const items = plan.items.filter((i) => i.hasVideo);
  return {
    ...plan,
    items,
    fromCode: items[0]?.code ?? plan.fromCode,
    toCode: items.at(-1)?.code ?? plan.toCode,
    motionMissingVideo: [],
    durationBlocked: items.some((i) => Boolean(i.durationIssue)),
    estimatedSec: items.reduce((n, i) => n + i.seconds, 0),
  };
}

export function existingTakeIds(plan: PreviewCutPlan) {
  return plan.items.filter((i) => i.hasVideo).map((i) => i.shotId);
}

/** Full story cut: keep every beat that has take or approved KF. Never drop a scripted shot. */
export function planCompleteCut(plan: PreviewCutPlan): PreviewCutPlan {
  const items = plan.items.filter((i) => i.hasVideo || i.hasKf);
  return {
    ...plan,
    items,
    fromCode: items[0]?.code ?? plan.fromCode,
    toCode: items.at(-1)?.code ?? plan.toCode,
    motionMissingVideo: items.filter((i) => !i.hasVideo).map((i) => i.shotId),
    durationBlocked: items.some((i) => Boolean(i.durationIssue)),
    estimatedSec: items.reduce((n, i) => n + i.seconds, 0),
  };
}

export function completeCutHolds(plan: PreviewCutPlan) {
  return plan.items.filter((i) => !i.hasVideo && i.hasKf);
}

export function completeCutBlocked(plan: PreviewCutPlan) {
  return plan.items.filter((i) => !i.hasVideo && !i.hasKf).map((i) => i.code);
}

export function completeCutReady(plan: PreviewCutPlan) {
  return plan.items.length > 0 && plan.items.every((i) => i.hasVideo || i.hasKf);
}

export function assembleNeedTtsOverlay(plan: PreviewCutPlan) {
  return plan.items.filter((i) => !i.silent && !i.hasLipsyncFile);
}

export function assembleConfirmCopy(plan: PreviewCutPlan) {
  const fal = plan.items.filter((i) => i.hasLipsyncFile).map((i) => i.code);
  const tts = assembleNeedTtsOverlay(plan).map((i) => i.code);
  const hold = completeCutHolds(plan).map((i) => i.code);
  const take = plan.items.filter((i) => i.hasVideo).map((i) => i.code);
  const bits = [`Đủ ${plan.items.length} shot kịch bản: ${plan.items.map((i) => i.code).join(', ')}.`];
  if (take.length) bits.push(`TAKE: ${take.join(', ')}.`);
  if (hold.length) {
    bits.push(`HOLD KF: ${hold.join(', ')} — I2V lỗi thì giữ khung đã duyệt + thoại, không cắt nhịp.`);
  }
  if (fal.length) bits.push(`FINAL_SOURCE=FAL: ${fal.join(', ')} — giữ tiếng khớp môi.`);
  if (tts.length) {
    bits.push(`Thoại mix TTS: ${tts.join(', ')}${hold.length ? ' (kể cả HOLD)' : ''} — miệng chưa theo lời nếu chưa Fal.`);
  }
  bits.push('Không bỏ shot. File phải có thoại. 0 cr Runway. 0 Fal.');
  return {
    okText: hold.length
      ? `Ghép tập hoàn chỉnh · ${take.length} take + ${hold.length} HOLD + thoại`
      : fal.length && !tts.length
        ? 'Ghép tập hoàn chỉnh · MP4 + thoại khớp môi'
        : `Ghép tập hoàn chỉnh · ${plan.items.length} shot + thoại`,
    detail: bits.join(' '),
  };
}

/** Runway/Fal links often have no .mp4 suffix — still playable. */
export function looksLikeVideoUrl(url?: string) {
  const href = (url ?? '').trim();
  if (!href) return false;
  if (/^(blob:|data:video)/i.test(href)) return true;
  if (!/^https?:\/\//i.test(href)) return false;
  if (/\.(mp4|webm|mov|m4v|ogg)(\?|#|$)/i.test(href)) return true;
  return /runway|fal\.ai|fal\.run|amazonaws|cloudfront|r2\.dev|videodelivery|mux\.com/i.test(href);
}
