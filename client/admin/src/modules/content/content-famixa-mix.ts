/** Famixa assemble mix — room + Foley compiler + optional 1 bed. No LLM Foley. No SFX in I2V. */

import { shotActionFromPack, shotRunOf, type FamixaSeriesShot, type SeriesPilotState } from './content-famixa-series';

type MixTimeline = {
  clips: { shotId: string; startSec: number; cues: { text: string }[] }[];
};

export type MixSfxId = 'footstep' | 'paper' | 'breath' | 'phone-tap' | 'chair' | 'door';

export type FamixaMixPrefs = {
  room: boolean;
  foley: boolean;
  music: boolean;
  loudnorm: boolean;
};

export type MixSfxCue = {
  assetId: MixSfxId;
  /** Absolute seconds on the cut. */
  startSec: number;
  gainDb: number;
};

export type MixCueSheet = {
  room: boolean;
  roomId: 'room.night.dining';
  music: boolean;
  musicId?: 'music.bed.dim';
  loudnorm: boolean;
  sfx: MixSfxCue[];
};

export const DEFAULT_MIX_PREFS: FamixaMixPrefs = {
  room: true,
  foley: true,
  music: false,
  loudnorm: true,
};

export const MIX_ROOM_ID = 'room.night.dining' as const;
export const MIX_MUSIC_ID = 'music.bed.dim' as const;

const MAX_SFX_PER_CLIP = 2;
const MAX_SFX_TOTAL = 24;

export function normalizeMixPrefs(v?: Partial<FamixaMixPrefs> | null): FamixaMixPrefs {
  return {
    room: v?.room !== false,
    foley: v?.foley !== false,
    music: v?.music === true,
    loudnorm: v?.loudnorm !== false,
  };
}

function clipText(shot: FamixaSeriesShot | undefined, state: SeriesPilotState, spoken: string) {
  if (!shot) return spoken;
  const run = shotRunOf(state, shot);
  return [
    shotActionFromPack(shot),
    run.shotAction,
    run.visualSpec?.shotAction,
    run.visualSpec?.purpose,
    run.visualSpec?.performance,
    run.visualSpec?.framing,
    spoken,
  ]
    .filter(Boolean)
    .join(' ')
    .replace(/\s+/g, ' ')
    .trim();
}

function isInsert(shot: FamixaSeriesShot | undefined, state: SeriesPilotState, text: string) {
  const framing = shot ? shotRunOf(state, shot).visualSpec?.framing : undefined;
  return framing === 'INSERT' || /giấy|bài kiểm|liếc giấy|tờ giấy|mẩu giấy/i.test(text);
}

function looksSilentReaction(text: string, spoken: string) {
  if (spoken.trim()) return false;
  return /nụ cười|đông cứng|hụt hẫng|im lặng|không ngẩng|không nói|cúi mặt|nhìn xuống/i.test(text);
}

function pickFoley(text: string, insert: boolean, silent: boolean): MixSfxId[] {
  const out: MixSfxId[] = [];
  const add = (id: MixSfxId) => {
    if (out.length >= MAX_SFX_PER_CLIP || out.includes(id)) return;
    out.push(id);
  };
  if (insert || /giấy|bài kiểm|tờ /i.test(text)) add('paper');
  if (/cửa|mở cửa|đóng cửa|vào nhà/i.test(text)) add('door');
  if (!insert && /chạy|bước|đi vào|đi tới|đi ra|bước tới|bước vào/i.test(text)) add('footstep');
  if (/kéo ghế|ngồi xuống|đứng dậy|kéo ghế/i.test(text)) add('chair');
  if (/điện thoại|vuốt máy|lướt màn|cầm máy|nhìn máy/i.test(text)) add('phone-tap');
  if (silent || looksSilentReaction(text, '')) add('breath');
  return out;
}

function gainOf(id: MixSfxId) {
  if (id === 'breath') return -12;
  if (id === 'paper') return -8;
  if (id === 'phone-tap') return -10;
  if (id === 'footstep') return -6;
  if (id === 'chair') return -8;
  return -7;
}

function offsetOf(id: MixSfxId) {
  if (id === 'breath') return 0.28;
  if (id === 'paper') return 0.12;
  if (id === 'phone-tap') return 0.2;
  if (id === 'footstep') return 0.16;
  if (id === 'door') return 0.08;
  return 0.18;
}

/** Compiler cue sheet from Action / Visual Spec. Does not invent story SFX. */
export function compileMixCueSheet(
  tl: MixTimeline,
  shots: FamixaSeriesShot[],
  state: SeriesPilotState,
  prefs?: Partial<FamixaMixPrefs>,
): MixCueSheet {
  const mix = normalizeMixPrefs(prefs ?? state.mixPrefs);
  const byId = new Map(shots.map((s) => [s.id, s]));
  const sfx: MixSfxCue[] = [];
  if (mix.foley) {
    for (const clip of tl.clips) {
      if (sfx.length >= MAX_SFX_TOTAL) break;
      const shot = byId.get(clip.shotId);
      const spoken = clip.cues.map((c) => c.text).join(' ');
      const text = clipText(shot, state, spoken);
      const insert = isInsert(shot, state, text);
      const silent = clip.cues.length === 0 || looksSilentReaction(text, spoken);
      for (const id of pickFoley(text, insert, silent)) {
        if (sfx.length >= MAX_SFX_TOTAL) break;
        sfx.push({
          assetId: id,
          startSec: Number(Math.max(0, clip.startSec + offsetOf(id)).toFixed(2)),
          gainDb: gainOf(id),
        });
      }
    }
  }
  return {
    room: mix.room,
    roomId: MIX_ROOM_ID,
    music: mix.music,
    musicId: mix.music ? MIX_MUSIC_ID : undefined,
    loudnorm: mix.loudnorm,
    sfx,
  };
}

export function formatMixConfirm(mix: MixCueSheet) {
  const foley = [...new Set(mix.sfx.map((s) => s.assetId))];
  return [
    mix.room ? `Phòng: ${mix.roomId}.` : 'Phòng: tắt.',
    foley.length ? `Foley: ${foley.join(', ')}.` : 'Foley: trống.',
    mix.music ? 'Nhạc: 1 bed, duck −12 dB dưới thoại.' : 'Nhạc: trống.',
    mix.loudnorm ? 'loudnorm −14 LUFS.' : 'Không chuẩn hóa LUFS.',
  ].join(' ');
}

export function assembleMixPayload(mix: MixCueSheet, smoothness?: { grade?: boolean; colorMatch?: boolean; interpolate?: boolean }) {
  return {
    room: mix.room,
    foley: mix.sfx.length > 0,
    music: mix.music,
    loudnorm: mix.loudnorm,
    roomId: mix.room ? mix.roomId : undefined,
    musicId: mix.music ? mix.musicId : undefined,
    sfx: mix.sfx.map((s) => ({
      assetId: s.assetId,
      startSec: s.startSec,
      gainDb: s.gainDb,
    })),
    grade: smoothness?.grade !== false,
    colorMatch: smoothness?.colorMatch !== false,
    interpolate: smoothness?.interpolate === true,
  };
}
