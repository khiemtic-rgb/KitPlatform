import {
  GOLDEN_SH01_04_PROMPT_A,
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
if (promptHasTextRisk(v1.text)) fail.push('V1 production text must not look like on-screen text');
if (promptViolatesRunwayI2vLaw(v1.text)) fail.push('V1 must pass I2V law');
const oldDump =
  'Minh and Linh stand in an indoor family room. Preserve the characters, wardrobe, room and composition from the input image. No text, logo or watermark. Mute take: do not animate spoken words. 5 seconds.';
if (!promptViolatesRunwayI2vLaw(oldDump)) fail.push('old V1 dump must fail I2V law');

const diag = compileRunwayPromptV1({ diagnostic: true });
if (diag.text !== GOLDEN_SH01_04_PROMPT_A) fail.push('diagnostic prompt A');

if (fail.length) {
  console.error('RUNWAY PROMPT V1 FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('RUNWAY PROMPT V1 OK');
