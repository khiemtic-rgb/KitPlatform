/** FAMIXA_SERIES_GRAPH_MEDIA_WRITE_CUTOVER_01A — PUT projection only. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  GRAPH_MEDIA_AUTHORITY_KEYS,
  omitGraphMediaAuthorityFromRun,
  slimPilotForServer,
  slimPilotForStorage,
  type SeriesPilotState,
  type SeriesShotRun,
} from './content-famixa-series';

const fail: string[] = [];
function ok(cond: boolean, name: string) {
  if (!cond) fail.push(name);
}

const here = dirname(fileURLToPath(import.meta.url));
const mediaRun: SeriesShotRun = {
  status: 'turbo_testing',
  takeUrl: 'https://take/new.mp4',
  previewUrl: 'https://take/new.mp4',
  lipsyncUrl: 'https://fal/new.mp4',
  acceptedTake: { n: 9, url: 'https://take/new.mp4', acceptedAt: '2026-09-24T00:00:00.000Z' },
  localVideoPath: 'E:/new.mp4',
  turboTaskId: 'turbo-new',
  lipsyncTaskId: 'lip-new',
  runwayAttempts: [{ n: 9, at: '2026-09-24T00:00:00.000Z', outputUrl: 'https://take/out.mp4' }],
  shotAction: 'turns',
};

const state: SeriesPilotState = {
  roles: [],
  scriptLocked: true,
  episode: {
    seriesCode: 'FAMIXA',
    seriesTitle: 'Famixa',
    episode: 'EP01',
    title: 'EP01',
    premise: '',
    moral: '',
    ctaRule: '',
    shots: [],
  },
  runs: { SH01: mediaRun },
};

const storage = slimPilotForStorage(state);
ok(storage.runs.SH01?.takeUrl === mediaRun.takeUrl, 'T10 storage / hydrate keeps takeUrl');
ok(storage.runs.SH01?.acceptedTake?.url === mediaRun.acceptedTake?.url, 'T10 storage keeps acceptedTake');

const put = slimPilotForServer(state);
ok(put.scriptLocked === true && put.episode?.title === 'EP01', 'T1/T9 script still on PUT');
ok(put.runs.SH01?.shotAction === 'turns', 'T9 run script field still on PUT');
ok(put.runs.SH01?.takeUrl == null, 'PUT omits takeUrl');
ok(put.runs.SH01?.previewUrl == null, 'PUT omits previewUrl');
ok(put.runs.SH01?.lipsyncUrl == null, 'PUT omits lipsyncUrl');
ok(put.runs.SH01?.acceptedTake == null, 'PUT omits acceptedTake');
ok(put.runs.SH01?.turboTaskId == null && put.runs.SH01?.runwayAttempts == null, 'PUT omits turbo/output pointers');
ok(state.runs.SH01?.takeUrl === mediaRun.takeUrl, 'in-memory run unchanged');

const omitted = omitGraphMediaAuthorityFromRun(mediaRun);
for (const key of GRAPH_MEDIA_AUTHORITY_KEYS) {
  ok(!(key in omitted) || (omitted as Record<string, unknown>)[key] == null, `omit ${key}`);
}

const tab = readFileSync(join(here, 'ContentFamixaSeriesTab.tsx'), 'utf8');
ok(tab.includes('slimPilotForServer'), 'queueServerSave still uses slimPilotForServer');

if (fail.length) {
  console.error(`FAIL ${fail.length}\n${fail.map((f) => `- ${f}`).join('\n')}`);
  process.exit(1);
}
console.log('PASS FAMIXA_SERIES_GRAPH_MEDIA_WRITE_CUTOVER_01A');
