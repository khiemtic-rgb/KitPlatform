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

export function newBuildId() {
  return crypto.randomUUID();
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
  const runs = shots.map((s) => state.runs[s.id]).filter(Boolean);
  const hasVideo = runs.some((r) => Boolean(r?.previewUrl?.trim() || r?.localVideoPath));
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
