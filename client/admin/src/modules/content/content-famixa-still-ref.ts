/** Shrink still/canon data URLs before POST /series/still. API rejects > ~2.4MB chars. */

export const STILL_REF_MAX_CHARS = 1_800_000;

function loadImg(src: string) {
  return new Promise<HTMLImageElement>((resolve, reject) => {
    const img = new Image();
    img.onload = () => resolve(img);
    img.onerror = () => reject(new Error('Không đọc được ảnh ref.'));
    img.src = src;
  });
}

export async function shrinkStillDataUrl(url: string, maxChars = STILL_REF_MAX_CHARS): Promise<string | undefined> {
  const src = (url ?? '').trim();
  if (!src.startsWith('data:image') || src.length < 32) return undefined;
  if (src.length <= maxChars) return src;
  try {
    const img = await loadImg(src);
    const maxEdge = 1280;
    const scale = Math.min(1, maxEdge / Math.max(img.naturalWidth || 1, img.naturalHeight || 1));
    const w = Math.max(1, Math.round((img.naturalWidth || 1) * scale));
    const h = Math.max(1, Math.round((img.naturalHeight || 1) * scale));
    const canvas = document.createElement('canvas');
    canvas.width = w;
    canvas.height = h;
    const ctx = canvas.getContext('2d');
    if (!ctx) return undefined;
    ctx.drawImage(img, 0, 0, w, h);
    for (const q of [0.82, 0.7, 0.55]) {
      const out = canvas.toDataURL('image/jpeg', q);
      if (out.length >= 32 && out.length <= maxChars) return out;
    }
  } catch {
    return undefined;
  }
  return undefined;
}
