/** Local image library via File System Access API — no server storage.
 * Each brand has its own folder handle in IndexedDB so Novixa/Famixa/… don’t mix images.
 */

const DB_NAME = 'kit-content-local-image-lib';
const STORE = 'meta';
/** Legacy single-folder keys (pre brand-scoped) — only used as migrate-once fallback. */
const LEGACY_HANDLE_KEY = 'dirHandle';
const LEGACY_NAME_KEY = 'dirName';

/** Max folder depth below the picked root (root = 0). */
const MAX_SCAN_DEPTH = 2;
/** Cap listing so UI stays snappy on huge folders. */
const MAX_LIST = 200;

export type LocalImageEntry = {
  /** Relative path from library root, e.g. `sub/a.jpg` or `a.jpg`. */
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

export type LocalLibraryPermission = 'granted' | 'prompt' | 'denied' | 'none';

export type LocalLibraryStatus = {
  name: string | null;
  hasHandle: boolean;
  /** Chrome often returns `prompt` after reload — must re-grant via a button click. */
  permission: LocalLibraryPermission;
  brandId: string;
};

async function queryReadPermission(handle: FileSystemDirectoryHandle): Promise<PermissionState> {
  try {
    return await handle.queryPermission({ mode: 'read' });
  } catch {
    return 'prompt';
  }
}

async function resolveHandle(brandId: string): Promise<FileSystemDirectoryHandle | null> {
  if (!brandId) return null;
  let handle = await idbGet<FileSystemDirectoryHandle>(handleKey(brandId));
  if (handle) return handle;
  // One-time: don’t auto-bind legacy global folder to every brand (would mix images).
  return null;
}

/** Request read permission — call only from a user gesture (button click). */
export async function requestLocalImageLibraryPermission(brandId: string): Promise<boolean> {
  const handle = await resolveHandle(brandId);
  if (!handle) return false;
  const q = await queryReadPermission(handle);
  if (q === 'granted') return true;
  try {
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

/** Lightweight name map for brand table (no permission prompt). */
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

export async function getLocalImageLibraryStatus(brandId: string): Promise<LocalLibraryStatus> {
  if (!brandId) {
    return { name: null, hasHandle: false, permission: 'none', brandId: '' };
  }
  const name = (await idbGet<string>(nameKey(brandId))) ?? null;
  const handle = await resolveHandle(brandId);
  if (!handle) {
    return { name, hasHandle: false, permission: 'none', brandId };
  }
  const q = await queryReadPermission(handle);
  return {
    name: name ?? handle.name,
    hasHandle: true,
    permission: q === 'granted' ? 'granted' : q === 'denied' ? 'denied' : 'prompt',
    brandId,
  };
}

export async function clearLocalImageLibrary(brandId: string): Promise<void> {
  if (!brandId) return;
  await idbDel(handleKey(brandId));
  await idbDel(nameKey(brandId));
}

/** Ask user to pick a folder for this brand; persist handle in IndexedDB (browser only). */
export async function pickLocalImageLibrary(brandId: string): Promise<{ name: string; count: number }> {
  if (!brandId) throw new Error('Thiếu thương hiệu — chọn brand trước khi gắn kho ảnh.');
  if (!hasFsAccess()) {
    throw new Error('Trình duyệt không hỗ trợ chọn thư mục (cần Chrome / Edge).');
  }

  // Must run in the same user-gesture turn as the click — no setState before this.
  let dir: FileSystemDirectoryHandle;
  try {
    dir = await window.showDirectoryPicker({ mode: 'read' });
  } catch (e) {
    const name = e && typeof e === 'object' && 'name' in e ? String((e as { name: string }).name) : '';
    if (name === 'AbortError') throw e;
    if (name === 'SecurityError' || name === 'NotAllowedError') {
      throw new Error(
        'Trình duyệt chặn hộp thoại thư mục. Bấm lại nút «Chọn thư mục ảnh» (chuột, không Enter), dùng Chrome/Edge, mở admin qua http://localhost hoặc HTTPS.',
      );
    }
    throw new Error(
      e instanceof Error && e.message
        ? e.message
        : 'Không mở được hộp thoại chọn thư mục. Thử Chrome/Edge và bấm lại nút.',
    );
  }

  try {
    await idbSet(handleKey(brandId), dir);
    await idbSet(nameKey(brandId), dir.name);
    await idbDel(LEGACY_HANDLE_KEY);
    await idbDel(LEGACY_NAME_KEY);
  } catch {
    throw new Error(
      'Đã chọn thư mục nhưng không lưu được vào trình duyệt (IndexedDB). Kiểm tra không chặn cookie/storage cho site này.',
    );
  }

  let count = 0;
  try {
    const images = await listLocalImages(brandId, { requestPermission: true });
    count = images.length;
  } catch {
    /* folder saved; listing can fail until permission — still OK */
  }
  return { name: dir.name, count };
}

export async function getLocalDirectoryHandle(
  brandId: string,
  opts?: { requestPermission?: boolean },
): Promise<FileSystemDirectoryHandle | null> {
  const handle = await resolveHandle(brandId);
  if (!handle) return null;
  const q = await queryReadPermission(handle);
  if (q === 'granted') return handle;
  if (opts?.requestPermission) {
    try {
      const r = await handle.requestPermission({ mode: 'read' });
      if (r === 'granted') return handle;
    } catch {
      return null;
    }
  }
  return null;
}

const IMAGE_RE = /\.(png|jpe?g|webp|gif)$/i;

async function collectImages(
  dir: FileSystemDirectoryHandle,
  prefix: string,
  depth: number,
  out: LocalImageEntry[],
): Promise<void> {
  for await (const [name, entry] of dir.entries()) {
    if (out.length >= MAX_LIST) return;
    if (entry.kind === 'file' && IMAGE_RE.test(name)) {
      out.push({ name: prefix + name, handle: entry as FileSystemFileHandle });
      continue;
    }
    if (entry.kind === 'directory' && depth < MAX_SCAN_DEPTH) {
      await collectImages(entry as FileSystemDirectoryHandle, `${prefix}${name}/`, depth + 1, out);
    }
  }
}

export async function listLocalImages(
  brandId: string,
  opts?: { requestPermission?: boolean },
): Promise<LocalImageEntry[]> {
  if (!brandId) return [];
  const dir = await getLocalDirectoryHandle(brandId, opts);
  if (!dir) return [];
  const out: LocalImageEntry[] = [];
  await collectImages(dir, '', 0, out);
  return out.sort((a, b) => a.name.localeCompare(b.name, 'vi'));
}

/** Build object URLs for gallery preview — caller must revoke via `revokeLocalPreviewUrls`. */
export async function loadLocalImagePreviews(
  entries: LocalImageEntry[],
  limit = 36,
): Promise<{ name: string; url: string }[]> {
  const slice = entries.slice(0, limit);
  const out: { name: string; url: string }[] = [];
  for (const entry of slice) {
    try {
      const file = await entry.handle.getFile();
      out.push({ name: entry.name, url: URL.createObjectURL(file) });
    } catch {
      /* skip unreadable */
    }
  }
  return out;
}

export function revokeLocalPreviewUrls(previews: { url: string }[]): void {
  for (const p of previews) {
    try {
      URL.revokeObjectURL(p.url);
    } catch {
      /* ignore */
    }
  }
}

function stripExt(name: string): string {
  return name.replace(IMAGE_RE, '');
}

/** Basename only (ignore folder prefix). */
function baseName(path: string): string {
  const parts = path.split(/[/\\]/);
  return parts[parts.length - 1] || path;
}

/** Fold Vietnamese accents + punctuation for comparison. */
function normalizeKey(text: string): string {
  return text
    .toLowerCase()
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/đ/g, 'd')
    .replace(/[^a-z0-9]+/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

/** Remove leading numbering: `01-`, `1.`, `P1_`, `(1) ` */
function stripLeadingIndex(text: string): string {
  return text
    .replace(/^\s*[\(\[]?\s*(?:p|bai|anh|img|image)?\s*\d{1,3}\s*[\)\]]?\s*[-_.–—:)\]\s]+/i, '')
    .trim();
}

/** Common words that appear in almost every pharmacy title — dilute matching. */
const STOPWORDS = new Set([
  'nha',
  'thuoc',
  'nhathuoc',
  'dang',
  'nhung',
  'cua',
  'cho',
  'cac',
  'mot',
  'nhung',
  'voi',
  'ma',
  'khong',
  'biet',
  'bao',
  'nhieu',
  'the',
  'nao',
  'va',
  'hay',
  'rat',
  'noi',
  'khi',
  'neu',
  'thi',
  'de',
  'tu',
  'tren',
  'duoi',
  'trong',
  'ngoai',
  'lai',
  'van',
  'can',
  'phai',
  'duoc',
  'se',
  'da',
  'la',
  'o',
  'tai',
  've',
  'toi',
  'minh',
  'anh',
  'img',
  'image',
  'jpeg',
  'jpg',
  'png',
  'webp',
  'novixa',
  'kit',
  'content',
]);

function tokenize(text: string): string[] {
  const key = normalizeKey(stripLeadingIndex(stripExt(baseName(text))));
  return key
    .split(' ')
    .map((t) => t.trim())
    .filter((t) => t.length >= 2 && !STOPWORDS.has(t));
}

function allTokens(text: string): string[] {
  const key = normalizeKey(stripLeadingIndex(stripExt(baseName(text))));
  return key
    .split(' ')
    .map((t) => t.trim())
    .filter((t) => t.length >= 2);
}

/** Dice coefficient on character bigrams — good for near-identical titles. */
function diceBigram(a: string, b: string): number {
  if (!a || !b) return 0;
  if (a === b) return 1;
  if (a.length < 2 || b.length < 2) return a === b ? 1 : 0;
  const grams = (s: string) => {
    const m = new Map<string, number>();
    for (let i = 0; i < s.length - 1; i++) {
      const g = s.slice(i, i + 2);
      m.set(g, (m.get(g) ?? 0) + 1);
    }
    return m;
  };
  const A = grams(a);
  const B = grams(b);
  let inter = 0;
  for (const [g, ca] of A) {
    const cb = B.get(g) ?? 0;
    if (cb > 0) inter += Math.min(ca, cb);
  }
  return (2 * inter) / (a.length - 1 + (b.length - 1));
}

export type LocalImageScore = {
  entry: LocalImageEntry;
  score: number;
  /** 0–100 for UI */
  confidence: number;
  reason: string;
};

/**
 * Rank local images for a topic title.
 * Priority: exact/near-full title match → distinctive keyword overlap → weak shared words.
 */
export function rankLocalImages(title: string, images: LocalImageEntry[]): LocalImageScore[] {
  if (images.length === 0) return [];

  const titleNorm = normalizeKey(title);
  const titleDistinct = tokenize(title);
  const titleAll = allTokens(title);
  const titleDistinctSet = new Set(titleDistinct);

  const ranked = images.map((entry) => {
    const rawBase = stripExt(baseName(entry.name));
    const fileNorm = normalizeKey(stripLeadingIndex(rawBase));
    const fileDistinct = tokenize(entry.name);
    const fileAll = allTokens(entry.name);

    let score = 0;
    let reason = 'yếu';

    // 1) Exact / near-exact full string
    if (fileNorm === titleNorm && fileNorm.length > 0) {
      score = 100_000;
      reason = 'khớp đúng tiêu đề';
    } else {
      const soft = diceBigram(fileNorm.replace(/\s/g, ''), titleNorm.replace(/\s/g, ''));
      if (soft >= 0.88) {
        score = 80_000 + Math.round(soft * 1000);
        reason = 'gần đúng tiêu đề';
      } else if (titleNorm.length >= 12 && (fileNorm.includes(titleNorm) || titleNorm.includes(fileNorm))) {
        const coverage =
          Math.min(fileNorm.length, titleNorm.length) / Math.max(fileNorm.length, titleNorm.length);
        score = 60_000 + Math.round(coverage * 5000);
        reason = 'tên file chứa tiêu đề';
      } else {
        // 2) Distinctive token overlap (ignore nhà/thuốc/đang…)
        let hitDistinct = 0;
        for (const t of fileDistinct) {
          if (titleDistinctSet.has(t)) hitDistinct += 1;
          else {
            for (const tt of titleDistinct) {
              if (tt.length >= 4 && t.length >= 4 && (tt.includes(t) || t.includes(tt))) {
                hitDistinct += 0.5;
                break;
              }
            }
          }
        }
        const denom = Math.max(titleDistinct.length, 1);
        const precision = hitDistinct / Math.max(fileDistinct.length, 1);
        const recall = hitDistinct / denom;
        const f1 = precision + recall > 0 ? (2 * precision * recall) / (precision + recall) : 0;

        score = Math.round(f1 * 40_000 + hitDistinct * 800);

        // 3) Soft bonus from all-token overlap, but small (avoids “nhà thuốc” wins)
        let weak = 0;
        const titleAllSet = new Set(titleAll);
        for (const t of fileAll) {
          if (titleAllSet.has(t) && !STOPWORDS.has(t)) weak += 1;
          else if (titleAllSet.has(t)) weak += 0.15;
        }
        score += Math.round(weak * 40);

        // 4) Bigram soft similarity as tie-breaker
        score += Math.round(soft * 2000);

        if (hitDistinct >= 2 && f1 >= 0.45) reason = 'khớp từ khóa chính';
        else if (hitDistinct >= 1) reason = 'khớp một phần';
        else if (soft >= 0.45) reason = 'gần chuỗi ký tự';
        else reason = 'yếu';
      }
    }

    const confidence = Math.max(0, Math.min(100, Math.round(score / 1000)));
    return { entry, score, confidence, reason };
  });

  ranked.sort((a, b) => b.score - a.score || a.entry.name.localeCompare(b.entry.name, 'vi'));
  return ranked;
}

/** Heuristic pick — only auto-select when confidence is meaningful. */
export function pickBestLocalImage(title: string, images: LocalImageEntry[]): LocalImageEntry | null {
  const ranked = rankLocalImages(title, images);
  if (ranked.length === 0) return null;
  const top = ranked[0]!;
  // Prefer strong matches; if all weak, still return top so UX has a default,
  // but callers can check rankLocalImages for confidence badge.
  return top.entry;
}

/** True when the top match is trustworthy enough to highlight as “đúng bài”. */
export function isConfidentLocalMatch(title: string, images: LocalImageEntry[]): boolean {
  const top = rankLocalImages(title, images)[0];
  return !!top && top.score >= 15_000;
}

export async function readLocalImageAsBase64(entry: LocalImageEntry): Promise<{
  base64: string;
  fileName: string;
  contentType: string;
}> {
  const prepared = await prepareLocalImageForPublish(entry);
  const base64 = await blobToBase64(prepared.blob);
  return { base64, fileName: prepared.fileName, contentType: prepared.contentType };
}

/** Compressed JPEG blob for multipart publish (preferred over huge JSON base64). */
export async function prepareLocalImageForPublish(entry: LocalImageEntry): Promise<{
  blob: Blob;
  fileName: string;
  contentType: string;
}> {
  const file = await entry.handle.getFile();
  return prepareImageBlobForPublish(file, entry.name);
}

/** Resize/compress for reliable FB/WP/Astro upload. */
async function prepareImageBlobForPublish(
  file: File,
  entryName: string,
): Promise<{ blob: Blob; fileName: string; contentType: string }> {
  const rawName = entryName.includes('/') ? entryName.split('/').pop()! : entryName;
  const fallBackMime = file.type || guessMime(entryName);

  try {
    const bitmap = await createImageBitmap(file);
    const maxEdge = 1600;
    const scale = Math.min(1, maxEdge / Math.max(bitmap.width, bitmap.height));
    const w = Math.max(1, Math.round(bitmap.width * scale));
    const h = Math.max(1, Math.round(bitmap.height * scale));
    const canvas = document.createElement('canvas');
    canvas.width = w;
    canvas.height = h;
    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error('canvas');
    ctx.drawImage(bitmap, 0, 0, w, h);
    bitmap.close();

    const blob: Blob = await new Promise((resolve, reject) => {
      canvas.toBlob(
        (b) => (b ? resolve(b) : reject(new Error('toBlob failed'))),
        'image/jpeg',
        0.85,
      );
    });
    const fileName = rawName.replace(/\.(png|webp|gif|jpe?g)$/i, '') + '.jpg';
    return { blob, fileName, contentType: 'image/jpeg' };
  } catch {
    return { blob: file, fileName: rawName, contentType: fallBackMime };
  }
}

function blobToBase64(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => {
      const dataUrl = String(reader.result ?? '');
      const i = dataUrl.indexOf(',');
      resolve(i >= 0 ? dataUrl.slice(i + 1) : dataUrl);
    };
    reader.onerror = () => reject(reader.error ?? new Error('FileReader failed'));
    reader.readAsDataURL(blob);
  });
}

function guessMime(name: string): string {
  const n = name.toLowerCase();
  if (n.endsWith('.png')) return 'image/png';
  if (n.endsWith('.webp')) return 'image/webp';
  if (n.endsWith('.gif')) return 'image/gif';
  return 'image/jpeg';
}
