/** Famixa bản dựng — graph in DB, files stay on the machine. */

import { setFamixaMediaScope } from './content-famixa-media-scope';
import {
  emptyPilot,
  episodeShots,
  type SeriesPilotState,
} from './content-famixa-series';

export type SeriesBuildStatus = 'draft' | 'script_locked' | 'voice_locked' | 'in_prod' | 'final';

export const SERIES_BUILD_STATUS_VI: Record<SeriesBuildStatus, string> = {
  draft: 'Nháp',
  script_locked: 'Đã khóa kịch bản',
  voice_locked: 'Đã khóa thoại',
  in_prod: 'Đang dựng',
  final: 'Final',
};

/** UUID v4. `randomUUID` chỉ có trên HTTPS hoặc localhost; HTTP vẫn có `getRandomValues`. */
export function newBuildId() {
  if (typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function') {
    return crypto.randomUUID();
  }
  const bytes = new Uint8Array(16);
  crypto.getRandomValues(bytes);
  bytes[6] = (bytes[6] & 0x0f) | 0x40;
  bytes[8] = (bytes[8] & 0x3f) | 0x80;
  const hex = Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
  return `${hex.slice(0, 8)}-${hex.slice(8, 12)}-${hex.slice(12, 16)}-${hex.slice(16, 20)}-${hex.slice(20)}`;
}

export function ensureBuildId(state: SeriesPilotState): SeriesPilotState {
  const id = (state.buildId ?? '').trim();
  if (id) {
    setFamixaMediaScope(id);
    return state;
  }
  const next = { ...state, buildId: newBuildId() };
  setFamixaMediaScope(next.buildId);
  return next;
}

export function bindBuildMedia(state: SeriesPilotState) {
  setFamixaMediaScope((state.buildId ?? '').trim());
}

export function buildStatusOf(state: SeriesPilotState): SeriesBuildStatus {
  if (state.sceneLocked) return 'final';
  const shots = episodeShots(state);
  const runs = shots.map((s) => state.runs?.[s.id]).filter(Boolean);
  const hasVideo = runs.some((r) => Boolean(r?.takeUrl?.trim() || r?.previewUrl?.trim() || r?.localVideoPath || r?.lipsyncUrl?.trim()));
  const hasKf = runs.some((r) => Boolean(r?.keyframeFileName || r?.keyframePath || r?.keyframeDataUrl));
  if (hasVideo || hasKf) return 'in_prod';
  if (state.voiceLocked) return 'voice_locked';
  if (state.scriptLocked) return 'script_locked';
  return 'draft';
}

export function buildTitleOf(state: SeriesPilotState) {
  return (state.episode?.title || state.episode?.episode || 'Bản dựng').trim().slice(0, 240);
}

/** New row: keep Canon / Voice ID / story memory. Do not copy shots / KF / TTS. */
export function newSeriesBuild(prev: SeriesPilotState): SeriesPilotState {
  const id = newBuildId();
  setFamixaMediaScope(id);
  return {
    ...emptyPilot(),
    buildId: id,
    characters: prev.characters,
    roles: prev.roles,
    castLocked: prev.castLocked,
    storyMemory: prev.storyMemory,
  };
}

export function buildLooksEmpty(state: SeriesPilotState) {
  return episodeShots(state).length === 0 && !(state.packDraft ?? '').trim() && (state.scenes?.length ?? 0) === 0;
}
