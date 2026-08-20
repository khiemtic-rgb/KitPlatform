/** Canon face pixels — keep off graph JSON (quota / server strip). Survives F5 via IndexedDB. */

const DB_NAME = 'kit-famixa-canon';
const STORE = 'pixels';
const mem = new Map<string, { dataUrl: string; fileName?: string }>();

function canonKey(characterId: string) {
  const m = characterId.toUpperCase().match(/CHAR\s*-?\s*(\d+)/);
  const id = m ? `CHAR-${String(m[1]).padStart(3, '0')}` : characterId.replace(/\s+/g, ' ').trim();
  return `FAMIXA:${id}`;
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

export function rememberCanonPixels(characterId: string, dataUrl?: string, fileName?: string) {
  const url = (dataUrl ?? '').trim();
  if (!characterId || !url.startsWith('data:image')) return;
  const prev = mem.get(canonKey(characterId));
  mem.set(canonKey(characterId), { dataUrl: url, fileName: fileName || prev?.fileName });
}

export function canonPixelsOf(characterId: string) {
  return mem.get(canonKey(characterId))?.dataUrl;
}

export function rememberCanonFromChars(
  characters: { id: string; canonImageDataUrl?: string; canonFileName?: string }[],
  stills?: { charCode?: string; imageDataUrl?: string; fileName?: string }[],
) {
  for (const c of characters) rememberCanonPixels(c.id, c.canonImageDataUrl, c.canonFileName);
  for (const s of stills ?? []) {
    if (s.charCode) rememberCanonPixels(s.charCode, s.imageDataUrl, s.fileName);
  }
}

export async function saveCanonPixels(characterId: string, dataUrl: string, fileName?: string) {
  rememberCanonPixels(characterId, dataUrl, fileName);
  const row = mem.get(canonKey(characterId));
  if (!row) return;
  const db = await openDb();
  await new Promise<void>((resolve, reject) => {
    const tx = db.transaction(STORE, 'readwrite');
    tx.objectStore(STORE).put(row, canonKey(characterId));
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
}

export async function loadCanonPixels(characterId: string) {
  const hit = mem.get(canonKey(characterId));
  if (hit) return hit;
  try {
    const db = await openDb();
    const row = await new Promise<{ dataUrl: string; fileName?: string } | undefined>((resolve, reject) => {
      const tx = db.transaction(STORE, 'readonly');
      const req = tx.objectStore(STORE).get(canonKey(characterId));
      req.onsuccess = () => resolve(req.result as { dataUrl: string; fileName?: string } | undefined);
      req.onerror = () => reject(req.error);
    });
    if (row?.dataUrl?.startsWith('data:image')) {
      mem.set(canonKey(characterId), row);
      return row;
    }
  } catch {
    /* private mode / quota */
  }
  return undefined;
}
