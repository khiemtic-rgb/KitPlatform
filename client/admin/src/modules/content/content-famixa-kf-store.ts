/** Scene/short KF pixels — off graph JSON (quota / server strip). Survives pane switch + F5. */

import { famixaLegacyKey, famixaMediaScope, famixaScopedKey } from './content-famixa-media-scope';

const DB_NAME = 'kit-famixa-kf';
const STORE = 'pixels';
const PREFIX = 'FAMIXA:kf';
const mem = new Map<string, { dataUrl: string; fileName?: string }>();

function kfKey(clipId: string) {
  return famixaScopedKey(PREFIX, clipId);
}

function kfLegacy(clipId: string) {
  return famixaLegacyKey(PREFIX, clipId);
}

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, 1);
    req.onupgradeneeded = () => {
      if (!req.result.objectStoreNames.contains(STORE)) req.result.createObjectStore(STORE);
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error ?? new Error('IndexedDB open failed'));
  });
}

const savedSig = new Set<string>();

export function rememberKfPixels(clipId: string, dataUrl?: string, fileName?: string) {
  const url = (dataUrl ?? '').trim();
  if (!clipId || !url.startsWith('data:image')) return;
  const prev = mem.get(kfKey(clipId));
  mem.set(kfKey(clipId), { dataUrl: url, fileName: fileName || prev?.fileName });
}

export function kfPixelsOf(clipId: string) {
  return mem.get(kfKey(clipId))?.dataUrl;
}

export function rememberKfFromRuns(
  runs: Record<string, { keyframeDataUrl?: string; keyframeFileName?: string }>,
) {
  for (const [id, run] of Object.entries(runs)) {
    rememberKfPixels(id, run.keyframeDataUrl, run.keyframeFileName);
  }
}

export async function saveKfPixels(clipId: string, dataUrl: string, fileName?: string) {
  rememberKfPixels(clipId, dataUrl, fileName);
  const row = mem.get(kfKey(clipId));
  if (!row) return;
  const sig = `${kfKey(clipId)}:${row.dataUrl.length}:${row.fileName ?? ''}`;
  if (savedSig.has(sig)) return;
  savedSig.add(sig);
  try {
    const db = await openDb();
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction(STORE, 'readwrite');
      tx.objectStore(STORE).put(row, kfKey(clipId));
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
  } catch {
    /* private mode / quota */
  }
}

export async function loadKfPixels(clipId: string) {
  const hit = mem.get(kfKey(clipId));
  if (hit) return hit;
  try {
    const db = await openDb();
    const read = (key: string) =>
      new Promise<{ dataUrl: string; fileName?: string } | undefined>((resolve, reject) => {
        const tx = db.transaction(STORE, 'readonly');
        const req = tx.objectStore(STORE).get(key);
        req.onsuccess = () => resolve(req.result as { dataUrl: string; fileName?: string } | undefined);
        req.onerror = () => reject(req.error);
      });
    const row = (await read(kfKey(clipId))) || (famixaMediaScope() ? await read(kfLegacy(clipId)) : undefined);
    if (row?.dataUrl?.startsWith('data:image')) {
      mem.set(kfKey(clipId), row);
      return row;
    }
  } catch {
    /* private mode */
  }
  return undefined;
}

export async function deleteKfScope(buildId: string) {
  const prefix = `${PREFIX}:${buildId.trim()}:`;
  if (!prefix || prefix.length < 12) return;
  for (const key of [...mem.keys()]) {
    if (key.startsWith(prefix)) mem.delete(key);
  }
  try {
    const db = await openDb();
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction(STORE, 'readwrite');
      const store = tx.objectStore(STORE);
      const req = store.getAllKeys();
      req.onsuccess = () => {
        for (const key of req.result ?? []) {
          if (String(key).startsWith(prefix)) store.delete(key);
        }
      };
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
  } catch {
    /* private mode */
  }
}
