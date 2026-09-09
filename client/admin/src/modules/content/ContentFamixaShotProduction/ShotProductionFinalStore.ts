/** Canonical assemble blobs — same IndexedDB idea as TTS. Survives reload. Not a migration. */

import { famixaScopedKey } from '../content-famixa-media-scope';

const DB_NAME = 'kit-famixa-shot-final';
const STORE = 'final';
const PREFIX = 'FAMIXA:final';
const mem = new Map<string, Blob>();

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

export function rememberFinalBlob(shotId: string, blob?: Blob) {
  if (!shotId || !blob || blob.size < 80) return;
  mem.set(finalKey(shotId), blob);
}

export function finalBlobOf(shotId: string) {
  return mem.get(finalKey(shotId));
}

export async function saveFinalBlob(shotId: string, blob: Blob) {
  rememberFinalBlob(shotId, blob);
  try {
    const db = await openDb();
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction(STORE, 'readwrite');
      tx.objectStore(STORE).put(blob, finalKey(shotId));
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
  } catch {
    /* private mode / quota */
  }
}

export async function loadFinalBlob(shotId: string) {
  const hit = mem.get(finalKey(shotId));
  if (hit) return hit;
  try {
    const db = await openDb();
    const read = (key: string) =>
      new Promise<Blob | undefined>((resolve, reject) => {
        const tx = db.transaction(STORE, 'readonly');
        const req = tx.objectStore(STORE).get(key);
        req.onsuccess = () => resolve(req.result as Blob | undefined);
        req.onerror = () => reject(req.error);
      });
    const row = await read(finalKey(shotId));
    if (row && row.size >= 80) {
      mem.set(finalKey(shotId), row);
      return row;
    }
  } catch {
    /* private mode */
  }
  return undefined;
}

export function finalObjectUrl(shotId: string) {
  const blob = mem.get(finalKey(shotId));
  return blob ? URL.createObjectURL(blob) : undefined;
}
