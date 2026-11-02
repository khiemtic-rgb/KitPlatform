/** AUTHORITY-STABILIZATION-01D gap closure. Editorial checklist and inventory counts are not official production progress. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { SHOT_PROGRESS_ROWS_AUTHORITY, shotProgressRows } from './ContentFamixaShotProduction/ShotProductionProgress';
import type { ShotProductionSnapshot } from './ContentFamixaShotProduction/ShotProductionState';
import {
  STUDIO_BUILD_COUNTS_AUTHORITY,
  studioBuildCounts,
  videoCardProgress,
  videoCardStatus,
} from './content-famixa-series-desk';
import { deriveSeriesTrack } from './kit-video-production-ux';
import { isProgressComplete, studioCardFromProgress } from './kit-video-production-progress';

const fail: string[] = [];
function ok(cond: boolean, name: string) {
  if (!cond) fail.push(name);
}

const here = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(here, name), 'utf8');
const src = (rel: string) => readFileSync(join(here, '../../../../../src', rel), 'utf8');

const view = read('kit-video-production-progress.ts');
const track = read('kit-video-production-ux.ts');
const desk = read('content-famixa-series-desk.ts');
const series = read('ContentFamixaSeriesTab.tsx');
const rows = read('ContentFamixaShotProduction/ShotProductionProgress.tsx');
const service = src('Packs/Content/KitPlatform.Packs.Content.Infrastructure/ProductionProgressService.cs');
const rules = src('Packs/Content/KitPlatform.Packs.Content.Application/ProductionProgressRules.cs');
const cutover = src('Packs/Content/KitPlatform.Packs.Content.Application/SeriesGraphMediaWriteCutover.cs');
const integrity = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/OfficialPreviewIntegrityV1.cs');
const accept = read('famixa-accept-existing-authority.ts');

const editorial = {
  isSilent: false,
  voiceReady: true,
  voiceStale: false,
  keyframeApproved: true,
  keyframeStale: false,
  picturePendingApproval: false,
  motionReady: true,
  motionBusy: false,
  motionStale: false,
  lipSyncReady: true,
  lipsyncBusy: false,
  qaReady: true,
  lipSyncStale: false,
  mixReady: true,
  mixStale: false,
  finalArtifactReady: true,
} as ShotProductionSnapshot;
const marks = Object.fromEntries(shotProgressRows(editorial).map((row) => [row.id, row.mark]));
const inventory = { status: 'final', shotCount: 6, kfCount: 6, videoCount: 6, voiceLines: 6 } as never;

ok(!view.includes('takeUrl') && !view.includes('lipsyncUrl') && !view.includes('previewUrl') && !view.includes('keyframeDataUrl'), 'T1 official progress view has no graph media URLs');
ok(!view.includes('kfCount') && !view.includes('videoCount'), 'T2 official progress view has no graph counts');
ok(!view.includes('video-generation-execution') && !view.includes('GetLatestAsync') && !service.includes('VideoGenerationExecutionRepository') && !service.includes('_vge'), 'T3/T14 official progress does not read VGE');
ok(SHOT_PROGRESS_ROWS_AUTHORITY === 'EDITORIAL' && rows.includes("SHOT_PROGRESS_ROWS_AUTHORITY = 'EDITORIAL'"), 'T4 shot checklist is editorial');
ok(marks.video === '✓' && marks.final === '✓' && !isProgressComplete(undefined) && studioCardFromProgress(undefined).tone !== 'done', 'T5 editorial marks cannot complete official production');
ok(!rows.includes('ListActiveBindingsAsync') && !view.includes('shotProgressRows'), 'T6 editorial checklist is not the OS progress reader');
ok(STUDIO_BUILD_COUNTS_AUTHORITY === 'INVENTORY' && desk.includes("STUDIO_BUILD_COUNTS_AUTHORITY = 'INVENTORY'"), 'T7 build counts are inventory');
ok(studioBuildCounts(inventory) === 'Thoại 6 · Hình 6/6 · Video 6/6' && videoCardProgress(inventory) === 0 && videoCardStatus(inventory).tone !== 'done', 'T8 full inventory counts cannot complete official production');
ok(!desk.slice(desk.indexOf('export function videoCardProgress'), desk.indexOf('export function videoCardChecks')).includes('kfCount'), 'T9 official percent does not read inventory counts');
ok(service.includes('ApplyOfficialOsProgress') && rules.includes('An empty list clears production flags'), 'T10 01D OS progress path remains');
ok(cutover.includes('acceptedTake'), 'T11 01A media list remains');
ok(integrity.includes('ReadVerifiedPhysicalBytes'), 'T12 01B SHA gate remains');
ok(accept.includes('acceptedTake ≠ official accept authority'), 'T13 01C acceptedTake stays non-authority');
ok(!rules.includes('class GenericProgress') && !service.includes('CREATE TABLE') && !view.includes('class GenericProgress'), 'T15 no new progress subsystem');
ok(!series.includes('Math.max(workflow?.imageProgress') && !series.includes('Math.max(workflow?.videoProgress'), 'series track does not raise OS counts from graph URLs');
const trackBody = track.slice(track.indexOf('export function deriveSeriesTrack'), track.indexOf('export const EPISODE_PIPELINE'));
ok(!trackBody.includes('kfCount') && !trackBody.includes('videoCount') && !trackBody.includes('takeUrl'), 'deriveSeriesTrack does not read graph counts or media URLs');
ok(deriveSeriesTrack({
  scriptLocked: true,
  sceneCount: 1,
  shotCount: 1,
  shotGraphLocked: true,
  characterLocked: true,
  prepDone: 1,
  shotTotal: 1,
  imagesMade: 0,
  imagesReviewed: 0,
  videosMade: 0,
  videosReviewed: 0,
  videosDone: 0,
  voiceReady: true,
  finalReady: false,
}).nodes.find((node) => node.id === 'video')?.done === false, 'graph-complete inputs do not mark the video stage done');

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_PROGRESS_GAP_CLOSURE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_PROGRESS_GAP_CLOSURE PASS FAIL=0 (no provider)');
