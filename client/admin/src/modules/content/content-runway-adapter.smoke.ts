import {
  buildRunwayJob,
  classifyRunwayFailure,
  creditStateOf,
  formatExactRequest,
  lifecycleReady,
  requestFingerprint,
  sameRequestBlocked,
  testRunwayInput,
} from './content-runway-adapter';
import { compileRunwayPromptV1, GOLDEN_SH01_04_PROMPT_A } from './content-runway-prompt-v1';

const fail: string[] = [];
const jpeg = `data:image/jpeg;base64,/9j/4AAQ${'A'.repeat(1200)}`;

const empty = testRunwayInput({ image: '', prompt: '' });
if (empty.ok) fail.push('Test 5: empty must block');

const dirtyPrompt = compileRunwayPromptV1({ action: 'Minh: "con đã tiến bộ"' }).text;
if (/tiến bộ/.test(dirtyPrompt)) fail.push('production compiler must drop dialogue');
const dumpBlocked = testRunwayInput({
  image: jpeg,
  prompt:
    'Minh and Linh stand in an indoor family room. No text, logo or watermark. Preserve the characters, wardrobe, room and composition from the input image.',
  width: 1280,
  height: 720,
});
if (dumpBlocked.ok) fail.push('old still-dump prompt must KIT-block (0 cr)');

const job = buildRunwayJob({
  shotId: 'SH01-04',
  image: jpeg,
  prompt: GOLDEN_SH01_04_PROMPT_A,
  duration: 5,
  ratio: '16:9',
});
if (job.exact.apiVersion !== '2024-11-06') fail.push('API version');
if (job.exact.model !== 'gen4_turbo') fail.push('model');
if (job.exact.promptCompiler !== 'RUNWAY_PROMPT_V1') fail.push('compiler stamp');
if (formatExactRequest(job.exact).includes('data:image')) fail.push('log must redact data-URI');

const fp = requestFingerprint({
  kfHash: job.exact.kfHash,
  promptHash: job.exact.promptHash,
  model: 'gen4_turbo',
  duration: 5,
});
if (fp !== job.fingerprint) fail.push('fingerprint stable');
if (
  !sameRequestBlocked(
    { failedFingerprint: fp, runwayAttempts: [{ fingerprint: fp, status: 'FAILED', taskId: 't', failureCode: 'INTERNAL' }] },
    fp,
  )
) {
  fail.push('Test 2: same request BLOCK');
}
if (sameRequestBlocked({ failedFingerprint: fp }, fp)) fail.push('stamp without generation job must not block');
if (sameRequestBlocked({ failedFingerprint: fp }, `${fp}|other`)) fail.push('Test 3/4: changed input must allow');

const intern = classifyRunwayFailure('INTERNAL.BAD_OUTPUT.CODE01', 'unexpected');
if (intern.retry !== 'AFTER_INPUT_CHANGE' || intern.layer !== 'GENERATION') fail.push('INTERNAL maps to review input');
if (classifyRunwayFailure('ASSET.INVALID').retry !== 'NO') fail.push('ASSET.INVALID no retry');
if (classifyRunwayFailure('INPUT_PREPROCESSING.INTERNAL').retry !== 'AFTER_DELAY') fail.push('preprocess delay');
if (classifyRunwayFailure('THIRD_PARTY.UNAVAILABLE').retry !== 'AFTER_DELAY') fail.push('provider wait');

if (lifecycleReady({ status: 'SUCCEEDED', outputUrl: '', downloadedOk: true })) fail.push('Test 7: no URL not READY');
if (lifecycleReady({ status: 'SUCCEEDED', outputUrl: 'https://x/a.mp4', downloadedOk: false })) {
  fail.push('Test 7: bad download not READY');
}
if (!lifecycleReady({ status: 'SUCCEEDED', outputUrl: 'https://x/a.mp4', downloadedOk: true })) {
  fail.push('Test 8: valid file is READY');
}
if (lifecycleReady({ status: 'FAILED', outputUrl: 'https://x/a.mp4', downloadedOk: true })) {
  fail.push('Test 6: FAILED is not READY');
}

if (creditStateOf({ failed: true, taskId: 't' }) !== 'REFUND_PENDING') fail.push('Test 9: fail is refund pending');
if (creditStateOf({ previewUrl: 'https://x/a.mp4', pipe: 'VIDEO_READY' }) !== 'ACTUAL') fail.push('success actual');
if (creditStateOf({ taskId: 't', pipe: 'VIDEO_SUBMITTED' }) !== 'PENDING') fail.push('created is pending');
if (creditStateOf({ failed: true, pipe: 'INPUT_INVALID' }) !== 'NONE') fail.push('KIT precheck is 0 cr not refund');

function jpegSof(width: number, height: number) {
  const bytes: number[] = [
    0xff, 0xd8, 0xff, 0xe0, 0x00, 0x10, 0x4a, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
    0xff, 0xc0, 0x00, 0x11, 0x08, (height >> 8) & 255, height & 255, (width >> 8) & 255, width & 255, 0x03, 0x01, 0x22,
    0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01, 0xff, 0xd9,
  ];
  while (bytes.length < 900) bytes.push(0);
  return `data:image/jpeg;base64,${Buffer.from(bytes).toString('base64')}`;
}
const sofJob = buildRunwayJob({
  shotId: 'SH01-04',
  image: jpegSof(1280, 720),
  prompt: GOLDEN_SH01_04_PROMPT_A,
  duration: 5,
  ratio: '16:9',
});
if (!sofJob.ok) fail.push(`SOF 1280×720 JPEG must pass adapter: ${sofJob.ok ? '' : sofJob.blocked.reasons.join(' ')}`);
const measured = buildRunwayJob({
  image: jpeg,
  prompt: GOLDEN_SH01_04_PROMPT_A,
  duration: 5,
  ratio: '16:9',
  width: 1280,
  height: 720,
});
if (!measured.ok) fail.push('canvas measure must unblock JPEG when SOF is missing');

const a = buildRunwayJob({ shotId: '1', image: jpeg, prompt: 'a', duration: 5 });
const b = buildRunwayJob({ shotId: '2', image: jpeg, prompt: 'b', duration: 5 });
const c = buildRunwayJob({ shotId: '3', image: jpeg, prompt: 'c', duration: 5 });
if (new Set([a.fingerprint, b.fingerprint, c.fingerprint]).size !== 3) fail.push('Test 10: independent logs');

if (fail.length) {
  console.error('RUNWAY ADAPTER FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('RUNWAY ADAPTER OK · acceptance 2–10');
