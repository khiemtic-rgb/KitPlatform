import {
  canManualRetry,
  capRunwayBatch,
  classifyVideoPipe,
  compareRunwayJobs,
  dataUriHash,
  explainPipeError,
  formatProductionLog,
  formatRunwayDiagnostic,
  hasVerifiedTake,
  inspectKfDataUri,
  inspectRunwayPayload,
  isKitPrecheckError,
  jpegSize,
  isPrivateRunwayImageUrl,
  isVideoPipeError,
  parseFailureCode,
  patchRunwayAttempt,
  sameFailedInput,
  sameKfAsInternalFail,
  startRunwayAttempt,
  summarizeAbDiagnostic,
} from './content-famixa-runway-pipe';

const fail: string[] = [];

if (hasVerifiedTake({ previewUrl: 'https://runway.example/a.mp4' }) !== true) {
  fail.push('legacy take URL is VIDEO READY');
}
if (hasVerifiedTake({ previewUrl: 'https://x/a.mp4', videoVerified: false })) {
  fail.push('explicit verify false is not READY');
}
if (classifyVideoPipe({ previewUrl: 'https://x/a.mp4' }) !== 'VIDEO_READY') {
  fail.push('previewUrl must be VIDEO_READY');
}
if (classifyVideoPipe({}) !== 'VIDEO_NOT_SENT') fail.push('empty is NOT_SENT');
if (classifyVideoPipe({ turboStatus: 'PENDING', turboTaskId: 't1' }) !== 'VIDEO_SUBMITTED') {
  fail.push('pending+id is SUBMITTED');
}
if (classifyVideoPipe({ turboStatus: 'RUNNING', turboTaskId: 't1' }) !== 'VIDEO_PROCESSING') {
  fail.push('running is PROCESSING');
}
if (classifyVideoPipe({ turboStatus: 'FAILED', turboTaskId: 't1', turboError: 'INTERNAL.BAD_OUTPUT' }) !== 'RUNWAY_FAILED') {
  fail.push('failed job is RUNWAY_FAILED');
}
if (classifyVideoPipe({ turboStatus: 'SUCCEEDED', videoPipe: 'DOWNLOAD_FAILED', turboError: 'Không tải được take' }) !== 'DOWNLOAD_FAILED') {
  fail.push('succeeded without file is DOWNLOAD_FAILED');
}
if (!isVideoPipeError('RUNWAY_FAILED') || isVideoPipeError('VIDEO_READY')) fail.push('error flags');

if (canManualRetry({ turboStatus: 'FAILED', turboTaskId: 't', turboError: 'unexpected' }).kind !== 'none') {
  fail.push('failed same input must block retry');
}
if (canManualRetry({ turboStatus: 'FAILED', turboTaskId: 't', turboError: 'INTERNAL.BAD_OUTPUT' }).kind !== 'none') {
  fail.push('INTERNAL must not offer Tạo lại cùng KF');
}
if (canManualRetry({ turboStatus: 'RUNNING', turboTaskId: 't' }).kind !== 'resume') {
  fail.push('in-flight must resume 0 cr');
}
if (canManualRetry({ previewUrl: 'https://x/a.mp4' }).ok) fail.push('READY must not retry');
if (canManualRetry({ videoPipe: 'INPUT_INVALID' }).ok) fail.push('invalid KF must not send');

const jpeg = `data:image/jpeg;base64,/9j/4AAQ${'A'.repeat(1200)}`;
if (sameFailedInput({ videoPipe: 'INPUT_INVALID', turboStatus: 'FAILED', turboError: 'Width — · Height —' })) {
  fail.push('KIT precheck without task must not open INTERNAL circuit');
}
if (canManualRetry({ videoPipe: 'INPUT_INVALID', turboStatus: 'BLOCKED', turboError: 'Width — · Height —' }).kind !== 'new') {
  fail.push('Width — precheck must allow Confirm after measure fix');
}
if (
  !sameFailedInput({
    videoPipe: 'INPUT_INVALID',
    turboStatus: 'FAILED',
    turboError: 'Width —',
    keyframeDataUrl: jpeg,
    failedKfHash: dataUriHash(jpeg),
    runwayAttempts: [{ n: 1, at: 'x', taskId: 't', source: { hash: dataUriHash(jpeg) }, failureCode: 'INTERNAL.BAD_OUTPUT' }],
  })
) {
  fail.push('same KF INTERNAL without new prompt stays locked');
}
if (
  sameFailedInput(
    {
      videoPipe: 'INPUT_INVALID',
      turboStatus: 'FAILED',
      turboError: 'Width —',
      keyframeDataUrl: jpeg,
      failedKfHash: dataUriHash(jpeg),
      runwayAttempts: [{ n: 1, at: 'x', taskId: 't', source: { hash: dataUriHash(jpeg) }, failureCode: 'INTERNAL.BAD_OUTPUT' }],
    },
    dataUriHash(jpeg),
    'hnewv1:12',
  )
) {
  fail.push('V1 prompt must unlock Width— deadlock on same KF');
}
if (
  canManualRetry({
    videoPipe: 'DOWNLOAD_FAILED',
    runwayAttempts: [{ n: 1, at: 'x', outputUrl: 'https://cdn.example/a.mp4' }],
  }).kind !== 'recover'
) {
  fail.push('download fail with URL must recover 0 cr');
}

if (inspectKfDataUri('').ok) fail.push('empty KF must block');
if (inspectKfDataUri('data:text/html;base64,PGh0bWw+').ok) fail.push('HTML must block');
if (!inspectKfDataUri('https://cdn.example/kf.jpg').ok) fail.push('https KF may send');
if (inspectKfDataUri('https://localhost:5290/api/content/assets/a.png').ok) {
  fail.push('localhost HTTPS must not send to Runway');
}
if (isPrivateRunwayImageUrl('https://cdn.example/v10.2/kf.jpg')) {
  fail.push('path v10.2 must not look private');
}
if (!isPrivateRunwayImageUrl('https://127.0.0.1:5290/a.png')) fail.push('127.0.0.1 is private');

const jpegCheck = inspectKfDataUri(jpeg);
if (!jpegCheck.ok || jpegCheck.mime !== 'image/jpeg') fail.push('jpeg data-URI must pass mime/magic');
if ((jpegCheck.bytes ?? 0) < 800) fail.push('jpeg byte estimate');

function jpegSofUri(width: number, height: number) {
  const bytes: number[] = [
    0xff, 0xd8, 0xff, 0xe0, 0x00, 0x10, 0x4a, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00, 0x00, 0x01, 0x00, 0x01, 0x00, 0x00,
    0xff, 0xc0, 0x00, 0x11, 0x08, (height >> 8) & 255, height & 255, (width >> 8) & 255, width & 255, 0x03, 0x01, 0x22,
    0x00, 0x02, 0x11, 0x01, 0x03, 0x11, 0x01, 0xff, 0xd9,
  ];
  while (bytes.length < 900) bytes.push(0);
  return `data:image/jpeg;base64,${Buffer.from(bytes).toString('base64')}`;
}
const sof = jpegSofUri(1280, 720);
const sofDim = jpegSize(
  [...Buffer.from(sof.slice(sof.indexOf(',') + 1), 'base64')].map((b) => b),
);
if (sofDim?.width !== 1280 || sofDim?.height !== 720) fail.push('jpeg SOF must read 1280×720');
const sofCheck = inspectKfDataUri(sof);
if (sofCheck.width !== 1280 || sofCheck.height !== 720) fail.push('inspectKfDataUri must parse JPEG SOF');
const sofPayload = inspectRunwayPayload(sof, '16:9');
if (!sofPayload.ok) fail.push(`valid 1280×720 JPEG must pass Runway gate: ${sofPayload.reasons.join(' ')}`);
if (!isKitPrecheckError('RUNWAY BLOCKED (0 cr): Width — · Height —')) fail.push('Width — is KIT precheck');
if (isKitPrecheckError('INTERNAL.BAD_OUTPUT.CODE01')) fail.push('INTERNAL is not KIT precheck');
if (!/KIT PRECHECK|0 cr/i.test(explainPipeError('Width — · Height —'))) fail.push('precheck copy must not look like 429');

const png1x1 =
  'data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==';
const pngCheck = inspectKfDataUri(png1x1);
if (pngCheck.ok) fail.push('1×1 PNG must block');
if (pngCheck.width !== 1 || pngCheck.height !== 1) fail.push('png size parse');

const attempts = startRunwayAttempt({}, { taskId: 'rw_1', submitOk: true, billed: 50 });
if (attempts[0]?.n !== 1) fail.push('first attempt is 1');
const patched = patchRunwayAttempt(attempts, 'rw_1', { status: 'FAILED', failureCode: 'INTERNAL.BAD_OUTPUT' });
if (patched[0]?.status !== 'FAILED' || patched[0]?.n !== 1) fail.push('patch keeps attempt number');

if (parseFailureCode('INTERNAL.BAD_OUTPUT: unexpected') !== 'INTERNAL.BAD_OUTPUT') {
  fail.push('parse INTERNAL.BAD_OUTPUT');
}
if (!/JPEG 1280|A\/B|không gửi lại/i.test(explainPipeError('INTERNAL.BAD_OUTPUT: unexpected error'))) {
  fail.push('error copy must stop same-KF retry');
}

const log = formatProductionLog({
  code: 'SH01-04',
  kfApproved: true,
  run: {
    turboTaskId: 'rw_fail',
    turboStatus: 'FAILED',
    turboError: 'INTERNAL.BAD_OUTPUT',
    runwayAttempts: patched,
  },
});
if (!log.includes('SHOT: SH01-04') || !log.includes('RUNWAY_FAILED') || !log.includes('rw_fail') || !/REFUND_PENDING|Estimated/i.test(log)) {
  fail.push('production log must name shot + job + pipe + refund pending');
}
const staleBlock = formatProductionLog({
  code: 'SH01-04',
  kfApproved: true,
  run: {
    turboTaskId: '3f9145a8-1481-4a90-9e41-4ae37f390bbc',
    turboStatus: 'FAILED',
    turboError: 'INTERNAL.BAD_OUTPUT.CODE01',
    sentKfCheck: {
      ok: true,
      kind: 'data',
      mime: 'image/jpeg',
      bytes: 74_000,
      width: 1280,
      height: 720,
      reasons: ['Chưa đo được pixel — cần đúng 1280×720.'],
      checks: [{ id: 'pixels', ok: true, label: '1280×720' }],
    },
    runwayAttempts: [
      { n: 1, at: 'x', taskId: '3f9145a8-1481-4a90-9e41-4ae37f390bbc', kf: { width: 1280, height: 720, mime: 'image/jpeg' } },
    ],
  },
});
if (/BLOCK:.*[Cc]hưa đo/.test(staleBlock)) fail.push('measured 1280×720 must not show pixel BLOCK');
if (!staleBlock.includes('3f9145a8-1481-4a90-9e41-4ae37f390bbc')) fail.push('log must keep task id for support');

const diff = compareRunwayJobs(
  { code: 'SH01-05', run: { previewUrl: 'https://x/ok.mp4', turboStatus: 'SUCCEEDED', turboTaskId: 'rw_ok' } },
  { code: 'SH01-04', run: { turboStatus: 'FAILED', turboTaskId: 'rw_fail', turboError: 'INTERNAL' } },
);
if (diff.find((r) => r.key === 'TAKE')?.same) fail.push('compare must show take vs none');
if (diff.find((r) => r.key === 'JOB')?.same) fail.push('compare must show different jobs');

const pngPayload = inspectRunwayPayload('data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==', '16:9');
if (pngPayload.ok) fail.push('PNG payload must not pass Runway gate');

const sameInternal = {
  turboStatus: 'FAILED',
  turboError: 'INTERNAL.BAD_OUTPUT',
  keyframeDataUrl: jpeg,
  runwayAttempts: [{ n: 1, at: 'x', source: { hash: dataUriHash(jpeg) }, failureCode: 'INTERNAL.BAD_OUTPUT' }],
};
if (!sameKfAsInternalFail(sameInternal)) fail.push('same source hash is INTERNAL lock');
if (canManualRetry(sameInternal).kind !== 'none') fail.push('same KF INTERNAL must block retry');
if (!sameKfAsInternalFail({ ...sameInternal, kfRetryOk: true })) fail.push('kfRetryOk alone must not unlock same hash');
const jpeg2 = `data:image/jpeg;base64,/9j/4AAQ${'B'.repeat(1200)}`;
if (sameFailedInput({ ...sameInternal, keyframeDataUrl: jpeg2, failedKfHash: dataUriHash(jpeg) })) {
  fail.push('new KF hash must unlock circuit');
}
if (canManualRetry({ ...sameInternal, keyframeDataUrl: jpeg2, failedKfHash: dataUriHash(jpeg) }).kind !== 'new') {
  fail.push('changed KF may Confirm 1 job');
}
if (capRunwayBatch([1, 2, 3, 4], false).length !== 1) fail.push('SAFE batch is 1');
if (capRunwayBatch([1, 2, 3, 4], true).length !== 3) fail.push('PRODUCTION batch is 3');
if (
  !sameFailedInput({
    turboStatus: 'FAILED',
    turboError: 'INTERNAL.BAD_OUTPUT',
    keyframeDataUrl: jpeg,
    runwayAttempts: [{ n: 1, at: 'x', kf: { hash: 'sent-jpeg-only' }, failureCode: 'INTERNAL.BAD_OUTPUT' }],
  })
) {
  fail.push('sent JPEG hash must not unlock INTERNAL without new stamped KF');
}

const diag = formatRunwayDiagnostic({
  code: 'SH01-02',
  kfApproved: true,
  run: {
    turboStatus: 'FAILED',
    turboError: 'INTERNAL.BAD_OUTPUT',
    sentKfCheck: { ok: true, kind: 'data', mime: 'image/jpeg', width: 1280, height: 720, reasons: [], checks: [] },
    runwayAttempts: [{ n: 1, at: 'x', submitOk: true, httpStatus: 200, status: 'FAILED', failureCode: 'INTERNAL.BAD_OUTPUT', kf: { mime: 'image/jpeg', width: 1280, height: 720 } }],
  },
});
if (!/RUNWAY DIAGNOSTIC|JPEG|1280×720|INTERNAL.BAD_OUTPUT|RUNWAY_GENERATION/i.test(diag)) {
  fail.push('diagnostic table must show sent JPEG + classification');
}

const ab = summarizeAbDiagnostic([
  { shotId: 'ok', inputHash: 'a', normalizedImageHash: 'n1', mimeType: 'image/jpeg', width: 1280, height: 720, fileSize: 1, model: 'gen4_turbo', duration: 5, promptHash: 'p', runwayStatus: 'SUCCEEDED', outputUrl: 'https://x/a.mp4', classification: 'PENDING', at: '1' },
  { shotId: 'ok', inputHash: 'a', normalizedImageHash: 'n1', mimeType: 'image/jpeg', width: 1280, height: 720, fileSize: 1, model: 'gen4_turbo', duration: 5, promptHash: 'p', runwayStatus: 'SUCCEEDED', outputUrl: 'https://x/b.mp4', classification: 'PENDING', at: '2' },
  { shotId: 'ok', inputHash: 'a', normalizedImageHash: 'n1', mimeType: 'image/jpeg', width: 1280, height: 720, fileSize: 1, model: 'gen4_turbo', duration: 5, promptHash: 'p', runwayStatus: 'SUCCEEDED', outputUrl: 'https://x/c.mp4', classification: 'PENDING', at: '3' },
  { shotId: 'bad', inputHash: 'b', normalizedImageHash: 'n2', mimeType: 'image/jpeg', width: 1280, height: 720, fileSize: 1, model: 'gen4_turbo', duration: 5, promptHash: 'q', runwayStatus: 'FAILED', classification: 'RUNWAY_GENERATION', at: '4' },
  { shotId: 'bad', inputHash: 'b', normalizedImageHash: 'n2', mimeType: 'image/jpeg', width: 1280, height: 720, fileSize: 1, model: 'gen4_turbo', duration: 5, promptHash: 'q', runwayStatus: 'FAILED', classification: 'RUNWAY_GENERATION', at: '5' },
  { shotId: 'bad', inputHash: 'b', normalizedImageHash: 'n2', mimeType: 'image/jpeg', width: 1280, height: 720, fileSize: 1, model: 'gen4_turbo', duration: 5, promptHash: 'q', runwayStatus: 'FAILED', classification: 'RUNWAY_GENERATION', at: '6' },
]);
if (ab.verdict !== 'INPUT') fail.push(`A/B ready-vs-fail must be INPUT, got ${ab.verdict}`);

if (fail.length) {
  console.error('RUNWAY PIPE FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('RUNWAY PIPE OK');
