import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  deriveDirectorWorkspace,
  directorStatusLabel,
  DIRECTOR_PIPELINE,
  DIRECTOR_PRODUCTION_WORKSPACE_ID,
  shortShotLabel,
  stepperMark,
  truncateSha,
} from './kit-video-director-workspace';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const ui = readFileSync(join(root, 'ContentKitVideoDirectorProductionWorkspace.tsx'), 'utf8');
const series = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const engine = readFileSync(join(root, 'ContentKitVideoEngineCard.tsx'), 'utf8');

ok(DIRECTOR_PRODUCTION_WORKSPACE_ID === 'PRODUCTION_DIRECTOR_WORKSPACE_UI_V1', '01 suite id');
ok(DIRECTOR_PIPELINE.join(' ') === 'CHARACTER SHOT IMAGE VIDEO FINAL', '02 pipeline order');
ok(shortShotLabel('CHAR-001-MINH-ERA01-SHOT-001') === 'SHOT-001', '03 short shot label');
ok(directorStatusLabel('READY_FOR_DIRECTOR') === 'Ready for Director', '04 status mapping');
ok(truncateSha('be439c39e067aa6c7727255e9643ac78cb7c6285917af60dda38bf14a32518f1') === 'be439c39...2518f1', '05 sha truncate');
ok(stepperMark('READY FOR REVIEW', true) === '●' && stepperMark('APPROVED', false) === '✓', '06 stepper marks');

const live = deriveDirectorWorkspace({
  master: 'LOCKED',
  dna: 'LOCKED',
  prp: 'LOCKED',
  shotContract: 'DIRECTOR_APPROVED',
  executionStatus: 'READY_FOR_DIRECTOR',
  directorApproval: 'PENDING',
  stillApproved: false,
  videoContractStatus: 'NOT_READY',
  videoContractId: null,
  videoGenerationStatus: 'BLOCKED',
  videoPreflightPass: false,
});
ok(live.current === 'IMAGE' && live.action === 'IMAGE_REVIEW', '07 live current image review');
ok(live.currentStage.includes('DIRECTOR REVIEW REQUIRED'), '08 one current stage');
ok(!live.showVideoGeneration, '09 video generation hidden');
ok(live.next.includes('Chuẩn bị video cho cảnh này'), '10 next is prepare video');

ok(ui.includes('✓ DUYỆT ẢNH') && ui.includes('aria-label="APPROVE IMAGE"') && ui.includes('aria-label="REJECT IMAGE"'), '11 director actions');
ok(ui.includes('Duyệt ảnh này?') && ui.includes('Yêu cầu làm lại ảnh?'), '12 confirm modals');
ok(ui.includes('Chi tiết kỹ thuật') && !ui.includes('Kỹ thuật / gỡ lỗi'), '13 technical collapsed');
ok(!ui.includes('generation=false') && !ui.includes('runProvider=false') && !ui.includes('VIDEO_GENERATION_NOT_READY'), '14 no raw engine state');
ok(!ui.includes('AUTO APPROVE') && !ui.includes('GENERATE IMAGE'), '15 no auto/generate');
ok(ui.includes('id="production-image-director-review"') && ui.includes('id="production-video-contract"'), '16 deep link anchors');
ok(series.includes('ContentKitVideoDirectorProductionWorkspace'), '17 Series wires workspace');
ok(!ui.includes('CURRENT ACTION') && !ui.includes('NEXT STEP') && !ui.includes('CURRENT DIRECTOR ACTION'), '18 no duplicate action chrome');
ok(!ui.includes('CREATE CONTRACT') && !ui.includes('disabled>PRE-FLIGHT') && !ui.includes('disabled>EXECUTE VIDEO'), '19 no dead video buttons');
ok(!ui.includes('fx-dir-ws__image-meta') && ui.includes('Execution ID'), '20 execution id in technical only');
ok(!series.includes('fx-dir-split') && series.includes('ContentFamixaStudioView') && ui.includes('Chi tiết kỹ thuật'), '21 studio parked in technical');
ok(engine.includes('Dành cho kỹ thuật') && engine.includes('Collapse'), '22 engine always collapsed');
ok(ui.includes('ĐI ĐẾN BƯỚC VIDEO') && ui.includes('Video Contract không tự tạo'), '23 no auto contract');
const inflight = deriveDirectorWorkspace({
  master: 'LOCKED',
  dna: 'LOCKED',
  prp: 'LOCKED',
  shotContract: 'DIRECTOR_APPROVED',
  executionStatus: 'IMAGE_APPROVED',
  directorApproval: 'APPROVED',
  stillApproved: true,
  videoContractStatus: 'DIRECTOR_APPROVED',
  videoContractId: 'vc-1',
  videoGenerationStatus: 'REQUESTED',
  videoPreflightPass: true,
  videoProviderRequestId: 'runway-task-1',
});
ok(inflight.action === 'VIDEO_PROCESSING' && !inflight.currentActionTitle.includes('Sẵn sàng tạo video'), '26 in-flight is wait not execute');
ok(ui.includes('Đang tạo video') && ui.includes('Không bấm tạo lại'), '27 no second generate button while in-flight');
const stalled = deriveDirectorWorkspace({
  ...{
    master: 'LOCKED',
    dna: 'LOCKED',
    prp: 'LOCKED',
    shotContract: 'DIRECTOR_APPROVED',
    executionStatus: 'IMAGE_APPROVED',
    directorApproval: 'APPROVED',
    stillApproved: true,
    videoContractStatus: 'DIRECTOR_APPROVED',
    videoContractId: 'vc-1',
    videoGenerationStatus: 'REQUESTED',
    videoPreflightPass: true,
    videoProviderRequestId: null,
    videoRequestedAt: new Date(Date.now() - 4 * 60 * 1000).toISOString(),
  },
});
ok(stalled.action === 'EXECUTE' && stalled.currentActionTitle.includes('đứt'), '28 orphan REQUESTED can recover once');
ok(ui.includes('fetchVideoGenerationExecutionArtifactBlob') && ui.includes('playsInline'), '29 video plays via auth blob');
ok(ui.includes('Tải video ·') && ui.includes('download='), '30 download link');
ok(ui.includes('Sang hoàn thiện tập') && ui.includes('Về danh sách cảnh'), '31 done has next');
ok(series.includes('ContentFamixaSeriesWorkspace'), '25 series workspace shell');
ok(!ui.includes('Mở phòng dựng tập') && !ui.includes('fx-studio-fold'), '24 no second studio screen');

if (fail.length) {
  console.error(`PRODUCTION_DIRECTOR_WORKSPACE_UI_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('PRODUCTION_DIRECTOR_WORKSPACE_UI_V1 PASS FAIL=0 P0=0 (file/SoT scan; no provider)');
