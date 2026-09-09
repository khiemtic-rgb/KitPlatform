/** KIT Video Engine Phase 06 — controlled Runway I2V + Video QA. Golden SH01-01 only. */

export const KIT_VIDEO_MOTION = 'KIT-VIDEO-MOTION-V1';
export const GOLDEN_I2V_SHOT = 'SH01-01';
export const RUNWAY_MODEL = 'gen4_turbo';
export const RUNWAY_RATIO = '1280:720';

export type KitVideoMotionContract = {
  shotCode: string;
  action: string;
  camera: string;
  motion: string;
  durationSec: 5 | 10;
  model: typeof RUNWAY_MODEL;
};

export function famixaGoldenMotion(): KitVideoMotionContract {
  return {
    shotCode: GOLDEN_I2V_SHOT,
    action: 'Minh remains seated and reacts to his mother. Mother remains beside him.',
    camera: 'Gentle cinematic camera movement. Preserve the identities, clothing, room layout, props and composition of the source image.',
    motion: 'Subtle natural movement. Minh remains seated naturally and reacts to his mother with a restrained emotional expression. His mother remains beside him with subtle natural movement.',
    durationSec: 5,
    model: RUNWAY_MODEL,
  };
}

const DUMP =
  /visual contract|character sheet|reference sheet|pack_content|lipsync|elevenlabs|tts|subtitle|watermark|do not |don't |no text|SELECT |INSERT |\{|\}/i;
const DIALOGUE = /[“"][^”"]{2,}[”"]|Mẹ ơi|con được|says?:|dialogue|thoại/i;

export function compileMotionPrompt(contract: KitVideoMotionContract): {
  prompt: string;
  model: string;
  durationSec: number;
  ratio: string;
  compiler: string;
} {
  if (contract.shotCode !== GOLDEN_I2V_SHOT) throw new Error('GOLDEN_ONLY: Phase 06 chỉ test SH01-01.');
  if (contract.model !== RUNWAY_MODEL) throw new Error('PREFLIGHT: model phải là gen4_turbo.');
  if (contract.durationSec !== 5 && contract.durationSec !== 10) throw new Error('PREFLIGHT: duration phải 5 hoặc 10.');
  const prompt = [contract.motion, contract.camera].filter(Boolean).join(' ');
  if (DIALOGUE.test(prompt) || DIALOGUE.test(contract.action)) throw new Error('PREFLIGHT: Runway prompt không chứa thoại.');
  if (DUMP.test(prompt) || DUMP.test(contract.action)) {
    throw new Error('PREFLIGHT: Runway chỉ nhận motion + camera.');
  }
  return { prompt, model: RUNWAY_MODEL, durationSec: contract.durationSec, ratio: RUNWAY_RATIO, compiler: KIT_VIDEO_MOTION };
}

export function motionPreflight(input: {
  shotCode: string;
  i2vReady: boolean;
  i2vBlocked?: string[];
  imageType?: string;
  liveHash: string;
  storedHash: string;
  qaHash: string;
  approvedHash: string;
  jpegOk: boolean;
  still1280x720: boolean;
  compiled: ReturnType<typeof compileMotionPrompt>;
  keyConfigured: boolean;
}): { ok: boolean; blocked: string[] } {
  const blocked: string[] = [];
  if (input.shotCode !== GOLDEN_I2V_SHOT) blocked.push('GOLDEN_ONLY: Phase 06 chỉ test SH01-01.');
  if (!input.i2vReady) blocked.push('I2V_READY = FALSE');
  if (input.i2vBlocked?.length) blocked.push(...input.i2vBlocked);
  if (input.imageType !== 'PRODUCTION_STILL') blocked.push('Image type is not PRODUCTION_STILL');
  if (!input.liveHash || input.liveHash !== input.storedHash || input.liveHash !== input.qaHash || input.liveHash !== input.approvedHash) {
    blocked.push('SHA256 keyframe không khớp file / QA / approve');
  }
  if (!input.jpegOk) blocked.push('Approved keyframe không đọc được');
  if (!input.still1280x720) blocked.push('Keyframe chưa validate 1280×720');
  if (input.compiled.durationSec !== 5 && input.compiled.durationSec !== 10) blocked.push('duration phải 5 hoặc 10');
  if (input.compiled.model !== RUNWAY_MODEL) blocked.push('model phải gen4_turbo');
  if (!input.keyConfigured) blocked.push('Runway API key chưa cấu hình');
  return { ok: blocked.length === 0, blocked };
}

export function ensureNotBlindRetry(previousFingerprint: string, nextFingerprint: string, reason?: string) {
  if (previousFingerprint && previousFingerprint === nextFingerprint) {
    throw new Error('DO_NOT_BLIND_RETRY: cùng keyframe + cùng motion đã FAIL. Cần MOTION_PROMPT_REVISION hoặc KEYFRAME_REPAIR.');
  }
  if (reason && reason !== 'MOTION_PROMPT_REVISION' && reason !== 'KEYFRAME_REPAIR') {
    throw new Error('Retry phải có reason MOTION_PROMPT_REVISION hoặc KEYFRAME_REPAIR.');
  }
}

export function evaluateVideoQa(motion: { characters?: string[]; action?: string }, obs: {
  characters: string[];
  identityBreak?: boolean;
  deformation?: boolean;
  wrongLocation?: boolean;
  wrongWardrobe?: boolean;
  actionOccurred?: boolean;
  forbiddenAction?: boolean;
  corrupt?: boolean;
}): { status: string; p0Fail: string[]; p1: string[]; p2: string[] } {
  const p0: string[] = [];
  const expected = motion.characters?.length ? motion.characters : ['CHAR-001', 'CHAR-003'];
  if (obs.characters.length !== expected.length) p0.push(`Character count: Expected ${expected.length} Detected ${obs.characters.length}`);
  for (const code of expected.filter((c) => !obs.characters.includes(c))) p0.push(`Missing person: ${code}`);
  for (const extra of obs.characters.filter((c) => !expected.includes(c))) p0.push(`Extra person: ${extra}`);
  if (obs.identityBreak) p0.push('Identity break');
  if (obs.deformation) p0.push('Major deformation');
  if (obs.wrongLocation) p0.push('Wrong location');
  if (obs.wrongWardrobe) p0.push('Wrong wardrobe');
  if (obs.corrupt) p0.push('Corrupt video');
  if (obs.actionOccurred === false) p0.push('Requested action did not happen');
  if (obs.forbiddenAction) p0.push('Forbidden action');
  return { status: p0.length ? 'FAIL' : 'PASS', p0Fail: p0, p1: [], p2: [] };
}

export function http200IsNotSuccess(httpStatus: number, taskId?: string, providerStatus?: string) {
  return httpStatus === 200 && Boolean(taskId) && providerStatus !== 'SUCCEEDED';
}

export function videoReady(input: {
  providerStatus: string;
  outputUrl?: string;
  fileOk: boolean;
  durationOk: boolean;
  hashMatch: boolean;
}): { ready: boolean; blocked: string[] } {
  const blocked: string[] = [];
  if (input.providerStatus !== 'SUCCEEDED') blocked.push('Provider status ≠ SUCCEEDED');
  if (!input.outputUrl) blocked.push('Output URL missing');
  if (!input.fileOk) blocked.push('Video file missing');
  if (!input.durationOk) blocked.push('Duration unreadable');
  if (!input.hashMatch) blocked.push('Video fingerprint mismatch');
  return { ready: blocked.length === 0, blocked };
}

export function isLiveMotionTake(take?: { takeId?: string | null }) {
  return Boolean(take?.takeId && !take.takeId.startsWith('0000'));
}

const POLLABLE = new Set([
  'SUBMITTED',
  'PROCESSING',
  'RUNWAY_ACCEPTED',
  'SUCCEEDED',
  'DOWNLOADING',
  'ARTIFACT_VERIFY',
  'VIDEO_QA',
]);

const POLL_DONE = new Set([
  'APPROVED_TAKE',
  'REJECTED',
  'READY_FOR_DIRECTOR',
  'VIDEO_READY',
  'VIDEO_QA_FAIL',
  'DIAGNOSE',
  'INVALIDATED',
  'BLOCKED',
  'FAILED',
]);

export function canPollMotionTake(take?: {
  takeId?: string | null;
  status?: string;
  runwayTaskId?: string | null;
  runwayAccepted?: boolean;
}) {
  if (!isLiveMotionTake(take)) return false;
  const status = (take?.status || '').toUpperCase();
  if (POLL_DONE.has(status)) return false;
  if (POLLABLE.has(status)) return true;
  return Boolean(take?.runwayTaskId || take?.runwayAccepted);
}

export function canSubmitMotionTake(take?: { takeId?: string | null; status?: string; runwayCalled?: boolean }) {
  if (!isLiveMotionTake(take)) return true;
  const status = (take?.status || '').toUpperCase();
  return status === 'BLOCKED' || (status === 'READY' && !take?.runwayCalled);
}

export function formatMotionBoard(input: {
  shotCode: string;
  status: string;
  prompt: string;
  credit: string;
  videoReady: boolean;
  qa?: { status: string; p0Fail?: string[] } | null;
  diagnose?: string;
  takeId?: string;
  idle?: boolean;
}): string {
  if (input.idle) {
    return [
      `${input.shotCode} · Phase 06 Golden Runway`,
      'Chưa load take — bấm Check I2V. Không bấm RUNWAY TEST nếu đã có job.',
    ].join('\n');
  }
  return [
    `${input.shotCode} · Phase 06 Golden Runway`,
    input.takeId ? `Take ${input.takeId}` : '',
    `State ${input.status}`,
    `Prompt: ${input.prompt}`,
    `Credit ${input.credit}`,
    input.qa?.status ? `Video QA ${input.qa.status}` : 'Video QA pending',
    input.qa?.p0Fail?.length ? `P0: ${input.qa.p0Fail.join(' | ')}` : '',
    input.diagnose || '',
    input.videoReady ? 'VIDEO_READY' : 'NOT VIDEO_READY',
    'HTTP 200 ≠ VIDEO_READY',
  ]
    .filter((line, i, arr) => line.length > 0 || arr[i - 1]?.length)
    .join('\n');
}
