/** FAMIXA_ACTING_BEAT_SAVE_UX_V1 — explicit save. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { compileI2vPrompt, slimPilotForStorage, type FamixaSeriesShot, type SeriesPilotState } from './content-famixa-series';
import { promptHashOf, sameFailedInput } from './content-famixa-runway-pipe';
import {
  ACTING_ACTION_CHIPS,
  ACTING_BEAT_SAVE_LABEL,
  ACTING_BEAT_SAVED_LABEL,
  ACTING_BEAT_UNSAVED_LABEL,
  ACTING_BEAT_UNSAVED_LEAVE,
  ACTING_BODY_CHIPS,
  ACTING_BODY_WEIGHT,
  ACTING_GAZE_CHIPS,
  ACTING_ROOM_CHIPS,
  ACTING_ROOM_STILL,
  ACTING_STANCE_DOORWAY,
  actingBeatDirty,
  confirmLeaveActingBeat,
  patchActingBeat,
} from './ContentFamixaShotProduction/ShotProductionActingBeat';
import { computeShotTiming } from './famixa-shot-production-timing';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const card = read('ContentFamixaShotProduction/ShotProductionActingBeatCard.tsx');
const helper = read('ContentFamixaShotProduction/ShotProductionActingBeat.ts');
const ws = read('ContentFamixaShotProduction/ShotProductionWorkspace.tsx');
const series = read('ContentFamixaSeriesTab.tsx');
const orch = read('ContentFamixaShotProduction/ShotProductionOrchestrator.ts');
const stamp = read('ContentFamixaShotProduction/ShotProductionStamp.ts');
const pipe = read('content-famixa-runway-pipe.ts');

ok(
  helper.includes(ACTING_BEAT_UNSAVED_LABEL) &&
    helper.includes(ACTING_BEAT_SAVE_LABEL) &&
    helper.includes(ACTING_BEAT_SAVED_LABEL) &&
    card.includes('ACTING_BEAT_UNSAVED_LABEL') &&
    card.includes('ACTING_BEAT_SAVE_LABEL') &&
    card.includes('ACTING_BEAT_SAVED_LABEL'),
  'UI save labels',
);
ok(card.includes('disabled={!dirty}') && card.includes('data-acting-beat-save'), 'save disabled when clean');
ok(card.includes('setDraft') && card.includes('actingBeatDirty') && !card.includes('onChange(patchActingBeat'), 'chips edit draft only');
ok(card.includes('onClick={save}') && card.includes('onChangeRef.current(next)'), 'save flushes persist callback');
ok(card.includes('beforeunload') && card.includes('ACTING_BEAT_UNSAVED_LEAVE'), 'leave guard on dirty');
ok(ws.includes('confirmLeaveActingBeat') && ws.includes('actingEditorRef.current.save()'), 'shot switch uses leave guard');
ok(ws.includes("action === 'picture'") && ws.includes('actingEditorRef.current?.save?.()'), 'picture flushes unsaved acting');
ok(ws.includes('preferKeyframe: showStill') && ws.includes('directorShowsStillPreview'), 'pending picture shows still not old take');
ok(series.includes('kfForceNew: true') && series.includes('onlyIds.includes'), 'Tạo hình mới forces new KF');
ok(series.includes('remake && lock?.id === s.id'), 'remake does not attach own KF as Scene Master');
ok(series.includes('onActingBeat') && series.includes('persistState') && /shots:\s*live\.episode\.shots\.map/.test(series), 'persist path unchanged');
ok(
  helper.includes('Không di chuyển') &&
    helper.includes('Hơi chuyển trọng tâm') &&
    helper.includes('Hơi do dự') &&
    helper.includes('Hạ mắt rồi nhìn mẹ') &&
    helper.includes('Phòng phía sau yên') &&
    card.includes('ACTING_BODY_CHIPS') &&
    card.includes('ACTING_ACTION_CHIPS') &&
    card.includes('ACTING_GAZE_CHIPS') &&
    card.includes('ACTING_ROOM_CHIPS'),
  'chips unchanged',
);

const timing = computeShotTiming({ voiceDurations: [1.52], leadInSec: 0.2, tailSec: 0.22 });
const shot = (over: Partial<FamixaSeriesShot> = {}): FamixaSeriesShot => ({
  id: 'EP99-SC01-SH01',
  scene: 'SC01',
  sceneId: 'SC01',
  shot: 'SH01',
  clock: '5s',
  seconds: 5,
  editSeconds: 1.94,
  timing,
  story: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  visual: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  characters: ['CHAR-001'],
  characterIds: ['CHAR-001', 'CHAR-003'],
  location: 'PHÒNG KHÁCH',
  motionPrompt: '',
  motionPromptVi: 'Minh đứng ở cửa phòng khách, hai tay cầm tờ giấy, nhìn mẹ.',
  status: 'story_locked',
  dialogueSegmentIds: ['line-SC01-CHAR-001-1'],
  ...over,
});

const doorway = ACTING_ACTION_CHIPS.find((c) => c.label === 'Đứng ở cửa')!;
const weight = ACTING_BODY_CHIPS.find((c) => c.label === 'Hơi chuyển trọng tâm')!;
const lower = ACTING_GAZE_CHIPS.find((c) => c.label === 'Hạ mắt rồi nhìn mẹ')!;
const roomQuiet = ACTING_ROOM_CHIPS.find((c) => c.label === 'Phòng phía sau yên')!;
ok(weight.value === ACTING_BODY_WEIGHT && roomQuiet.value === ACTING_ROOM_STILL, 'SH01 chip English');

const sh01 = shot();
const beforeBeat = patchActingBeat(sh01, true, {
  emotion: 'uneasy',
  action: doorway.value,
  gaze: lower.value,
});
ok(!actingBeatDirty(beforeBeat, beforeBeat), 'load SH01 clean');
ok(actingBeatDirty(undefined, beforeBeat), 'empty vs draft is dirty');

let draft = beforeBeat;
ok(!actingBeatDirty(beforeBeat, draft), 'no edit → Đã lưu');

draft = patchActingBeat({ ...sh01, actingBeat: draft }, true, { body: weight.value, room: roomQuiet.value });
ok(actingBeatDirty(beforeBeat, draft), 'edit → dirty');
ok(draft.before.body === ACTING_BODY_WEIGHT && draft.before.room === ACTING_ROOM_STILL, 'draft holds V2 fields');
ok(draft.before.action === ACTING_STANCE_DOORWAY, 'stance unchanged');

ok(confirmLeaveActingBeat(false, () => false) === true, 'clean leave allowed');
ok(confirmLeaveActingBeat(true, () => false) === false, 'dirty leave cancel stays');
ok(confirmLeaveActingBeat(true, (msg) => msg === ACTING_BEAT_UNSAVED_LEAVE) === true, 'dirty leave confirm saves path');

const kfUrl = 'data:image/jpeg;base64,xxSH01KF';
const baseState = (row: FamixaSeriesShot): SeriesPilotState => ({
  roles: [{ id: 'role-CHAR-001', title: 'Con', name: 'Minh', characterId: 'CHAR-001', voiceId: 'v1' }],
  runs: {
    'EP99-SC01-SH01': {
      status: 'approved',
      kfApproved: true,
      keyframeDataUrl: kfUrl,
      keyframeFileName: 'kf-EP99-SC01-SH01-canon.jpg',
      failedKfHash: 'h2b879d96:147815',
      failedPromptHash: 'hf56bc88:377',
      runwayAttempts: [
        {
          n: 3,
          at: '',
          status: 'FAILED',
          failureCode: 'INTERNAL.BAD_OUTPUT',
          promptHash: 'hf56bc88:377',
          taskId: '9ef1c551',
          source: { hash: 'h2b879d96:147815' },
        },
      ],
    },
  },
  characters: [{ id: 'CHAR-001', name: 'Minh', voiceId: 'v1' }],
  lines: [{ id: 'line-SC01-CHAR-001-1', characterId: 'CHAR-001', text: 'Mẹ xem giúp con tờ này.', sceneId: 'SC01' }],
  voiceAssets: { 'line-SC01-CHAR-001-1': { lineId: 'line-SC01-CHAR-001-1', duration: 1.52, status: 'ready', characterId: 'CHAR-001' } },
  episode: {
    seriesCode: 'FAMIXA',
    seriesTitle: 'F',
    episode: 'EP99',
    title: 'TEST BATCH 3 SHORT',
    premise: '',
    moral: '',
    ctaRule: '',
    shots: [row],
  },
});

const beforeShot = { ...sh01, actingBeat: beforeBeat };
const beforeState = baseState(beforeShot);
const beforePrompt = compileI2vPrompt(beforeState, beforeShot, beforeShot.story);
const beforeHash = promptHashOf(beforePrompt);

const savedShot = { ...sh01, actingBeat: draft };
const savedState = {
  ...beforeState,
  episode: {
    ...beforeState.episode!,
    shots: beforeState.episode!.shots.map((s) => (s.id === 'EP99-SC01-SH01' ? { ...s, actingBeat: draft } : s)),
  },
};
ok(actingBeatDirty(beforeBeat, savedShot.actingBeat), 'pre-reload draft differs from loaded');

const reloaded = slimPilotForStorage(savedState);
const loadedBeat = reloaded.episode?.shots[0]?.actingBeat;
ok(loadedBeat?.before.body === ACTING_BODY_WEIGHT, 'reload keeps body');
ok(loadedBeat?.before.room === ACTING_ROOM_STILL, 'reload keeps room');
ok(loadedBeat?.before.action === ACTING_STANCE_DOORWAY, 'reload keeps stance');
ok(loadedBeat?.during.emotion === 'uneasy', 'reload keeps emotion');
ok(!actingBeatDirty(draft, loadedBeat), 'reload matches saved draft');

const afterShot = { ...sh01, actingBeat: loadedBeat };
const afterPrompt = compileI2vPrompt(reloaded, afterShot, afterShot.story);
const afterHash = promptHashOf(afterPrompt);
ok(/He shifts his weight slightly and hesitates/i.test(afterPrompt), 'compiled reflects body');
ok(/The living room behind him stays still; only he moves/i.test(afterPrompt), 'compiled reflects room');
ok(beforeHash !== afterHash, 'promptHash changes after save');
ok(afterHash !== 'hf56bc88:377', 'promptHash differs from SH01 n=3');
ok(
  sameFailedInput(reloaded.runs['EP99-SC01-SH01']!, 'h2b879d96:147815', afterHash) === false,
  'sameFailedInput opens after save',
);

ok(savedState.runs['EP99-SC01-SH01']?.keyframeDataUrl === kfUrl, 'KF unchanged');
ok(reloaded.runs['EP99-SC01-SH01']?.keyframeFileName === 'kf-EP99-SC01-SH01-canon.jpg', 'KF file name survives slim');
ok(afterShot.seconds === 5 && afterShot.actingBeat?.before.holdSec === 0.2, 'timing holdSec / provider unchanged');

ok(!orch.includes('Lưu diễn xuất') && !orch.includes('actingBeatDirty'), 'orchestrator untouched');
ok(!stamp.includes('actingBeatDirty') && !pipe.includes('actingBeatDirty'), 'fingerprints / sameFailedInput untouched');
ok(!card.includes('startContentSeriesTurbo') && !card.includes('startSceneTurbo') && !helper.includes('fetch('), 'no provider calls');
ok(!card.includes('generateSceneKf') && !ws.includes('eleven') && !card.includes('fal-ai/'), 'no media regeneration');
ok(!card.includes('holdSec') && !card.includes('prompt editor'), 'no holdSec / prompt editor');

if (fail.length) {
  console.error(`FAMIXA_ACTING_BEAT_SAVE_UX_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_ACTING_BEAT_SAVE_UX_V1 PASS FAIL=0 (no provider)');
console.log(`beforeHash=${beforeHash}`);
console.log(`afterHash=${afterHash}`);
console.log(`beforePrompt=${beforePrompt}`);
console.log(`afterPrompt=${afterPrompt}`);
