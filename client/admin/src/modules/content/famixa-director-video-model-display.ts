/** Director video model display — Production OS authority only. */

import type { FamixaMotionEngine } from './famixa-motion-engine';

export const DIRECTOR_VIDEO_MODEL_DISPLAY_ID = 'FAMIXA_DIRECTOR_VIDEO_MODEL_DISPLAY_FIX_V1';
export const DIRECTOR_VIDEO_MODEL_UNRESOLVED = 'Chưa xác định';

export type DirectorVideoSelection = {
  providerId: string;
  modelId: string;
  selectionId?: string;
};

export type DirectorVideoTakeProvenance = {
  present: boolean;
  artifactId?: string;
  providerId?: string;
  modelId?: string;
  durationSec?: number;
};

export type DirectorVideoModelDisplay = {
  selection?: DirectorVideoSelection;
  take?: DirectorVideoTakeProvenance;
};

export function formatDirectorVideoModelLabel(
  providerId?: string | null,
  modelId?: string | null,
): string {
  const p = (providerId || '').trim().toLowerCase();
  const m = (modelId || '').trim().toLowerCase();
  if (!p && !m) return DIRECTOR_VIDEO_MODEL_UNRESOLVED;
  if (p === 'runway' || m === 'gen4_turbo' || m === 'turbo') return 'Runway · Gen-4 Turbo';
  if (p === 'wan' || m.includes('wan')) return 'Wan 2.1 · Fal';
  if (p === 'google' || p === 'veo' || m.includes('veo')) return 'Google Veo 3';
  return DIRECTOR_VIDEO_MODEL_UNRESOLVED;
}

export function formatDirectorVideoDuration(sec?: number | null): string | undefined {
  if (sec == null || !Number.isFinite(sec) || sec <= 0) return undefined;
  return `${(Math.round(sec * 100) / 100).toFixed(2)}s`;
}

export function formatVideoTakeProvenanceLine(take?: DirectorVideoTakeProvenance | null): string | undefined {
  if (!take?.present) return undefined;
  const model = formatDirectorVideoModelLabel(take.providerId, take.modelId);
  const duration = formatDirectorVideoDuration(take.durationSec);
  return [model, duration].filter(Boolean).join(' · ');
}

export function motionEngineFromProviderModel(
  providerId?: string | null,
  modelId?: string | null,
): FamixaMotionEngine | undefined {
  const p = (providerId || '').trim().toLowerCase();
  const m = (modelId || '').trim().toLowerCase();
  if (p === 'runway' || m === 'gen4_turbo' || m === 'turbo') return 'turbo';
  if (p === 'wan' || m.includes('wan')) return 'wan';
  if (p === 'google' || p === 'veo' || m.includes('veo')) return 'veo';
  return undefined;
}

export function providerModelFromEngine(engine: FamixaMotionEngine): { providerId: string; modelId: string } {
  if (engine === 'wan') return { providerId: 'wan', modelId: 'wan-2.1' };
  if (engine === 'veo') return { providerId: 'google', modelId: 'veo-3.1' };
  return { providerId: 'runway', modelId: 'gen4_turbo' };
}

/** Next production selection only. Existing VIDEO_TAKE stays frozen. */
export function applyDirectorVideoModelSelection(
  current: DirectorVideoModelDisplay | undefined,
  next: { providerId: string; modelId: string; selectionId?: string },
): DirectorVideoModelDisplay {
  const providerId = (next.providerId || '').trim();
  const modelId = (next.modelId || '').trim();
  if (!providerId || !modelId) {
    return { selection: current?.selection, take: current?.take };
  }
  return {
    selection: { providerId, modelId, selectionId: next.selectionId || current?.selection?.selectionId },
    take: current?.take,
  };
}

/** Confirmed Production OS row only — local optimistic provider/model is not authority. */
export function isConfirmedDirectorVideoSelection(row?: DirectorVideoSelection | null): boolean {
  return Boolean((row?.selectionId || '').trim() && (row?.providerId || '').trim() && (row?.modelId || '').trim());
}

export function directorVideoSelectionFromRow(row?: {
  providerId?: string | null;
  modelId?: string | null;
  selectionId?: string | null;
} | null): DirectorVideoSelection | undefined {
  const providerId = (row?.providerId || '').trim();
  const modelId = (row?.modelId || '').trim();
  const selectionId = (row?.selectionId || '').trim();
  if (!providerId || !modelId || !selectionId) return undefined;
  return { providerId, modelId, selectionId };
}

export function productionAiSelectionHttpStatus(error: unknown): number | undefined {
  if (!error || typeof error !== 'object') return undefined;
  const status = (error as { response?: { status?: number } }).response?.status;
  return typeof status === 'number' ? status : undefined;
}

export type DirectorVideoSelectionHydrateStatus = 'ok' | 'missing' | 'error';

/** GET 200 → server row. GET 404 → undefined. Other errors → prior confirmed row only. */
export function hydrateDirectorVideoSelection(input: {
  status: DirectorVideoSelectionHydrateStatus;
  row?: { providerId?: string | null; modelId?: string | null; selectionId?: string | null } | null;
  previous?: DirectorVideoSelection | null;
}): DirectorVideoSelection | undefined {
  if (input.status === 'ok') return directorVideoSelectionFromRow(input.row);
  if (input.status === 'missing') return undefined;
  return isConfirmedDirectorVideoSelection(input.previous) ? input.previous! : undefined;
}

export function revertDirectorVideoSelectionAfterPostFailure(
  previous?: DirectorVideoSelection | null,
): DirectorVideoSelection | undefined {
  return isConfirmedDirectorVideoSelection(previous) ? previous! : undefined;
}

/** Bind take provenance to an artifactId. A new selection does not rewrite a known take. */
export function bindVideoTakeProvenance(
  existing: DirectorVideoTakeProvenance | undefined,
  input: {
    artifactId?: string | null;
    selection?: DirectorVideoSelection;
    durationSec?: number | null;
  },
): DirectorVideoTakeProvenance | undefined {
  const artifactId = (input.artifactId || existing?.artifactId || '').trim();
  if (!artifactId) return existing?.present ? existing : undefined;
  if (existing?.present && existing.artifactId === artifactId) {
    return {
      ...existing,
      durationSec: input.durationSec ?? existing.durationSec,
    };
  }
  return {
    present: true,
    artifactId,
    providerId: input.selection?.providerId,
    modelId: input.selection?.modelId,
    durationSec: input.durationSec ?? undefined,
  };
}

export function productionScopeFromShotCode(shotCode: string): { episodeCode: string; sceneCode: string } {
  const id = (shotCode || '').trim();
  const parts = id.split('-');
  if (parts.length >= 3) return { episodeCode: parts[0] || '', sceneCode: parts[1] || '' };
  return { episodeCode: '', sceneCode: '' };
}
