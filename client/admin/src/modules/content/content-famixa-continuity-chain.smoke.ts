import { buildContinuityChain, compileContinuityI2v, inferTransitionType } from './content-famixa-continuity-chain';
import { i2vPromptIsEnglish } from './content-famixa-i2v-en';
import type { FamixaSeriesShot, SeriesPilotState } from './content-famixa-series';

function shot(partial: Partial<FamixaSeriesShot> & { id: string }): FamixaSeriesShot {
  return {
    scene: 'SC01',
    sceneId: 'SC01',
    shot: partial.id,
    clock: '5s',
    seconds: 5,
    story: 'Minh bước ra khỏi cổng.',
    visual: '',
    characters: ['CHAR-001'],
    characterIds: ['CHAR-001'],
    location: 'cổng trường',
    motionPrompt: '',
    motionPromptVi: '',
    status: 'story_locked',
    ...partial,
  };
}

const shots = [
  shot({ id: 'a', story: 'Minh bước ra khỏi cổng, cầm bài kiểm tra.' }),
  shot({ id: 'b', story: 'Minh nhìn điểm và cười.' }),
  shot({ id: 'c', story: 'Minh đưa bài. Linh nhận lấy.' }),
];

const state = {
  roles: [],
  runs: {},
  continuity: {
    id: 'L',
    episode: 'EP01',
    scene: 'SC01',
    characters: 'Minh',
    wardrobe: 'polo trắng cổ xanh',
    position: 'gần cổng',
    environment: 'Ngoài cổng trường. Chiều ấm.',
    camera: 'eye-level 35mm',
    performance: '',
    locked: true,
  },
  episode: {
    seriesCode: 'FAMIXA',
    seriesTitle: '',
    episode: 'EP01',
    title: '',
    premise: '',
    moral: '',
    ctaRule: '',
    shots,
  },
} as SeriesPilotState;

const fail: string[] = [];
const chain = buildContinuityChain(state, shots);
if (chain.length !== 3) fail.push(`chain ${chain.length}`);
if (chain[1]?.previousShotId !== 'a') fail.push('SH02 must link SH01');
if (chain[1]?.start.prop && !/bài|paper|kiểm/i.test(chain[1].start.prop + chain[0]!.end.prop)) {
  fail.push('SH02 should inherit test paper');
}
if (inferTransitionType(shots[2]!, shots[2]!.story, shots[1]) !== 'CUT_ON_ACTION') {
  fail.push('hand-off should be CUT_ON_ACTION');
}
const prompt = compileContinuityI2v(chain[1]!, 5);
if (!i2vPromptIsEnglish(prompt)) fail.push('I2V leaked Vietnamese');
if (!/START|first frame/i.test(prompt)) fail.push('missing START');
if (!/END/i.test(prompt)) fail.push('missing END');
if (!/no scream|no hug/i.test(prompt)) fail.push('acting law missing on I2V');

if (fail.length) {
  console.error('CONTINUITY CHAIN FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('CONTINUITY CHAIN PASS');
