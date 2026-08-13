/** Local image library via File System Access API — no server storage.
 * Each brand has its own folder handle in IndexedDB so Novixa/Famixa/… don’t mix images.
 */

const DB_NAME = 'kit-content-local-image-lib';
const STORE = 'meta';
/** Legacy single-folder keys (pre brand-scoped) — not auto-bound to brands. */
const LEGACY_HANDLE_KEY = 'dirHandle';
const LEGACY_NAME_KEY = 'dirName';

export type LocalImageEntry = {
  name: string;
  handle: FileSystemFileHandle;
};

function handleKey(brandId: string) {
  return `dirHandle:${brandId}`;
}

function nameKey(brandId: string) {
  return `dirName:${brandId}`;
}

function hasFsAccess(): boolean {
  return typeof window !== 'undefined' && 'showDirectoryPicker' in window;
}

function openDb(): Promise<IDBDatabase> {
  return new Promise((resolve, reject) => {
    const req = indexedDB.open(DB_NAME, 1);
    req.onupgradeneeded = () => {
      const db = req.result;
      if (!db.objectStoreNames.contains(STORE)) db.createObjectStore(STORE);
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error ?? new Error('IndexedDB open failed'));
  });
}

async function idbGet<T>(key: string): Promise<T | undefined> {
  const db = await openDb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE, 'readonly');
    const req = tx.objectStore(STORE).get(key);
    req.onsuccess = () => resolve(req.result as T | undefined);
    req.onerror = () => reject(req.error);
  });
}

async function idbSet(key: string, value: unknown): Promise<void> {
  const db = await openDb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE, 'readwrite');
    tx.objectStore(STORE).put(value, key);
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
}

async function idbDel(key: string): Promise<void> {
  const db = await openDb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE, 'readwrite');
    tx.objectStore(STORE).delete(key);
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error);
  });
}

async function ensurePermission(handle: FileSystemDirectoryHandle): Promise<boolean> {
  try {
    const q = await handle.queryPermission({ mode: 'read' });
    if (q === 'granted') return true;
    const r = await handle.requestPermission({ mode: 'read' });
    return r === 'granted';
  } catch {
    return false;
  }
}

export function isLocalImageLibrarySupported() {
  return hasFsAccess();
}

export async function getLocalImageLibraryName(brandId: string): Promise<string | null> {
  if (!brandId) return null;
  return (await idbGet<string>(nameKey(brandId))) ?? null;
}

/** Lightweight name map for brand table. */
export async function getLocalImageLibraryNames(
  brandIds: string[],
): Promise<Record<string, string | null>> {
  const out: Record<string, string | null> = {};
  await Promise.all(
    brandIds.map(async (id) => {
      out[id] = (await idbGet<string>(nameKey(id))) ?? null;
    }),
  );
  return out;
}

export async function clearLocalImageLibrary(brandId: string): Promise<void> {
  if (!brandId) return;
  await idbDel(handleKey(brandId));
  await idbDel(nameKey(brandId));
}

/**
 * Pick a folder for this brand. Call directly from a button click —
 * do not setState before this (loses user gesture → SecurityError).
 */
export async function pickLocalImageLibrary(brandId: string): Promise<{ name: string; count: number }> {
  if (!brandId) throw new Error('Thiếu thương hiệu — chọn brand trước khi gắn kho ảnh.');
  if (!hasFsAccess()) {
    throw new Error('Trình duyệt không hỗ trợ chọn thư mục (cần Chrome / Edge).');
  }

  let dir: FileSystemDirectoryHandle;
  try {
    dir = await window.showDirectoryPicker({ mode: 'read' });
  } catch (e) {
    const name = e && typeof e === 'object' && 'name' in e ? String((e as { name: string }).name) : '';
    if (name === 'AbortError') throw e;
    if (name === 'SecurityError' || name === 'NotAllowedError') {
      throw new Error(
        'Trình duyệt chặn hộp thoại thư mục. Bấm lại nút bằng chuột (Chrome/Edge, localhost/HTTPS).',
      );
    }
    throw new Error(
      e instanceof Error && e.message
        ? e.message
        : 'Không mở được hộp thoại chọn thư mục.',
    );
  }

  try {
    await idbSet(handleKey(brandId), dir);
    await idbSet(nameKey(brandId), dir.name);
    await idbDel(LEGACY_HANDLE_KEY);
    await idbDel(LEGACY_NAME_KEY);
  } catch {
    throw new Error('Đã chọn thư mục nhưng không lưu được (IndexedDB). Kiểm tra cookie/storage.');
  }

  let count = 0;
  try {
    const images = await listLocalImages(brandId);
    count = images.length;
  } catch {
    /* folder saved; listing may need permission later */
  }
  return { name: dir.name, count };
}

export async function getLocalDirectoryHandle(brandId: string): Promise<FileSystemDirectoryHandle | null> {
  if (!brandId) return null;
  const handle = await idbGet<FileSystemDirectoryHandle>(handleKey(brandId));
  if (!handle) return null;
  if (!(await ensurePermission(handle))) return null;
  return handle;
}

const IMAGE_RE = /\.(png|jpe?g|webp|gif)$/i;

export async function listLocalImages(brandId: string): Promise<LocalImageEntry[]> {
  if (!brandId) return [];
  const dir = await getLocalDirectoryHandle(brandId);
  if (!dir) return [];
  const out: LocalImageEntry[] = [];
  for await (const [name, entry] of dir.entries()) {
    if (entry.kind === 'file' && IMAGE_RE.test(name)) {
      out.push({ name, handle: entry as FileSystemFileHandle });
    }
  }
  return out.sort((a, b) => a.name.localeCompare(b.name, 'vi'));
}

function tokenize(text: string): string[] {
  return text
    .toLowerCase()
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/[^a-z0-9àáạảãâầấậẩẫăằắặẳẵèéẹẻẽêềếệểễìíịỉĩòóọỏõôồốộổỗơờớợởỡùúụủũưừứựửữỳýỵỷỹđ\s-]/gi, ' ')
    .split(/[\s_-]+/)
    .map((t) => t.trim())
    .filter((t) => t.length >= 2);
}

/** Heuristic pick: score filename tokens against title — no server, no Vision. */
export function pickBestLocalImage(title: string, images: LocalImageEntry[]): LocalImageEntry | null {
  if (images.length === 0) return null;
  const titleTokens = new Set(tokenize(title));
  let best = images[0]!;
  let bestScore = -1;
  for (const img of images) {
    const nameTokens = tokenize(img.name.replace(IMAGE_RE, ''));
    let score = 0;
    for (const t of nameTokens) {
      if (titleTokens.has(t)) score += 3;
      else {
        for (const tt of titleTokens) {
          if (tt.includes(t) || t.includes(tt)) score += 1;
        }
      }
    }
    score = score * 100 - nameTokens.length;
    if (score > bestScore) {
      bestScore = score;
      best = img;
    }
  }
  return best;
}

export async function readLocalImageAsBase64(entry: LocalImageEntry): Promise<{
  base64: string;
  fileName: string;
  contentType: string;
}> {
  const file = await entry.handle.getFile();
  const buf = await file.arrayBuffer();
  const bytes = new Uint8Array(buf);
  let binary = '';
  const chunk = 0x8000;
  for (let i = 0; i < bytes.length; i += chunk) {
    binary += String.fromCharCode(...bytes.subarray(i, i + chunk));
  }
  const base64 = btoa(binary);
  const contentType = file.type || guessMime(entry.name);
  return { base64, fileName: entry.name, contentType };
}

function guessMime(name: string): string {
  const n = name.toLowerCase();
  if (n.endsWith('.png')) return 'image/png';
  if (n.endsWith('.webp')) return 'image/webp';
  if (n.endsWith('.gif')) return 'image/gif';
  return 'image/jpeg';
}
