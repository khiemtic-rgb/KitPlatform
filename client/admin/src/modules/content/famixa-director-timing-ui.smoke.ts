/** FAMIXA_DIRECTOR_TIMING_UI_V1 */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  DIRECTOR_PRODUCTION_TIMING_ID,
  DIRECTOR_PRODUCTION_TIMING_OPTIONS,
  DIRECTOR_PRODUCTION_TIMING_UNDECIDED,
  directorTimingFromPreview,
  directorTimingFromSnapshot,
  explicitDirectorSpeechInterval,
  formatDirectorProductionDuration,
  isAuthoritativeDirectorTiming,
} from './famixa-director-timing';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(root, name), 'utf8');
const picker = read('FamixaDirectorTimingPicker.tsx');
const helper = read('famixa-director-timing.ts');
const series = read('ContentFamixaSeriesTab.tsx');
const workspace = read('ContentFamixaShotProduction/ShotProductionWorkspace.tsx');
const studio = read('ContentFamixaStudioView.tsx');
const api = read('../../shared/api/content.api.ts');
const persist = series.slice(series.indexOf('const persistDirectorTiming'), series.indexOf('const refuseUnwiredVeo'));
const hydrate = series.slice(series.indexOf('const scope = productionScopeFromShotCode(shotId);'), series.indexOf('const persistEngine'));

ok(DIRECTOR_PRODUCTION_TIMING_ID === 'FAMIXA_DIRECTOR_TIMING_UI_V1', 'id');
ok(DIRECTOR_PRODUCTION_TIMING_OPTIONS.join(',') === '5,10', 'options 5 and 10');
ok(formatDirectorProductionDuration(undefined) === DIRECTOR_PRODUCTION_TIMING_UNDECIDED, 'A undecided empty');
ok(
  formatDirectorProductionDuration({ durationSec: 5, timingStatus: 'TIMING_PRODUCTION_DECISION_REQUIRED' })
    === DIRECTOR_PRODUCTION_TIMING_UNDECIDED,
  'A preview without AUTHORITATIVE is undecided',
);
ok(
  formatDirectorProductionDuration({ durationSec: 5, timingStatus: 'AUTHORITATIVE' }) === '5 giây',
  'authoritative 5s label',
);
ok(
  isAuthoritativeDirectorTiming({
    durationSec: 5,
    timingSource: 'DIRECTOR',
    decisionType: 'EXPLICIT_PRODUCTION_DECISION',
  }),
  'OS snapshot DIRECTOR + EXPLICIT is authoritative',
);
ok(
  !isAuthoritativeDirectorTiming({ durationSec: 5, timingSource: 'RUNWAY', decisionType: 'PROVIDER' }),
  'provider duration is not authority',
);

const fromPreview = directorTimingFromPreview({
  durationSec: 0,
  timingStatus: 'TIMING_PRODUCTION_DECISION_REQUIRED',
});
ok(!isAuthoritativeDirectorTiming(fromPreview), 'A create-video-input-preview missing timing');

const fromSnap = directorTimingFromSnapshot({
  productionDurationSec: 10,
  timingSource: 'DIRECTOR',
  decisionType: 'EXPLICIT_PRODUCTION_DECISION',
  performanceSnapshotId: 'snap-1',
});
ok(isAuthoritativeDirectorTiming(fromSnap) && formatDirectorProductionDuration(fromSnap) === '10 giây', 'C 10s snapshot');

ok(picker.includes('Production duration') && picker.includes('5 giây') && picker.includes('10 giây'), 'picker copy');
ok(!picker.includes('setShortSeconds') && !picker.includes('onSeconds'), 'picker does not write graph seconds');
ok(workspace.includes('FamixaDirectorTimingPicker') && studio.includes('FamixaDirectorTimingPicker'), 'placed on BÀN SHOT and TẠO VIDEO');
ok(workspace.includes('onDirectorTiming') && studio.includes('onDirectorTiming'), 'confirm callback wired');

ok(persist.includes('materializeProductionDirectorTiming'), 'persist uses canonical POST');
ok(persist.includes('durationSec') && persist.includes('shotCode') && persist.includes('buildId'), 'request body fields');
ok(!persist.includes('setShortSeconds') && !persist.includes('episode.shots'), 'D persist does not write graph seconds');
ok(
  !persist.includes('createProductionRequest')
    && !persist.includes("action: 'CREATE_VIDEO'")
    && !persist.includes('issueProductionExecutionPermit')
    && !persist.includes('upsertProductionAiSelection'),
  'F no CREATE_VIDEO / permit / selection on timing confirm',
);
ok(persist.includes('getProductionShotActions') && persist.includes('getCreateVideoInputPreview'), 'reload OS after success');
ok(persist.includes('message.error') && persist.includes('Không lưu được Production duration'), 'D error toast');
ok(hydrate.includes('getCreateVideoInputPreview') && !hydrate.includes('shot.seconds'), 'E hydrate reads OS preview not graph seconds');
ok(hydrate.includes('directorTimingFromPreview'), 'E reload display from canonical preview');
ok(api.includes("'/content/production/director-timing'"), 'canonical API path');
ok(!helper.includes('setShortSeconds') && !helper.includes('takeUrl') && !helper.includes('previewUrl'), 'helper ignores graph/video clocks');
ok(!persist.includes('4ef164d6') && !persist.includes('EP01-SC01-SH01'), 'G no SH01 hardcode in persist');
ok(series.includes('osDirectorTimingByShot') && series.includes('persistDirectorTiming'), 'series state + persist');
ok(explicitDirectorSpeechInterval({ timingSource: 'DIRECTOR', durationSec: 5 }) === null, '5s picture duration is not a speech interval');
ok(explicitDirectorSpeechInterval({ timingSource: 'DIRECTOR', durationSec: 10 }) === null, '10s picture duration is not a speech interval');
ok(explicitDirectorSpeechInterval({ timingSource: 'DIRECTOR', speechStartSec: 2 }) === null, 'one speech end is not an interval');
ok(
  explicitDirectorSpeechInterval({ timingSource: 'DIRECTOR', speechStartSec: 2, speechEndSec: 4.5 })?.speechEndSec === 4.5,
  'both typed ends are the director interval',
);
ok(picker.includes('data-director-speech-start') && picker.includes('onConfirm(sec, typed)'), 'picker sends the typed interval with the duration');
ok(picker.includes('data-director-speech-save') && picker.includes('Lưu khoảng thoại'), 'the speech interval has its own save');
const clickStart = series.indexOf('const director = osDirectorTimingByShot[shotId]');
const click = series.slice(clickStart, series.indexOf('if (clocks.length)', clickStart));
ok(
  click.includes('osDirectorTimingByShot[shotId]')
    && click.includes('approvedDialogueIntervals')
    && !click.includes('shot.timing'),
  'the click uses the stored director window',
);
ok(!picker.includes('SPEECH_LEAD_IN') && !picker.includes('computeShotTiming'), 'picker does not invent a lead-in');
ok(persist.includes('speechStartSec') && persist.includes('speechEndSec'), 'persist posts both speech ends');

if (fail.length) {
  console.error(`FAMIXA_DIRECTOR_TIMING_UI_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_DIRECTOR_TIMING_UI_V1 ok');
