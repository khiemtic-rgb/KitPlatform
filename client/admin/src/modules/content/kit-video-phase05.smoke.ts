import { compileVisualPrompt, MAX_AUTO_ATTEMPTS } from './kit-video-vision';
import {
  FAMIXA_VISUAL_STYLE,
  KIT_VIDEO_PIXEL,
  famixaGoldenContract,
  formatPixelBoard,
  jpegMagicOk,
  parseVisionJson,
  pixelI2vReady,
  productionPromptHasDialogue,
} from './kit-video-pixel';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

ok(KIT_VIDEO_PIXEL === 'KIT-VIDEO-PIXEL-V1', 'engine pixel id');
ok(MAX_AUTO_ATTEMPTS === 3, 'max auto attempts');
ok(FAMIXA_VISUAL_STYLE === 'FAMIXA_VISUAL_STYLE_V1', 'project style');

const contract = famixaGoldenContract();
ok(contract.shotCode === 'SH01-01', 'golden shot SH01-01');
ok(contract.characters.some((c) => c.code === 'CHAR-001') && contract.characters.some((c) => c.code === 'CHAR-003'), 'golden minh + mother');
ok(contract.props.some((p) => p.code === 'PROP-001' && p.state === 'held_by_minh'), 'golden paper held');
ok(contract.location.code === 'LOC-001', 'golden living room');
ok(contract.wardrobe['CHAR-001'] === 'WARDROBE-001', 'golden school uniform');

const dialogue = 'Mẹ ơi, con được 9 điểm!';
ok(!productionPromptHasDialogue(contract, dialogue), '01 no dialogue in image prompt');
const compiled = compileVisualPrompt(contract, { projectStyle: FAMIXA_VISUAL_STYLE });
ok(compiled.sections.ACTION && compiled.sections.STYLE === FAMIXA_VISUAL_STYLE, '01 compiler style from project');
ok(compiled.i2vImageSource === 'APPROVED_KEYFRAME', '01 i2v source keyframe');
ok(!compiled.prompt.includes('{') && !compiled.prompt.includes('pack_content'), '01 no json dump');

ok(jpegMagicOk(new Uint8Array([0xff, 0xd8, 0xff, 0xe0])), '02 jpeg magic ok');
ok(!jpegMagicOk(new Uint8Array([0x00, 0x01, 0x02])), '02 garbage not jpeg');

const parsed = parseVisionJson(
  '{"overall":"FAIL","characters":{"expected":2,"detected":3,"ids":["CHAR-001","CHAR-003","EXTRA"]},"requirements":[{"id":"PROP-001","status":"FAIL","reason":"Test paper not visible","confidence":0.94}]}',
);
ok(parsed.overall === 'FAIL' && parsed.detected === 3 && parsed.expected === 2, '03 structured vision parse');

const uncertain = parseVisionJson('{"overall":"UNCERTAIN","uncertain":true,"characters":{"expected":2,"detected":2,"ids":["CHAR-001","CHAR-003"]}}');
ok(uncertain.uncertain && uncertain.overall === 'UNCERTAIN', '04 P0 UNCERTAIN not auto PASS');

const blocked = pixelI2vReady({ attemptStatus: 'VISION_PASS', qaStatus: 'PASS', allowI2v: true, runwayCalled: false });
ok(!blocked.ready && blocked.blocked.some((b) => /APPROVED/i.test(b)), '05 vision pass without director is not I2V');

const failVision = pixelI2vReady({ attemptStatus: 'APPROVED', qaStatus: 'FAIL', allowI2v: false, runwayCalled: false });
ok(!failVision.ready, '05 vision fail no I2V');

const ready = pixelI2vReady({ attemptStatus: 'APPROVED', qaStatus: 'PASS', allowI2v: true, runwayCalled: false });
ok(ready.ready && ready.runwaySubmitted === false, '06 I2V READY only after PASS + APPROVE');
ok(!ready.blocked.includes('Runway'), '06 runway not submitted');

const illegal = pixelI2vReady({ attemptStatus: 'APPROVED', qaStatus: 'PASS', allowI2v: true, runwayCalled: true });
ok(!illegal.ready, '07 runway called blocks Phase 05');

const board = formatPixelBoard({
  shotCode: 'SH01-01',
  jobState: 'VISION_FAIL',
  provider: 'GEMINI',
  model: 'configured',
  artifactOk: true,
  qa: { status: 'FAIL', p0Fail: ['Test paper not visible'] },
  repair: 'Increase test paper visibility.',
  i2vReady: false,
  runwayCalled: false,
});
ok(board.includes('P0: Test paper not visible'), '08 FAIL shows P0');
ok(board.includes('Suggested repair'), '08 repair shown');
ok(board.includes('RUNWAY CALLED = FALSE'), '08 no runway button');
ok(!board.includes('Send to Runway'), '08 no Send to Runway');

ok(compiled.fingerprint.length > 8, '09 fingerprint exists');
ok(compiled.fingerprint !== compileVisualPrompt(contract, { projectStyle: 'OTHER_STYLE' }).fingerprint, '09 style changes fingerprint');

if (fail.length) {
  console.error('KIT VIDEO ENGINE PHASE 05 FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('KIT VIDEO ENGINE PHASE 05 PASS · offline · real-pixel gates + no Runway');
