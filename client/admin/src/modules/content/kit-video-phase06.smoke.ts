import {
  canPollMotionTake,
  canSubmitMotionTake,
  compileMotionPrompt,
  ensureNotBlindRetry,
  evaluateVideoQa,
  famixaGoldenMotion,
  GOLDEN_I2V_SHOT,
  http200IsNotSuccess,
  isLiveMotionTake,
  KIT_VIDEO_MOTION,
  motionPreflight,
  RUNWAY_MODEL,
  videoReady,
} from './kit-video-motion';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

ok(KIT_VIDEO_MOTION === 'KIT-VIDEO-MOTION-V1', 'motion id');
const motion = famixaGoldenMotion();
ok(motion.shotCode === GOLDEN_I2V_SHOT && motion.durationSec === 5 && motion.model === RUNWAY_MODEL, 'golden SH01-01 5s turbo');
const compiled = compileMotionPrompt(motion);
ok(compiled.prompt.includes('Subtle natural movement') && compiled.prompt.includes('Gentle cinematic'), '01 compiler from MOTION_CONTRACT');
ok(!/Mẹ ơi|dialogue|character sheet|\{/.test(compiled.prompt), 'no dialogue/sheet/json');

const hash = 'aaa111';
const readyPre = {
  shotCode: GOLDEN_I2V_SHOT,
  i2vReady: true,
  imageType: 'PRODUCTION_STILL' as const,
  liveHash: hash,
  storedHash: hash,
  qaHash: hash,
  approvedHash: hash,
  jpegOk: true,
  still1280x720: true,
  compiled,
  keyConfigured: true,
};
ok(motionPreflight(readyPre).ok, '01 valid I2V package may call Runway');
ok(!motionPreflight({ ...readyPre, i2vReady: false }).ok, '02 I2V_READY=false not called');
ok(!motionPreflight({ ...readyPre, liveHash: 'bbb', storedHash: hash, qaHash: hash, approvedHash: hash }).ok, '03 hash mismatch not called');
ok(!motionPreflight({ ...readyPre, imageType: 'CHARACTER_SHEET' }).ok, '04 character sheet not called');
ok(!motionPreflight({ ...readyPre, imageType: 'COLLAGE' }).ok, '05 wrong image type not called');

ok(http200IsNotSuccess(200, 'task-1', 'PENDING'), '06 HTTP 200 + task = SUBMITTED not success');
ok(http200IsNotSuccess(200, 'task-1', 'PROCESSING'), '07 PROCESSING not success');
ok(!videoReady({ providerStatus: 'FAILED', outputUrl: undefined, fileOk: false, durationOk: false, hashMatch: false }).ready, '08 FAILED no artifact');
ok(!videoReady({ providerStatus: 'SUCCEEDED', outputUrl: 'https://x/v.mp4', fileOk: false, durationOk: false, hashMatch: false }).ready, '09 invalid download not VIDEO_READY');
ok(
  videoReady({ providerStatus: 'SUCCEEDED', outputUrl: 'https://x/v.mp4', fileOk: true, durationOk: true, hashMatch: true }).ready,
  '10 valid MP4 VIDEO_READY',
);

const clean = { characters: ['CHAR-001', 'CHAR-003'], actionOccurred: true };
ok(evaluateVideoQa({ characters: ['CHAR-001', 'CHAR-003'] }, clean).status === 'PASS', '15 valid motion PASS');
ok(evaluateVideoQa({}, { ...clean, characters: ['CHAR-001', 'CHAR-003', 'EXTRA'] }).p0Fail.some((x) => /Extra/.test(x)), '11 extra person P0');
ok(evaluateVideoQa({}, { ...clean, characters: ['CHAR-001'] }).p0Fail.some((x) => /Missing/.test(x)), '12 missing character P0');
ok(evaluateVideoQa({}, { ...clean, identityBreak: true, deformation: true }).status === 'FAIL', '13 identity deformation P0');
ok(evaluateVideoQa({}, { ...clean, actionOccurred: false, forbiddenAction: true }).status === 'FAIL', '14 wrong action FAIL');

try {
  compileMotionPrompt({ ...motion, action: 'Minh says: "Mẹ ơi, con được 9 điểm!"' });
  fail.push('dialogue must throw');
} catch {
  ok(true, 'dialogue rejected');
}
try {
  compileMotionPrompt({ ...motion, shotCode: 'SH01-02' });
  fail.push('SH01-02 must throw');
} catch (e) {
  ok(/GOLDEN_ONLY|PHASE_06/.test(String(e)), 'SH01-02 blocked');
}

try {
  ensureNotBlindRetry('same', 'same');
  fail.push('blind retry must throw');
} catch (e) {
  ok(String(e).includes('DO_NOT_BLIND_RETRY'), '16 same fingerprint blocked');
}
ensureNotBlindRetry('same', 'next', 'MOTION_PROMPT_REVISION');
ok(true, '17 changed motion + reason allowed');
ok(!motionPreflight({ ...readyPre, approvedHash: 'changed-after-approve' }).ok, '18 artifact modified after approve BLOCK');
ok(!isLiveMotionTake({ takeId: '00000000-0000-0000-0000-000000000000' }), 'preflight empty take is not live');
ok(canPollMotionTake({ takeId: '7505cd20-94dd-4e29-8bd5-aab2fb24fea8', status: 'SUBMITTED' }), 'SUBMITTED stays pollable');
ok(!canPollMotionTake({ takeId: '7505cd20-94dd-4e29-8bd5-aab2fb24fea8', status: 'APPROVED_TAKE', runwayTaskId: 't' }), 'approved take not polled');
ok(!canSubmitMotionTake({ takeId: '7505cd20-94dd-4e29-8bd5-aab2fb24fea8', status: 'SUBMITTED', runwayCalled: true }), 'no second Runway while live');

if (fail.length) {
  console.error('KIT VIDEO ENGINE PHASE 06 FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('KIT VIDEO ENGINE PHASE 06 PASS · tests 01–18 · golden lock + video QA + no blind retry');
