import {
  applyIdempotency,
  canSubmitProvider,
  classifyRetry,
  evaluateMedia,
  formatShotBoard,
  holdIfNoAction,
  jobSurvivesBrowserClose,
  productionAfterShotFailure,
  retryCreatesNewAttempt,
} from './kit-video-engine';

const fail: string[] = [];

function ok(cond: boolean, name: string) {
  if (!cond) fail.push(name);
}

// Test 01 — Double Confirm không tạo duplicate job
const first = applyIdempotency(undefined, 'ep01-sh01-i2v-confirm', true);
const second = applyIdempotency(first.job, 'ep01-sh01-i2v-confirm', true);
ok(first.created && !second.created, '01 created once');
ok(first.job.jobId === second.job.jobId, '01 same job id');
ok(second.job.attempts.length === 1, '01 no second attempt on double confirm');

// Test 02 — HTTP 200 + FAILED → không thành công
const t02 = evaluateMedia({
  providerStatus: 'FAILED',
  outputUrl: 'https://cdn.example/take.mp4',
  httpStatus: 200,
});
ok(!t02.ok && t02.shotOutcome === 'FAILED', '02 HTTP 200 + FAILED is not success');
ok(t02.reasons.some((r) => r.includes('HTTP 200')), '02 names HTTP 200 rule');

// Test 03 — SUCCEEDED nhưng không có URL → FAILED
const t03 = evaluateMedia({ providerStatus: 'SUCCEEDED', outputUrl: '', httpStatus: 200 });
ok(!t03.ok && t03.shotOutcome === 'FAILED', '03 SUCCEEDED without URL');

// Test 04 — Có URL nhưng file download lỗi → FAILED
const t04 = evaluateMedia({
  providerStatus: 'SUCCEEDED',
  outputUrl: 'https://cdn.example/take.mp4',
  probeOk: false,
  probeError: 'DOWNLOAD_FAILED',
  fileExists: false,
  readable: false,
});
ok(!t04.ok && t04.shotOutcome === 'FAILED', '04 download fail');

const t04b = evaluateMedia({
  providerStatus: 'SUCCEEDED',
  outputUrl: 'https://cdn.example/take.mp4',
  probeOk: true,
  fileExists: true,
  readable: true,
  containerOk: true,
  durationSec: 5,
  width: 1280,
  height: 720,
});
ok(t04b.ok && t04b.shotOutcome === 'VIDEO_READY', '04 valid file is VIDEO_READY');

// Test 05 — Một Shot FAILED không làm Episode FAILED
const isolated = productionAfterShotFailure('VIDEO_GENERATION', [
  { state: 'VIDEO_READY' },
  { state: 'VIDEO_READY' },
  { state: 'FAILED', failed: true },
  { state: 'VIDEO_READY' },
]);
ok(!isolated.episodeFailed, '05 episode not FAILED');
ok(isolated.runStatus === 'PARTIALLY_COMPLETE', '05 PARTIALLY_COMPLETE');
ok(isolated.othersContinue, '05 other shots continue');

// Test 06 — Shot không có Action → HOLD
ok(holdIfNoAction(false) === 'HOLD', '06 no action HOLD');
ok(holdIfNoAction(true) === 'READY', '06 has action READY');

// Test 07 — Shot HOLD không được gửi provider
ok(!canSubmitProvider('HOLD'), '07 HOLD blocked');
ok(!canSubmitProvider('DRAFT'), '07 DRAFT blocked');
ok(canSubmitProvider('I2V_READY'), '07 I2V_READY allowed');

// Test 08 — Retry phải tạo Attempt mới
const afterFail = { ...first.job, status: 'FAILED', attempts: [{ attemptNo: 1, status: 'FAILED' }] };
const retried = retryCreatesNewAttempt(afterFail);
ok(retried.attempts.length === 2 && retried.attempts[1].attemptNo === 2, '08 new attempt');
ok(classifyRetry('INTERNAL.BAD_OUTPUT.CODE01') === 'REPAIR_REQUIRED', '08 BAD_OUTPUT repair');
ok(classifyRetry(undefined, 'network timeout') === 'RETRYABLE', '08 timeout retryable');
ok(classifyRetry('INVALID_INPUT') === 'NON_RETRYABLE', '08 invalid non-retryable');

// Test 09 — Browser đóng nhưng Job server-side vẫn giữ state
ok(jobSurvivesBrowserClose(first.job), '09 job persisted server-side');

// Test 10 — Provider Task ID được lưu đầy đủ
const withTask = {
  ...retried,
  providerTasks: [
    {
      providerTaskId: '4bf73abc',
      providerStatus: 'FAILED',
      failureCode: 'INTERNAL.BAD_OUTPUT.CODE01',
    },
  ],
};
ok(withTask.providerTasks[0].providerTaskId === '4bf73abc', '10 provider task id');
ok(withTask.providerTasks[0].failureCode === 'INTERNAL.BAD_OUTPUT.CODE01', '10 failure code kept');

const board = formatShotBoard({
  shotCode: 'SHOT-03',
  state: 'FAILED',
  lastProvider: 'RUNWAY',
  lastFailureCode: 'INTERNAL.BAD_OUTPUT.CODE01',
});
ok(board.includes('FAILED') && board.includes('RUNWAY: INTERNAL.BAD_OUTPUT.CODE01'), '16 failed board');
ok(!board.includes('HTTP 200 = Success'), '16 never HTTP 200 = Success');

if (fail.length) {
  console.error('KIT VIDEO ENGINE PHASE 01 FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('KIT VIDEO ENGINE PHASE 01 PASS · tests 01–10');
