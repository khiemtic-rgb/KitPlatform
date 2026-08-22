/** TTS blobs — same idea as KF IndexedDB. Survives F5 so Final can mux. */

import { famixaLegacyKey, famixaMediaScope, famixaScopedKey } from './content-famixa-media-scope';

const DB_NAME = 'kit-famixa-tts';
const STORE = 'audio';
const PREFIX = 'FAMIXA:tts';
const mem = new Map<string, Blob>();

function ttsKey(lineId: string) {
  return famixaScopedKey(PREFIX, lineId);
}

function ttsLegacy(lineId: string) {
  return famixaLegacyKey(PREFIX, lineId);
}

export function ttsTextKey(text: string, voiceId?: string) {
  const t = text.replace(/\s+/g, ' ').trim();
  const v = (voiceId ?? '').trim();
  return v ? `text:${v}:${t}` : `text:${t}`;
}

export function ttsLineKey(lineId: string, voiceId?: string) {
  const id = lineId.trim();
  const v = (voiceId ?? '').trim();
  return v ? `${id}#${v}` : id;
}

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, 1);
    req.onupgradeneeded = () => {
      if (!req.result.objectStoreNames.contains(STORE)) req.result.createObjectStore(STORE);
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error ?? new Error('IndexedDB TTS open failed'));
  });
}

export function rememberTtsBlob(lineId: string, blob?: Blob) {
  if (!lineId || !blob || blob.size < 32) return;
  mem.set(ttsKey(lineId), blob);
}

export function ttsBlobOf(lineId: string) {
  return mem.get(ttsKey(lineId));
}

export function measureAudioSec(blob: Blob) {
  return new Promise<number>((resolve) => {
    const url = URL.createObjectURL(blob);
    const el = new Audio();
    el.preload = 'metadata';
    el.onloadedmetadata = () => {
      const n = Number.isFinite(el.duration) ? el.duration : 0;
      URL.revokeObjectURL(url);
      resolve(n > 0 && n < 120 ? n : 0);
    };
    el.onerror = () => {
      URL.revokeObjectURL(url);
      resolve(0);
    };
    el.src = url;
  });
}

export async function saveTtsBlob(lineId: string, blob: Blob) {
  rememberTtsBlob(lineId, blob);
  try {
    const db = await openDb();
    await new Promise<void>((resolve, reject) => {
      const tx = db.transaction(STORE, 'readwrite');
      tx.objectStore(STORE).put(blob, ttsKey(lineId));
      tx.oncomplete = () => resolve();
      tx.onerror = () => reject(tx.error);
    });
  } catch {
    /* private mode / quota */
  }
}

export async function loadTtsBlob(lineId: string) {
  const hit = mem.get(ttsKey(lineId));
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
    const row = (await read(ttsKey(lineId))) || (famixaMediaScope() ? await read(ttsLegacy(lineId)) : undefined);
    if (row && row.size >= 32) {
      mem.set(ttsKey(lineId), row);
      if (famixaMediaScope()) void saveTtsBlob(lineId, row);
      return row;
    }
  } catch {
    /* private mode */
  }
  return undefined;
}

export async function deleteTtsScope(buildId: string) {
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

/** First hit among current line id and older fingerprints (script id can shift). */
export async function loadTtsBlobAny(lineIds: string[]) {
  for (const id of lineIds) {
    const t = id.trim();
    if (!t) continue;
    const blob = await loadTtsBlob(t);
    if (blob) return blob;
  }
  return undefined;
}
