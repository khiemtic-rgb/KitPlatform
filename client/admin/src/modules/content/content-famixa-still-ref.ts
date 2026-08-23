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

/** Runway data-URI cap is 5MB. Official gen4_turbo pixels only — Gemini often returns 1024×1024. */
export const RUNWAY_KF_MAX_CHARS = 3_500_000;

export function runwayFrameSize(ratio?: string) {
  const r = (ratio ?? '').trim().toLowerCase();
  if (r === '9:16' || r === '9x16' || r === '720:1280') return { width: 720, height: 1280 };
  return { width: 1280, height: 720 };
}

/** When dest is much taller/wider than a 2-person still, cover-crop cuts a parent off. */
export function shouldContainFit(srcW: number, srcH: number, destW: number, destH: number, people?: number) {
  if ((people ?? 0) < 2 || !srcW || !srcH) return false;
  const srcA = srcW / srcH;
  const destA = destW / destH;
  return Math.abs(srcA - destA) / destA > 0.08;
}

export function containDrawRect(srcW: number, srcH: number, destW: number, destH: number) {
  const scale = Math.min(destW / Math.max(1, srcW), destH / Math.max(1, srcH));
  const dw = Math.max(1, Math.round(srcW * scale));
  const dh = Math.max(1, Math.round(srcH * scale));
  return { dx: Math.round((destW - dw) / 2), dy: Math.round((destH - dh) / 2), dw, dh };
}

/** Cover-crop source rect. Landscape dest keeps faces (bias up). */
export function coverCropRect(srcW: number, srcH: number, destW: number, destH: number) {
  const sw0 = Math.max(1, srcW);
  const sh0 = Math.max(1, srcH);
  const srcA = sw0 / sh0;
  const destA = destW / destH;
  if (srcA > destA) {
    const sw = Math.round(sh0 * destA);
    const sx = Math.round((sw0 - sw) / 2);
    return { sx: Math.max(0, sx), sy: 0, sw: Math.min(sw, sw0), sh: sh0 };
  }
  const sh = Math.round(sw0 / destA);
  const slack = Math.max(0, sh0 - sh);
  const sy = Math.round(slack * 0.28);
  return { sx: 0, sy: Math.max(0, Math.min(sy, slack)), sw: sw0, sh: Math.min(sh, sh0) };
}

export function aspectMatchesRunway(width: number, height: number, ratio?: string, slack = 0.04) {
  if (!width || !height) return false;
  const { width: tw, height: th } = runwayFrameSize(ratio);
  const a = width / height;
  const t = tw / th;
  return Math.abs(a - t) / t <= slack;
}

/** Official gen4_turbo pixels only — 1344×768 is 16:9 but not 1280×720. */
export function pixelsMatchRunway(width: number, height: number, ratio?: string) {
  const { width: tw, height: th } = runwayFrameSize(ratio);
  return width === tw && height === th;
}

export function isJpegDataUri(url?: string) {
  const t = (url ?? '').trim().toLowerCase();
  return t.startsWith('data:image/jpeg') || t.startsWith('data:image/jpg');
}

/**
 * Mandatory Runway payload: JPEG at exact 1280×720 or 720×1280.
 * Never pass through Gemini/SK PNG, even when the aspect is already 16:9.
 */
export async function prepareRunwayKf(url: string, ratio?: string, opts?: { people?: number }): Promise<string> {
  const src = (url ?? '').trim();
  if (!src.startsWith('data:image')) {
    throw new Error('KF phải là data-URI trước Runway — không gửi URL/PNG gốc (0 cr).');
  }
  const img = await loadImg(src);
  const { width: dw, height: dh } = runwayFrameSize(ratio);
  const sw = img.naturalWidth || 1;
  const sh = img.naturalHeight || 1;
  if (isJpegDataUri(src) && pixelsMatchRunway(sw, sh, ratio) && src.length >= 800 && src.length <= RUNWAY_KF_MAX_CHARS) {
    return src;
  }
  const canvas = document.createElement('canvas');
  canvas.width = dw;
  canvas.height = dh;
  const ctx = canvas.getContext('2d');
  if (!ctx) throw new Error('Không normalize được KF — không gửi PNG gốc (0 cr).');
  ctx.fillStyle = '#1a1410';
  ctx.fillRect(0, 0, dw, dh);
  if (shouldContainFit(sw, sh, dw, dh, opts?.people)) {
    const fit = containDrawRect(sw, sh, dw, dh);
    ctx.drawImage(img, 0, 0, sw, sh, fit.dx, fit.dy, fit.dw, fit.dh);
  } else {
    const crop = coverCropRect(sw, sh, dw, dh);
    ctx.drawImage(img, crop.sx, crop.sy, crop.sw, crop.sh, 0, 0, dw, dh);
  }
  for (const q of [0.92, 0.88, 0.82, 0.7]) {
    const out = canvas.toDataURL('image/jpeg', q);
    if (out.length >= 800 && out.length <= RUNWAY_KF_MAX_CHARS) return out;
  }
  throw new Error('KF vẫn quá nặng sau JPEG 1280 — không gửi Runway (0 cr).');
}

export async function measureKfImage(url: string) {
  const src = (url ?? '').trim();
  if (!src.startsWith('data:image') && !/^https:\/\//i.test(src)) {
    return { width: 0, height: 0 };
  }
  const img = await loadImg(src);
  return { width: img.naturalWidth || 0, height: img.naturalHeight || 0 };
}
