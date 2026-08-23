/** Per-shot Runway I2V pipeline. HTTP 200 ≠ VIDEO READY. */

export type VideoPipeStatus =
  | 'VIDEO_NOT_SENT'
  | 'VIDEO_QUEUED'
  | 'VIDEO_SUBMITTED'
  | 'VIDEO_PROCESSING'
  | 'VIDEO_SUCCEEDED'
  | 'VIDEO_DOWNLOADING'
  | 'VIDEO_READY'
  | 'INPUT_INVALID'
  | 'RUNWAY_FAILED'
  | 'TIMEOUT'
  | 'DOWNLOAD_FAILED'
  | 'FILE_INVALID';

export type KfInputCheck = {
  ok: boolean;
  kind: 'data' | 'https' | 'none';
  mime?: string;
  bytes?: number;
  width?: number;
  height?: number;
  reasons: string[];
  checks: { id: string; ok: boolean; label: string }[];
};

export type RunwayImageMeta = {
  mime?: string;
  bytes?: number;
  width?: number;
  height?: number;
  hash?: string;
};

export type RunwayFailureClass = 'INPUT' | 'RUNWAY_GENERATION' | 'INTERMITTENT' | 'PENDING';

export type RunwayAttempt = {
  n: number;
  at: string;
  taskId?: string;
  submitOk?: boolean;
  status?: string;
  failureCode?: string;
  outputUrl?: string;
  downloadOk?: boolean;
  videoBytes?: number;
  videoMime?: string;
  error?: string;
  billed?: number;
  kf?: RunwayImageMeta;
  source?: RunwayImageMeta;
  promptHash?: string;
  model?: string;
  duration?: number;
  ratio?: string;
  httpStatus?: number;
  diagnostic?: boolean;
  classification?: RunwayFailureClass;
};

export type RunwayDiagRow = {
  shotId: string;
  kfId?: string;
  inputHash: string;
  normalizedImageHash: string;
  mimeType: string;
  width: number;
  height: number;
  fileSize: number;
  model: string;
  duration: number;
  promptHash: string;
  requestId?: string;
  runwayJobId?: string;
  httpStatus?: number;
  runwayStatus?: string;
  outputUrl?: string;
  downloadOk?: boolean;
  videoBytes?: number;
  errorCode?: string;
  errorMessage?: string;
  classification: RunwayFailureClass;
  at: string;
  diagnostic?: boolean;
};

export type RunwayPipeRun = {
  previewUrl?: string;
  localVideoPath?: string;
  turboTaskId?: string;
  turboStatus?: string;
  turboError?: string;
  videoPipe?: VideoPipeStatus;
  videoVerified?: boolean;
  videoBytes?: number;
  videoMime?: string;
  runwayAttempts?: RunwayAttempt[];
  kfCheck?: KfInputCheck;
  sentKfCheck?: KfInputCheck;
  keyframeDataUrl?: string;
  runwayDiagnostics?: RunwayDiagRow[];
  kfRetryOk?: boolean;
};

const PIPE_LABEL: Record<VideoPipeStatus, string> = {
  VIDEO_NOT_SENT: 'Chưa gửi',
  VIDEO_QUEUED: 'Hàng đợi',
  VIDEO_SUBMITTED: 'Đã nhận job',
  VIDEO_PROCESSING: 'Runway đang làm',
  VIDEO_SUCCEEDED: 'Có URL — chưa xác file',
  VIDEO_DOWNLOADING: 'Đang tải file',
  VIDEO_READY: 'VIDEO READY',
  INPUT_INVALID: 'KF không gửi được',
  RUNWAY_FAILED: 'Runway FAILED',
  TIMEOUT: 'Hết giờ poll',
  DOWNLOAD_FAILED: 'Không tải được file',
  FILE_INVALID: 'File không phải video',
};

export function videoPipeLabel(status: VideoPipeStatus) {
  return PIPE_LABEL[status];
}

export function hasVerifiedTake(run?: RunwayPipeRun) {
  const url = (run?.previewUrl || run?.localVideoPath || '').trim();
  if (!url) return false;
  if (run?.videoVerified === false) return false;
  return true;
}

function upperStatus(run?: RunwayPipeRun) {
  return (run?.turboStatus || '').trim().toUpperCase();
}

export function classifyVideoPipe(run?: RunwayPipeRun): VideoPipeStatus {
  if (run?.videoPipe && run.videoPipe !== 'VIDEO_READY' && run.videoPipe !== 'VIDEO_NOT_SENT') {
    if (run.videoPipe === 'INPUT_INVALID' && !hasVerifiedTake(run)) return 'INPUT_INVALID';
  }
  if (hasVerifiedTake(run)) return 'VIDEO_READY';
  const stored = run?.videoPipe;
  if (stored === 'INPUT_INVALID') return 'INPUT_INVALID';
  if (stored === 'DOWNLOAD_FAILED') return 'DOWNLOAD_FAILED';
  if (stored === 'FILE_INVALID') return 'FILE_INVALID';
  if (stored === 'TIMEOUT') return 'TIMEOUT';
  if (stored === 'VIDEO_DOWNLOADING') return 'VIDEO_DOWNLOADING';
  const st = upperStatus(run);
  const err = run?.turboError || '';
  if (st === 'FAILED' || st === 'CANCELLED') return 'RUNWAY_FAILED';
  if (st === 'SUCCEEDED' && !hasVerifiedTake(run)) {
    if (/download|tải|không đọc|không tải/i.test(err)) return 'DOWNLOAD_FAILED';
    if (/file|mime|không phải video|output url/i.test(err)) return 'FILE_INVALID';
    return 'VIDEO_SUCCEEDED';
  }
  if (st === 'RUNNING' || st === 'PROCESSING' || st === 'IN_PROGRESS') return 'VIDEO_PROCESSING';
  if (st === 'PENDING' || st === 'THROTTLED' || st === 'QUEUED') {
    return run?.turboTaskId?.trim() ? 'VIDEO_SUBMITTED' : 'VIDEO_QUEUED';
  }
  if (st === 'RETRY') return /timeout|hết giờ|chưa trả take/i.test(err) ? 'TIMEOUT' : 'VIDEO_QUEUED';
  if (run?.turboTaskId?.trim()) return stored && stored !== 'VIDEO_NOT_SENT' ? stored : 'VIDEO_SUBMITTED';
  if (stored === 'INPUT_INVALID') return 'INPUT_INVALID';
  return 'VIDEO_NOT_SENT';
}

export function isVideoPipeError(status: VideoPipeStatus) {
  return (
    status === 'INPUT_INVALID' ||
    status === 'RUNWAY_FAILED' ||
    status === 'TIMEOUT' ||
    status === 'DOWNLOAD_FAILED' ||
    status === 'FILE_INVALID'
  );
}

export function canManualRetry(run?: RunwayPipeRun): {
  ok: boolean;
  kind: 'resume' | 'new' | 'recover' | 'none';
  reason?: string;
} {
  const pipe = classifyVideoPipe(run);
  if (pipe === 'VIDEO_READY') return { ok: false, kind: 'none', reason: 'Đã có file.' };
  if (pipe === 'VIDEO_PROCESSING' || pipe === 'VIDEO_SUBMITTED' || pipe === 'VIDEO_QUEUED' || pipe === 'VIDEO_DOWNLOADING') {
    return { ok: true, kind: 'resume', reason: 'Hỏi lại task cũ — 0 cr.' };
  }
  if (pipe === 'TIMEOUT') return { ok: true, kind: 'resume', reason: 'Hỏi lại task cũ — 0 cr.' };
  if (pipe === 'INPUT_INVALID') return { ok: false, kind: 'none', reason: 'Sửa KF rồi mới gửi.' };
  const output = latestAttempt(run)?.outputUrl?.trim();
  if ((pipe === 'DOWNLOAD_FAILED' || pipe === 'FILE_INVALID' || pipe === 'VIDEO_SUCCEEDED') && output) {
    return { ok: true, kind: 'recover', reason: 'Thử đọc URL đã có — 0 cr.' };
  }
  if (pipe === 'RUNWAY_FAILED' && /INTERNAL/i.test(run?.turboError || '')) {
    if (sameKfAsInternalFail(run)) {
      return {
        ok: false,
        kind: 'none',
        reason: 'INTERNAL.BAD_OUTPUT — cùng KF không gửi lại. Sửa/duyệt KF mới rồi gửi 1 lần.',
      };
    }
    return { ok: true, kind: 'new', reason: 'KF đã đổi — gửi 1 job mới, không spam.' };
  }
  if (pipe === 'RUNWAY_FAILED' || pipe === 'DOWNLOAD_FAILED' || pipe === 'FILE_INVALID') {
    return { ok: true, kind: 'new', reason: 'Job mới — trừ cr thêm.' };
  }
  if (pipe === 'VIDEO_NOT_SENT') return { ok: true, kind: 'new', reason: 'Gửi job mới.' };
  return { ok: false, kind: 'none' };
}

export function latestAttempt(run?: RunwayPipeRun) {
  const list = run?.runwayAttempts ?? [];
  return list.at(-1);
}

export function startRunwayAttempt(run: RunwayPipeRun | undefined, init: Omit<RunwayAttempt, 'n' | 'at'> & { at?: string }): RunwayAttempt[] {
  const prev = [...(run?.runwayAttempts ?? [])].slice(-7);
  const next: RunwayAttempt = {
    n: (prev.at(-1)?.n ?? 0) + 1,
    at: init.at || new Date().toISOString(),
    ...init,
  };
  return [...prev, next];
}

export function patchRunwayAttempt(attempts: RunwayAttempt[] | undefined, taskId: string | undefined, patch: Partial<RunwayAttempt>) {
  const list = [...(attempts ?? [])];
  if (!list.length) return list;
  let idx = list.length - 1;
  if (taskId) {
    for (let i = list.length - 1; i >= 0; i--) {
      if (list[i]?.taskId === taskId) {
        idx = i;
        break;
      }
    }
  }
  list[idx] = { ...list[idx]!, ...patch, n: list[idx]!.n };
  return list;
}

export function isPrivateRunwayImageUrl(raw: string) {
  try {
    const u = new URL(raw);
    if (u.protocol !== 'https:' && u.protocol !== 'http:') return false;
    const h = u.hostname.toLowerCase();
    if (h === 'localhost' || h === '127.0.0.1' || h === '::1') return true;
    if (h.startsWith('192.168.') || h.startsWith('10.')) return true;
    return /^172\.(1[6-9]|2\d|3[01])\./.test(h);
  } catch {
    return /https?:\/\/(localhost|127\.0\.0\.1)/i.test(raw);
  }
}

function dataUriMeta(raw: string) {
  const comma = raw.indexOf(',');
  if (!raw.toLowerCase().startsWith('data:') || comma < 0) return undefined;
  const header = raw.slice(5, comma);
  const semi = header.indexOf(';');
  const mime = (semi > 0 ? header.slice(0, semi) : header).trim().toLowerCase();
  const payload = raw.slice(comma + 1).replace(/\s+/g, '');
  const pad = payload.endsWith('==') ? 2 : payload.endsWith('=') ? 1 : 0;
  const bytes = Math.max(0, Math.floor((payload.length * 3) / 4) - pad);
  return { mime, bytes, payload };
}

function decodePrefix(payload: string, n = 16): number[] {
  try {
    const chunk = payload.slice(0, Math.ceil((n * 4) / 3) + 4);
    const bin = typeof atob === 'function' ? atob(chunk) : '';
    if (!bin) return [];
    const out: number[] = [];
    for (let i = 0; i < Math.min(n, bin.length); i++) out.push(bin.charCodeAt(i));
    return out;
  } catch {
    return [];
  }
}

function looksLikeImageMagic(bytes: number[], mime?: string) {
  if (bytes.length >= 3 && bytes[0] === 0xff && bytes[1] === 0xd8 && bytes[2] === 0xff) return true;
  if (bytes.length >= 4 && bytes[0] === 0x89 && bytes[1] === 0x50 && bytes[2] === 0x4e && bytes[3] === 0x47) return true;
  if (bytes.length >= 1 && (bytes[0] === 0x3c || bytes[0] === 0x7b)) return false;
  return Boolean(mime?.startsWith('image/'));
}

function pngSize(bytes: number[]) {
  if (bytes.length < 24) return undefined;
  if (!(bytes[0] === 0x89 && bytes[1] === 0x50 && bytes[2] === 0x4e && bytes[3] === 0x47)) return undefined;
  const w = (bytes[16]! << 24) | (bytes[17]! << 16) | (bytes[18]! << 8) | bytes[19]!;
  const h = (bytes[20]! << 24) | (bytes[21]! << 16) | (bytes[22]! << 8) | bytes[23]!;
  if (w <= 0 || h <= 0 || w > 16_000 || h > 16_000) return undefined;
  return { width: w, height: h };
}

/** Sync KF gate — 0 cr. HTTPS is checked again on the API. */
export function inspectKfDataUri(raw?: string): KfInputCheck {
  const src = (raw ?? '').trim();
  const checks: KfInputCheck['checks'] = [];
  const reasons: string[] = [];
  if (!src) {
    return {
      ok: false,
      kind: 'none',
      reasons: ['Chưa có KF.'],
      checks: [{ id: 'exist', ok: false, label: 'Có KF' }],
    };
  }
  if (/^https:\/\//i.test(src)) {
    const privateHost = isPrivateRunwayImageUrl(src);
    checks.push({ id: 'exist', ok: !privateHost, label: privateHost ? 'URL máy local — Runway không tải được' : 'URL HTTPS' });
    if (privateHost) {
      return {
        ok: false,
        kind: 'https',
        reasons: ['KF là URL máy local — gửi data-URI, không gửi localhost.'],
        checks,
      };
    }
    checks.push({ id: 'https', ok: true, label: 'KIT đọc Content-Type khi gửi' });
    return { ok: true, kind: 'https', reasons: [], checks };
  }
  if (!src.toLowerCase().startsWith('data:image/')) {
    return {
      ok: false,
      kind: 'none',
      reasons: ['KF không phải ảnh (data:image hoặc https).'],
      checks: [{ id: 'exist', ok: false, label: 'MIME ảnh' }],
    };
  }
  const meta = dataUriMeta(src);
  const mime = meta?.mime || 'image/*';
  const bytes = meta?.bytes ?? 0;
  const mimeOk = mime === 'image/png' || mime === 'image/jpeg' || mime === 'image/jpg';
  checks.push({ id: 'exist', ok: true, label: 'Có data-URI' });
  checks.push({ id: 'mime', ok: mimeOk, label: mimeOk ? mime : `${mime} — cần png/jpeg` });
  if (!mimeOk) reasons.push('KF phải là PNG hoặc JPEG.');
  const sizeOk = bytes >= 800 && bytes <= 5_000_000;
  checks.push({
    id: 'size',
    ok: sizeOk,
    label: bytes ? `${Math.round(bytes / 1024)} KB` : '0 byte',
  });
  if (bytes < 800) reasons.push('KF trống / placeholder.');
  if (bytes > 5_000_000) reasons.push('KF > 5MB data-URI.');
  const magic = decodePrefix(meta?.payload || '', 24);
  const magicOk = looksLikeImageMagic(magic, mime);
  checks.push({ id: 'magic', ok: magicOk, label: magicOk ? 'Đọc được PNG/JPEG' : 'Không phải file ảnh (HTML/lỗi?)' });
  if (!magicOk) reasons.push('KF không đọc được — không gửi Runway.');
  const dim = pngSize(magic);
  if (dim) {
    const dimOk = dim.width >= 64 && dim.height >= 64 && !(dim.width === 1 && dim.height === 1);
    checks.push({ id: 'res', ok: dimOk, label: `${dim.width}×${dim.height}` });
    if (!dimOk) reasons.push('Resolution không hợp lệ.');
    return { ok: reasons.length === 0, kind: 'data', mime, bytes, width: dim.width, height: dim.height, reasons, checks };
  }
  return { ok: reasons.length === 0, kind: 'data', mime, bytes, reasons, checks };
}

export function kfCheckFromMeasure(base: KfInputCheck, width?: number, height?: number): KfInputCheck {
  if (!width || !height) return base;
  const dimOk = width >= 64 && height >= 64;
  const checks = [...base.checks.filter((c) => c.id !== 'res'), { id: 'res', ok: dimOk, label: `${width}×${height}` }];
  const reasons = dimOk ? base.reasons.filter((r) => !/resolution/i.test(r)) : [...base.reasons, 'Resolution không hợp lệ.'];
  return { ...base, width, height, checks, reasons, ok: reasons.length === 0 && checks.every((c) => c.ok || c.id === 'https') };
}

export function formatProductionLog(opts: {
  code: string;
  kfLabel?: string;
  kfApproved?: boolean;
  run?: RunwayPipeRun;
}) {
  const pipe = classifyVideoPipe(opts.run);
  const kf = opts.run?.sentKfCheck || opts.run?.kfCheck || inspectKfDataUri(opts.run?.keyframeDataUrl);
  const att = latestAttempt(opts.run);
  const attempts = opts.run?.runwayAttempts ?? [];
  const lines = [
    `SHOT: ${opts.code}`,
    `PIPE: ${pipe} — ${videoPipeLabel(pipe)}`,
    '',
    'KF SENT (sau normalize — đây mới là payload Runway):',
    `  ${opts.kfLabel || '—'} · ${opts.kfApproved ? 'APPROVED' : 'chưa duyệt / DRAFT'}`,
    `  ${kf.kind} ${kf.mime || ''} ${kf.bytes ? `${Math.round(kf.bytes / 1024)} KB` : ''} ${kf.width && kf.height ? `${kf.width}×${kf.height}` : ''}`.trim(),
    ...kf.checks.map((c) => `  ${c.ok ? '✓' : '✗'} ${c.label}`),
    kf.reasons.length ? `  BLOCK: ${kf.reasons.join(' ')}` : '  VALIDATE: OK hoặc sẽ kiểm khi gửi',
    '',
    'RUNWAY:',
    `  Job ID: ${opts.run?.turboTaskId || att?.taskId || 'NONE'}`,
    `  Status: ${opts.run?.turboStatus || '—'}`,
    `  Attempts: ${attempts.length ? `${att?.n || attempts.length}` : '0'}`,
    `  Output: ${opts.run?.previewUrl || att?.outputUrl || 'NONE'}`,
    `  Download: ${
      opts.run?.videoVerified === true
        ? `OK ${opts.run.videoMime || att?.videoMime || 'video'} ${opts.run.videoBytes ? `${Math.round(opts.run.videoBytes / 1024)} KB` : ''}`
        : opts.run?.videoVerified === false
          ? 'FAILED'
          : hasVerifiedTake(opts.run)
            ? 'READY (take cũ — đã có URL)'
            : 'NONE'
    }`,
    `  Error: ${opts.run?.turboError || att?.error || '—'}`,
    `  Failure: ${att?.failureCode || '—'}`,
    `  Billed this shot: ${attempts.reduce((n, a) => n + (a.billed || 0), 0) || '—'} cr (Runway trừ khi nhận job, không hoàn)`,
  ];
  if (attempts.length) {
    lines.push('', 'ATTEMPTS:');
    for (const a of attempts) {
      lines.push(
        `  #${a.n} ${a.at} job=${a.taskId || '—'} ${a.status || ''} out=${a.outputUrl ? 'URL' : 'NONE'} ${a.error || ''}`.trim(),
      );
    }
  }
  return lines.join('\n');
}

export function compareRunwayJobs(
  a: { code: string; run?: RunwayPipeRun },
  b: { code: string; run?: RunwayPipeRun },
) {
  const pa = classifyVideoPipe(a.run);
  const pb = classifyVideoPipe(b.run);
  const aa = latestAttempt(a.run);
  const ab = latestAttempt(b.run);
  const ka = a.run?.sentKfCheck || a.run?.kfCheck || inspectKfDataUri(a.run?.keyframeDataUrl);
  const kb = b.run?.sentKfCheck || b.run?.kfCheck || inspectKfDataUri(b.run?.keyframeDataUrl);
  const row = (key: string, left: string, right: string) => ({
    key,
    a: left,
    b: right,
    same: left === right,
  });
  return [
    row('PIPE', `${pa} ${videoPipeLabel(pa)}`, `${pb} ${videoPipeLabel(pb)}`),
    row('TAKE', a.run?.previewUrl ? 'URL' : 'NONE', b.run?.previewUrl ? 'URL' : 'NONE'),
    row('JOB', a.run?.turboTaskId || aa?.taskId || 'NONE', b.run?.turboTaskId || ab?.taskId || 'NONE'),
    row('STATUS', a.run?.turboStatus || '—', b.run?.turboStatus || '—'),
    row('OUTPUT', aa?.outputUrl || a.run?.previewUrl || 'NONE', ab?.outputUrl || b.run?.previewUrl || 'NONE'),
    row('KF MIME', ka.mime || ka.kind, kb.mime || kb.kind),
    row('KF KB', ka.bytes ? String(Math.round(ka.bytes / 1024)) : '—', kb.bytes ? String(Math.round(kb.bytes / 1024)) : '—'),
    row('KF RES', ka.width && ka.height ? `${ka.width}×${ka.height}` : '—', kb.width && kb.height ? `${kb.width}×${kb.height}` : '—'),
    row('ERROR', a.run?.turboError || '—', b.run?.turboError || '—'),
  ];
}

export function dataUriHash(raw?: string) {
  const s = (raw ?? '').trim();
  if (!s) return '';
  let h = 5381;
  const step = Math.max(1, Math.floor(s.length / 2048));
  for (let i = 0; i < s.length; i += step) h = ((h << 5) + h) ^ s.charCodeAt(i);
  h = ((h << 5) + h) ^ s.length;
  return `h${(h >>> 0).toString(16)}:${s.length}`;
}

export function lastSentKfHash(run?: RunwayPipeRun) {
  return latestAttempt(run)?.kf?.hash || '';
}

export function lastSourceKfHash(run?: RunwayPipeRun) {
  return latestAttempt(run)?.source?.hash || dataUriHash(run?.keyframeDataUrl);
}

export function sameKfAsInternalFail(run?: RunwayPipeRun, sourceHash?: string) {
  if (!run || !/INTERNAL/i.test(run.turboError || latestAttempt(run)?.failureCode || '')) return false;
  if (run.kfRetryOk) return false;
  const prev = latestAttempt(run)?.source?.hash || lastSentKfHash(run);
  const next = sourceHash || dataUriHash(run.keyframeDataUrl);
  if (!prev || !next) return true;
  return prev === next;
}

export function classifyRunwayFailure(opts: {
  sentOk?: boolean;
  httpStatus?: number;
  runwayStatus?: string;
  failureCode?: string;
  error?: string;
}): RunwayFailureClass {
  const blob = `${opts.failureCode || ''} ${opts.error || ''}`;
  if (/INPUT|INVALID_DATA|INVALID_DIM|UNSUPPORTED|CORRUPT/i.test(blob)) return 'INPUT';
  if (opts.sentOk === false || (opts.httpStatus && opts.httpStatus >= 400 && !/INTERNAL/i.test(blob))) return 'INPUT';
  if (/INTERNAL|BAD_OUTPUT/i.test(blob) || opts.runwayStatus === 'FAILED') return 'RUNWAY_GENERATION';
  return 'PENDING';
}

export function inspectRunwayPayload(raw?: string, ratio?: string): KfInputCheck {
  const base = inspectKfDataUri(raw);
  const src = (raw ?? '').trim();
  const jpeg = src.toLowerCase().startsWith('data:image/jpeg') || src.toLowerCase().startsWith('data:image/jpg');
  const checks = [...base.checks];
  const reasons = [...base.reasons];
  const jpegIdx = checks.findIndex((c) => c.id === 'mime');
  const jpegCheck = { id: 'jpeg', ok: jpeg, label: jpeg ? 'JPEG' : `${base.mime || '—'} — cần JPEG` };
  if (jpegIdx >= 0) checks[jpegIdx] = jpegCheck;
  else checks.push(jpegCheck);
  if (!jpeg) reasons.push('Runway chỉ nhận JPEG data-URI — không gửi PNG Gemini.');
  const w = base.width ?? 0;
  const h = base.height ?? 0;
  const want = (ratio || '').includes('9:16') || (ratio || '').includes('720:1280') ? '720×1280' : '1280×720';
  const exact =
    ((want === '1280×720' && w === 1280 && h === 720) || (want === '720×1280' && w === 720 && h === 1280));
  checks.push({ id: 'pixels', ok: exact, label: w && h ? `${w}×${h}${exact ? '' : ` ≠ ${want}`}` : `cần ${want}` });
  if (w && h && !exact) reasons.push(`Pixel ${w}×${h} — Runway cần đúng ${want}.`);
  if (!w || !h) reasons.push(`Chưa đo được pixel — cần đúng ${want}.`);
  return {
    ...base,
    mime: jpeg ? 'image/jpeg' : base.mime,
    checks,
    reasons,
    ok: reasons.length === 0,
  };
}

export function formatRunwayDiagnostic(opts: {
  code: string;
  kfApproved?: boolean;
  run?: RunwayPipeRun;
  ratio?: string;
}) {
  const pipe = classifyVideoPipe(opts.run);
  const att = latestAttempt(opts.run);
  const sent = opts.run?.sentKfCheck;
  const src = opts.run?.kfCheck;
  const fail = att?.failureCode || parseFailureCode(opts.run?.turboError) || '—';
  const klass = att?.classification || classifyRunwayFailure({
    sentOk: sent?.ok ?? att?.submitOk,
    httpStatus: att?.httpStatus,
    runwayStatus: opts.run?.turboStatus,
    failureCode: att?.failureCode,
    error: opts.run?.turboError,
  });
  const mark = (ok: boolean, label: string) => `${ok ? '✓' : '✗'} ${label}`;
  const lines = [
    'RUNWAY DIAGNOSTIC',
    '',
    'Input',
    mark(Boolean(opts.kfApproved), 'KF approved'),
    mark(sent?.mime === 'image/jpeg' || att?.kf?.mime === 'image/jpeg', 'JPEG'),
    mark(
      (sent?.width === 1280 && sent?.height === 720) ||
        (att?.kf?.width === 1280 && att?.kf?.height === 720) ||
        (sent?.width === 720 && sent?.height === 1280),
      sent?.width && sent?.height ? `${sent.width}×${sent.height}` : att?.kf?.width ? `${att.kf.width}×${att.kf.height}` : '1280×720',
    ),
    mark(Boolean(sent?.ok ?? att?.submitOk), 'File valid'),
    mark(Boolean(att?.kf?.hash || sent?.ok), 'Data URI valid'),
    src?.width && src.height && (src.width !== sent?.width || src.height !== sent?.height)
      ? `  SOURCE (Gemini/SK, không gửi): ${src.mime || ''} ${src.width}×${src.height} ${src.bytes ? `${Math.round(src.bytes / 1024)} KB` : ''}`
      : '',
    '',
    'Request',
    mark(Boolean(att?.model || /turbo|gen4/i.test(opts.run?.turboStatus || '')), `Model: ${att?.model || 'gen4_turbo'}`),
    mark(Boolean(att?.duration), `Duration: ${att?.duration || '—'}s`),
    mark(Boolean(att?.submitOk || opts.run?.turboTaskId), 'Job submitted'),
    '',
    'Runway',
    `Job ID: ${opts.run?.turboTaskId || att?.taskId || 'NONE'}`,
    `HTTP: ${att?.httpStatus ?? (att?.submitOk ? 200 : '—')}`,
    `Status: ${opts.run?.turboStatus || '—'}`,
    '',
    'Output',
    mark(Boolean(opts.run?.previewUrl || att?.outputUrl), 'URL'),
    mark(Boolean(opts.run?.videoVerified || /\.mp4/i.test(opts.run?.previewUrl || att?.outputUrl || '')), 'MP4'),
    '',
    'Error',
    fail,
    '',
    'Classification',
    klass === 'INPUT' ? 'INPUT_FAILURE' : klass === 'RUNWAY_GENERATION' ? 'RUNWAY_GENERATION_FAILURE' : klass,
    '',
    `PIPE: ${pipe} — ${videoPipeLabel(pipe)}`,
  ];
  return lines.filter((l) => l !== '').join('\n');
}

export function attemptToDiagRow(shotId: string, att: RunwayAttempt, extra?: Partial<RunwayDiagRow>): RunwayDiagRow {
  return {
    shotId,
    kfId: extra?.kfId,
    inputHash: att.source?.hash || '',
    normalizedImageHash: att.kf?.hash || '',
    mimeType: att.kf?.mime || '',
    width: att.kf?.width || 0,
    height: att.kf?.height || 0,
    fileSize: att.kf?.bytes || 0,
    model: att.model || 'gen4_turbo',
    duration: att.duration || 0,
    promptHash: att.promptHash || '',
    requestId: extra?.requestId,
    runwayJobId: att.taskId,
    httpStatus: att.httpStatus,
    runwayStatus: att.status,
    outputUrl: att.outputUrl,
    downloadOk: att.downloadOk,
    videoBytes: att.videoBytes,
    errorCode: att.failureCode,
    errorMessage: att.error,
    classification: att.classification || 'PENDING',
    at: att.at,
    diagnostic: att.diagnostic,
    ...extra,
  };
}

export function summarizeAbDiagnostic(rows: RunwayDiagRow[]) {
  const byShot = new Map<string, RunwayDiagRow[]>();
  for (const r of rows) {
    const list = byShot.get(r.shotId) ?? [];
    list.push(r);
    byShot.set(r.shotId, list);
  }
  const lines: string[] = [];
  let intermittent = false;
  let inputSpecific = false;
  for (const [shot, list] of byShot) {
    const ok = list.filter((r) => r.runwayStatus === 'SUCCEEDED' && r.outputUrl).length;
    const fail = list.filter((r) => r.runwayStatus === 'FAILED' || r.classification === 'RUNWAY_GENERATION').length;
    lines.push(`${shot}: ${ok} SUCCESS / ${fail} FAIL trên ${list.length} lần (hash ${list[0]?.normalizedImageHash || '—'})`);
    if (ok && fail) intermittent = true;
    if (!ok && fail === list.length && list.length >= 2) inputSpecific = true;
  }
  if (intermittent && !inputSpecific) {
    return { verdict: 'INTERMITTENT' as const, lines: [...lines, 'Cùng KF chuẩn hóa vừa SUCCESS vừa INTERNAL → lỗi Runway/API ngẫu nhiên.'] };
  }
  if (inputSpecific && byShot.size >= 2) {
    return { verdict: 'INPUT' as const, lines: [...lines, 'Một KF luôn FAIL, KF kia SUCCESS → lỗi input/nội dung KF, không phải retry.'] };
  }
  if (inputSpecific) {
    return { verdict: 'INPUT' as const, lines: [...lines, 'KF này FAIL đều sau normalize — đừng gửi lại cùng ảnh.'] };
  }
  return { verdict: 'PENDING' as const, lines };
}

export function parseFailureCode(raw?: string) {
  const t = (raw || '').trim();
  const m = t.match(/\b(INTERNAL(?:\.[A-Z0-9_]+)?|ASSET(?:\.[A-Z0-9_]+)?|SAFETY(?:\.[A-Z0-9_]+)?)\b/i);
  return m?.[1]?.toUpperCase();
}

export function explainPipeError(raw?: string | null) {
  const t = (raw ?? '').trim();
  if (!t) return 'Chưa có file take.';
  if (/moderation|safety|content.?policy/i.test(t)) {
    return 'Runway chặn nội dung. Job này FAILED — đã trừ cr. Sửa prompt/KF rồi gửi job mới.';
  }
  if (/ASSET\.INVALID/i.test(t)) {
    return 'ASSET.INVALID: KF Runway không đọc được. Không gửi lại cùng file. Nén JPEG / đổi KF.';
  }
  if (/INTERNAL\.BAD_OUTPUT|INTERNAL\b/i.test(t) || /unexpected error/i.test(t)) {
    return 'Runway INTERNAL: họ nhận job (trừ cr) rồi gãy khi render. Không gửi lại cùng KF. Normalize JPEG 1280×720 rồi A/B 3+3 — đừng spam Gửi lại.';
  }
  if (/SUCCEEDED.*output|không đọc được output/i.test(t)) {
    return 'Runway báo SUCCEEDED nhưng KIT không đọc được output URL. Mở Nhật ký — Hỏi lại · 0 cr, đừng gửi job mới.';
  }
  if (/download|không tải|take-proxy/i.test(t)) {
    return 'Có URL nhưng không tải được file. Thử đọc lại · 0 cr. Đừng Gửi lại.';
  }
  return t.length > 180 ? `${t.slice(0, 180)}…` : t;
}
