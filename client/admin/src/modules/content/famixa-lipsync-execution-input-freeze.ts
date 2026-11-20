/** FAMIXA_LIPSYNC_EXECUTION_INPUT_FREEZE_V2 — freeze Motion+Voice BEFORE provider. Identity is Famixa. */

import type { SeriesShotRun } from './content-famixa-series';
import {
  VOICE_PHYSICAL_NOT_FOUND,
  loadVoicePhysicalArtifact,
  parseVoicePhysicalStorageKey,
  voicePhysicalIdentityOf,
  type VoicePhysicalStore,
} from './famixa-voice-physical-identity';
import {
  currentNonLateMotionAttempt,
  lipsyncBoundArtifactId,
  lipsyncBoundMotionAttempt,
  lipsyncBoundMotionStillAuthority,
  parseMotionArtifactN,
} from './famixa-lipsync-motion-authority';

export const LIPSYNC_EXECUTION_INPUT_FREEZE_ID = 'FAMIXA_LIPSYNC_EXECUTION_INPUT_FREEZE_V2';
export const LIPSYNC_PROVIDER_INPUT_ENFORCEMENT_ID = 'FAMIXA_LIPSYNC_PROVIDER_INPUT_ENFORCEMENT_V1';

export type LipSyncExecutionFrozenInput = {
  motionArtifactId: string;
  motionFingerprint: string;
  motionDurationSec: number;
  voiceArtifactId: string;
  voiceFingerprint: string;
  voiceDurationSec: number;
  fingerprint: string;
  voiceLineIds?: string[];
  voiceDurationByLineId?: Record<string, number>;
  usedValidity?: string;
  providerId?: string;
  modelId?: string;
  buildId?: string;
  voiceId?: string;
  lineId?: string;
  motionTaskId?: string;
  motionUrl?: string;
  speakerCharacterId?: string;
  speakerFaceRegion?: { x: number; y: number; width: number; height: number };
  speakerFrameNumber?: number;
};

export type LipSyncExecutionAttempt = {
  providerId?: string;
  modelId?: string;
  motionArtifactId: string;
  motionFingerprint: string;
  motionDurationSec: number;
  voiceArtifactId: string;
  voiceFingerprint: string;
  voiceDurationSec: number;
  providerRequestId?: string;
  status: 'SUBMITTING' | 'PENDING' | 'SUCCEEDED' | 'FAILED' | 'RETRY';
  motionTaskId?: string;
  motionUrl?: string;
  voiceId?: string;
  buildId?: string;
  lineId?: string;
  motionPhysicalFingerprint?: string;
  voicePhysicalFingerprint?: string;
  speakerCharacterId?: string;
};

export type AuthoritativeLipSyncMotionSource = {
  artifactId: string;
  n?: number;
  taskId?: string;
  url?: string;
  durationSec: number;
  fingerprint: string;
};

export type AuthoritativeLipSyncVoiceSource = {
  artifactId: string;
  lineId: string;
  lineIds: string[];
  storageKey?: string;
  voiceId?: string;
  fingerprint: string;
  durationSec: number;
  durationByLineId: Record<string, number>;
};

export type LipSyncVoiceAssetRef = {
  artifactId?: string;
  storageKey?: string;
  voiceId?: string;
  duration?: number;
  lineId?: string;
};

export type LipSyncMotionPhysicalInput = {
  motionArtifactId: string;
  n?: number;
  taskId?: string;
  url?: string;
};

export type LipSyncProviderPhysicalInput<TVoice> = {
  motionArtifactId: string;
  voiceArtifactId: string;
  videoUrl?: string;
  takeTaskId?: string;
  voiceBytes: TVoice;
};

function trimOf(v?: string) {
  return (v || '').trim();
}

function motionArtifactIdOf(shotId: string, n: number) {
  return `mot:${shotId}:${n}`;
}

function attemptDurationSec(row?: { duration?: number; exactRequest?: { duration?: number } }) {
  const dur = Number(row?.duration) || Number(row?.exactRequest?.duration) || 0;
  return dur > 0 ? dur : 0;
}

export function validateLipSyncFrozenInput(
  frozen?: Partial<LipSyncExecutionFrozenInput> | null,
): frozen is LipSyncExecutionFrozenInput {
  if (!frozen) return false;
  if (!trimOf(frozen.motionArtifactId)) return false;
  if (!trimOf(frozen.voiceArtifactId)) return false;
  if (!trimOf(frozen.motionFingerprint)) return false;
  if (!trimOf(frozen.voiceFingerprint)) return false;
  if (!trimOf(frozen.fingerprint)) return false;
  if (!(Number(frozen.motionDurationSec) > 0)) return false;
  if (!(Number(frozen.voiceDurationSec) > 0)) return false;
  return true;
}

export function freezeLipSyncExecutionInput(
  source: LipSyncExecutionFrozenInput,
): LipSyncExecutionFrozenInput {
  if (!validateLipSyncFrozenInput(source)) {
    throw new Error('LipSyncFrozenInput incomplete — không gửi provider.');
  }
  return {
    motionArtifactId: trimOf(source.motionArtifactId),
    motionFingerprint: trimOf(source.motionFingerprint),
    motionDurationSec: Number(source.motionDurationSec),
    voiceArtifactId: trimOf(source.voiceArtifactId),
    voiceFingerprint: trimOf(source.voiceFingerprint),
    voiceDurationSec: Number(source.voiceDurationSec),
    fingerprint: trimOf(source.fingerprint),
    voiceLineIds: source.voiceLineIds?.map((id) => trimOf(id)).filter(Boolean),
    voiceDurationByLineId: source.voiceDurationByLineId,
    usedValidity: source.usedValidity,
    providerId: source.providerId ? trimOf(source.providerId) : undefined,
    modelId: source.modelId ? trimOf(source.modelId) : undefined,
    buildId: source.buildId ? trimOf(source.buildId) : undefined,
    voiceId: source.voiceId ? trimOf(source.voiceId) : undefined,
    lineId: source.lineId ? trimOf(source.lineId) : source.voiceLineIds?.[0],
    motionTaskId: source.motionTaskId ? trimOf(source.motionTaskId) : undefined,
    motionUrl: source.motionUrl ? trimOf(source.motionUrl) : undefined,
    speakerCharacterId: source.speakerCharacterId ? trimOf(source.speakerCharacterId) : undefined,
    speakerFaceRegion: source.speakerFaceRegion,
    speakerFrameNumber:
      Number.isInteger(source.speakerFrameNumber) && Number(source.speakerFrameNumber) >= 0
        ? Number(source.speakerFrameNumber)
        : undefined,
  };
}

/** CURRENT LipSync motion — bound CURRENT or newest non-late SUCCESS. Never acceptedTake / lastSuccess LATE. */
export function resolveAuthoritativeLipSyncMotionSource(opts: {
  run?: SeriesShotRun;
  shotId: string;
  motionFingerprint?: string;
}): AuthoritativeLipSyncMotionSource | undefined {
  const shotId = trimOf(opts.shotId);
  if (!shotId) return undefined;
  const bound = lipsyncBoundMotionStillAuthority(opts.run, shotId);
  const row = bound
    ? lipsyncBoundMotionAttempt(opts.run, shotId)
    : currentNonLateMotionAttempt(opts.run?.runwayAttempts);
  const n = row?.n;
  if (!n) return undefined;
  const artifactId = bound
    ? trimOf(lipsyncBoundArtifactId(opts.run, shotId)) || motionArtifactIdOf(shotId, n)
    : motionArtifactIdOf(shotId, n);
  const durationSec = attemptDurationSec(row);
  const fingerprint =
    (bound ? trimOf(opts.run?.lipsyncFrozenInput?.motionFingerprint) : '') ||
    trimOf(opts.motionFingerprint) ||
    trimOf(row?.fingerprint);
  if (!artifactId) return undefined;
  return {
    artifactId,
    n,
    taskId: trimOf(row?.taskId) || undefined,
    url: trimOf(row?.outputUrl) || undefined,
    durationSec,
    fingerprint,
  };
}

/** Graph Voice physical id only. Never invent artifactId from lineId. */
export function voicePhysicalArtifactIdFromGraph(
  lineId: string,
  voiceAssets?: Record<string, LipSyncVoiceAssetRef | undefined>,
): string | undefined {
  const id = trimOf(lineId);
  const artifactId = trimOf(voiceAssets?.[id]?.artifactId);
  if (!id || !artifactId || artifactId === id) return undefined;
  return artifactId;
}

export function resolveAuthoritativeLipSyncVoiceSource(opts: {
  lineIds: string[];
  voiceFingerprint: string;
  durationByLineId?: Record<string, number>;
  voiceAssets?: Record<string, LipSyncVoiceAssetRef | undefined>;
  voiceId?: string;
}): AuthoritativeLipSyncVoiceSource | undefined {
  const lineIds = (opts.lineIds ?? []).map((id) => trimOf(id)).filter(Boolean);
  const lineId = lineIds[0];
  const artifactId = voicePhysicalArtifactIdFromGraph(lineId, opts.voiceAssets);
  if (!lineId || !artifactId || !trimOf(opts.voiceFingerprint)) return undefined;
  const asset = opts.voiceAssets?.[lineId];
  const durationByLineId = Object.fromEntries(
    lineIds.map((id) => [id, Number(opts.durationByLineId?.[id] ?? opts.voiceAssets?.[id]?.duration ?? 0)]),
  );
  const voiceId = trimOf(asset?.voiceId) || trimOf(opts.voiceId) || undefined;
  const storageKey = trimOf(asset?.storageKey) || undefined;
  return {
    artifactId,
    lineId,
    lineIds,
    storageKey,
    voiceId,
    fingerprint: trimOf(opts.voiceFingerprint),
    durationSec: durationByLineId[lineId] || Number(asset?.duration) || 0,
    durationByLineId,
  };
}

/** Remake only. Rebuild Motion from the freeze that already produced CURRENT LipSync. */
export function resolveLipSyncMotionSourceFromFrozen(
  run?: SeriesShotRun,
  shotId?: string,
  motionFingerprint?: string,
): AuthoritativeLipSyncMotionSource | undefined {
  const id = trimOf(shotId);
  const frozen = run?.lipsyncFrozenInput;
  const artifactId = trimOf(frozen?.motionArtifactId);
  const n = parseMotionArtifactN(artifactId, id);
  if (!id || !artifactId || !n) return undefined;
  const row = (run?.runwayAttempts ?? []).find((item) => item.n === n);
  const durationSec = attemptDurationSec(row) || Number(frozen?.motionDurationSec) || 0;
  const fingerprint =
    trimOf(frozen?.motionFingerprint) || trimOf(motionFingerprint) || trimOf(row?.fingerprint);
  if (!fingerprint || !(durationSec > 0)) return undefined;
  return {
    artifactId,
    n,
    taskId: trimOf(row?.taskId) || trimOf(frozen?.motionTaskId) || undefined,
    url: trimOf(row?.outputUrl) || trimOf(frozen?.motionUrl) || undefined,
    durationSec,
    fingerprint,
  };
}

/** Remake only. Graph may have dropped vpa-* and kept lineId — freeze still has the physical id. */
export function resolveLipSyncVoiceSourceFromFrozen(
  frozen?: Partial<LipSyncExecutionFrozenInput> | null,
  opts?: {
    lineIds?: string[];
    voiceFingerprint?: string;
    durationByLineId?: Record<string, number>;
    voiceAssets?: Record<string, LipSyncVoiceAssetRef | undefined>;
    voiceId?: string;
  },
): AuthoritativeLipSyncVoiceSource | undefined {
  const artifactId = trimOf(frozen?.voiceArtifactId);
  const lineIds = (frozen?.voiceLineIds?.length ? frozen.voiceLineIds : opts?.lineIds ?? [])
    .map((id) => trimOf(id))
    .filter(Boolean);
  const lineId = trimOf(frozen?.lineId) || lineIds[0];
  if (!artifactId || !lineId || artifactId === lineId) return undefined;
  const fingerprint = trimOf(frozen?.voiceFingerprint) || trimOf(opts?.voiceFingerprint);
  const durationSec =
    Number(frozen?.voiceDurationSec) ||
    Number(opts?.durationByLineId?.[lineId]) ||
    Number(opts?.voiceAssets?.[lineId]?.duration) ||
    0;
  if (!fingerprint || !(durationSec > 0)) return undefined;
  const durationByLineId = {
    ...(opts?.durationByLineId ?? {}),
    ...(frozen?.voiceDurationByLineId ?? {}),
    [lineId]: durationSec,
  };
  return {
    artifactId,
    lineId,
    lineIds: lineIds.length ? lineIds : [lineId],
    storageKey: trimOf(opts?.voiceAssets?.[lineId]?.storageKey) || undefined,
    voiceId: trimOf(frozen?.voiceId) || trimOf(opts?.voiceAssets?.[lineId]?.voiceId) || trimOf(opts?.voiceId) || undefined,
    fingerprint,
    durationSec,
    durationByLineId,
  };
}

/** Remake CURRENT LipSync. Resolver first, then the freeze that already ran Fal. */
export function resolveLipSyncRemakeSources(opts: {
  run?: SeriesShotRun;
  shotId: string;
  motionFingerprint?: string;
  lineIds: string[];
  voiceFingerprint: string;
  durationByLineId?: Record<string, number>;
  voiceAssets?: Record<string, LipSyncVoiceAssetRef | undefined>;
  voiceId?: string;
}): { motion?: AuthoritativeLipSyncMotionSource; voice?: AuthoritativeLipSyncVoiceSource } {
  const liveMotion = resolveAuthoritativeLipSyncMotionSource({
    run: opts.run,
    shotId: opts.shotId,
    motionFingerprint: opts.motionFingerprint,
  });
  const frozenMotion = resolveLipSyncMotionSourceFromFrozen(opts.run, opts.shotId, opts.motionFingerprint);
  const motion =
    liveMotion && liveMotion.fingerprint && liveMotion.durationSec > 0
      ? liveMotion
      : frozenMotion
        ? {
            ...frozenMotion,
            ...liveMotion,
            artifactId: liveMotion?.artifactId || frozenMotion.artifactId,
            n: liveMotion?.n || frozenMotion.n,
            fingerprint: liveMotion?.fingerprint || frozenMotion.fingerprint,
            durationSec: liveMotion?.durationSec || frozenMotion.durationSec,
            taskId: liveMotion?.taskId || frozenMotion.taskId,
            url: liveMotion?.url || frozenMotion.url,
          }
        : liveMotion;
  const liveVoice = resolveAuthoritativeLipSyncVoiceSource({
    lineIds: opts.lineIds,
    voiceFingerprint: opts.voiceFingerprint,
    durationByLineId: opts.durationByLineId,
    voiceAssets: opts.voiceAssets,
    voiceId: opts.voiceId,
  });
  const frozenVoice = resolveLipSyncVoiceSourceFromFrozen(opts.run?.lipsyncFrozenInput, opts);
  const voice =
    liveVoice && liveVoice.artifactId && liveVoice.durationSec > 0
      ? liveVoice
      : frozenVoice
        ? {
            ...frozenVoice,
            ...liveVoice,
            artifactId: liveVoice?.artifactId || frozenVoice.artifactId,
            lineId: liveVoice?.lineId || frozenVoice.lineId,
            lineIds: liveVoice?.lineIds?.length ? liveVoice.lineIds : frozenVoice.lineIds,
            fingerprint: liveVoice?.fingerprint || frozenVoice.fingerprint,
            durationSec: liveVoice?.durationSec || frozenVoice.durationSec,
            voiceId: liveVoice?.voiceId || frozenVoice.voiceId,
            storageKey: liveVoice?.storageKey || frozenVoice.storageKey,
            durationByLineId: { ...frozenVoice.durationByLineId, ...liveVoice?.durationByLineId },
          }
        : liveVoice;
  return {
    motion: motion && motion.fingerprint && motion.durationSec > 0 ? motion : undefined,
    voice: voice && voice.artifactId && voice.durationSec > 0 ? voice : undefined,
  };
}

/** Remake only. Use an already-stored physical key. Never mint. lineId key is OK if bytes exist. */
export async function resolveStoredVoicePhysicalSource(opts: {
  store: VoicePhysicalStore;
  buildId?: string;
  lineId: string;
  lineIds?: string[];
  voiceId?: string;
  voiceFingerprint: string;
  durationSec: number;
  durationByLineId?: Record<string, number>;
  preferredArtifactId?: string;
}): Promise<AuthoritativeLipSyncVoiceSource | undefined> {
  const lineId = trimOf(opts.lineId);
  const fingerprint = trimOf(opts.voiceFingerprint);
  const durationSec = Number(opts.durationSec) || 0;
  const buildId = trimOf(opts.buildId);
  const voiceId = trimOf(opts.voiceId);
  if (!lineId || !fingerprint || !(durationSec > 0) || !buildId || !voiceId) return undefined;
  const keys = await opts.store.keys();
  const preferred = trimOf(opts.preferredArtifactId);
  const parsed = keys.map(parseVoicePhysicalStorageKey).filter((row): row is NonNullable<typeof row> => {
    if (!row) return false;
    if (row.buildId !== buildId) return false;
    if (row.voiceId !== voiceId) return false;
    return true;
  });
  const preferredHit = preferred ? parsed.find((row) => row.artifactId === preferred) : undefined;
  const physical = parsed.filter((row) => row.artifactId !== lineId);
  const lineHits = parsed.filter((row) => row.artifactId === lineId);
  const hit =
    preferredHit || (physical.length === 1 ? physical[0] : undefined) || (lineHits.length === 1 ? lineHits[0] : undefined);
  if (!hit) return undefined;
  const bytes = await opts.store.get(hit.storageKey);
  if (!bytes?.length) return undefined;
  const lineIds = (opts.lineIds?.length ? opts.lineIds : [lineId]).map(trimOf).filter(Boolean);
  return {
    artifactId: hit.artifactId,
    lineId,
    lineIds,
    storageKey: hit.storageKey,
    voiceId: hit.voiceId,
    fingerprint,
    durationSec,
    durationByLineId: { ...(opts.durationByLineId ?? {}), [lineId]: durationSec },
  };
}

export function createLipSyncAttemptProvenance(
  frozen: LipSyncExecutionFrozenInput,
  status: LipSyncExecutionAttempt['status'] = 'SUBMITTING',
): LipSyncExecutionAttempt {
  if (!validateLipSyncFrozenInput(frozen)) {
    throw new Error('LipSync attempt requires frozen identity.');
  }
  return {
    providerId: frozen.providerId,
    modelId: frozen.modelId,
    motionArtifactId: frozen.motionArtifactId,
    motionFingerprint: frozen.motionFingerprint,
    motionDurationSec: frozen.motionDurationSec,
    voiceArtifactId: frozen.voiceArtifactId,
    voiceFingerprint: frozen.voiceFingerprint,
    voiceDurationSec: frozen.voiceDurationSec,
    status,
    motionTaskId: frozen.motionTaskId,
    motionUrl: frozen.motionUrl,
    voiceId: frozen.voiceId,
    buildId: frozen.buildId,
    lineId: frozen.lineId || frozen.voiceLineIds?.[0],
    motionPhysicalFingerprint: frozen.motionFingerprint,
    speakerCharacterId: frozen.speakerCharacterId,
  };
}

export function stampLipSyncAttemptProviderRequest(
  attempt: LipSyncExecutionAttempt,
  providerRequestId?: string,
  status: LipSyncExecutionAttempt['status'] = 'PENDING',
): LipSyncExecutionAttempt {
  return {
    ...attempt,
    providerRequestId: trimOf(providerRequestId) || attempt.providerRequestId,
    status,
  };
}

/** Physical motion from frozen motionArtifactId only. Never acceptedTake / lastSuccess / take-url resolver. */
export function resolveLipSyncMotionFromFrozenInput(
  run: SeriesShotRun | undefined,
  shotId: string,
  frozen: LipSyncExecutionFrozenInput,
): LipSyncMotionPhysicalInput {
  const motionArtifactId = trimOf(frozen.motionArtifactId);
  const n = parseMotionArtifactN(motionArtifactId, shotId);
  const snappedTask = trimOf(frozen.motionTaskId);
  const snappedUrl = trimOf(frozen.motionUrl);
  if (snappedTask || snappedUrl) {
    return {
      motionArtifactId,
      n,
      taskId: snappedTask || undefined,
      url: snappedUrl || undefined,
    };
  }
  const row = n ? (run?.runwayAttempts ?? []).find((item) => item.n === n) : undefined;
  if (row && n && row.n !== n) {
    return { motionArtifactId, n };
  }
  return {
    motionArtifactId,
    n,
    taskId: trimOf(row?.taskId) || undefined,
    url: trimOf(row?.outputUrl) || undefined,
  };
}

export type LipSyncProviderHowRequest = {
  clipId: string;
  videoUrl: string;
  takeTaskId?: string;
  motionArtifactId: string;
  voiceArtifactId: string;
  performanceDurationSec: number;
  syncMode?: string;
  model?: string;
  speakerX?: number;
  speakerY?: number;
  speakerVideoWidth?: number;
  speakerVideoHeight?: number;
  voiceDurationSec: number;
  speakerCharacterId?: string;
  speakerFrameNumber?: number;
};

/** HOW only. Frozen physical Motion + Voice. No mutable graph selectors. */
export function buildLipSyncProviderHowRequest<TVoice>(opts: {
  frozen: LipSyncExecutionFrozenInput;
  input: LipSyncProviderPhysicalInput<TVoice>;
  clipId: string;
  syncMode?: string;
  model?: string;
  speaker?: { x?: number; y?: number; width?: number; height?: number };
}): LipSyncProviderHowRequest {
  const frozen = freezeLipSyncExecutionInput(opts.frozen);
  if (opts.input.motionArtifactId !== frozen.motionArtifactId) {
    throw new Error('Provider motion is not frozen motionArtifactId.');
  }
  if (opts.input.voiceArtifactId !== frozen.voiceArtifactId) {
    throw new Error('Provider voice is not frozen voiceArtifactId.');
  }
  const videoUrl = trimOf(opts.input.videoUrl) || trimOf(frozen.motionUrl);
  const takeTaskId = trimOf(opts.input.takeTaskId) || trimOf(frozen.motionTaskId);
  if (!videoUrl && !takeTaskId) {
    throw new Error('Frozen motion has no physical URL/taskId.');
  }
  if (frozen.motionUrl && videoUrl && videoUrl !== frozen.motionUrl) {
    throw new Error('Provider videoUrl drifted from frozen motionUrl.');
  }
  if (frozen.motionTaskId && takeTaskId && takeTaskId !== frozen.motionTaskId) {
    throw new Error('Provider takeTaskId drifted from frozen motionTaskId.');
  }
  const model = opts.model || frozen.modelId;
  const region = frozen.speakerFaceRegion;
  return {
    clipId: trimOf(opts.clipId),
    videoUrl: videoUrl || '',
    takeTaskId: takeTaskId || undefined,
    motionArtifactId: frozen.motionArtifactId,
    voiceArtifactId: frozen.voiceArtifactId,
    performanceDurationSec: frozen.motionDurationSec,
    voiceDurationSec: frozen.voiceDurationSec,
    speakerCharacterId: frozen.speakerCharacterId,
    speakerFrameNumber: frozen.speakerFrameNumber,
    syncMode: opts.syncMode,
    model,
    speakerX: region?.x,
    speakerY: region?.y,
    speakerVideoWidth: region
      ? Number(region.width) > 1
        ? Math.round(Number(region.width))
        : 1280
      : undefined,
    speakerVideoHeight: region
      ? Number(region.height) > 1
        ? Math.round(Number(region.height))
        : 720
      : undefined,
  };
}

export function stampLipSyncProviderPhysicalProvenance(
  attempt: LipSyncExecutionAttempt,
  physical: {
    motionTaskId?: string;
    motionUrl?: string;
    motionPhysicalFingerprint?: string;
    voicePhysicalFingerprint?: string;
    voiceId?: string;
    buildId?: string;
    lineId?: string;
  },
): LipSyncExecutionAttempt {
  return {
    ...attempt,
    motionTaskId: trimOf(physical.motionTaskId) || attempt.motionTaskId,
    motionUrl: trimOf(physical.motionUrl) || attempt.motionUrl,
    motionPhysicalFingerprint:
      trimOf(physical.motionPhysicalFingerprint) || attempt.motionPhysicalFingerprint || attempt.motionFingerprint,
    voicePhysicalFingerprint:
      trimOf(physical.voicePhysicalFingerprint) || attempt.voicePhysicalFingerprint || attempt.voiceFingerprint,
    voiceId: trimOf(physical.voiceId) || attempt.voiceId,
    buildId: trimOf(physical.buildId) || attempt.buildId,
    lineId: trimOf(physical.lineId) || attempt.lineId,
  };
}

export async function sha256HexOfBytes(bytes: Uint8Array): Promise<string> {
  if (typeof crypto === 'undefined' || !crypto.subtle) {
    throw new Error('SHA-256 is not available in this runtime.');
  }
  const buf = await crypto.subtle.digest('SHA-256', bytes);
  return [...new Uint8Array(buf)].map((b) => b.toString(16).padStart(2, '0')).join('').toUpperCase();
}

/** Physical audio from frozen voiceArtifactId only. Caller must not re-pick a line. */
export function resolveLipSyncVoiceFromFrozenInput<T>(
  frozen: LipSyncExecutionFrozenInput,
  loadBytes: (voiceArtifactId: string) => T,
): { voiceArtifactId: string; bytes: T } {
  const voiceArtifactId = trimOf(frozen.voiceArtifactId);
  if (!voiceArtifactId) throw new Error('Frozen voiceArtifactId missing.');
  return { voiceArtifactId, bytes: loadBytes(voiceArtifactId) };
}

/** V2: frozen identity → bytes. Does not know IDB key format. No legacy search. */
export async function loadFrozenVoicePhysicalBytes(
  frozen: LipSyncExecutionFrozenInput,
  store: VoicePhysicalStore,
): Promise<Uint8Array> {
  const ident = voicePhysicalIdentityOf({
    artifactId: frozen.voiceArtifactId,
    buildId: frozen.buildId,
    lineId: frozen.lineId || frozen.voiceLineIds?.[0],
    voiceId: frozen.voiceId,
    fingerprint: frozen.voiceFingerprint,
    durationSec: frozen.voiceDurationSec,
  });
  if (!ident) throw new Error(VOICE_PHYSICAL_NOT_FOUND);
  const bytes = await loadVoicePhysicalArtifact(ident, store);
  if (bytes === VOICE_PHYSICAL_NOT_FOUND) throw new Error(VOICE_PHYSICAL_NOT_FOUND);
  return bytes;
}

export async function submitLipSyncFrozenExecution<TVoice, TResult>(opts: {
  frozen: LipSyncExecutionFrozenInput;
  run?: SeriesShotRun;
  shotId: string;
  persistAttempt: (attempt: LipSyncExecutionAttempt) => void;
  loadVoiceBytes: (voiceArtifactId: string) => TVoice | Promise<TVoice>;
  loadMotionBytes?: (motionArtifactId: string) => void;
  startProvider: (input: LipSyncProviderPhysicalInput<TVoice>) => TResult | Promise<TResult>;
}): Promise<{
  attempt: LipSyncExecutionAttempt;
  providerInput: LipSyncProviderPhysicalInput<TVoice>;
  providerResult: TResult;
}> {
  const frozen = freezeLipSyncExecutionInput(opts.frozen);
  const attempt = createLipSyncAttemptProvenance(frozen, 'SUBMITTING');
  opts.persistAttempt(attempt);
  const motion = resolveLipSyncMotionFromFrozenInput(opts.run, opts.shotId, frozen);
  if (motion.motionArtifactId !== frozen.motionArtifactId) {
    throw new Error('Frozen motion identity drifted — không gửi provider.');
  }
  if (!motion.taskId && !motion.url) {
    throw new Error('Frozen motion không resolve được file — không gửi provider.');
  }
  opts.loadMotionBytes?.(frozen.motionArtifactId);
  const voice = resolveLipSyncVoiceFromFrozenInput(frozen, (id) => opts.loadVoiceBytes(id));
  const voiceBytes = await voice.bytes;
  const providerInput: LipSyncProviderPhysicalInput<TVoice> = {
    motionArtifactId: frozen.motionArtifactId,
    voiceArtifactId: frozen.voiceArtifactId,
    videoUrl: motion.url,
    takeTaskId: motion.taskId,
    voiceBytes,
  };
  let voicePhysicalFingerprint: string | undefined;
  if (voiceBytes instanceof Uint8Array) {
    voicePhysicalFingerprint = await sha256HexOfBytes(voiceBytes);
  } else if (typeof Blob !== 'undefined' && voiceBytes instanceof Blob) {
    voicePhysicalFingerprint = await sha256HexOfBytes(new Uint8Array(await voiceBytes.arrayBuffer()));
  }
  opts.persistAttempt(
    stampLipSyncProviderPhysicalProvenance(attempt, {
      motionTaskId: motion.taskId,
      motionUrl: motion.url,
      motionPhysicalFingerprint: frozen.motionFingerprint,
      voicePhysicalFingerprint,
      voiceId: frozen.voiceId,
      buildId: frozen.buildId,
      lineId: frozen.lineId || frozen.voiceLineIds?.[0],
    }),
  );
  const how = buildLipSyncProviderHowRequest({
    frozen,
    input: providerInput,
    clipId: opts.shotId,
    model: frozen.modelId,
  });
  if (how.takeTaskId !== providerInput.takeTaskId && (how.takeTaskId || providerInput.takeTaskId)) {
    throw new Error('HOW takeTaskId drifted from frozen physical input.');
  }
  const providerResult = await opts.startProvider(providerInput);
  return { attempt, providerInput, providerResult };
}
