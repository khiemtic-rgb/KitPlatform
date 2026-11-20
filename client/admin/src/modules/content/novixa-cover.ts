/**
 * Ảnh bìa cho bài đăng video (Video Studio bước 5): lấy một khung hình của video, phủ nền tối và chữ tiêu đề.
 * Chạy trong trình duyệt bằng canvas, không tốn tiền AI. Ảnh JPG dưới 2 MB (giới hạn ảnh bìa YouTube).
 */

export type CoverShape = 'wide' | 'tall';

export const COVER_SIZE: Record<CoverShape, { width: number; height: number }> = {
  wide: { width: 1280, height: 720 },
  tall: { width: 1080, height: 1920 },
};

const MAX_BYTES = 1.9 * 1024 * 1024;

export type CoverOptions = {
  shape: CoverShape;
  title: string;
  /** Giây lấy khung hình. Bỏ trống: khoảng 1/3 thời lượng (thường đã vào nội dung chính). */
  atSeconds?: number;
  withTitle?: boolean;
};

export type CoverResult = { blob: Blob; atSeconds: number; duration: number };

function once(target: HTMLVideoElement, event: string, timeoutMs = 20_000) {
  return new Promise<void>((resolve, reject) => {
    const timer = window.setTimeout(() => {
      cleanup();
      reject(new Error('Video không phản hồi khi lấy khung hình.'));
    }, timeoutMs);
    const ok = () => {
      cleanup();
      resolve();
    };
    const fail = () => {
      cleanup();
      reject(new Error('Trình duyệt không đọc được video để lấy khung hình.'));
    };
    const cleanup = () => {
      window.clearTimeout(timer);
      target.removeEventListener(event, ok);
      target.removeEventListener('error', fail);
    };
    target.addEventListener(event, ok, { once: true });
    target.addEventListener('error', fail, { once: true });
  });
}

/** Cắt chữ thành các dòng vừa khung, tối đa maxLines dòng (dòng cuối thêm … nếu thiếu chỗ). */
function wrap(ctx: CanvasRenderingContext2D, text: string, maxWidth: number, maxLines: number) {
  const words = text.trim().split(/\s+/).filter(Boolean);
  const lines: string[] = [];
  let line = '';
  for (const word of words) {
    const next = line ? `${line} ${word}` : word;
    if (ctx.measureText(next).width <= maxWidth || !line) {
      line = next;
    } else {
      lines.push(line);
      line = word;
      if (lines.length === maxLines) break;
    }
  }
  if (lines.length < maxLines && line) lines.push(line);
  const used = lines.join(' ').split(/\s+/).length;
  if (used < words.length && lines.length) {
    let last = lines[lines.length - 1];
    while (last && ctx.measureText(`${last}…`).width > maxWidth) last = last.split(' ').slice(0, -1).join(' ');
    lines[lines.length - 1] = `${last}…`;
  }
  return lines;
}

function toBlob(canvas: HTMLCanvasElement, quality: number) {
  return new Promise<Blob>((resolve, reject) =>
    canvas.toBlob((b) => (b ? resolve(b) : reject(new Error('Không xuất được ảnh bìa.'))), 'image/jpeg', quality),
  );
}

/** Bỏ tên kênh / tên phim sau dấu "|" và hashtag: ảnh bìa chỉ cần câu chính, ngắn. */
export function coverHeadline(title: string) {
  return title.split('|')[0].replace(/#[\p{L}\p{N}_]+/gu, '').replace(/\s+/g, ' ').trim();
}

export async function makeCover(video: Blob, options: CoverOptions): Promise<CoverResult> {
  const { width, height } = COVER_SIZE[options.shape];
  const url = URL.createObjectURL(video);
  const el = document.createElement('video');
  el.muted = true;
  el.playsInline = true;
  el.preload = 'auto';
  el.src = url;
  try {
    if (el.readyState < 1) await once(el, 'loadedmetadata');
    const duration = Number.isFinite(el.duration) ? el.duration : 0;
    const wanted = options.atSeconds ?? Math.min(duration * 0.33, 20);
    const at = Math.max(0, Math.min(wanted, Math.max(0, duration - 0.1)));
    el.currentTime = at;
    await once(el, 'seeked');

    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error('Trình duyệt không hỗ trợ vẽ ảnh bìa.');

    // Khung hình phủ kín ảnh (cắt bớt phần thừa, giữ giữa).
    const vw = el.videoWidth || width;
    const vh = el.videoHeight || height;
    const scale = Math.max(width / vw, height / vh);
    const dw = vw * scale;
    const dh = vh * scale;
    ctx.fillStyle = '#000';
    ctx.fillRect(0, 0, width, height);
    ctx.drawImage(el, (width - dw) / 2, (height - dh) / 2, dw, dh);

    const headline = coverHeadline(options.title);
    if (options.withTitle !== false && headline) {
      const wide = options.shape === 'wide';
      // Ảnh dọc: chữ ở khoảng 1/3 trên-giữa để không bị nút và chú thích của Shorts/Reels che.
      const band = wide ? { top: height * 0.5, bottom: height } : { top: height * 0.18, bottom: height * 0.58 };
      const grad = ctx.createLinearGradient(0, band.top, 0, band.bottom);
      if (wide) {
        grad.addColorStop(0, 'rgba(0,0,0,0)');
        grad.addColorStop(0.45, 'rgba(0,0,0,0.6)');
        grad.addColorStop(1, 'rgba(0,0,0,0.85)');
      } else {
        grad.addColorStop(0, 'rgba(0,0,0,0)');
        grad.addColorStop(0.25, 'rgba(0,0,0,0.6)');
        grad.addColorStop(0.75, 'rgba(0,0,0,0.6)');
        grad.addColorStop(1, 'rgba(0,0,0,0)');
      }
      ctx.fillStyle = grad;
      ctx.fillRect(0, band.top, width, band.bottom - band.top);

      const pad = wide ? 64 : 72;
      let size = wide ? 76 : 96;
      let lines: string[] = [];
      for (; size >= (wide ? 44 : 60); size -= 4) {
        ctx.font = `800 ${size}px "Be Vietnam Pro", "Inter", "Segoe UI", Arial, sans-serif`;
        lines = wrap(ctx, headline.toLocaleUpperCase('vi-VN'), width - pad * 2, wide ? 3 : 4);
        if (!lines.some((l) => l.endsWith('…'))) break;
      }
      const lineHeight = size * 1.15;
      const blockHeight = lines.length * lineHeight;
      const startY = wide ? height - pad - blockHeight + size : (band.top + band.bottom) / 2 - blockHeight / 2 + size;
      ctx.textBaseline = 'alphabetic';
      ctx.textAlign = wide ? 'left' : 'center';
      const x = wide ? pad : width / 2;
      // Vạch nhấn trên khối chữ.
      ctx.fillStyle = '#FFC83D';
      const barWidth = wide ? 120 : 160;
      ctx.fillRect(wide ? pad : width / 2 - barWidth / 2, startY - size - 28, barWidth, 10);
      ctx.shadowColor = 'rgba(0,0,0,0.6)';
      ctx.shadowBlur = 12;
      ctx.lineJoin = 'round';
      ctx.lineWidth = Math.max(4, size / 9);
      ctx.strokeStyle = 'rgba(0,0,0,0.85)';
      ctx.fillStyle = '#FFFFFF';
      lines.forEach((l, i) => {
        const y = startY + i * lineHeight;
        ctx.strokeText(l, x, y);
        ctx.fillText(l, x, y);
      });
      ctx.shadowBlur = 0;
    }

    let quality = 0.92;
    let blob = await toBlob(canvas, quality);
    while (blob.size > MAX_BYTES && quality > 0.5) {
      quality -= 0.08;
      blob = await toBlob(canvas, quality);
    }
    return { blob, atSeconds: at, duration };
  } finally {
    el.removeAttribute('src');
    el.load();
    URL.revokeObjectURL(url);
  }
}

/** Ảnh người dùng tự chọn: đổi về đúng khổ, JPG dưới 2 MB. */
export async function fitImage(file: Blob, shape: CoverShape): Promise<Blob> {
  const { width, height } = COVER_SIZE[shape];
  const bitmap = await createImageBitmap(file);
  try {
    const canvas = document.createElement('canvas');
    canvas.width = width;
    canvas.height = height;
    const ctx = canvas.getContext('2d');
    if (!ctx) throw new Error('Trình duyệt không hỗ trợ vẽ ảnh bìa.');
    const scale = Math.max(width / bitmap.width, height / bitmap.height);
    const dw = bitmap.width * scale;
    const dh = bitmap.height * scale;
    ctx.drawImage(bitmap, (width - dw) / 2, (height - dh) / 2, dw, dh);
    let quality = 0.92;
    let blob = await toBlob(canvas, quality);
    while (blob.size > MAX_BYTES && quality > 0.5) {
      quality -= 0.08;
      blob = await toBlob(canvas, quality);
    }
    return blob;
  } finally {
    bitmap.close();
  }
}
