import {
  actionNearlySame,
  applyEditDurations,
  continueScenePrompt,
  deriveSceneMaster,
  fullEpisodeBlockReason,
  i2vSecondsForEdit,
  kfIsApprovedStill,
  lockSceneMaster,
  pickShots,
  planEditSeconds,
  previousSceneKf,
  prodGateState,
  sequentialKfIds,
  shotProdStatus,
} from './content-famixa-scene-first';
import type { FamixaSeriesShot, SeriesPilotState } from './content-famixa-series';

function shot(partial: Partial<FamixaSeriesShot> & { id: string }): FamixaSeriesShot {
  return {
    scene: 'SC01',
    sceneId: 'SC01',
    shot: partial.id,
    clock: '5s',
    seconds: 5,
    story: 'Minh đưa bài kiểm tra cho mẹ.',
    visual: '',
    characters: ['CHAR-001', 'CHAR-003'],
    characterIds: ['CHAR-001', 'CHAR-003'],
    location: 'Phòng ăn',
    motionPrompt: '',
    motionPromptVi: '',
    status: 'story_locked',
    beatId: 'SC01-BEAT01',
    ...partial,
  };
}

const shots = [
  shot({ id: 'SH01', story: 'Minh lấy bài kiểm tra ra và đưa cho mẹ.' }),
  shot({ id: 'SH02', story: 'Mẹ nhận bài và nhìn điểm.' }),
  shot({ id: 'SH02b', story: 'Mẹ nhận bài và nhìn điểm.', voiceChainFrom: 'SH02' }),
  shot({ id: 'empty', story: '' }),
];

const state = {
  roles: [],
  runs: {
    SH01: { status: 'keyframe_ready', keyframeDataUrl: 'data:image/png;base64,aa', kfApproved: true },
    SH02: { status: 'story_locked', keyframeDataUrl: 'data:image/png;base64,bb', kfApproved: false },
    SH02b: { status: 'story_locked' },
    empty: { status: 'story_locked' },
  },
  scriptLocked: true,
  voiceLocked: true,
  shotGraphLocked: true,
  scenes: [{ id: 'SC01', title: 'Minh khoe điểm', environment: 'Phòng ăn buổi tối ấm', characterIds: ['CHAR-001', 'CHAR-003'] }],
  characters: [
    { id: 'CHAR-001', name: 'Minh', wardrobe: 'polo trắng cổ xanh' },
    { id: 'CHAR-003', name: 'Linh', wardrobe: 'áo be' },
  ],
  episode: {
    seriesCode: 'FAMIXA',
    seriesTitle: 'F',
    episode: 'EP01',
    title: 'Mẹ chỉ xấu hổ vì con',
    premise: '',
    moral: '',
    ctaRule: '',
    shots,
  },
} as SeriesPilotState;

const fail: string[] = [];
const master = deriveSceneMaster(state, 'SC01');
if (master.sceneId !== 'SC01') fail.push(`scene ${master.sceneId}`);
if (master.time !== 'evening') fail.push(`time ${master.time}`);
if (!/phòng ăn/i.test(master.location)) fail.push(`location ${master.location}`);
if (master.locked) fail.push('derived master must start unlocked');

const locked = lockSceneMaster(state, 'SC01');
if (!locked.sceneMasters?.SC01?.locked) fail.push('lock Scene Master');

if (!actionNearlySame('Mẹ nhận bài và nhìn điểm.', 'Mẹ nhận bài và nhìn điểm.')) fail.push('same action');
if (actionNearlySame('Minh đưa bài.', 'Mẹ nhận bài và nhìn điểm.')) fail.push('different action must not reuse');

const prev = previousSceneKf(state, shots[1]!, shots);
if (prev?.shot.id !== 'SH01') fail.push(`previous KF ${prev?.shot.id}`);

if (sequentialKfIds(state, shots).includes('empty')) fail.push('HOLD in sequential queue');
if (sequentialKfIds(state, shots).includes('SH01')) fail.push('approved KF must skip sequential');
if (!sequentialKfIds(state, shots).includes('SH02')) fail.push('draft SH02 must generate');

if (shotProdStatus(state, shots[0]!) !== 'KF APPROVED') fail.push('SH01 KF APPROVED');
if (shotProdStatus(state, shots[1]!) !== 'KF DRAFT') fail.push('SH02 KF DRAFT');
if (shotProdStatus(state, shots[3]!) !== 'HOLD') fail.push('empty HOLD');
if (kfIsApprovedStill(state.runs.SH02!)) fail.push('draft must not be approved');

if (planEditSeconds(2.5, 0.4, 0) < 2.8 || planEditSeconds(2.5, 0.4, 0) > 3.1) {
  fail.push(`edit ${planEditSeconds(2.5, 0.4, 0)}`);
}
if (i2vSecondsForEdit(3.2) !== 5) fail.push('3s edit → I2V 5');
if (i2vSecondsForEdit(7) !== 10) fail.push('7s edit → I2V 10');

const timed = applyEditDurations(state, [shots[0]!]);
if (timed.episode?.shots[0] && ![5, 10].includes(timed.episode.shots[0].seconds)) {
  fail.push('I2V seconds must stay 5 or 10');
}

if (pickShots(shots, ['SH02', 'SH01']).map((s) => s.id).join() !== 'SH01,SH02') {
  fail.push('pick keeps script order');
}

const gates = prodGateState(locked, [shots[0]!, shots[1]!]);
if (!gates.script || !gates.voice || !gates.shotPlan || !gates.sceneMaster) fail.push('early gates');
if (gates.kf) fail.push('KF gate must wait SH02 approve');
if (!fullEpisodeBlockReason(locked, [shots[0]!, shots[1]!])) fail.push('full EP must block');

const prompt = continueScenePrompt(master, 'SH01-01', 'Mẹ nhận bài và nhìn điểm.');
if (!/continue the exact same scene/i.test(prompt)) fail.push('continue prompt');
if (!/only change the action/i.test(prompt)) fail.push('action-only');
if (/ôm|xin lỗi|hug|apology/i.test(prompt)) fail.push('must not invent plot');

if (fail.length) {
  console.error('SCENE FIRST FAIL');
  for (const f of fail) console.error(` - ${f}`);
  process.exit(1);
}
console.log('SCENE FIRST PASS · master', master.sceneId, master.time);
