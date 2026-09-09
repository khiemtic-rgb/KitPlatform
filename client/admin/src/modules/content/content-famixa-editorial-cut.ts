/** FAMIXA Film Editing Engine V1 — overlay only. Does not mutate production artifacts. */

import { resolveTakeUrl, type FinalSourceRun } from './content-famixa-final-source';
import { clampShortSeconds } from './content-famixa-prod-v2';
import type { FamixaSeriesShot, SeriesPilotState } from './content-famixa-series';
import { performanceDurationOf, roundCentisecond, speechCutPlayableSec } from './famixa-shot-production-timing';

export const EDITORIAL_CUT_ID = 'FAMIXA_FILM_EDITING_ENGINE_V1';
export const EDITORIAL_SOURCE_GONE = 'Video nguồn không còn khả dụng. Hãy tạo lại video.';

export type EditorialCutMode = 'FULL_TAKE' | 'SPEECH_CUT';

export type EditorialShotTrim = {
  inSec?: number;
  outSec?: number;
};

export type EditorialCut = {
  mode: EditorialCutMode;
  shots?: Record<string, EditorialShotTrim>;
  room: boolean;
  foley: boolean;
  music: boolean;
  loudnorm: boolean;
  grade: boolean;
};

export type EditorialWindow = {
  shotId: string;
  usableStart: number;
  usableEnd: number;
  seconds: number;
  cap: 5 | 10;
};

export const DEFAULT_EDITORIAL_CUT: EditorialCut = {
  mode: 'SPEECH_CUT',
  shots: {},
  room: true,
  foley: true,
  music: false,
  loudnorm: true,
  grade: true,
};

export function editorialModeOf(v?: string | null): EditorialCutMode {
  return v === 'full' || v === 'FULL_TAKE' ? 'FULL_TAKE' : 'SPEECH_CUT';
}

export function normalizeEditorialCut(v?: Partial<EditorialCut> | null): EditorialCut {
  const shots: Record<string, EditorialShotTrim> = {};
  for (const [id, row] of Object.entries(v?.shots ?? {})) {
    if (!id || !row || typeof row !== 'object') continue;
    const inSec = Number(row.inSec);
    const outSec = Number(row.outSec);
    shots[id] = {
      inSec: Number.isFinite(inSec) && inSec >= 0 ? roundCentisecond(inSec) : undefined,
      outSec: Number.isFinite(outSec) && outSec > 0 ? roundCentisecond(outSec) : undefined,
    };
  }
  return {
    mode: editorialModeOf(v?.mode),
    shots,
    room: v?.room !== false,
    foley: v?.foley !== false,
    music: v?.music === true,
    loudnorm: v?.loudnorm !== false,
    grade: v?.grade !== false,
  };
}

export function editorialCutOf(state: Pick<SeriesPilotState, 'editorialCut'>): EditorialCut {
  return normalizeEditorialCut(state.editorialCut);
}

export function defaultEditorialOutSec(
  state: SeriesPilotState,
  shot: FamixaSeriesShot,
  cut: EditorialCut,
  voiceSecOf?: (lineId: string) => number,
) {
  const cap = clampShortSeconds(shot.seconds || 5);
  if (cut.mode === 'SPEECH_CUT') return speechCutPlayableSec(state, shot, cap, voiceSecOf);
  const performance = performanceDurationOf(state, shot, voiceSecOf);
  return Math.min(cap, Math.max(0.4, performance || cap));
}

function looksEditorialHttpUrl(url?: string) {
  return /^https?:\/\//i.test((url ?? '').trim());
}

/** Assemble source only. Does not read finalReady / KF pixels / IndexedDB. */
export function editorialSourceOf(run?: FinalSourceRun & { motionNeedsRemake?: boolean; kfApproved?: boolean }) {
  if (run?.motionNeedsRemake || run?.kfApproved === false) return '';
  const lip = run?.lipsyncUrl?.trim();
  if (looksEditorialHttpUrl(lip)) return lip;
  return resolveTakeUrl(run) || '';
}

export function editorialSourceReady(run?: FinalSourceRun) {
  return Boolean(editorialSourceOf(run));
}

/** @deprecated use editorialSourceOf */
export function editorialSourceUrl(run?: FinalSourceRun) {
  return editorialSourceOf(run);
}

export function editorialAssembleError(err: unknown) {
  const msg = err instanceof Error ? err.message : '';
  if (/hết hạn|404|403|410|expired|không tải được take/i.test(msg)) return EDITORIAL_SOURCE_GONE;
  return msg.trim() || 'Không ghép được bản dựng.';
}

export function editorialFileStem(epCode: string, kind: 'preview' | 'final') {
  const ep = (epCode || 'EP01').replace(/\s+/g, '');
  return kind === 'final' ? `${ep}-edit` : `${ep}-edit-preview`;
}

/** Maps overlay trim → assemble UsableStart / Seconds. Does not write shot.timing. */
export function resolveEditorialWindow(
  state: SeriesPilotState,
  shot: FamixaSeriesShot,
  cut: EditorialCut = editorialCutOf(state),
  voiceSecOf?: (lineId: string) => number,
): EditorialWindow {
  const cap = clampShortSeconds(shot.seconds || 5);
  const stored = cut.shots?.[shot.id];
  const rawIn = stored?.inSec;
  const inSec = roundCentisecond(Math.min(Math.max(0, Number.isFinite(rawIn) ? Number(rawIn) : 0), cap - 0.4));
  const rawOut = stored?.outSec;
  const fallbackOut = defaultEditorialOutSec(state, shot, cut, voiceSecOf);
  const desiredOut = Number.isFinite(rawOut) ? Number(rawOut) : fallbackOut;
  const outSec = roundCentisecond(Math.min(cap, Math.max(inSec + 0.4, desiredOut)));
  return {
    shotId: shot.id,
    usableStart: inSec,
    usableEnd: outSec,
    seconds: roundCentisecond(Math.max(0.4, outSec - inSec)),
    cap,
  };
}

export function estimateEditorialTotalSec(
  state: SeriesPilotState,
  shots: FamixaSeriesShot[],
  cut: EditorialCut = editorialCutOf(state),
  voiceSecOf?: (lineId: string) => number,
) {
  return roundCentisecond(shots.reduce((n, shot) => n + resolveEditorialWindow(state, shot, cut, voiceSecOf).seconds, 0));
}

export function editorialMixClips(
  shots: FamixaSeriesShot[],
  windows: EditorialWindow[],
  cuesOf: (shot: FamixaSeriesShot) => { text: string }[],
) {
  const byId = new Map(shots.map((s) => [s.id, s]));
  let startSec = 0;
  return windows.map((win) => {
    const shot = byId.get(win.shotId);
    const clip = {
      shotId: win.shotId,
      startSec,
      seconds: win.seconds,
      cues: shot ? cuesOf(shot) : [],
    };
    startSec = roundCentisecond(startSec + win.seconds);
    return clip;
  });
}

export function patchEditorialCut(state: SeriesPilotState, patch: Partial<EditorialCut>): SeriesPilotState {
  return { ...state, editorialCut: normalizeEditorialCut({ ...editorialCutOf(state), ...patch }) };
}

export function patchEditorialShotTrim(
  state: SeriesPilotState,
  shotId: string,
  trim: EditorialShotTrim | null,
): SeriesPilotState {
  const cur = editorialCutOf(state);
  const shots = { ...(cur.shots ?? {}) };
  if (!trim) delete shots[shotId];
  else shots[shotId] = { ...shots[shotId], ...trim };
  return { ...state, editorialCut: { ...cur, shots } };
}
