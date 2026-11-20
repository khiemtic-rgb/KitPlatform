/** Canonical assemble blobs — same IndexedDB idea as TTS. Survives reload. Not a migration. */

import { famixaMediaScope, famixaScopedKey } from '../content-famixa-media-scope';
import { shotRunOf, type FamixaSeriesShot, type SeriesPilotState } from '../content-famixa-series';
import {
  currentNonLateMotionAttempt,
  lipsyncBoundArtifactId,
} from '../famixa-lipsync-motion-authority';
import { computeInputFingerprints } from './ShotProductionStamp';

const DB_NAME = 'kit-famixa-shot-final';
const STORE = 'final';
const PREFIX = 'FAMIXA:final';

export const FINAL_ARTIFACT_IDENTITY_ID = 'FAMIXA_FINAL_ARTIFACT_IDENTITY_BINDING_V1';

export type FinalArtifactIdentity = {
  buildId: string;
  shotId: string;
  assembleFp: string;
  motionArtifactId?: string;
  lipsyncTaskId?: string;
};

export type FinalArtifactMeta = FinalArtifactIdentity & {
  size: number;
  at: string;
};

export type FinalArtifactRecord = {
  blob: Blob;
  meta: FinalArtifactMeta;
};

export type FinalArtifactValidity = 'CURRENT' | 'LEGACY_UNVERIFIED' | 'MISMATCH' | 'MISSING';

export type StoredFinalArtifact =
  | { kind: 'missing' }
  | { kind: 'legacy'; blob: Blob }
  | { kind: 'incomplete'; blob: Blob }
  | { kind: 'record'; record: FinalArtifactRecord };

export type FinalArtifactProof = {
  ok: boolean;
  validity: FinalArtifactValidity;
};

const mem = new Map<string, StoredFinalArtifact>();

function finalKey(shotId: string) {
  return famixaScopedKey(PREFIX, shotId);
}

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, 1);
    req.onupgradeneeded = () => {
      if (!req.result.objectStoreNames.contains(STORE)) req.result.createObjectStore(STORE);
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error ?? new Error('IndexedDB final open failed'));
  });
}

export function asPlayableVideoBlob(blob: Blob) {
  if (blob.type.startsWith('video/')) return blob;
  return new Blob([blob], { type: 'video/mp4' });
}

function isRawBlob(row: unknown): row is Blob {
  return typeof Blob !== 'undefined' && row instanceof Blob;
}

function trimOf(v?: string) {
  return (v || '').trim();
}

export function parseStoredFinalArtifact(row: unknown): StoredFinalArtifact {
  if (!row) return { kind: 'missing' };
  if (isRawBlob(row)) {
    return row.size >= 80 ? { kind: 'legacy', blob: row } : { kind: 'missing' };
  }
  if (typeof row !== 'object') return { kind: 'missing' };
  const rec = row as { blob?: unknown; meta?: Partial<FinalArtifactMeta> };
  const blob = isRawBlob(rec.blob) ? rec.blob : undefined;
  if (!blob || blob.size < 80) return { kind: 'missing' };
  const meta = rec.meta;
  if (!meta || !trimOf(meta.buildId) || !trimOf(meta.shotId) || !trimOf(meta.assembleFp)) {
    return { kind: 'incomplete', blob };
  }
  return {
    kind: 'record',
    record: {
      blob,
      meta: {
        buildId: trimOf(meta.buildId),
        shotId: trimOf(meta.shotId),
        assembleFp: trimOf(meta.assembleFp),
        motionArtifactId: trimOf(meta.motionArtifactId) || undefined,
        lipsyncTaskId: trimOf(meta.lipsyncTaskId) || undefined,
        size: Number(meta.size) || blob.size,
        at: trimOf(meta.at) || '',
      },
    },
  };
}

export function finalArtifactIdentityOf(state: SeriesPilotState, shot: FamixaSeriesShot): FinalArtifactIdentity {
  const run = shotRunOf(state, shot);
  const fps = computeInputFingerprints(state, shot);
  const bound = lipsyncBoundArtifactId(run, shot.id);
  const current = currentNonLateMotionAttempt(run.runwayAttempts);
  const motionArtifactId =
    bound ||
    (current?.n
      ? `mot:${shot.id}:${current.n}`
      : trimOf(run.lipsyncFrozenInput?.motionArtifactId) || undefined);
  return {
    buildId: trimOf(state.buildId) || trimOf(famixaMediaScope()),
    shotId: shot.id,
    assembleFp: trimOf(fps.mix),
    motionArtifactId,
    lipsyncTaskId: trimOf(run.lipsyncTaskId) || undefined,
  };
}

export function proveFinalArtifact(
  stored: StoredFinalArtifact | undefined,
  current: FinalArtifactIdentity,
): FinalArtifactProof {
  const row = stored ?? { kind: 'missing' as const };
  if (row.kind === 'missing') return { ok: false, validity: 'MISSING' };
  if (row.kind === 'legacy' || row.kind === 'incomplete') return { ok: false, validity: 'LEGACY_UNVERIFIED' };
  const meta = row.record.meta;
  const blob = row.record.blob;
  if (!blob || blob.size < 80) return { ok: false, validity: 'MISSING' };
  if (meta.buildId !== trimOf(current.buildId)) return { ok: false, validity: 'MISMATCH' };
  if (meta.shotId !== trimOf(current.shotId)) return { ok: false, validity: 'MISMATCH' };
  if (!meta.assembleFp || meta.assembleFp !== trimOf(current.assembleFp)) return { ok: false, validity: 'MISMATCH' };
  const wantMotion = trimOf(current.motionArtifactId);
  if (wantMotion && trimOf(meta.motionArtifactId) !== wantMotion) return { ok: false, validity: 'MISMATCH' };
  const wantLip = trimOf(current.lipsyncTaskId);
  if (wantLip && trimOf(meta.lipsyncTaskId) !== wantLip) return { ok: false, validity: 'MISMATCH' };
  return { ok: true, validity: 'CURRENT' };
}

export function artifactPresentOfProof(proof: FinalArtifactProof) {
  return proof.ok === true;
}

/** Playable Final bytes only after CURRENT identity proof. Legacy leftover stays on disk. */
export function playableCurrentFinalBlob(
  stored: StoredFinalArtifact | undefined,
  current: FinalArtifactIdentity,
): Blob | undefined {
  if (!stored || stored.kind !== 'record') return undefined;
  if (!proveFinalArtifact(stored, current).ok) return undefined;
  return asPlayableVideoBlob(stored.record.blob);
}

function playableStored(stored: StoredFinalArtifact): Blob | undefined {
  if (stored.kind === 'legacy' || stored.kind === 'incomplete') return asPlayableVideoBlob(stored.blob);
  if (stored.kind === 'record') return asPlayableVideoBlob(stored.record.blob);
  return undefined;
}

export function rememberFinalBlob(shotId: string, blob?: Blob, identity?: FinalArtifactIdentity) {
  if (!shotId || !blob || blob.size < 80) return;
  const playable = asPlayableVideoBlob(blob);
  if (!identity || !trimOf(identity.buildId) || !trimOf(identity.shotId) || !trimOf(identity.assembleFp)) {
    mem.set(finalKey(shotId), { kind: 'legacy', blob: playable });
    return;
  }
  mem.set(finalKey(shotId), {
    kind: 'record',
    record: {
      blob: playable,
      meta: {
        ...identity,
        buildId: trimOf(identity.buildId),
        shotId: trimOf(identity.shotId),
        assembleFp: trimOf(identity.assembleFp),
        motionArtifactId: trimOf(identity.motionArtifactId) || undefined,
        lipsyncTaskId: trimOf(identity.lipsyncTaskId) || undefined,
        size: playable.size,
        at: new Date().toISOString(),
      },
    },
  });
}

export function finalBlobOf(shotId: string) {
  return playableStored(mem.get(finalKey(shotId)) ?? { kind: 'missing' });
}

export async function saveFinalBlob(shotId: string, blob: Blob, identity: FinalArtifactIdentity) {
  rememberFinalBlob(shotId, blob, identity);
  const stored = mem.get(finalKey(shotId));
  if (!stored || stored.kind !== 'record') return;
  try {
    const db = await openDb();
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction(STORE, 'readwrite');
      tx.objectStore(STORE).put(stored.record, finalKey(shotId));
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
  } catch {
    /* private mode / quota */
  }
}

export async function loadFinalArtifact(shotId: string): Promise<StoredFinalArtifact> {
  const hit = mem.get(finalKey(shotId));
  if (hit) return hit;
  try {
    const db = await openDb();
    const row = await new Promise<unknown>((resolve, reject) => {
      const tx = db.transaction(STORE, 'readonly');
      const req = tx.objectStore(STORE).get(finalKey(shotId));
      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });
    const stored = parseStoredFinalArtifact(row);
    if (stored.kind !== 'missing') mem.set(finalKey(shotId), stored);
    return stored;
  } catch {
    return { kind: 'missing' };
  }
}

/** Bytes only. Not a current-Final proof. */
export async function loadFinalBlob(shotId: string) {
  return playableStored(await loadFinalArtifact(shotId));
}

export function finalObjectUrl(shotId: string) {
  const blob = finalBlobOf(shotId);
  return blob ? URL.createObjectURL(blob) : undefined;
}
