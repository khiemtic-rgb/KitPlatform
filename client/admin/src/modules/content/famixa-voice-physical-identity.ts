/** FAMIXA_VOICE_PHYSICAL_IDENTITY_CONTRACT_V1 — VoiceArtifactId → one storage key → exact bytes. */

export const VOICE_PHYSICAL_IDENTITY_ID = 'FAMIXA_VOICE_PHYSICAL_IDENTITY_CONTRACT_V1';
export const VOICE_PHYSICAL_KEY_PREFIX = 'FAMIXA:voice';
export const VOICE_PHYSICAL_NOT_FOUND = 'VOICE_PHYSICAL_NOT_FOUND';

export type VoicePhysicalArtifactIdentity = {
  artifactId: string;
  buildId: string;
  lineId: string;
  voiceId: string;
  fingerprint?: string;
  durationSec?: number;
};

export type VoicePhysicalArtifact = VoicePhysicalArtifactIdentity & {
  storageKey: string;
};

export type VoicePhysicalStore = {
  get(key: string): Uint8Array | undefined | Promise<Uint8Array | undefined>;
  set(key: string, bytes: Uint8Array): void | Promise<void>;
  delete(key: string): void | Promise<void>;
  keys(): string[] | Promise<string[]>;
};

function trimOf(v?: string) {
  return (v || '').trim();
}

export function createMemoryVoicePhysicalStore(): VoicePhysicalStore {
  const rows = new Map<string, Uint8Array>();
  return {
    get(key) {
      const hit = rows.get(key);
      return hit ? new Uint8Array(hit) : undefined;
    },
    set(key, bytes) {
      rows.set(key, new Uint8Array(bytes));
    },
    delete(key) {
      rows.delete(key);
    },
    keys() {
      return [...rows.keys()];
    },
  };
}

export function voicePhysicalIdentityOf(
  raw: Partial<VoicePhysicalArtifactIdentity>,
): VoicePhysicalArtifactIdentity | undefined {
  const artifactId = trimOf(raw.artifactId);
  const buildId = trimOf(raw.buildId);
  const lineId = trimOf(raw.lineId) || artifactId;
  const voiceId = trimOf(raw.voiceId);
  if (!artifactId || !buildId || !lineId || !voiceId) return undefined;
  return {
    artifactId,
    buildId,
    lineId,
    voiceId,
    fingerprint: raw.fingerprint ? trimOf(raw.fingerprint) : undefined,
    durationSec: Number(raw.durationSec) > 0 ? Number(raw.durationSec) : undefined,
  };
}

/** New physical id. Never copies a historical SH01 artifact string. */
export function mintVoicePhysicalArtifactId(now = new Date()): string {
  const stamp = now.toISOString().replace(/[-:]/g, '').replace(/\.\d+Z$/, 'Z');
  const rand =
    typeof crypto !== 'undefined' && typeof crypto.randomUUID === 'function'
      ? crypto.randomUUID().replace(/-/g, '').slice(0, 8)
      : Math.floor(Math.random() * 0xffffffff)
          .toString(16)
          .padStart(8, '0');
  return `vpa-${stamp}-${rand}`;
}

/** Parse `FAMIXA:voice:{buildId}:{artifactId}:{voiceId}`. Does not invent ids. */
export function parseVoicePhysicalStorageKey(key?: string): {
  buildId: string;
  artifactId: string;
  voiceId: string;
  storageKey: string;
} | undefined {
  const raw = trimOf(key);
  const prefix = `${VOICE_PHYSICAL_KEY_PREFIX}:`;
  if (!raw.startsWith(prefix)) return undefined;
  const rest = raw.slice(prefix.length);
  const buildEnd = rest.indexOf(':');
  if (buildEnd <= 0) return undefined;
  const buildId = rest.slice(0, buildEnd);
  const after = rest.slice(buildEnd + 1);
  const artEnd = after.lastIndexOf(':');
  if (artEnd <= 0) return undefined;
  const artifactId = after.slice(0, artEnd);
  const voiceId = after.slice(artEnd + 1);
  if (!buildId || !artifactId || !voiceId || artifactId === voiceId) return undefined;
  return { buildId, artifactId, voiceId, storageKey: raw };
}

/** Stored refs for this build. Prefer one artifactId / voiceId. Never mint. */
export function findVoicePhysicalRefsInStoreKeys(
  keys: string[],
  opts: { buildId?: string; voiceId?: string; preferredArtifactId?: string },
): { buildId: string; artifactId: string; voiceId: string; storageKey: string } | undefined {
  const buildId = trimOf(opts.buildId);
  const voiceId = trimOf(opts.voiceId);
  const preferred = trimOf(opts.preferredArtifactId);
  const rows = (keys ?? []).map(parseVoicePhysicalStorageKey).filter((row): row is NonNullable<typeof row> => {
    if (!row) return false;
    if (buildId && row.buildId !== buildId) return false;
    if (voiceId && row.voiceId !== voiceId) return false;
    return true;
  });
  if (!rows.length) return undefined;
  if (preferred) {
    const hit = rows.find((row) => row.artifactId === preferred);
    if (hit) return hit;
  }
  return rows.length === 1 ? rows[0] : undefined;
}

export function voicePhysicalStorageKey(identity: VoicePhysicalArtifactIdentity): string {
  const id = voicePhysicalIdentityOf(identity);
  if (!id) throw new Error('VoicePhysicalArtifact identity incomplete.');
  return `${VOICE_PHYSICAL_KEY_PREFIX}:${id.buildId}:${id.artifactId}:${id.voiceId}`;
}

export function assertVoicePhysicalStorageKey(key: string) {
  const raw = trimOf(key);
  if (!raw.startsWith(`${VOICE_PHYSICAL_KEY_PREFIX}:`)) {
    throw new Error('VoicePhysicalStore refuses non-canonical keys.');
  }
  return raw;
}

export function voicePhysicalArtifactOf(identity: VoicePhysicalArtifactIdentity): VoicePhysicalArtifact {
  const id = voicePhysicalIdentityOf(identity);
  if (!id) throw new Error('VoicePhysicalArtifact identity incomplete.');
  return { ...id, storageKey: voicePhysicalStorageKey(id) };
}

export async function saveVoicePhysicalArtifact(
  identity: VoicePhysicalArtifactIdentity,
  bytes: Uint8Array,
  store: VoicePhysicalStore,
): Promise<VoicePhysicalArtifact> {
  const artifact = voicePhysicalArtifactOf(identity);
  if (!bytes.length) throw new Error('VoicePhysicalArtifact bytes empty.');
  await store.set(artifact.storageKey, bytes);
  return artifact;
}

export async function loadVoicePhysicalArtifact(
  identity: VoicePhysicalArtifactIdentity,
  store: VoicePhysicalStore,
): Promise<Uint8Array | typeof VOICE_PHYSICAL_NOT_FOUND> {
  const id = voicePhysicalIdentityOf(identity);
  if (!id) return VOICE_PHYSICAL_NOT_FOUND;
  const hit = await store.get(voicePhysicalStorageKey(id));
  return hit && hit.length ? hit : VOICE_PHYSICAL_NOT_FOUND;
}

export async function hasVoicePhysicalArtifact(identity: VoicePhysicalArtifactIdentity, store: VoicePhysicalStore) {
  return (await loadVoicePhysicalArtifact(identity, store)) !== VOICE_PHYSICAL_NOT_FOUND;
}

export async function deleteVoicePhysicalArtifact(identity: VoicePhysicalArtifactIdentity, store: VoicePhysicalStore) {
  const id = voicePhysicalIdentityOf(identity);
  if (!id) return;
  await store.delete(voicePhysicalStorageKey(id));
}

export function sameVoicePhysicalBytes(a: Uint8Array, b: Uint8Array) {
  if (a.length !== b.length) return false;
  for (let i = 0; i < a.length; i++) if (a[i] !== b[i]) return false;
  return true;
}
