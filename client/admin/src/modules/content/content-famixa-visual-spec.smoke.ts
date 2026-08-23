import {
  applyOperatorCheck,
  compileVisualPrompt,
  deriveVisualSpec,
  parseVisionQa,
  seedQaChecks,
  visualQaAllowsApprove,
} from './content-famixa-visual-spec';

const fail: string[] = [];
const spec = deriveVisualSpec({
  shotId: 'SH01-09',
  action: 'Liếc nhìn con số 9 đúng nửa giây.',
  location: 'Phòng ăn',
  lighting: 'dim warm indoor evening',
  names: ['Minh', 'Linh'],
  ids: ['CHAR-001', 'CHAR-003'],
  speakers: [],
});
if (spec.framing !== 'MCU') fail.push(`glance must be MCU, got ${spec.framing}`);
if (spec.primary?.name !== 'Minh' || spec.primary.face !== 'full') fail.push('Minh is primary full face');
if (!spec.secondary.some((p) => p.name === 'Linh' && p.face === 'partial')) fail.push('Linh is partial secondary');
if (!spec.required.some((r) => r.id === 'paper' && r.hard)) fail.push('test paper is hard required');
if (!spec.overlay || spec.overlay.text !== '9') fail.push('score overlay 9');
if (!/right third|Paper bottom/i.test(spec.composition)) fail.push('MCU composition lock');
if (/ôm|xin lỗi|hạnh phúc|không vui/i.test(spec.intent)) fail.push('intent must not invent hug/apology/disappointment');
const inherit = deriveVisualSpec({
  shotId: 'SH01-10',
  action: 'Minh nhìn mẹ.',
  names: ['Minh', 'Linh'],
  ids: ['CHAR-001', 'CHAR-003'],
  prevAction: 'Minh cầm bài bằng tay phải.',
});
if (!inherit.inheritFromPrev || !inherit.required.some((r) => r.id === 'inherit-prop')) {
  fail.push('next shot must inherit paper/hand from previous END');
}
const prompt = compileVisualPrompt(spec);
if (!/FACE SAFE|PRIMARY: Minh|FRAMING LOCK|COMPOSITION LOCK/i.test(prompt)) fail.push('compiled prompt missing locks');
if (/Both characters full body/i.test(prompt) === false && !spec.notRequired.length) {
  fail.push('MCU must list not-required full bodies');
}

const pending = seedQaChecks(spec);
if (visualQaAllowsApprove(pending)) fail.push('unchecked hard items must block approve');
let qa = pending;
for (const id of Object.keys(pending.checks)) qa = applyOperatorCheck(qa, id, true);
if (!visualQaAllowsApprove(qa)) fail.push('all hard checks allow approve');

const vision = parseVisionQa(
  { total: 45, axes: { face: 40, character: 90 }, hardFails: ['MISSING_FACE'], notes: 'Mother torso only' },
  spec,
);
if (vision.status !== 'REJECT' || visualQaAllowsApprove(vision)) fail.push('missing face is hard reject');

if (fail.length) {
  console.error('VISUAL SPEC FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('VISUAL SPEC PASS · MCU Minh face + paper');
