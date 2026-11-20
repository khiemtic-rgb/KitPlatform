/** FAMIXA_PRODUCTION_OS_SHOT_IDENTITY_ROUTING_V1 */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  PRODUCTION_OS_TEST_SCOPE_ID,
  formatShotIdentityError,
  isProductionOsTestBuild,
  looksLikeProductionShotCode,
  productionOsBuildIdForShot,
  productionOsDeskShots,
  productionOsTestShotStub,
  resolveShotWorkspaceIdentity,
  SHOT_IDENTITY_MISMATCH,
  SHOT_IDENTITY_NOT_FOUND,
} from './production-os-action-authority';
import type { FamixaSeriesShot } from './content-famixa-series';
import { resolveLaneAShotId } from './ContentFamixaShotProduction/ShotProductionNavigator';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(root, name), 'utf8');
const series = read('ContentFamixaSeriesTab.tsx');
const workspace = read('ContentFamixaShotProduction/ShotProductionWorkspace.tsx');
const nav = read('ContentFamixaShotProduction/ShotProductionNavigator.tsx');

const OFFICIAL_BUILD = '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f';
const TEST_BUILD = PRODUCTION_OS_TEST_SCOPE_ID;
const OFFICIAL_SH02 = 'EP01-SC01-SH02';
const TEST_SH02 = 'TEST-SC01-SH02';

const officialSh02 = {
  id: OFFICIAL_SH02,
  scene: 'SC01',
  shot: '02',
  clock: '0:00',
  seconds: 10,
  story: 'Official SH02 story',
  visual: 'Official visual',
  characters: ['CHAR-001'],
  location: 'nhà',
  motionPrompt: 'official',
  motionPromptVi: 'official',
  status: 'story_locked' as const,
  sceneId: 'SC01',
  characterIds: ['CHAR-001'],
  dialogueSegmentIds: [],
} satisfies FamixaSeriesShot;

const officialDesk = productionOsDeskShots(OFFICIAL_BUILD, [officialSh02]);
const testDesk = productionOsDeskShots(TEST_BUILD, [officialSh02]);

ok(!officialDesk.some((s) => s.id === TEST_SH02) && officialDesk.some((s) => s.id === OFFICIAL_SH02), 'A official desk has EP01 SH02 only');
ok(resolveLaneAShotId(officialDesk, OFFICIAL_SH02) === OFFICIAL_SH02, 'A resolve official SH02');
ok(productionOsBuildIdForShot(OFFICIAL_BUILD, OFFICIAL_SH02) === OFFICIAL_BUILD, 'A API build stays official');

ok(
  resolveShotWorkspaceIdentity({
    requestedBuildId: OFFICIAL_BUILD,
    requestedShotId: OFFICIAL_SH02,
    loadedBuildId: OFFICIAL_BUILD,
    loadedShotId: OFFICIAL_SH02,
  }).ok,
  'B deep-link official pair accepted',
);

ok(testDesk.every((s) => s.id.startsWith('TEST-')) && !testDesk.some((s) => s.id === OFFICIAL_SH02), 'C TEST desk stays TEST');
ok(productionOsBuildIdForShot(TEST_BUILD, TEST_SH02) === TEST_BUILD, 'C TEST pair stays TEST');

ok(resolveLaneAShotId(testDesk, TEST_SH02) === TEST_SH02, 'D switch to TEST SH02');
ok(resolveLaneAShotId(officialDesk, OFFICIAL_SH02) === OFFICIAL_SH02, 'E switch back to official SH02');

ok(resolveLaneAShotId(officialDesk, 'EP01-SC01-SH99') === undefined, 'F missing shot is not first/latest');
ok(
  resolveShotWorkspaceIdentity({
    requestedBuildId: OFFICIAL_BUILD,
    requestedShotId: 'EP01-SC01-SH99',
    loadedBuildId: OFFICIAL_BUILD,
    loadedShotId: OFFICIAL_SH02,
  }).reason === SHOT_IDENTITY_MISMATCH,
  'F loaded other shot is mismatch',
);

ok(productionOsBuildIdForShot(TEST_BUILD, OFFICIAL_SH02) === undefined, 'G official shot on TEST build fail-closed');
ok(
  resolveShotWorkspaceIdentity({
    requestedBuildId: TEST_BUILD,
    requestedShotId: OFFICIAL_SH02,
  }).reason === SHOT_IDENTITY_MISMATCH,
  'G pair mismatch',
);

ok(looksLikeProductionShotCode(OFFICIAL_SH02) && looksLikeProductionShotCode(TEST_SH02), 'H shot-code shape');
ok(isProductionOsTestBuild(TEST_BUILD) && !isProductionOsTestBuild(OFFICIAL_BUILD), 'H build isolation');

const mismatch = resolveShotWorkspaceIdentity({
  requestedBuildId: OFFICIAL_BUILD,
  requestedShotId: OFFICIAL_SH02,
  loadedBuildId: TEST_BUILD,
  loadedShotId: TEST_SH02,
});
ok(!mismatch.ok && mismatch.reason === SHOT_IDENTITY_MISMATCH, 'I mismatch detected');
ok(
  formatShotIdentityError(mismatch).includes(OFFICIAL_BUILD) &&
    formatShotIdentityError(mismatch).includes(OFFICIAL_SH02) &&
    formatShotIdentityError(mismatch).includes(TEST_BUILD) &&
    formatShotIdentityError(mismatch).includes(TEST_SH02),
  'I error lists requested + loaded',
);

ok(series.includes('productionOsDeskShots(state.buildId, shots)'), 'desk shots scoped to open build');
ok(series.includes('setStudioShotId(undefined)'), 'openBuild clears stale shot');
ok(series.includes('setSearchParams({ build, shot }'), 'H/I URL keeps BuildId+ShotId');
ok(series.includes('resolveShotWorkspaceIdentity'), 'SeriesTab identity guard');
ok(!series.includes('[...productionOsTestShotStubs(), ...shots.filter'), 'TEST stubs not prepended onto every film');
ok(workspace.includes('data-shot-identity-error') && workspace.includes('SHOT_NOT_FOUND'), 'workspace fail-closed');
ok(nav.includes('looksLikeProductionShotCode(want)'), 'navigator does not remap shot codes');
ok(productionOsBuildIdForShot(OFFICIAL_BUILD, TEST_SH02) === undefined, 'no silent official→TEST remap');
ok(resolveShotWorkspaceIdentity({ requestedBuildId: '', requestedShotId: OFFICIAL_SH02 }).reason === SHOT_IDENTITY_NOT_FOUND, 'empty build not found');
ok(productionOsTestShotStub(TEST_SH02).story === 'TEST fixture story', 'TEST stub story stays isolated');

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_OS_SHOT_IDENTITY_ROUTING_V1 FAIL=${fail.length}`);
  fail.forEach((name) => console.error(' - ' + name));
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_OS_SHOT_IDENTITY_ROUTING_V1 PASS FAIL=0');
