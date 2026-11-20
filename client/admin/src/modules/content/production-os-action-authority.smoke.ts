/** FAMIXA_PRODUCTION_OS_CREATE_VIDEO_ACTION_AUTHORITY_CUTOVER_FIX_V1 */

import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import type { ProductionShotActions } from '../../shared/api/content.api';
import {
  PRODUCTION_OS_CTA,
  applyProductionOsActionAuthority,
  isOsActionAvailable,
  mayExecuteProductionAction,
  osActionRow,
} from './production-os-action-authority';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (name: string) => readFileSync(join(root, name), 'utf8');
const series = read('ContentFamixaSeriesTab.tsx');
const workspace = read('ContentFamixaShotProduction/ShotProductionWorkspace.tsx');
const cta = read('ContentFamixaShotProduction/ShotProductionCta.ts');
const authority = read('production-os-action-authority.ts');
const motionBlock = series.slice(series.indexOf('const startShotMotion'), series.indexOf('const executeProductionOsAction'));
const execBlock = series.slice(series.indexOf('const executeProductionOsAction'), series.indexOf('const rememberLineAudio'));

const catalogOf = (
  shotCode: string,
  rows: ProductionShotActions['actions'],
): ProductionShotActions => ({
  buildId: '4ef164d6-10aa-42fd-b3d2-acff9b3d2c7f',
  shotCode,
  currentState: 'EMPTY',
  actions: rows,
});

const sh02 = catalogOf('EP01-SC01-SH02', [
  {
    action: 'CREATE_VIDEO',
    uiCta: 'Tạo video',
    available: false,
    blockReason: 'MISSING_DEPENDENCY',
    requiredInputs: ['KEYFRAME'],
  },
  { action: 'CREATE_IMAGE', uiCta: 'Tạo hình', available: true },
]);

const sh01 = catalogOf('EP01-SC01-SH01', [
  { action: 'CREATE_VIDEO', uiCta: 'Tạo video', available: true },
  { action: 'CREATE_IMAGE', uiCta: 'Tạo hình', available: false },
]);

const legacyDesk = {
  pendingApproval: false,
  phase: 'motion' as const,
  note: 'Hình đã duyệt.',
  primary: { label: 'Tạo video', action: 'motion' as const },
  secondary: { label: 'Tạo hình', action: 'picture' as const },
};

const unavailableDesk = applyProductionOsActionAuthority(legacyDesk, sh02);
ok(!unavailableDesk.primary || unavailableDesk.primary.action !== 'os-create-video', 'A unavailable action → no CREATE_VIDEO CTA');
ok(unavailableDesk.primary?.label !== 'Tạo video' && unavailableDesk.secondary?.label !== 'Tạo video', 'A no Tạo video label');
ok(!isOsActionAvailable(sh02, 'CREATE_VIDEO'), 'A SH02 CREATE_VIDEO.available=false');
ok((unavailableDesk.note || '').includes('MISSING_DEPENDENCY'), 'A dependency state from contract');

ok(!mayExecuteProductionAction(sh02, 'CREATE_VIDEO'), 'B unavailable action → no execution');
ok(!mayExecuteProductionAction(undefined, 'CREATE_VIDEO'), 'B missing catalog fail-closed');
ok(!mayExecuteProductionAction({ ...sh02, actions: [] }, 'CREATE_VIDEO'), 'B empty catalog fail-closed');
ok(!mayExecuteProductionAction(sh02, 'UNKNOWN'), 'B unknown action fail-closed');

const availableDesk = applyProductionOsActionAuthority(legacyDesk, sh01);
ok(availableDesk.primary?.action === 'os-create-video' && availableDesk.primary.label === PRODUCTION_OS_CTA.CREATE_VIDEO, 'C available action → exact CREATE_VIDEO CTA');
ok(mayExecuteProductionAction(sh01, 'CREATE_VIDEO'), 'C available → execute exact action');
ok(osActionRow(sh01, 'CREATE_VIDEO')?.action === 'CREATE_VIDEO', 'D CREATE_VIDEO never remaps');
ok(PRODUCTION_OS_CTA.CREATE_VIDEO === 'Tạo video', 'D CTA map CREATE_VIDEO');
ok(PRODUCTION_OS_CTA.CREATE_IMAGE === 'Tạo hình', 'D CTA map CREATE_IMAGE');
ok(PRODUCTION_OS_CTA.CREATE_VOICE === 'Tạo thoại', 'D CTA map CREATE_VOICE');
ok(PRODUCTION_OS_CTA.CREATE_LIPSYNC === 'Khớp môi', 'D CTA map CREATE_LIPSYNC');
ok(PRODUCTION_OS_CTA.MIX_VIDEO === 'Hoàn thiện', 'D CTA map MIX_VIDEO');
ok(PRODUCTION_OS_CTA.ACCEPT === 'Dùng video này', 'D CTA map ACCEPT');

ok(!motionBlock.includes('createProductionVideo'), 'E startShotMotion does not execute CREATE_VIDEO');
ok(!motionBlock.includes('sendTurbo') && !motionBlock.includes('startContentSeriesTurbo'), 'E startShotMotion does not sendTurbo');
ok(execBlock.includes('mayExecuteProductionAction') && execBlock.includes('createProductionRequest'), 'E executeAction gated');
ok(execBlock.includes("result.action !== action"), 'E POST action must match');
ok(workspace.includes("if (action === 'motion') return"), 'E legacy motion click is no-op');
ok(workspace.includes('OS_ACTION_FOR_DESK') && workspace.includes('onExecuteOsAction?.('), 'E CTA click is executeAction');
ok(cta.includes('This no longer enables Tạo video'), 'E applyOsCreateVideoCta is no-op');
ok(authority.includes('GET /actions says available === true'), 'E authority is GET /actions');

const sh01Desk = applyProductionOsActionAuthority(legacyDesk, sh01);
const sh02Desk = applyProductionOsActionAuthority(legacyDesk, sh02);
ok(sh01Desk.primary?.action === 'os-create-video' && sh02Desk.primary?.action !== 'os-create-video', 'F SH01/SH02 action state independent');
ok(mayExecuteProductionAction(sh01, 'CREATE_VIDEO') && !mayExecuteProductionAction(sh02, 'CREATE_VIDEO'), 'F catalogs do not leak');
ok(unavailableDesk.primary?.action === 'os-create-image' && unavailableDesk.primary?.label === 'Tạo hình', 'F SH02 Tạo hình from CREATE_IMAGE');
ok(!availableDesk.primary || availableDesk.primary.action !== 'os-create-image', 'F SH01 does not inherit SH02 image CTA');

ok(workspace.includes('applyProductionOsActionAuthority'), 'wire desk authority');
ok(read('ContentFamixaShotProduction/ShotProductionCard.tsx').includes('authorizeOsDeskCta'), 'card CREATE_VIDEO uses OS authority');
ok(series.includes('osActions={') && series.includes('onExecuteOsAction={executeProductionOsAction}'), 'wire SeriesTab OS props');
ok(!series.includes('osCreateVideo='), 'no legacy osCreateVideo prop');
ok(!series.includes('registerProductionKeyframe'), 'no silent keyframe import on hydrate');

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_OS_CREATE_VIDEO_ACTION_AUTHORITY_CUTOVER_FIX_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_OS_CREATE_VIDEO_ACTION_AUTHORITY_CUTOVER_FIX_V1 PASS FAIL=0 (no provider)');
