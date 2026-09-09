/** Deterministic production fingerprints. Not a visual/character authority. */

export function productionHash(parts: Array<string | number | undefined | null>) {
  const s = parts.map((p) => String(p ?? '').replace(/\s+/g, ' ').trim()).join('\u001f');
  let h = 2166136261;
  for (let i = 0; i < s.length; i++) {
    h ^= s.charCodeAt(i);
    h = Math.imul(h, 16777619);
  }
  return (h >>> 0).toString(16);
}

export type ProductionInputFps = {
  voice: string;
  keyframe: string;
  motion: string;
  lipsync: string;
  mix: string;
  character: string;
};

export function voiceInputFingerprint(opts: {
  shotId: string;
  dialogue: { id: string; text: string; voiceId?: string }[];
  emotion?: string;
  acting?: string;
}) {
  return productionHash([
    opts.shotId,
    'voice',
    ...opts.dialogue.flatMap((d) => [d.id, d.text, d.voiceId]),
    opts.emotion,
    opts.acting,
  ]);
}

export function keyframeInputFingerprint(opts: {
  shotId: string;
  action: string;
  blocking?: string;
  camera?: string;
  emotion?: string;
  acting?: string;
  characterIds: string[];
}) {
  return productionHash([
    opts.shotId,
    'kf',
    opts.action,
    opts.blocking,
    opts.camera,
    opts.emotion,
    opts.acting,
    opts.characterIds.join(','),
  ]);
}

export function motionInputFingerprint(opts: {
  keyframeFp: string;
  action: string;
  acting?: string;
  voiceDurationSec?: number;
  productionDurationSec?: number;
}) {
  const dur = opts.voiceDurationSec && opts.voiceDurationSec > 0 ? Math.round(opts.voiceDurationSec * 10) / 10 : '';
  const prod =
    opts.productionDurationSec && opts.productionDurationSec > 0 ? Math.round(opts.productionDurationSec * 100) / 100 : '';
  return productionHash(['motion', opts.keyframeFp, opts.action, opts.acting, dur, prod]);
}

export function lipsyncInputFingerprint(opts: {
  voiceFp: string;
  motionFp: string;
  performanceDurationSec?: number;
  productionDurationSec?: number;
}) {
  const dur = opts.performanceDurationSec && opts.performanceDurationSec > 0
    ? opts.performanceDurationSec
    : opts.productionDurationSec;
  const prod = dur && dur > 0 ? Math.round(dur * 100) / 100 : '';
  return productionHash(['lipsync', opts.voiceFp, opts.motionFp, prod]);
}

export function mixInputFingerprint(opts: { lipsyncFp: string; voiceFp: string; motionFp: string; productionDurationSec?: number }) {
  const prod =
    opts.productionDurationSec && opts.productionDurationSec > 0 ? Math.round(opts.productionDurationSec * 100) / 100 : '';
  return productionHash(['mix', opts.lipsyncFp, opts.voiceFp, opts.motionFp, prod]);
}

export function sameFingerprint(a?: string, b?: string) {
  const x = (a || '').trim();
  const y = (b || '').trim();
  return Boolean(x && y && x === y);
}

export function isStale(stored?: string, current?: string) {
  const have = (stored || '').trim();
  const now = (current || '').trim();
  if (!have || !now) return false;
  return have !== now;
}
