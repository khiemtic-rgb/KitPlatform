import {
  applyOperatorCheck,
  canonIdsForSpec,
  compileGeminiStillBrief,
  compileVisualPrompt,
  coverageRepeatWarning,
  deriveVisualSpec,
  framingFromAction,
  parseVisionQa,
  sanitizeHardFails,
  peopleCountForSpec,
  qaLane,
  seedQaChecks,
  shouldAttachPrevKf,
  visualQaAllowsApprove,
} from './content-famixa-visual-spec';

const fail: string[] = [];
if (framingFromAction('Liếc nhìn con số 9 đúng nửa giây.') !== 'INSERT') fail.push('glance action → INSERT');
if (framingFromAction('Minh bước vào nhà.') !== 'WIDE') fail.push('enter → WIDE');
if (framingFromAction('GẤU NƯỚC LẠNH') !== 'CU') fail.push('cold-bucket heading → CU');
if (framingFromAction('Minh đưa bài kiểm tra cho mẹ.') !== 'MEDIUM') fail.push('hand object → MEDIUM');

const spec = deriveVisualSpec({
  shotId: 'SH01-09',
  action: 'Liếc nhìn con số 9 đúng nửa giây.',
  location: 'Phòng ăn',
  lighting: 'dim warm indoor evening',
  names: ['Minh', 'Linh'],
  ids: ['CHAR-001', 'CHAR-003'],
  speakers: [],
});
if (spec.framing !== 'INSERT') fail.push(`glance must be INSERT, got ${spec.framing}`);
if (spec.subjectKind !== 'prop' || spec.subjectName !== 'Test paper') fail.push('INSERT primary is the paper');
if (spec.primary) fail.push('INSERT must not make Minh the primary body');
if (!spec.secondary.some((p) => p.name === 'Minh' && p.face === 'partial')) fail.push('Minh is eyes/hands only');
if (!spec.required.some((r) => r.id === 'score' && r.hard)) fail.push('score 9 is hard required');
if (!spec.overlay || spec.overlay.text !== '9') fail.push('score overlay 9');
if (!/notice the 9|phải thấy số 9|must notice the 9/i.test(spec.purpose)) fail.push('purpose is the 9');
if (/ôm|xin lỗi|hạnh phúc|không vui/i.test(`${spec.intent} ${spec.purpose} ${spec.endState}`)) {
  fail.push('intent must not invent hug/apology/disappointment');
}
const prompt = compileVisualPrompt(spec);
if (!/INSERT|Test paper|FORBIDDEN/i.test(prompt)) fail.push('compiled prompt missing INSERT lock');
if (/PRIMARY: Minh/i.test(prompt)) fail.push('INSERT prompt must not FACE LOCK Minh as primary');

const mid = deriveVisualSpec({
  shotId: 'SH01-04',
  action: 'Minh đưa bài cho mẹ.',
  names: ['Minh', 'Linh'],
  prevFraming: 'MEDIUM',
});
if (mid.framing !== 'MEDIUM') fail.push('hand-object stays MEDIUM — do not auto-zoom to CU');
if (shouldAttachPrevKf('MEDIUM', 'MEDIUM') !== true) fail.push('same MEDIUM may attach prev');
if (shouldAttachPrevKf('MEDIUM', 'CU') !== false) fail.push('CU must not zoom the previous still');
if (shouldAttachPrevKf('MCU', 'INSERT') !== false) fail.push('INSERT must not zoom the previous still');

if (!coverageRepeatWarning(['MEDIUM', 'MEDIUM', 'MEDIUM', 'MEDIUM', 'MCU'])) {
  fail.push('repeat MEDIUM must warn');
}

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

const pending = seedQaChecks(spec);
if (visualQaAllowsApprove(pending)) fail.push('unchecked hard items must block approve');
let ticked = pending;
for (const id of Object.keys(pending.checks)) ticked = applyOperatorCheck(ticked, id, true);
if (visualQaAllowsApprove(ticked)) fail.push('operator ticks alone must not approve — still PENDING');

const scored = parseVisionQa({ total: 76, axes: { face: 88, character: 90 }, hardFails: [] }, spec);
if (scored.status !== 'PASS' || !visualQaAllowsApprove(scored) || qaLane(scored) === 'BLOCK') {
  fail.push('QA 76 without hard fail is PASS — score is soft');
}
const pass = parseVisionQa({ total: 88, axes: { face: 96, character: 96, prop: 95 }, hardFails: [] }, spec);
if (pass.status !== 'PASS' || !visualQaAllowsApprove(pass)) fail.push('QA 88 + hard OK is PASS');
const vision = parseVisionQa(
  { total: 99, axes: { face: 40, character: 90 }, hardFails: ['MISSING_PROP'], notes: 'No paper' },
  spec,
);
if (vision.status !== 'REJECT' || visualQaAllowsApprove(vision)) fail.push('hard fail blocks even at 99');
const low = parseVisionQa({ total: 68, axes: { face: 70 }, hardFails: [] }, spec);
if (low.status !== 'PASS' || !visualQaAllowsApprove(low) || qaLane(low) === 'BLOCK') {
  fail.push('QA 68 without hard fail is PASS');
}

const ots = deriveVisualSpec({
  shotId: 'SH01-03',
  action: 'Minh nhìn mẹ, chờ phản ứng.',
  names: ['Minh', 'Linh'],
  speakers: ['Minh'],
  prevFraming: 'MCU',
});
const otsQa = parseVisionQa({ total: 45, axes: { face: 45, character: 80 }, hardFails: ['WRONG_COUNT', 'MISSING_FACE'] }, ots);
if (otsQa.hardFails.includes('WRONG_COUNT')) fail.push('OTS must not HARD FAIL WRONG_COUNT for a shoulder secondary');
if (otsQa.hardFails.includes('MISSING_FACE')) fail.push('OTS secondary shoulder is not MISSING_FACE');
if (otsQa.status !== 'PASS') fail.push('OTS QA 45 without remaining hard fail is PASS');
if (!visualQaAllowsApprove(otsQa)) fail.push('OTS 45 with sanitized hard fails may approve');

const khoe = deriveVisualSpec({
  shotId: 'SH01-02',
  action: 'Minh chạy vào nhà, háo hức khoe bài.',
  spoken: 'Mẹ ơi chín điểm!',
  names: ['Minh', 'Linh'],
  ids: ['CHAR-001', 'CHAR-003'],
  speakers: ['Minh'],
});
const wait = deriveVisualSpec({
  shotId: 'SH01-03',
  action: 'Minh nhìn mẹ, chờ phản ứng.',
  names: ['Minh', 'Linh'],
  ids: ['CHAR-001', 'CHAR-003'],
  prevFraming: khoe.framing,
});
const hold = deriveVisualSpec({
  shotId: 'SH01-10',
  action: 'Không đổi biểu cảm',
  names: ['Minh', 'Linh'],
  ids: ['CHAR-001', 'CHAR-003'],
});
const spokenMcu = deriveVisualSpec({
  shotId: 'SH01-05',
  action: '',
  spoken: 'Không... đề khó mà mẹ.',
  names: ['Minh', 'Linh'],
  speakers: ['Minh'],
});
const directorBlob = [khoe, wait, hold, spokenMcu, spec]
  .map((s) => `${s.purpose} ${s.intent} ${s.gaze} ${s.shotAction} ${s.performance} ${compileVisualPrompt(s)}`)
  .join('\n');
if (/face on camera|speak this line/i.test(directorBlob)) fail.push('ban face on camera / speak-this-line template');
if (khoe.purpose === wait.purpose) fail.push('two Actions must not share one Audience must see template');
if (!/Linh/i.test(khoe.gaze || '') || !/never|lens|not the camera|off-lens/i.test(khoe.gaze || '')) {
  fail.push('MCU gaze toward Linh, not the lens');
}
if (khoe.focus !== 'Minh') fail.push('MCU focus is Minh');
if (!khoe.secondary.some((p) => p.name === 'Linh' && /shoulder|partial/i.test(p.body))) {
  fail.push('Linh is secondary shoulder / partial');
}
if (hold.framing !== 'CU') fail.push(`hold face → CU, got ${hold.framing}`);
if (/không đổi biểu cảm/i.test(hold.shotAction)) fail.push('hold note is Performance, not Action');
if (!/đứng im|nhìn/i.test(hold.shotAction)) fail.push('hold Action is stand still and look');
if (!/giữ nét|không đổi|không phản ứng/i.test(hold.performance)) fail.push('hold Performance keeps the face');
const lockQa = parseVisionQa(
  { total: 45, hardFails: ['WRONG_LOCATION', 'WRONG_ACTION', 'WRONG_COUNT'] },
  deriveVisualSpec({ shotId: 'SH01-01', action: 'Minh ở phòng ăn.', names: ['Minh'], speakers: ['Minh'] }),
  { sceneMaster: true },
);
if (lockQa.hardFails.length || lockQa.status !== 'PASS') {
  fail.push('Scene Master Chấm lại must not BLOCK on location/action/count');
}
if (sanitizeHardFails(spokenMcu, ['WRONG_COUNT'], { sceneMaster: true }).includes('WRONG_COUNT')) {
  fail.push('scene master drops WRONG_COUNT');
}

if (framingFromAction('Minh nói với mẹ.', 'Mẹ ơi', ['Minh'], 'MCU') !== 'OTS') {
  fail.push('MCU after MCU spoken → OTS');
}
if (!/notice the 9/i.test(spec.purpose)) fail.push('INSERT paper purpose stays the gold standard');

const insertBrief = compileGeminiStillBrief(spec);
if (!/GEMINI STILL/i.test(insertBrief)) fail.push('INSERT brief header');
if (!/Test paper/i.test(insertBrief)) fail.push('INSERT brief draws the paper');
if (/exactly 2 people/i.test(insertBrief)) fail.push('INSERT brief must not say exactly 2 people');
if (peopleCountForSpec(spec) !== 0) fail.push('INSERT people count is 0');

const mcuBrief = compileGeminiStillBrief(spokenMcu);
if (spokenMcu.framing !== 'MCU') fail.push(`spoken one-liner must be MCU, got ${spokenMcu.framing}`);
if (!/Minh/i.test(mcuBrief) || !/full face|face visible/i.test(mcuBrief)) fail.push('MCU brief must lock Minh face');
if (/exactly 2 people/i.test(mcuBrief)) fail.push('MCU brief must not force two people');
if (peopleCountForSpec(spokenMcu) !== 1) fail.push('MCU people count is 1');
if (canonIdsForSpec(spokenMcu, ['CHAR-001', 'CHAR-003'])[0] !== 'CHAR-001') {
  fail.push('MCU identity pack starts with Minh');
}
if (canonIdsForSpec(spec, ['CHAR-001', 'CHAR-003'])[0] !== 'CHAR-001') {
  fail.push('INSERT still needs Minh as eyes/hands ref');
}

if (fail.length) {
  console.error('VISUAL SPEC FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('VISUAL SPEC PASS · INSERT paper + QA gate + Gemini still brief');
