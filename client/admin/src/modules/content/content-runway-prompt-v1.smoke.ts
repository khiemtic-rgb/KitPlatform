import {
  GOLDEN_SH01_04_PROMPT_A,
  cameraLineForRetry,
  compileRunwayPromptV1,
  promptHasTextRisk,
  promptViolatesRunwayI2vLaw,
  stripSpokenAndContract,
} from './content-runway-prompt-v1';

const fail: string[] = [];

const dirty = 'Minh: "Nhưng con đã tiến bộ hơn tháng trước rồi mà mẹ..." — hai má đỏ ửng';
if (stripSpokenAndContract(dirty).includes('tiến bộ')) fail.push('must strip spoken line');
if (promptHasTextRisk(dirty)) {
  /* expected */
} else fail.push('dialogue is text risk');

const v1 = compileRunwayPromptV1({ action: dirty });
if (v1.version !== 'RUNWAY_PROMPT_V1') fail.push('compiler version');
if (v1.chars > 900) fail.push('V1 must stay ≤900');
if (/tiến bộ|Nhưng con|says:|STORY:/i.test(v1.text)) fail.push('V1 must not send dialogue or contract');
if (/stand in|dining room|Vietnamese family|Preserve|No text|Mute take|do not animate|5 seconds/i.test(v1.text)) {
  fail.push('V1 must not re-describe the still or use negatives');
}
if (!/Subtle body movement|Blink and breathe|Camera remains steady/i.test(v1.text)) {
  fail.push('V1 must be motion + camera only');
}
const retry = compileRunwayPromptV1({ action: dirty, retry: 1 });
if (retry.text === v1.text) fail.push('retry must change camera so circuit can open');
const holdThenRetry = compileRunwayPromptV1({
  action: dirty,
  retry: 1,
  motion: { camera: 'Camera remains steady.' },
});
if (holdThenRetry.text === v1.text || /Camera remains steady/i.test(holdThenRetry.text)) {
  fail.push('retry must override coverage HOLD camera so SAME REQUEST can open');
}
const wrapHold = compileRunwayPromptV1({
  action: dirty,
  retry: 7,
  motion: { camera: 'Camera remains steady.' },
});
if (wrapHold.text === v1.text || /Camera remains steady/i.test(wrapHold.text)) {
  fail.push('retry 7 must not wrap back to HOLD camera');
}
if (cameraLineForRetry(0, 'Camera remains steady.') !== 'Camera remains steady.') {
  fail.push('retry 0 keeps coverage HOLD');
}
if (cameraLineForRetry(7, 'Camera remains steady.') === 'Camera remains steady.') {
  fail.push('retry 7 must skip the first HOLD line');
}
if (promptViolatesRunwayI2vLaw(retry.text)) fail.push('retry camera must still pass I2V law');
if (retry.chars > 900) fail.push('retry V1 must stay ≤900');
if (promptHasTextRisk(v1.text)) fail.push('V1 production text must not look like on-screen text');
if (promptViolatesRunwayI2vLaw(v1.text)) fail.push('V1 must pass I2V law');
const oldDump =
  'Minh and Linh stand in an indoor family room. Preserve the characters, wardrobe, room and composition from the input image. No text, logo or watermark. Mute take: do not animate spoken words. 5 seconds.';
if (!promptViolatesRunwayI2vLaw(oldDump)) fail.push('old V1 dump must fail I2V law');

const diag = compileRunwayPromptV1({ diagnostic: true });
if (diag.text !== GOLDEN_SH01_04_PROMPT_A) fail.push('diagnostic prompt A');

const sh01 = compileRunwayPromptV1({
  action: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
});
if (!/Standing at the living-room doorway|doorway/i.test(sh01.text)) fail.push('SH01 action/blocking');
if (!/sheet of paper|both hands/i.test(sh01.text)) fail.push('SH01 prop');
if (!/mother|not the camera/i.test(sh01.text)) fail.push('SH01 gaze');
if (!/uneasy|contained|Subtle body movement/i.test(sh01.text)) fail.push('SH01 acting');
if (!/Camera remains steady/i.test(sh01.text)) fail.push('SH01 camera');
if (!/short beat|Blink and breathe/i.test(sh01.text)) fail.push('SH01 timing');
if (/Visual Authority|Visual Universe|wardrobe|DNA|Master|appearance recreation|3D|stylized|polo|age 11/i.test(sh01.text)) {
  fail.push('SH01 must not dump Visual Authority');
}
if (promptViolatesRunwayI2vLaw(sh01.text)) fail.push('SH01 must pass I2V law');

if (fail.length) {
  console.error('RUNWAY PROMPT V1 FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('RUNWAY PROMPT V1 OK');
