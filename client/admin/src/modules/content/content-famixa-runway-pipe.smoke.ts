import {
  canManualRetry,
  classifyVideoPipe,
  compareRunwayJobs,
  dataUriHash,
  explainPipeError,
  formatProductionLog,
  formatRunwayDiagnostic,
  hasVerifiedTake,
  inspectKfDataUri,
  inspectRunwayPayload,
  isPrivateRunwayImageUrl,
  isVideoPipeError,
  parseFailureCode,
  patchRunwayAttempt,
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

if (canManualRetry({ turboStatus: 'FAILED', turboTaskId: 't', turboError: 'unexpected' }).kind !== 'new') {
  fail.push('failed unexpected may start a new attempt');
}
if (canManualRetry({ turboStatus: 'FAILED', turboTaskId: 't', turboError: 'INTERNAL.BAD_OUTPUT' }).ok) {
  fail.push('INTERNAL must not offer blind Gửi lại');
}
if (canManualRetry({ turboStatus: 'RUNNING', turboTaskId: 't' }).kind !== 'resume') {
  fail.push('in-flight must resume 0 cr');
}
if (canManualRetry({ previewUrl: 'https://x/a.mp4' }).ok) fail.push('READY must not retry');
if (canManualRetry({ videoPipe: 'INPUT_INVALID' }).ok) fail.push('invalid KF must not send');
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

const jpeg = `data:image/jpeg;base64,/9j/4AAQ${'A'.repeat(1200)}`;
const jpegCheck = inspectKfDataUri(jpeg);
if (!jpegCheck.ok || jpegCheck.mime !== 'image/jpeg') fail.push('jpeg data-URI must pass mime/magic');
if ((jpegCheck.bytes ?? 0) < 800) fail.push('jpeg byte estimate');

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
if (!log.includes('SHOT: SH01-04') || !log.includes('RUNWAY_FAILED') || !log.includes('rw_fail')) {
  fail.push('production log must name shot + job + pipe');
}

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
if (canManualRetry(sameInternal).ok) fail.push('same KF INTERNAL must not retry');
if (sameKfAsInternalFail({ ...sameInternal, kfRetryOk: true })) fail.push('approved new KF unlocks one retry');

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
