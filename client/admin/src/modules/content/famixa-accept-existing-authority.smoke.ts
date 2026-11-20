/** AUTHORITY-STABILIZATION-01C — acceptedTake is not Official Production authority. 0 providers. */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { acceptExistingTake } from './ContentFamixaShotProduction/ShotProductionArtifacts';
import {
  ACCEPT_EXISTING_AUTHORITY_NEGATIVE,
  ACCEPT_EXISTING_NON_AUTHORITY_NOTE,
  acceptExistingAuthorityEffect,
  decideOfficialAccept,
  officialArtifactId,
} from './famixa-accept-existing-authority';
import { slimPilotForServer, type SeriesPilotState, type SeriesShotRun } from './content-famixa-series';

const fail: string[] = [];
function ok(cond: boolean, name: string) {
  if (!cond) fail.push(name);
}

const here = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(here, name), 'utf8');
const src = (rel: string) => readFileSync(join(here, '../../../../../src', rel), 'utf8');

const VIDEO = 'b44e530b-5b59-422f-9b80-e058a3a639b5';
const MIX = '2f88e0a0-313a-47c6-8c1d-2dbb189bbbf2';
const GRAPH = 'https://take/56.mp4';

const effect = acceptExistingAuthorityEffect({
  acceptedTakeUrl: GRAPH,
  takeUrl: GRAPH,
  finalStoreUrl: 'idb://final/sh01',
  idbKey: 'kit-famixa-tts',
  videoTakeArtifactId: VIDEO,
  mixArtifactId: MIX,
});
ok(effect.officialPreview === false && effect.promotesGraphUrl === false, 'T1/T2 acceptedTake is not Official Preview');
ok(effect.classification === 'LEGACY_COMPATIBILITY', 'T3/T14 Accept Existing is non-authoritative');
ok(effect.createsProductionArtifact === false, 'T4/T12 acceptedTake does not create ProductionArtifact');
ok(effect.createsProductionBinding === false, 'T5/T13 acceptedTake does not create ProductionBinding');
ok(effect.refreezesInput === false, 'T11 no refreeze because acceptedTake exists');
ok(effect.promotesIdbOrFinalStore === false, 'T10 IDB/FinalStore cannot regain authority through acceptedTake');

const graphOnly = decideOfficialAccept({
  acceptedTakeUrl: GRAPH,
  takeUrl: GRAPH,
  videoTakeArtifactId: GRAPH,
  mixArtifactId: GRAPH,
});
ok(graphOnly.outcome === 'REFUSE' && officialArtifactId(GRAPH) == null, 'T7 no OS authority does not fallback to acceptedTake');
ok(graphOnly.createsOsAcceptedFromAcceptedTake === false, 'T7 refuse creates no binding');

const compat = acceptExistingTake({
  takeUrl: GRAPH,
  acceptedTake: { n: 56, url: GRAPH, acceptedAt: '2026-09-14T01:00:00.000Z' },
  runwayAttempts: [{ n: 56, status: 'SUCCEEDED', outputUrl: GRAPH }],
});
ok(Boolean(compat?.acceptedTake?.url) && !('artifactId' in (compat || {})), 'T9 compatibility object has no artifact id');

const videoAccept = decideOfficialAccept({ videoTakeArtifactId: VIDEO, acceptedTakeUrl: GRAPH, takeUrl: GRAPH });
ok(
  videoAccept.outcome === 'OS_ACCEPT' && videoAccept.artifactId === VIDEO && videoAccept.createsOsAcceptedFromAcceptedTake === false,
  'T6 valid OS ArtifactId stays an OS ACCEPT input',
);

const historical: SeriesShotRun = {
  status: 'turbo_testing',
  acceptedTake: { n: 56, url: GRAPH, acceptedAt: '2026-09-14T01:00:00.000Z' },
};
ok(historical.acceptedTake?.url === GRAPH, 'T9 historical acceptedTake remains on the run');

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
  runs: { SH01: historical },
};
const put = slimPilotForServer(state);
ok(put.runs.SH01?.acceptedTake == null && put.scriptLocked === true, 'T15 01A PUT still omits NEW acceptedTake');
ok(state.runs.SH01?.acceptedTake?.url === GRAPH, 'T9 in-memory history kept');

const workspace = read('ContentFamixaShotProduction/ShotProductionWorkspace.tsx');
const series = read('ContentFamixaSeriesTab.tsx');
const artifacts = read('ContentFamixaShotProduction/ShotProductionArtifacts.ts');
const authority = read('famixa-accept-existing-authority.ts');
const integrity = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/OfficialPreviewIntegrityV1.cs');
const create = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.cs');
const actions = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/ProductionCreateVideoService.Actions.cs');
const legacy = src('Packs/Content/KitPlatform.Packs.Content.Application/Production/Legacy/LegacySeriesGraphAdapter.cs');
const finalStore = read('ContentFamixaShotProduction/ShotProductionFinalStore.ts');

const click = series.slice(series.indexOf('const acceptExistingOfficial'), series.indexOf('const rememberLineAudio'));
ok(workspace.includes('looksLikeProductionShotCode(current.id) || osAcceptedVideo'), 'T1 official preview authority is OS-only');
ok(
  workspace.includes(': osPreviewAuthority') && workspace.includes('lipsyncUrl: undefined, finalUrl: undefined'),
  'T2 official empty preview does not use acceptedTake.url',
);
ok(!click.includes('executeProductionOsAction') && !click.includes('createProductionRequest'), 'T3 Accept Existing does not POST');
ok(!click.includes('acceptExistingTake') && !click.includes('patchRun'), 'T3 click does not stamp acceptedTake');
ok(click.includes(ACCEPT_EXISTING_NON_AUTHORITY_NOTE) || click.includes('ACCEPT_EXISTING_NON_AUTHORITY_NOTE'), 'T14 click is explicitly non-authoritative');
ok(actions.includes('await _os.AcceptArtifactAsync(artifact.ArtifactId, ct)'), 'T6 OS ACCEPT still binds ArtifactId');
ok(actions.includes('ProductionBindingRoles.AcceptedFor(artifact.Role)'), 'T6 OS ACCEPT still writes ACCEPTED');
ok(series.includes("acceptArtifactId: action === 'ACCEPT'"), 'T6 official handler still sends acceptArtifactId');
ok(!integrity.includes('acceptedTake') && !integrity.includes('takeUrl'), 'T8 SHA gate ignores acceptedTake');
ok(integrity.includes('ReadVerifiedPhysicalBytes') && integrity.includes('Sha256Hex'), 'T8 physical SHA gate remains');
ok(create.includes('ReadPreviewMediaAsync') && create.includes('OfficialPreviewIntegrityV1.ReadVerifiedPhysicalBytes'), 'T8 preview bytes use the SHA gate');
ok(create.includes('ReadCurrentVoiceMediaAsync') && create.split('ReadCurrentVoiceMediaAsync')[1]?.includes('ReadVerifiedPhysicalBytes'), 'T8 voice bytes use the same SHA gate');
ok(legacy.includes('MayDriveNewBinding(string field) => false'), 'T5/T7 graph acceptedTake cannot drive a binding');
ok(legacy.includes('SeriesShotRun.acceptedTake.url'), 'T9 field remains catalogued as legacy');
ok(!click.includes('kit-famixa-shot-final') && !click.includes('kit-famixa-tts'), 'T10 click does not read IDB or FinalStore');
ok(finalStore.includes('kit-famixa-shot-final'), 'T10 FinalStore remains a non-official store');
ok(!click.includes('freezeMotion') && !click.includes('OpenFrozen') && !click.includes('Refreeze'), 'T11 click does not refreeze');
ok(artifacts.includes('acceptedTake?: AcceptedTake') && artifacts.includes(ACCEPT_EXISTING_AUTHORITY_NEGATIVE), 'T9/T14 helper stays compatibility');
ok(authority.includes(ACCEPT_EXISTING_AUTHORITY_NEGATIVE), 'NEGATIVE acceptedTake ≠ official accept authority');
ok((series.match(/onAcceptExisting=\{acceptExistingOfficial\}/g) || []).length === 2, 'both mounts use the non-authority click');

if (fail.length) {
  console.error(`FAMIXA_ACCEPT_EXISTING_AUTHORITY_01C FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_ACCEPT_EXISTING_AUTHORITY_01C PASS FAIL=0 (no provider)');
