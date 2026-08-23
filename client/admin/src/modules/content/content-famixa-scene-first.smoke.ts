import {
  actionNearlySame,
  applyEditDurations,
  continueScenePrompt,
  deriveSceneMaster,
  fullEpisodeBlockReason,
  i2vSecondsForEdit,
  kfIsApprovedStill,
  lockSceneMaster,
  upsertSceneMaster,
  sceneMasterOf,
  pickShots,
  planEditSeconds,
  peopleCountLock,
  previousSceneKf,
  prodGateState,
  sequentialKfIds,
  shotProdStatus,
  compileShotSceneCard,
  compileShotStillMood,
  visibleFrameCast,
} from './content-famixa-scene-first';
import { looksLikePackHeading, seriesSceneStillPrompt, stripStillLettering, type FamixaSeriesShot, type SeriesPilotState } from './content-famixa-series';

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
  shot({
    id: 'SH01',
    story: 'Minh lấy bài kiểm tra ra và đưa cho mẹ.',
    characterIds: ['CHAR-001', 'CHAR-002', 'CHAR-003'],
    characters: ['CHAR-001', 'CHAR-002', 'CHAR-003'],
    dialogueSegmentIds: ['L1'],
  }),
  shot({
    id: 'SH02',
    story: 'Mẹ nhận bài và nhìn điểm.',
    characterIds: ['CHAR-001', 'CHAR-002', 'CHAR-003'],
    characters: ['CHAR-001', 'CHAR-002', 'CHAR-003'],
    dialogueSegmentIds: ['L2'],
  }),
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
  scenes: [{
    id: 'SC01',
    title: 'Minh khoe điểm',
    environment: 'Phòng ăn buổi tối ấm',
    characterIds: ['CHAR-001', 'CHAR-002', 'CHAR-003'],
    dialogue: [
      { id: 'L1', characterId: 'CHAR-001', text: 'Mẹ xem bài này.' },
      { id: 'L2', characterId: 'CHAR-003', text: 'Tám?' },
      { id: 'L3', characterId: 'CHAR-002', text: 'Anh về rồi.' },
    ],
  }],
  characters: [
    { id: 'CHAR-001', name: 'Minh', wardrobe: 'polo trắng cổ xanh' },
    { id: 'CHAR-002', name: 'Nam', wardrobe: 'sơ mi' },
    { id: 'CHAR-003', name: 'Linh', wardrobe: 'áo be', voiceNote: 'Giọng sắc lạnh, thực dụng, mệt mỏi, không vui' },
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
if (shotProdStatus({ ...state, sceneLocked: true }, shots[1]!) !== 'KF DRAFT') {
  fail.push('sceneLocked without take must stay KF DRAFT');
}
if (kfIsApprovedStill(state.runs.SH02!)) fail.push('draft must not be approved');
if (!looksLikePackHeading('KHOE BÀI TRONG VỠ ÒA (0–6s)')) fail.push('pack title with duration is not Action');
if (
  shotProdStatus(
    {
      ...state,
      runs: {
        ...state.runs,
        SH02: { ...state.runs.SH02!, visualQa: { status: 'PENDING', hardFails: [], checks: {} } },
      },
    },
    shots[1]!,
  ) !== 'KF DRAFT'
) {
  fail.push('QA PENDING must not look like QA BLOCK');
}

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
if (!/do not copy the previous smile|huddle|brightened room/i.test(prompt)) fail.push('must not copy previous smile');
if (/ôm|xin lỗi|hug|apology/i.test(prompt)) fail.push('must not invent plot');

const mood = compileShotStillMood(state, shots[1]!, 'Mẹ nhận bài. Mặt nghiêm nghị không vui.');
if (!/stern|unsatisfied|tense/i.test(mood)) fail.push(`still mood ${mood}`);
if (/\bôm\b|xin lỗi|bài học/i.test(mood)) fail.push('mood must not invent hug');

const c1 = visibleFrameCast(state, shots[0]!);
if (c1.count !== 2 || c1.ids.includes('CHAR-002')) fail.push(`SH01 must be 2 people not Nam, got ${c1.ids.join()}`);
const c2 = visibleFrameCast(state, shots[1]!, shots[0]!);
if (c2.count !== 2 || c2.ids.includes('CHAR-002')) fail.push(`SH02 must stay 2 people, got ${c2.ids.join()}`);
if (!peopleCountLock(c2, c1.count).includes('exactly 2')) fail.push('count lock text');
if (!/FACE LOCK|primary face/i.test(peopleCountLock(c2, c1.count))) fail.push('count lock must keep primary face');
const anState = {
  ...state,
  characters: [...(state.characters ?? []), { id: 'CHAR-004', name: 'An', role: 'Bạn' }],
} as SeriesPilotState;
const anShot = shot({
  id: 'SH03an',
  story: 'Minh nói với mẹ. Bạn An được nhắc.',
  characterIds: ['CHAR-001', 'CHAR-003', 'CHAR-004'],
  characters: ['CHAR-001', 'CHAR-003', 'CHAR-004'],
  dialogueSegmentIds: ['L1'],
});
const anCast = visibleFrameCast(anState, anShot, shots[0]!);
if (anCast.ids.includes('CHAR-004') || anCast.count !== 2) {
  fail.push(`An/CHAR-004 is off-frame, got ${anCast.names.join()} (${anCast.count})`);
}
if (anCast.names.some((n) => /^CHAR-/i.test(n))) fail.push('cast UI must show names not raw CHAR ids');
const enter = visibleFrameCast(
  state,
  shot({ id: 'SH03', story: 'Nam bước vào phòng ăn.', characterIds: ['CHAR-001', 'CHAR-002', 'CHAR-003'] }),
  shots[1]!,
);
if (!enter.ids.includes('CHAR-002')) fail.push('Action may add Nam when named');

const sh08three = shot({
  id: 'SH08k',
  story: 'Nam đứng đối diện Linh.',
  characterIds: ['CHAR-001', 'CHAR-002', 'CHAR-003'],
  characters: ['CHAR-001', 'CHAR-002', 'CHAR-003'],
  dialogueSegmentIds: ['L3'],
});
const stayState = {
  ...state,
  runs: {
    ...state.runs,
    SH08k: { status: 'keyframe_ready' as const, keyframeDataUrl: 'data:image/png;base64,nn' },
  },
} as SeriesPilotState;
const sh09parents = shot({
  id: 'SH09k',
  story: 'Linh đáp bố.',
  characterIds: ['CHAR-002', 'CHAR-003'],
  characters: ['CHAR-002', 'CHAR-003'],
  dialogueSegmentIds: ['L2'],
});
const stay = visibleFrameCast(stayState, sh09parents, sh08three);
if (!stay.ids.includes('CHAR-001') || stay.count !== 3) {
  fail.push(`after Nam entered keep 3 (Minh stays), got ${stay.ids.join()}`);
}
const twoAfterThree = visibleFrameCast(
  stayState,
  shot({
    id: 'SH07k',
    story: 'Minh nói với mẹ.',
    characterIds: ['CHAR-001', 'CHAR-003'],
    characters: ['CHAR-001', 'CHAR-003'],
    dialogueSegmentIds: ['L1'],
  }),
  sh08three,
);
if (twoAfterThree.ids.includes('CHAR-002') || twoAfterThree.count !== 2) {
  fail.push(`SH07 Linh+Minh must not keep Nam from prev 3, got ${twoAfterThree.ids.join()}`);
}
if (!/CAST SHRINK|only/i.test(peopleCountLock(twoAfterThree, 3))) {
  fail.push('count drop must CAST SHRINK');
}

const namShot = shot({
  id: 'SH08',
  story: 'Nam bước vào cửa.',
  characterIds: ['CHAR-002'],
  characters: ['CHAR-002'],
  dialogueSegmentIds: ['L3'],
});
const namCast = visibleFrameCast(state, namShot, shots[1]!);
if (!namCast.ids.includes('CHAR-002') || !namCast.fromDialogue.includes('CHAR-002')) {
  fail.push(`Nam speaking must be in frame, got ${namCast.ids.join()}`);
}
const namCard = compileShotSceneCard(state, namShot, shots[1]!);
if (!/Nam/i.test(namCard.spoken) || !/Anh về rồi/i.test(namCard.oneLiner)) fail.push(`card ${namCard.oneLiner}`);
if (/Anh về rồi/i.test(namCard.stillAction)) fail.push('still prompt must not include spoken line (Gemini paints captions)');
if (!/Nam/i.test(namCard.stillAction)) fail.push('still prompt must keep speaker name');
const glance = shot({
  id: 'SH01-09',
  story: 'Liếc nhìn con số 9 đúng nửa giây.',
  visual: 'Liếc nhìn con số 9 đúng nửa giây.',
});
const glanceCard = compileShotSceneCard(state, glance);
if (/face on camera|speak this line/i.test(`${glanceCard.visualSpec.purpose} ${glanceCard.stillAction}`)) {
  fail.push('card must not say face on camera');
}
if (glanceCard.visualSpec.framing !== 'INSERT') fail.push(`glance framing ${glanceCard.visualSpec.framing}`);
if (glanceCard.visualSpec.subjectKind !== 'prop') fail.push('glance subject is the paper');
if (glanceCard.visualSpec.primary?.name === 'Minh') fail.push('glance must not MCU Minh as primary');
const glancePrompt = seriesSceneStillPrompt({
  aspect: '16:9',
  visual: glanceCard.stillAction,
  action: glanceCard.stillAction,
  refs: [],
  peopleCount: 2,
  peopleNames: 'Minh, Linh',
  visualSpec: glanceCard.visualSpec,
});
if (!/Test paper|INSERT|PRIORITY 1|HARD BAN/i.test(glancePrompt)) fail.push('visual spec must compile INSERT into still prompt');
if (/every named person[\s\S]*complete face/i.test(glancePrompt)) {
  fail.push('INSERT glance must not force both full faces');
}
if (/exactly 2 FULL|exactly 2 people/i.test(glancePrompt)) {
  fail.push('INSERT glance must not CAST COUNT 2');
}
if (stripStillLettering('Spoken this shot (speaker on camera): Minh: Không... đề khó mà mẹ.').includes('Không')) {
  fail.push('stripStillLettering must drop quoted dialogue');
}
const stillPrompt = seriesSceneStillPrompt({
  aspect: '16:9',
  visual: namCard.stillAction,
  action: namCard.stillAction,
  refs: [],
  speakers: namCard.speakerNames.join(', '),
});
if (/Anh về rồi/i.test(stillPrompt)) fail.push('Gemini still prompt must not contain the spoken line');
if (!/HARD BAN/i.test(stillPrompt)) fail.push('still prompt must lead with text ban');
const twoStill = seriesSceneStillPrompt({
  aspect: '9:16',
  visual: 'Minh and Linh in the dining room',
  action: 'Minh glances at the phone.',
  refs: [],
  peopleCount: 2,
  peopleNames: 'Minh, Linh',
});
if (!/FACE LOCK|complete face/i.test(twoStill)) fail.push('9:16 two-shot must lock both faces');
if (!/VERTICAL BLOCKING|upper two-thirds/i.test(twoStill)) fail.push('9:16 two-shot must not be a cropped table');
const mcuStill = seriesSceneStillPrompt({
  aspect: '9:16',
  visual: 'Dining room, dim evening',
  action: glanceCard.visualSpec.shotAction,
  refs: [],
  peopleCount: 2,
  peopleNames: 'Minh, Linh',
  visualSpec: compileShotSceneCard(state, shot({
    id: 'SH01-05',
    story: 'Minh nói với mẹ.',
    characterIds: ['CHAR-001'],
    characters: ['CHAR-001'],
    dialogueSegmentIds: ['L1'],
  })).visualSpec,
});
if (!/PRIORITY 1|Minh/i.test(mcuStill)) fail.push('MCU still must lead with story priority');
if (/exactly 2 FULL|vertical two-shot/i.test(mcuStill)) fail.push('MCU 9:16 must not force a two-shot');
if (!/SPEAKER LOCK/i.test(namCard.blocking)) fail.push('card blocking must lock speaker');
if (!/family portrait/i.test(namCard.blocking)) fail.push('card must forbid family portrait');
if (!/dim/i.test(namCard.lighting)) fail.push(`lighting ${namCard.lighting}`);

const director = upsertSceneMaster(lockSceneMaster(state, 'SC01'), {
  sceneId: 'SC01',
  screenDirection: 'Minh left → mẹ right',
  coverage: 'Wide → Medium → Close',
  lens: '35mm',
});
const dirMaster = sceneMasterOf(director, 'SC01');
if (dirMaster.screenDirection !== 'Minh left → mẹ right') fail.push('Test 8: screen direction must persist');
const cont = continueScenePrompt(dirMaster, 'SH01', 'Minh đưa bài');
if (!/Preserve everything unless Shot Action/i.test(cont)) fail.push('Test 8: still must preserve unless action');
if (!/Screen direction lock/i.test(cont)) fail.push('Test 8: still must lock screen direction');

const finalBlock = fullEpisodeBlockReason(
  {
    ...state,
    scriptLocked: true,
    voiceLocked: true,
    shotGraphLocked: true,
    previewApproved: true,
    sceneMasters: { SC01: { ...dirMaster, locked: true } },
    runs: {
      SH01: { status: 'turbo_testing', previewUrl: 'https://x/a.mp4', kfApproved: true, keyframeDataUrl: 'data:image/png;base64,aa' },
    },
  } as SeriesPilotState,
  [shots[0]!],
);
if (!finalBlock) fail.push('Test 9: Final must block spoken RUNWAY_TTS');

if (fail.length) {
  console.error('SCENE FIRST FAIL');
  for (const f of fail) console.error(` - ${f}`);
  process.exit(1);
}
console.log('SCENE FIRST PASS · master', master.sceneId, master.time);
