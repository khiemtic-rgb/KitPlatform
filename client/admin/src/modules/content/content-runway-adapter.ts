/** Famixa talks to this module only. HTTP lives in ContentRunwayClient (C#). */

import { inspectKfDataUri, inspectRunwayPayload, dataUriHash } from './content-famixa-runway-pipe';
import {
  RUNWAY_PROMPT_API_MAX,
  RUNWAY_PROMPT_COMPILER,
  RUNWAY_PROMPT_MAX,
  promptHasTextRisk,
  promptViolatesRunwayI2vLaw,
  stripSpokenAndContract,
} from './content-runway-prompt-v1';

export const RUNWAY_API_VERSION = '2024-11-06';
export const RUNWAY_MODEL = 'gen4_turbo';
export const RUNWAY_PROMPT_VERSION = RUNWAY_PROMPT_COMPILER;

export type RunwayFailureLayer =
  | 'KIT_PRECHECK'
  | 'ASSET'
  | 'GENERATION'
  | 'PREPROCESSING'
  | 'PROVIDER'
  | 'OUTPUT';

export type RunwayErrorClass =
  | 'KIT.PRECHECK'
  | 'KIT.DUPLICATE'
  | 'ASSET.INVALID'
  | 'INTERNAL.BAD_OUTPUT'
  | 'INPUT_PREPROCESSING.INTERNAL'
  | 'THIRD_PARTY.UNAVAILABLE'
  | 'UNKNOWN';

export type RunwayRetryPolicy = 'NO' | 'AFTER_DELAY' | 'AFTER_INPUT_CHANGE';

export type RunwayCreditState = 'NONE' | 'PENDING' | 'ACTUAL' | 'REFUND_PENDING';

export type RunwayExactRequest = {
  shotId?: string;
  kfId?: string;
  kfHash: string;
  promptHash: string;
  fingerprint: string;
  model: string;
  duration: 5 | 10;
  ratio: string;
  promptText: string;
  promptCompiler: string;
  promptImageType: string;
  promptImageWidth?: number;
  promptImageHeight?: number;
  promptImageBytes?: number;
  apiVersion: string;
  requestId?: string;
  taskId?: string;
};

export type RunwayInputCheck = { id: string; ok: boolean; label: string };

export function utf16Len(s: string) {
  return s.length;
}

export function requestFingerprint(opts: {
  kfHash: string;
  promptHash: string;
  model?: string;
  duration?: number;
}) {
  const model = opts.model || RUNWAY_MODEL;
  const dur = opts.duration === 10 ? 10 : 5;
  return `${opts.kfHash}|${opts.promptHash}|${model}|${dur}`;
}

export function classifyRunwayFailure(code?: string, error?: string) {
  const blob = `${code || ''} ${error || ''}`.toUpperCase();
  if (/ASSET\.INVALID|INVALID_ASSET|UNSUPPORTED|CORRUPT|INVALID_DIM/.test(blob)) {
    return {
      code: 'ASSET.INVALID' as const,
      layer: 'ASSET' as const,
      retry: 'NO' as const,
      action: 'Tạo asset JPEG sạch. Không retry cùng file.',
    };
  }
  if (/INPUT_PREPROCESSING/.test(blob)) {
    return {
      code: 'INPUT_PREPROCESSING.INTERNAL' as const,
      layer: 'PREPROCESSING' as const,
      retry: 'AFTER_DELAY' as const,
      action: 'Đợi rồi thử lại một lần — hoặc sửa KF.',
    };
  }
  if (/THIRD_PARTY|UNAVAILABLE|503|502/.test(blob)) {
    return {
      code: 'THIRD_PARTY.UNAVAILABLE' as const,
      layer: 'PROVIDER' as const,
      retry: 'AFTER_DELAY' as const,
      action: 'WAIT. Không spam. Hỏi lại task cũ nếu đã có ID.',
    };
  }
  if (/INTERNAL|BAD_OUTPUT/.test(blob)) {
    return {
      code: 'INTERNAL.BAD_OUTPUT' as const,
      layer: 'GENERATION' as const,
      retry: 'AFTER_INPUT_CHANGE' as const,
      action: 'Review KF / Prompt. Không gửi lại cùng fingerprint.',
    };
  }
  return {
    code: 'UNKNOWN' as const,
    layer: 'GENERATION' as const,
    retry: 'AFTER_INPUT_CHANGE' as const,
    action: 'Xem Request + Response. Đừng bấm lại cùng input.',
  };
}

export function creditStateOf(opts: { pipe?: string; previewUrl?: string; failed?: boolean; taskId?: string }): RunwayCreditState {
  if (opts.previewUrl?.trim() && opts.pipe === 'VIDEO_READY') return 'ACTUAL';
  if (opts.pipe === 'INPUT_INVALID') return 'NONE';
  if (opts.failed || opts.pipe === 'RUNWAY_FAILED') return 'REFUND_PENDING';
  if (opts.taskId?.trim()) return 'PENDING';
  return 'NONE';
}

export function testRunwayInput(opts: { image?: string; prompt?: string; ratio?: string; width?: number; height?: number }) {
  let img = inspectRunwayPayload(opts.image, opts.ratio);
  if (opts.width && opts.height) {
    img = {
      ...img,
      width: opts.width,
      height: opts.height,
      reasons: img.reasons.filter((r) => !/chưa đo|pixel|1280|720/i.test(r)),
      checks: [
        ...img.checks.filter((c) => c.id !== 'pixels' && c.id !== 'res' && c.id !== 'aspect'),
        { id: 'pixels', ok: true, label: `${opts.width}×${opts.height}` },
        { id: 'aspect', ok: true, label: `aspect ${(opts.width / opts.height).toFixed(3)}` },
      ],
      ok: img.reasons.filter((r) => !/chưa đo|pixel|1280|720/i.test(r)).length === 0,
    };
  }
  const src = inspectKfDataUri(opts.image);
  const prompt = stripSpokenAndContract(opts.prompt);
  const rawPrompt = (opts.prompt ?? '').trim();
  const checks: RunwayInputCheck[] = [
    { id: 'exist', ok: Boolean((opts.image ?? '').trim()), label: 'File exists' },
    { id: 'readable', ok: src.checks.some((c) => c.id === 'magic' && c.ok) || img.ok, label: 'File readable' },
    {
      id: 'mime',
      ok: /image\/jpeg|image\/jpg|image\/png/.test(img.mime || src.mime || ''),
      label: `MIME ${img.mime || src.mime || '—'}`,
    },
    { id: 'width', ok: Boolean(img.width || src.width || opts.width), label: `Width ${img.width || src.width || opts.width || '—'}` },
    { id: 'height', ok: Boolean(img.height || src.height || opts.height), label: `Height ${img.height || src.height || opts.height || '—'}` },
    {
      id: 'aspect',
      ok: img.checks.some((c) => c.id === 'aspect' && c.ok) || Boolean((img.width || opts.width) && (img.height || opts.height)),
      label: 'Aspect ratio',
    },
    { id: 'size', ok: (img.bytes ?? src.bytes ?? 0) > 800, label: `Size ${Math.round((img.bytes ?? src.bytes ?? 0) / 1024)} KB` },
    { id: 'uri', ok: (opts.image ?? '').startsWith('data:image/'), label: 'Data URI valid' },
    { id: 'prompt', ok: rawPrompt.length > 0, label: 'Prompt not empty' },
    {
      id: 'promptLen',
      ok: utf16Len(rawPrompt) <= RUNWAY_PROMPT_API_MAX && utf16Len(rawPrompt) <= RUNWAY_PROMPT_MAX,
      label: `Prompt ${utf16Len(rawPrompt)} UTF-16 (max ${RUNWAY_PROMPT_MAX})`,
    },
    { id: 'noText', ok: !promptHasTextRisk(rawPrompt), label: 'No on-screen text / dialogue' },
    { id: 'noInject', ok: !/write a prompt|visual contract|FAIL CONDITIONS/i.test(rawPrompt), label: 'No UI/contract dump' },
    {
      id: 'i2vLaw',
      ok: !promptViolatesRunwayI2vLaw(rawPrompt),
      label: 'I2V law: motion + camera only',
    },
  ];
  const jpegOk = (img.mime || '').includes('jpeg') || (opts.image ?? '').toLowerCase().startsWith('data:image/jpeg');
  checks.push({ id: 'jpegProd', ok: jpegOk, label: jpegOk ? 'Production JPEG' : 'Need JPEG production asset' });
  const ok = checks.every((c) => c.ok);
  return {
    ok,
    layer: (ok ? 'READY' : 'KIT_PRECHECK') as 'READY' | 'KIT_PRECHECK',
    checks,
    prompt,
    reasons: checks.filter((c) => !c.ok).map((c) => c.label),
  };
}

export function formatRunwayInputTest(result: ReturnType<typeof testRunwayInput>) {
  return [
    result.ok ? 'RUNWAY INPUT TEST · PASS · 0 cr' : 'RUNWAY BLOCKED · KIT PRECHECK · 0 cr',
    ...result.checks.map((c) => `${c.ok ? '✓' : '✗'} ${c.label}`),
    result.ok ? 'Có thể Confirm 1 job.' : `Không gọi Runway. ${result.reasons.join(' · ')}`,
  ].join('\n');
}

export function buildRunwayJob(opts: {
  shotId?: string;
  kfId?: string;
  image?: string;
  prompt?: string;
  duration?: number;
  ratio?: string;
  model?: string;
  width?: number;
  height?: number;
}) {
  const duration = (opts.duration === 10 ? 10 : 5) as 5 | 10;
  const ratio = (opts.ratio || '').includes('9:16') || (opts.ratio || '').includes('720:1280') ? '720:1280' : '1280:720';
  const model = opts.model || RUNWAY_MODEL;
  const pre = testRunwayInput({
    image: opts.image,
    prompt: opts.prompt,
    ratio,
    width: opts.width,
    height: opts.height,
  });
  const kfHash = dataUriHash(opts.image);
  const promptText = (opts.prompt ?? '').trim();
  const promptHash = dataUriHash(promptText);
  const fingerprint = requestFingerprint({ kfHash, promptHash, model, duration });
  const img = inspectRunwayPayload(opts.image, ratio);
  const exact: RunwayExactRequest = {
    shotId: opts.shotId,
    kfId: opts.kfId,
    kfHash,
    promptHash,
    fingerprint,
    model,
    duration,
    ratio,
    promptText,
    promptCompiler: RUNWAY_PROMPT_VERSION,
    promptImageType: img.mime || 'image/jpeg',
    promptImageWidth: opts.width || img.width,
    promptImageHeight: opts.height || img.height,
    promptImageBytes: img.bytes,
    apiVersion: RUNWAY_API_VERSION,
  };
  if (!pre.ok) {
    return {
      ok: false as const,
      blocked: { layer: 'KIT_PRECHECK' as const, code: 'KIT.PRECHECK' as const, reasons: pre.reasons },
      exact,
      fingerprint,
      pre,
    };
  }
  return {
    ok: true as const,
    payload: {
      model,
      promptImage: opts.image,
      promptText,
      ratio,
      duration,
    },
    exact,
    fingerprint,
    pre,
  };
}

export function formatExactRequest(req: RunwayExactRequest) {
  return JSON.stringify(
    {
      model: req.model,
      promptImage: `[data-uri omitted · ${req.promptImageType} ${req.promptImageWidth || '?'}×${req.promptImageHeight || '?'} ${req.promptImageBytes ? `${Math.round(req.promptImageBytes / 1024)}KB` : ''}]`,
      promptText: req.promptText,
      ratio: req.ratio,
      duration: req.duration,
      apiVersion: req.apiVersion,
      compiler: req.promptCompiler,
      kfHash: req.kfHash,
      promptHash: req.promptHash,
      fingerprint: req.fingerprint,
      taskId: req.taskId || null,
    },
    null,
    2,
  );
}

export function sameRequestBlocked(
  run: {
    runwayAttempts?: { fingerprint?: string; status?: string; failureCode?: string; taskId?: string }[];
    failedFingerprint?: string;
  },
  fingerprint: string,
) {
  const failedAtt = [...(run.runwayAttempts ?? [])].reverse().find(
    (a) =>
      Boolean(a.taskId?.trim()) &&
      (/INTERNAL|BAD_OUTPUT/i.test(`${a.failureCode || ''} ${a.status || ''}`) || /FAIL/i.test(a.status || '')),
  );
  const stamp = failedAtt?.fingerprint;
  if (stamp && stamp === fingerprint) return true;
  return (run.runwayAttempts ?? []).some(
    (a) =>
      Boolean(a.taskId?.trim()) &&
      a.fingerprint === fingerprint &&
      /FAIL|INTERNAL/i.test(`${a.status || ''} ${a.failureCode || ''}`),
  );
}

export function lifecycleReady(opts: { status?: string; outputUrl?: string; downloadedOk?: boolean }) {
  const st = (opts.status || '').toUpperCase();
  if (st !== 'SUCCEEDED') return false;
  if (!opts.outputUrl?.trim()) return false;
  return opts.downloadedOk === true;
}

export function formatFailureUi(opts: { code?: string; error?: string; credit?: RunwayCreditState }) {
  const klass = classifyRunwayFailure(opts.code, opts.error);
  return {
    title: klass.layer === 'KIT_PRECHECK' ? 'RUNWAY BLOCKED' : 'RUNWAY FAILED',
    layer: klass.layer,
    code: opts.code || klass.code,
    action: klass.action,
    credit: opts.credit || (klass.layer === 'KIT_PRECHECK' ? 'NONE' : 'REFUND_PENDING'),
    retry: klass.retry,
  };
}
