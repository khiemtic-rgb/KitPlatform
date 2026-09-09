import { deriveVisualSpec } from './content-famixa-visual-spec';
import {
  classifyQaFailure,
  applySoloCast,
  compileCorrectionPrompt,
  soloCastFromNote,
  compileNarrativeStillPrompt,
  compileVisualContract,
  identityCanonIds,
  mergeReferencePack,
} from './content-famixa-kf-pipeline';

const fail: string[] = [];
const spec = deriveVisualSpec({
  shotId: 'SH01-02',
  action: 'Minh cầm bài kiểm tra và nói với mẹ.',
  spoken: 'Mẹ ơi chín điểm!',
  names: ['Minh', 'Linh'],
  ids: ['CHAR-001', 'CHAR-003'],
  speakers: ['Minh'],
  location: 'Phòng ăn',
});
const contract = compileVisualContract(spec);
if (!/Minh/i.test(contract.primary)) fail.push('contract primary is Minh');
if (!/Linh/i.test(contract.secondary)) fail.push('contract secondary is Linh partial');
if (/face on camera|speak this line/i.test(`${contract.gaze} ${contract.shotIntent}`)) {
  fail.push('contract must not say face on camera');
}
const prompt = compileNarrativeStillPrompt({
  spec,
  aspect: '9:16',
  location: 'Phòng ăn',
  lighting: 'dim evening',
  refs: [
    { name: 'Scene Master', role: 'scene' },
    { name: 'Minh', role: 'identity' },
    { name: 'Linh', role: 'identity-secondary' },
    { name: 'Previous KF', role: 'continuity' },
  ],
});
if (/Photorealistic cinematic|This is a photoreal /i.test(prompt)) fail.push('compiler must not ask photoreal');
if (!/Stylized cinematic|designed-character/i.test(prompt)) fail.push('compiler asks stylized designed-character');
if (!/PRIORITY 1 — STORY/i.test(prompt)) fail.push('compiler is priority narrative');
if (!/REFERENCE 2 \(Minh\)/i.test(prompt)) fail.push('compiler labels Minh canon');
if (!/never into the lens|never toward/i.test(prompt) && !/never the lens/i.test(prompt)) {
  fail.push('compiler bans looking at camera');
}
if (/exactly 2 people|exactly 2 FULL/i.test(prompt)) fail.push('MCU narrative must not force two bodies');
if (identityCanonIds(spec, ['CHAR-001', 'CHAR-003'])[0] !== 'CHAR-001') fail.push('identity pack starts Minh');
const pack = mergeReferencePack({
  scene: { name: 'Scene Master', role: 'scene', imageDataUrl: 'data:image/png;base64,aa' },
  prev: { name: 'Previous KF', role: 'continuity', imageDataUrl: 'data:image/png;base64,bb' },
  identities: [
    { name: 'Minh', role: 'identity', imageDataUrl: 'data:image/png;base64,cc' },
    { name: 'Linh', role: 'identity-secondary', imageDataUrl: 'data:image/png;base64,dd' },
  ],
});
if (pack.length !== 4) fail.push(`pack must be 4, got ${pack.length}`);
if (pack[0]?.role !== 'identity' || pack.at(-1)?.role !== 'scene') fail.push('pack order Master Character → prev KF → Scene Master');
const same = mergeReferencePack({
  scene: { name: 'Scene Master', role: 'scene', imageDataUrl: 'data:image/png;base64,bb' },
  prev: { name: 'Previous KF', role: 'continuity', imageDataUrl: 'data:image/png;base64,bb' },
  identities: [{ name: 'Minh', role: 'identity', imageDataUrl: 'data:image/png;base64,cc' }],
});
if (same.some((r) => r.role === 'scene')) fail.push('do not duplicate scene when it is the previous KF');
const ticks = classifyQaFailure({ status: 'REJECT', hardFails: ['MISSING_PROP'], checks: {}, evidence: 'Không thấy bài kiểm tra' });
if (!ticks.includes('missing-prop')) fail.push('classify MISSING_PROP');
const edit = compileCorrectionPrompt(spec, ['missing-prop'], 'test paper not visible');
if (!/Keep the entire image|LOCK/i.test(edit) || !/test paper|bài|prop/i.test(edit)) fail.push('edit prompt must lock the frame and name the miss');
if (soloCastFromNote('chỉ có mẹ Linh đang xem điện thoại') !== 'Linh') fail.push('solo note is Linh');
const drop = compileCorrectionPrompt(spec, ['wrong-character'], 'chỉ có mẹ Linh đang xem điện thoại');
if (/the other person|Keep the entire image/i.test(drop) || !/Only Linh|Remove every extra/i.test(drop)) {
  fail.push('wrong-character must drop Minh, not preserve the other person');
}
const soloSpec = applySoloCast(spec, 'Linh');
if (soloSpec.secondary.length) fail.push('solo spec has no secondary');
if (soloSpec.primary?.id !== 'CHAR-003' || soloSpec.primary?.name !== 'Linh') {
  fail.push('solo Linh must use CHAR-003, not Minh id');
}
if (fail.length) {
  console.error('KF PIPELINE FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('KF PIPELINE PASS · contract + compiler + pack + edit');
