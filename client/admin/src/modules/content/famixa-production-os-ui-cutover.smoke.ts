/** FAMIXA_PRODUCTION_OS_UI_CUTOVER_V1 */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { ProductionShotActions } from '../../shared/api/content.api';
import {
  OS_ACTION_FOR_DESK,
  PRODUCTION_OS_CTA,
  PRODUCTION_OS_TEST_SCOPE_ID,
  applyProductionOsActionAuthority,
  isProductionOsTestShot,
  mayExecuteProductionAction,
  mayAuthorizeTenantEp01Image,
  mayMutateProductionOs,
  mayPostProductionEpisode,
  productionActionForDeskCta,
  productionOsBuildIdForShot,
  productionOsIdempotencyKey,
  productionOsLegacyRefuseNote,
  visibleProductionOsCatalogActions,
} from './production-os-action-authority';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(root, name), 'utf8');
const series = read('ContentFamixaSeriesTab.tsx');
const workspace = read('ContentFamixaShotProduction/ShotProductionWorkspace.tsx');
const api = read('../../shared/api/content.api.ts');
const execBlock = series.slice(series.indexOf('const executeProductionOsAction'), series.indexOf('const rememberLineAudio'));

const catalogOf = (shotCode: string, rows: ProductionShotActions['actions']): ProductionShotActions => ({
  buildId: PRODUCTION_OS_TEST_SCOPE_ID,
  shotCode,
  currentState: 'VISIBLE',
  actions: rows,
});

const sh02 = catalogOf('TEST-SC01-SH02', [
  { action: 'CREATE_IMAGE', uiCta: 'Tạo hình', available: true },
  { action: 'CREATE_VIDEO', uiCta: 'Tạo video', available: true },
  { action: 'CREATE_VOICE', uiCta: 'Tạo thoại', available: true },
  { action: 'CREATE_LIPSYNC', uiCta: 'Khớp môi', available: true },
  { action: 'MIX_VIDEO', uiCta: 'Hoàn thiện Shot', available: true },
  { action: 'ACCEPT', uiCta: 'Dùng video này', available: true },
  { action: 'ASSEMBLE', uiCta: 'Lắp tập', available: true },
]);

const desk = applyProductionOsActionAuthority(
  {
    pendingApproval: false,
    phase: 'finish' as const,
    note: '',
    primary: { label: 'Hoàn thiện Shot', action: 'mix' as const },
    secondary: { label: 'Khớp môi', action: 'lipsync' as const },
    tertiary: { label: 'Dùng video này', action: 'use-current-take' as const },
  },
  sh02,
);

ok(desk.primary?.action === 'os-create-mix' && desk.primary.label === PRODUCTION_OS_CTA.MIX_VIDEO, 'mix CTA → MIX_VIDEO');
ok(desk.secondary?.action === 'os-create-lipsync' && desk.secondary.label === PRODUCTION_OS_CTA.CREATE_LIPSYNC, 'lipsync CTA → CREATE_LIPSYNC');
ok(desk.tertiary?.action === 'os-accept' && desk.tertiary.label === PRODUCTION_OS_CTA.ACCEPT, 'accept CTA → ACCEPT');
ok(productionActionForDeskCta({ label: 'Tạo hình mới', action: 'picture' }) === 'CREATE_IMAGE', 'Tạo hình → CREATE_IMAGE');
ok(productionActionForDeskCta({ label: 'Nạp thoại', action: 'os-create-voice' }) === 'CREATE_VOICE', 'Nạp thoại → CREATE_VOICE');
ok(productionActionForDeskCta({ label: 'Tạo lại video', action: 'motion' }) === 'REGENERATE', 'Tạo lại → REGENERATE');
ok(OS_ACTION_FOR_DESK['os-create-video'] === 'CREATE_VIDEO', 'desk 1:1 CREATE_VIDEO');
ok(mayExecuteProductionAction(sh02, 'CREATE_LIPSYNC') && mayExecuteProductionAction(sh02, 'ASSEMBLE'), 'availability from catalog');
ok(!mayExecuteProductionAction(undefined, 'CREATE_VIDEO'), 'missing catalog fail-closed');
ok(isProductionOsTestShot('TEST-SC01-SH02') && !isProductionOsTestShot('EP01-SC01-SH01'), 'TEST shot isolation');
ok(productionOsBuildIdForShot(PRODUCTION_OS_TEST_SCOPE_ID, 'TEST-SC01-SH02') === PRODUCTION_OS_TEST_SCOPE_ID, 'TEST uses TEST build');
ok(productionOsBuildIdForShot('4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f', 'TEST-SC01-SH02') === undefined, 'official build does not remap to TEST');
ok(!mayMutateProductionOs('4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f', 'EP01-SC01-SH01'), 'Film POST forbidden');
ok(!mayAuthorizeTenantEp01Image('4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f', 'EP01-SC01-SH01'), 'official film image stays closed');
ok(mayAuthorizeTenantEp01Image('30ba4061-a443-4c0d-85aa-1d13ff4fdedb', 'EP01-SC01-SH01'), 'tenant SH01 image can be authorized');
ok(!mayAuthorizeTenantEp01Image('30ba4061-a443-4c0d-85aa-1d13ff4fdedb', 'EP01-SC01-SH03'), 'SH03 image stays closed');
ok(PRODUCTION_OS_CTA.ASSEMBLE_EPISODE === 'Lắp tập phim', 'episode CTA mapped');
ok(!mayMutateProductionOs('4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f', 'EP01'), 'episode POST still blocked');
ok(!mayPostProductionEpisode('4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f'), 'official film episode stays blocked');
ok(mayPostProductionEpisode('30ba4061-a443-4c0d-85aa-1d13ff4fdedb'), 'tenant episode post allowed');
ok(execBlock.includes("action === 'ASSEMBLE_EPISODE'") && execBlock.includes('mayPostProductionEpisode'), 'Director posts tenant ASSEMBLE_EPISODE');
ok(execBlock.includes('EPISODE_ASSEMBLY_ORDER') && execBlock.includes("productionNodeArtifactId(row, 'ASSEMBLY')"), 'episode parents are CURRENT:ASSEMBLY ids');
ok(mayMutateProductionOs(PRODUCTION_OS_TEST_SCOPE_ID, 'TEST-SC01-SH02'), 'TEST POST allowed');
ok(productionOsIdempotencyKey('CREATE_VIDEO', PRODUCTION_OS_TEST_SCOPE_ID, 'TEST-SC01-SH02') === `os-ui:CREATE_VIDEO:${PRODUCTION_OS_TEST_SCOPE_ID}:TEST-SC01-SH02`, 'idempotency keyed');

ok(execBlock.includes('createProductionRequest'), 'execute POSTs /production/requests');
ok(execBlock.includes("action === 'CREATE_IMAGE'") || execBlock.includes('action,'), 'execute sends Action');
ok(execBlock.includes('motionArtifactId') && execBlock.includes('voiceArtifactId'), 'lipsync parents are artifactIds');
ok(execBlock.includes("action === 'REGENERATE'") && execBlock.includes('motionId'), 'REGENERATE sends prior VIDEO_TAKE artifactId');
ok(execBlock.includes('lipsyncArtifactId') && execBlock.includes('MISSING_LIPSYNC'), 'mix requires LIPSYNC artifactId');
ok(execBlock.includes('acceptArtifactId'), 'accept uses artifactId');
os(execBlock.includes('assemblyArtifactIds'), 'assemble uses artifactId list');
ok(execBlock.includes('PROVIDER_UNAVAILABLE') && execBlock.includes('FILM_PROTECTED'), 'Film refused');
ok(execBlock.includes('osInflightRef') && execBlock.includes('idempotencyKey'), 'double-click lock + idempotency');
ok(!execBlock.includes('startContentSeriesTurbo') && !execBlock.includes('/series/turbo'), 'OS handler no turbo');
ok(!execBlock.includes('generateContentSeriesStill') && !execBlock.includes('/series/still'), 'OS handler no still');
ok(!execBlock.includes('startContentSeriesLipsync') && !execBlock.includes('/series/lipsync'), 'OS handler no lipsync');
ok(!execBlock.includes('assembleContentSeriesCut'), 'OS handler no assemble');
ok(!execBlock.includes('acceptedTake.url') && !execBlock.includes('previewUrl') && !execBlock.includes('takeUrl'), 'OS handler no URL identity');

ok(series.includes('productionOsLegacyRefuseNote(\'CREATE_VIDEO\', \'POST /series/turbo\')'), 'sendTurbo refused');
ok(series.includes('productionOsLegacyRefuseNote(\'CREATE_IMAGE\', \'POST /series/still\')'), 'still refused');
ok(series.includes('productionOsLegacyRefuseNote(\'CREATE_LIPSYNC\', \'POST /series/lipsync\')'), 'lipsync refused');
ok(series.includes('productionOsLegacyRefuseNote(\'MIX_VIDEO\', \'POST /content/series/assemble\')'), 'mix refused');
ok(series.includes('productionOsLegacyRefuseNote(\'ASSEMBLE\', \'POST /content/series/assemble\')'), 'export assemble refused');
ok(series.includes('productionOsLegacyRefuseNote(\'CREATE_VOICE\''), 'voice refused');
ok(series.includes('getProductionShotState') && series.includes('osStateByShot'), 'hydrate from OS state');
ok(workspace.includes('OS_ACTION_FOR_DESK') && workspace.includes('onExecuteOsAction?.('), 'desk os-* → OS action');
ok(workspace.includes('visibleProductionOsCatalogActions'), 'catalog extras render available OS CTAs');
ok(
  visibleProductionOsCatalogActions(sh02).some((row) => row.action === 'CREATE_VOICE' && row.label === 'Nạp thoại')
    && visibleProductionOsCatalogActions(sh02).some((row) => row.action === 'ACCEPT' && row.label === 'Dùng kết quả'),
  'catalog labels cover Nạp thoại / Dùng kết quả',
);
ok(workspace.includes('osPreviewAuthority'), 'TEST preview is VISIBLE only');
ok(workspace.includes('osPreviewAuthority') && workspace.includes('motionUrl: undefined'), 'TEST empty preview is EMPTY');
ok(api.includes('LEGACY / NON-AUTHORITY') && api.includes("'/content/series/still'"), 'legacy endpoints classified');
ok(api.includes('createProductionRequest') && api.includes('getProductionShotState'), 'OS client present');
ok(productionOsLegacyRefuseNote('CREATE_VIDEO', 'POST /series/turbo').includes('NON-AUTHORITY'), 'refuse copy');

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_OS_UI_CUTOVER_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_OS_UI_CUTOVER_V1 PASS FAIL=0 (no provider)');

function os(cond: boolean, name: string) {
  ok(cond, name);
}
