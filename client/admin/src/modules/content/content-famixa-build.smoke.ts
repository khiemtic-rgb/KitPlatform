import { buildLooksEmpty, buildStatusOf, ensureBuildId, newSeriesBuild } from './content-famixa-build';
import { famixaMediaScope } from './content-famixa-media-scope';
import type { FamixaSeriesShot, SeriesPilotState } from './content-famixa-series';

const fail: string[] = [];
const empty = {
  roles: [],
  runs: {},
  characters: [{ id: 'CHAR-001', name: 'Minh' }],
} as SeriesPilotState;

if (buildStatusOf(empty) !== 'draft') fail.push('empty is draft');
if (!buildLooksEmpty(empty)) fail.push('empty looks empty');

const withId = ensureBuildId(empty);
if (!withId.buildId) fail.push('ensureBuildId');
if (famixaMediaScope() !== withId.buildId) fail.push('scope follows buildId');

const locked = { ...withId, scriptLocked: true, voiceLocked: true } as SeriesPilotState;
if (buildStatusOf(locked) !== 'voice_locked') fail.push('voice lock status');

const shot = { id: 'SH01' } as FamixaSeriesShot;
const prod = {
  ...locked,
  episode: { seriesCode: 'FAMIXA', seriesTitle: '', episode: 'EP01', title: 'Tám', premise: '', moral: '', ctaRule: '', shots: [shot] },
  runs: { SH01: { status: 'keyframe_ready', keyframeFileName: 'a.png' } },
} as SeriesPilotState;
if (buildStatusOf(prod) !== 'in_prod') fail.push('kf means in_prod');

const neu = newSeriesBuild(prod);
if (neu.buildId === prod.buildId) fail.push('new build must new id');
if ((neu.episode?.shots?.length ?? 0) !== 0) fail.push('new build has no shots');
if (neu.characters?.[0]?.id !== 'CHAR-001') fail.push('Canon stays');

if (fail.length) {
  console.error('SERIES BUILD FAIL');
  for (const f of fail) console.error(` - ${f}`);
  process.exit(1);
}
console.log('SERIES BUILD PASS');
