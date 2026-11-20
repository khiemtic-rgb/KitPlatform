/** CACHE / RECOVERY only. Reuses kit-famixa-tts IDB. Not Production OS CURRENT:VOICE authority. */

import {
  assertVoicePhysicalStorageKey,
  VOICE_PHYSICAL_KEY_PREFIX,
  type VoicePhysicalStore,
} from './famixa-voice-physical-identity';

const DB_NAME = 'kit-famixa-tts';
const STORE = 'audio';

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, 1);
    req.onupgradeneeded = () => {
      if (!req.result.objectStoreNames.contains(STORE)) req.result.createObjectStore(STORE);
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error ?? new Error('IndexedDB voice physical open failed'));
  });
}

async function asBytes(row: unknown): Promise<Uint8Array | undefined> {
  if (!row) return undefined;
  if (row instanceof Uint8Array) return row.length ? new Uint8Array(row) : undefined;
  if (row instanceof ArrayBuffer) return row.byteLength ? new Uint8Array(row) : undefined;
  if (typeof Blob !== 'undefined' && row instanceof Blob) {
    if (row.size < 1) return undefined;
    return new Uint8Array(await row.arrayBuffer());
  }
  return undefined;
}

/** Same IDB FAMIXA already uses for TTS. Canonical Voice keys only — does not read FAMIXA:tts: legacy. */
export function createIdbVoicePhysicalStore(): VoicePhysicalStore {
  return {
    async get(key) {
      const id = assertVoicePhysicalStorageKey(key);
      const db = await openDb();
      const row = await new Promise<unknown>((resolve, reject) => {
        const tx = db.transaction(STORE, 'readonly');
        const req = tx.objectStore(STORE).get(id);
        req.onsuccess = () => resolve(req.result);
        req.onerror = () => reject(req.error);
      });
      return asBytes(row);
    },
    async set(key, bytes) {
      const id = assertVoicePhysicalStorageKey(key);
      if (!bytes.length) throw new Error('VoicePhysicalArtifact bytes empty.');
      const db = await openDb();
      const blob = new Blob([bytes], { type: 'application/octet-stream' });
      await new Promise<void>((resolve, reject) => {
        const tx = db.transaction(STORE, 'readwrite');
        tx.objectStore(STORE).put(blob, id);
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
      });
    },
    async delete(key) {
      const id = assertVoicePhysicalStorageKey(key);
      const db = await openDb();
      await new Promise<void>((resolve, reject) => {
        const tx = db.transaction(STORE, 'readwrite');
        tx.objectStore(STORE).delete(id);
        tx.oncomplete = () => resolve();
        tx.onerror = () => reject(tx.error);
      });
    },
    async keys() {
      const db = await openDb();
      const all = await new Promise<IDBValidKey[]>((resolve, reject) => {
        const tx = db.transaction(STORE, 'readonly');
        const req = tx.objectStore(STORE).getAllKeys();
        req.onsuccess = () => resolve(req.result ?? []);
        req.onerror = () => reject(req.error);
      });
      const prefix = `${VOICE_PHYSICAL_KEY_PREFIX}:`;
      return all.map(String).filter((k) => k.startsWith(prefix));
    },
  };
}
