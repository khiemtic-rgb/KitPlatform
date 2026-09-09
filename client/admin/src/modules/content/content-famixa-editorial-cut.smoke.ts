/** FAMIXA Film Editing Engine V1 — overlay trim / mix. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  DEFAULT_EDITORIAL_CUT,
  EDITORIAL_SOURCE_GONE,
  editorialAssembleError,
  editorialCutOf,
  editorialFileStem,
  editorialSourceOf,
  editorialSourceReady,
  estimateEditorialTotalSec,
  patchEditorialCut,
  patchEditorialShotTrim,
  resolveEditorialWindow,
} from './content-famixa-editorial-cut';
import type { FamixaSeriesShot, SeriesPilotState } from './content-famixa-series';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const series = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const desk = readFileSync(join(root, 'ContentFamixaDirectorShortDesk.tsx'), 'utf8');
const timing = readFileSync(join(root, 'famixa-shot-production-timing.ts'), 'utf8');
const stamp = readFileSync(join(root, 'ContentFamixaShotProduction/ShotProductionStamp.ts'), 'utf8');

function shot(partial: Partial<FamixaSeriesShot> & { id: string }): FamixaSeriesShot {
  return {
    scene: 'SC01',
    sceneId: 'SC01',
    shot: partial.id,
    clock: '5s',
    seconds: 5,
    story: '',
    visual: '',
    characters: ['CHAR-001'],
    characterIds: ['CHAR-001'],
    location: 'bàn ăn',
    motionPrompt: '',
    motionPromptVi: '',
    status: 'story_locked',
    dialogueSegmentIds: partial.dialogueSegmentIds ?? ['L1'],
    ...partial,
  };
}

const state: SeriesPilotState = {
  roles: [],
  runs: {},
  voiceAssets: { L1: { lineId: 'L1', duration: 2.0, status: 'ready' } },
};

const sh01 = shot({ id: 'SH01' });
const sh02 = shot({ id: 'SH02', dialogueSegmentIds: ['L1'] });
const silent = shot({ id: 'SH04', dialogueSegmentIds: [] });

const speech = resolveEditorialWindow(state, sh01, DEFAULT_EDITORIAL_CUT);
ok(speech.usableStart === 0, 'E1 speech in=0');
ok(speech.seconds < 5 && speech.seconds >= 2.2, `E1 speech keep dialogue+tail got ${speech.seconds}`);
ok(speech.usableEnd === speech.seconds, 'E1 out=keep when in=0');

const full = resolveEditorialWindow(state, sh01, { ...DEFAULT_EDITORIAL_CUT, mode: 'FULL_TAKE' });
ok(full.seconds >= 4.8 && full.seconds <= 5, `E2 FULL_TAKE uses performance got ${full.seconds}`);
ok(DEFAULT_EDITORIAL_CUT.mode === 'SPEECH_CUT', 'E2 default speech cut');

const silentWin = resolveEditorialWindow(state, silent, DEFAULT_EDITORIAL_CUT);
ok(silentWin.seconds >= 4.8, `E3 silent keeps take got ${silentWin.seconds}`);

const trimmed = resolveEditorialWindow(
  state,
  sh01,
  { ...DEFAULT_EDITORIAL_CUT, shots: { SH01: { inSec: 0.8, outSec: 3.9 } } },
);
ok(trimmed.usableStart === 0.8 && trimmed.usableEnd === 3.9 && trimmed.seconds === 3.1, 'E4 UsableStart/End overlay');

const patched = patchEditorialShotTrim(state, 'SH01', { inSec: 0.8, outSec: 3.9 });
ok(patched.editorialCut?.shots?.SH01?.inSec === 0.8, 'E5 overlay write');
ok(!patched.episode && !(patched.runs && Object.keys(patched.runs).length), 'E5 no production run write');
ok(sh01.timing === undefined, 'E5 shot timing untouched');

const next = patchEditorialCut(patched, { room: false, music: true });
ok(editorialCutOf(next).room === false && editorialCutOf(next).music === true, 'E6 mix overlay');
ok(next.smoothness === undefined, 'E6 does not write smoothness');

const three = estimateEditorialTotalSec(
  state,
  [sh01, sh02, shot({ id: 'SH03' })],
  DEFAULT_EDITORIAL_CUT,
);
ok(three < 15 && three > 6, `E7 3 spoken shots shorter than 15s got ${three}`);

const exportFn = series.slice(
  series.indexOf('const exportEditorialCut'),
  series.indexOf('const ensureShotVoice'),
);
ok(series.includes('usableStart: win.usableStart'), 'E8 assemble sends UsableStart');
ok(series.includes("kind === 'final' && ready.length !== pack.length"), 'E8 Final requires editorial sources');
ok(series.includes('editorialSourceReady(shotRunOf(live, s))'), 'E8 preview uses editorial source');
ok(!series.includes('applyEditorialPreset('), 'E8 does not write Timing V3 preset');
ok(series.includes('editorialSourceOf(run)'), 'E8 uses lipsync/take resolver');
ok(!exportFn.includes('finalReady') && !exportFn.includes('episodeFinishReady'), 'E8 export ignores production finalReady');
ok(!/exportEditorialCut[\s\S]{0,1800}previewUrl/.test(series), 'E8 export does not add previewUrl SoT');
ok(
  desk.includes('Xem preview') && desk.includes('Xuất cắt thoại') && desk.includes('data-director-export-short'),
  'E9 desk preview vs Xuất',
);
ok(desk.includes('Chỉnh dựng') && desk.includes('Chuẩn âm lượng'), 'E9 Chỉnh dựng + loudnorm label');
ok(desk.includes('disabled={!ready') && desk.includes('disabled={!canExport'), 'E9 preview before Xuất');
ok(desk.includes('Có video để dựng') && desk.includes('Hoàn thiện là cửa sản xuất'), 'E9 editorial ≠ Hoàn thiện');
ok(desk.includes('Watermark') || desk.includes('watermark'), 'E9 watermark gap stated');
ok(editorialFileStem('EP01', 'final') === 'EP01-edit', 'E9 EP01-edit stem');
ok(
  editorialAssembleError(new Error('Không tải được take (404). Link Runway hết hạn.')) === EDITORIAL_SOURCE_GONE,
  'E9 expired URL does not retry provider',
);
ok(editorialSourceOf({ lipsynced: true, lipsyncUrl: 'https://fal.example/a.mp4', takeUrl: 'https://t.example/b.mp4' }) === 'https://fal.example/a.mp4', 'E9 fal lipsync first');
ok(editorialSourceOf({ takeUrl: 'https://t.example/b.mp4' }) === 'https://t.example/b.mp4', 'E9 take resolver');
ok(editorialSourceOf({ lipsyncUrl: 'https://fal.example/old.mp4', motionNeedsRemake: true }) === '', 'E9 remake does not export old lipsync');
ok(editorialSourceReady({ lipsyncUrl: 'https://fal.example/a.mp4' }) && !editorialSourceReady({}), 'E9 sourceReady ≠ finalReady');

const ep01 = [
  { id: 'EP01-SC01-SH01', lipsyncUrl: 'https://v3b.fal.media/sh01.mp4', takeUrl: 'https://v3b.fal.media/sh01-take.mp4' },
  { id: 'EP01-SC01-SH02', lipsyncUrl: 'https://v3b.fal.media/sh02.mp4', takeUrl: 'https://v3b.fal.media/sh02-take.mp4' },
  { id: 'EP01-SC01-SH03', lipsyncUrl: 'https://v3b.fal.media/sh03.mp4', takeUrl: 'https://v3b.fal.media/sh03-take.mp4' },
  { id: 'EP01-SC01-SH04', takeUrl: 'https://dnznrvs05pmza.cloudfront.net/sh04.mp4', acceptedTake: { url: 'https://dnznrvs05pmza.cloudfront.net/sh04.mp4' } },
  { id: 'EP01-SC01-SH05', lipsyncUrl: 'https://v3b.fal.media/sh05.mp4', takeUrl: 'https://v3b.fal.media/sh05-take.mp4' },
  { id: 'EP01-SC01-SH06', lipsyncUrl: 'https://v3b.fal.media/sh06.mp4', takeUrl: 'https://dnznrvs05pmza.cloudfront.net/sh06.mp4' },
];
ok(ep01.every((run) => editorialSourceReady(run)), 'E12 EP01 all 6 have editorial source');
ok(editorialSourceOf(ep01[0]).includes('fal.media/sh01.mp4'), 'E12 SH01 lipsync');
ok(editorialSourceOf(ep01[3]).includes('cloudfront.net/sh04'), 'E12 SH04 take');
ok(editorialSourceOf(ep01[5]).includes('fal.media/sh06.mp4'), 'E12 SH06 lipsync over mute take');
ok(exportFn.includes('assembleContentSeriesCut') && exportFn.includes('usableStart'), 'E11 export uses existing assemble');
ok(!exportFn.includes('startContentSeriesTurbo') && !exportFn.includes('startContentSeriesLipsync'), 'E11 export calls 0 providers');
ok(!exportFn.includes('ensureShotVoice') && !exportFn.includes('elevenLabs'), 'E11 export does not TTS');
ok(!timing.includes('editorialCut'), 'E10 Timing V3 file unchanged by overlay');
ok(!stamp.includes('editorialCut'), 'E10 mix stamp ignores editorial overlay');
ok(
  /const finalReady =\s*!blockingReason &&\s*keyframeApproved &&\s*motionUsable &&/.test(
    readFileSync(join(root, 'ContentFamixaShotProduction/ShotProductionState.ts'), 'utf8'),
  ),
  'E13 production finalReady still requires keyframeApproved',
);
ok(
  readFileSync(join(root, 'ContentFamixaShotProduction/ShotProductionState.ts'), 'utf8').includes(
    'const notReady = snaps.filter((s) => !s.finalReady);',
  ),
  'E13 episodeFinishReady still uses finalReady',
);

if (fail.length) {
  console.error('EDITORIAL CUT FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log(`EDITORIAL CUT PASS · speech ${speech.seconds}s · 3-shot ~${three}s`);
