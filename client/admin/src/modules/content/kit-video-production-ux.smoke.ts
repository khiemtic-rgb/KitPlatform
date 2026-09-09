import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  deriveEpisodePipeline,
  friendlyStatus,
  nextWorkCopy,
  PRODUCTION_UX_ID,
  SERIES_STAFF_TABS,
  shotUserLabel,
} from './kit-video-production-ux';
import { SHOT_PAGE_DEFAULT, staffVideoSendError, videoStatus } from './content-famixa-shot-catalog';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const overview = readFileSync(join(root, 'ContentFamixaProductionOverview.tsx'), 'utf8');
const workspace = readFileSync(join(root, 'ContentKitVideoDirectorProductionWorkspace.tsx'), 'utf8');
const series = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const shell = readFileSync(join(root, 'ContentFamixaSeriesWorkspace.tsx'), 'utf8');
const boards = readFileSync(join(root, 'ContentFamixaProductionBoards.tsx'), 'utf8');
const catalog = readFileSync(join(root, 'ContentFamixaBuildBoards.tsx'), 'utf8');

ok(PRODUCTION_UX_ID === 'FAMIXA_PRODUCTION_UI_V1', '01 suite id');
ok(friendlyStatus('READY_FOR_DIRECTOR').label === 'Đang chờ bạn duyệt', '02 ready maps to review');
ok(friendlyStatus('DIRECTOR_APPROVED').label === 'Đã duyệt', '03 approved maps');
ok(friendlyStatus('BLOCKED').label === 'Chưa thể thực hiện', '04 blocked maps');
ok(!friendlyStatus('READY_FOR_DIRECTOR').label.includes('READY_FOR_DIRECTOR'), '05 no raw enum');

const live = shotUserLabel({
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
ok(live.label === 'Chờ duyệt' && live.action === 'Xem và duyệt ảnh', '06 SHOT-001 user status');

const ep = deriveEpisodePipeline({
  scriptLocked: true,
  sceneCount: 1,
  shotGraphLocked: true,
  characterLocked: true,
  prepDone: 1,
  shotTotal: 1,
  imagesMade: 1,
  imagesReviewed: 0,
  videosMade: 0,
  videosReviewed: 0,
  videosDone: 0,
});
ok(ep.current === 'REVIEW_IMAGE' && ep.percent > 0 && ep.percent < 100, '07 episode current is image review');

ok(overview.includes('TIẾN ĐỘ SẢN XUẤT') && overview.includes('VIỆC CẦN LÀM NGAY'), '08 overview surface');
ok(overview.includes('Sang hoàn thiện tập') && series.includes('episodeDone'), '08b episode can finish');
ok(!overview.includes('READY_FOR_DIRECTOR') && !overview.includes('SHA256'), '09 overview no tech enums');
ok(workspace.includes('← Quay lại danh sách cảnh') && workspace.includes('Yêu cầu làm lại'), '10 workspace back + reject');
ok(workspace.includes('ĐI ĐẾN BƯỚC VIDEO') && !workspace.includes('ĐI ĐẾN VIDEO CONTRACT'), '11 no contract jargon');
ok(series.includes('ContentFamixaSeriesWorkspace') && series.includes('ContentFamixaProductionOverview'), '12 Series wires workspace');
ok(shell.includes('Nhân viên') && shell.includes('Tạo hình (đã xong)') && shell.includes('Chi tiết hệ thống'), '12b look parked off staff');
ok(shell.includes('focusSeriesTaskPanel') && shell.includes('data-series-task="open"'), '12d2 Mở thoại still focuses panel on same tab');
ok(catalog.includes('id="fx-voice-board"') && catalog.includes('data-voice-gap') && catalog.includes('onAssignVoice')
  && catalog.includes('Khóa thoại') && catalog.includes('Giọng đã gán ở tab Nhân vật'), '12d3 voice board is assignable + lock');
ok(shell.includes('Nhân vật đã khóa · Minh') && shell.includes("mode === 'director'"), '12d staff sees lock line only');
ok(series.includes('lookBuild') && shell.includes('Tạo hình (đã xong) — không dùng để làm tập'), '12c staff no look path');
ok(series.includes('ContentFamixaBuildImageBoard') && series.includes('ContentFamixaBuildVideoBoard') && series.includes('ContentFamixaBuildSceneList'), '12e tabs are dedicated boards');
ok(series.includes('không thuộc bản dựng này') || series.includes('ContentFamixaSharedProductionNote'), '12h production shot not reused per build');
ok(series.includes('ContentFamixaSeriesDesk') && series.includes("seriesDesk === 'list'"), '12g series list first');
ok(SERIES_STAFF_TABS.map((t) => t.label).join('|') === 'Tổng quan|Kịch bản|Chia cảnh|Nhân vật|Thoại|Hình ảnh|Video|Hoàn thiện|Xuất bản', '12f staff tabs');
ok(catalog.includes('Tìm cảnh') && catalog.includes('fx-shot-table') && catalog.includes('/ trang') && SHOT_PAGE_DEFAULT === 20, '12i compact scene catalog');
ok(catalog.includes('Tạo ảnh') && series.includes('confirmCreateShotImage') && series.includes('void generateSceneKf([shotId])'),
  '12j empty image board can create via existing KF path');
ok(nextWorkCopy({
  scriptLocked: true,
  sceneCount: 3,
  characterLocked: false,
  characterNeed: 'Hoàn thiện bộ ảnh chuẩn cho Linh',
  sceneLabel: 'Cảnh 01',
  done: false,
  imagesPending: false,
  videosPending: true,
}).tab === 'video' && nextWorkCopy({
  scriptLocked: true,
  sceneCount: 3,
  characterLocked: false,
  characterNeed: 'Hoàn thiện bộ ảnh chuẩn cho Linh',
  sceneLabel: 'Cảnh 01',
  done: false,
  imagesPending: true,
  videosPending: true,
}).tab === 'characters',
  '12k approved images unlock video even if Linh CRP incomplete');
ok(shell.includes("tabId === 'video'") && shell.includes("'script', 'scenes', 'image'"),
  '12l video tab does not wait on cast after images done');
ok(catalog.includes('Tạo video') && series.includes('startSceneTurbo([shotId])'),
  '12m video board creates via existing I2V path');
ok(staffVideoSendError('CONFIRMATION_REQUIRED').includes('không cần sửa ảnh'),
  '12n confirm miss is not a KF repair');
ok(staffVideoSendError('INTERNAL.BAD_OUTPUT — RENDER_FAILURE').includes('Gửi lại với camera khác')
  && catalog.includes('Gửi lại với camera khác'),
  '12o INTERNAL render fail offers camera retry, not KF regen');
ok(catalog.includes('kind="video"') && catalog.includes('onApprove(shot.id)')
  && series.includes('videoApproved: true')
  && videoStatus({ previewUrl: 'https://x/a.mp4' }).label === 'Chờ duyệt'
  && videoStatus({ previewUrl: 'https://x/a.mp4', videoApproved: true }).label === 'Đã duyệt'
  && nextWorkCopy({
    scriptLocked: true,
    sceneCount: 3,
    characterLocked: false,
    sceneLabel: 'Cảnh 01',
    done: false,
    imagesPending: false,
    videosPending: false,
    videosReviewPending: true,
  }).tab === 'video',
  '12p video approve persists videoApproved');
ok(catalog.includes('chưa phải file ghép')
  && catalog.includes('Preview')
  && !catalog.includes('src={clips[0]}'),
  '12q publish plays every take, not only Cảnh 01');
ok(catalog.includes('MUTE_TAKE')
  && catalog.includes('không phải Final')
  && series.includes('mixStaffEpisode')
  && series.includes('MIX_STAFF_EPISODE_ROLE'),
  '12r staff stitch is preview-only; Runway takes stay mute');
ok(!overview.includes('GENERATE') && !workspace.includes('AUTO APPROVE'), '13 no auto/generate');

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_UI_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_UI_V1 PASS FAIL=0 P0=0 (file/SoT scan; no provider)');
