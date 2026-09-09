import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  SERIES_DESK_ID,
  filterStudioBuilds,
  studioBuildCounts,
  studioBuildLabel,
  studioNextAction,
  suggestNextEpisodeCode,
  videoCardProgress,
  videoCardStatus,
  videoPrimaryAction,
} from './content-famixa-series-desk';
import { studioCardFromProgress } from './kit-video-production-progress';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const desk = readFileSync(join(root, 'ContentFamixaSeriesDesk.tsx'), 'utf8');
const tab = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const shell = readFileSync(join(root, 'ContentFamixaSeriesWorkspace.tsx'), 'utf8');

ok(SERIES_DESK_ID === 'FAMIXA_SERIES_DESK_V1', '01 desk id');
ok(suggestNextEpisodeCode([{ episodeCode: 'EP01' }, { episodeCode: 'EP99' }]) === 'EP100', '02 next episode');
ok(videoPrimaryAction({ status: 'draft', shotCount: 0, kfCount: 0, videoCount: 0 } as never) === 'Bắt đầu', '04 start action');

const progress = {
  buildId: '1',
  seriesCode: 'FAMIXA',
  episodeCode: 'EP01',
  title: 'A',
  sceneCount: 1,
  shotCount: 11,
  characterCount: 2,
  imageProgress: 0,
  imageApprovalProgress: 0,
  videoProgress: 0,
  videoApprovalProgress: 0,
  finalization: 'Chưa sẵn sàng',
  publication: 'Chưa sẵn sàng',
  currentStep: 'cast',
  nextAction: 'Hoàn thiện bộ ảnh chuẩn cho Minh',
  storyLine: '1 cảnh · 11 shot · 2 nhân vật',
  completedStages: 2,
  totalStages: 9,
  tone: 'work',
  stages: [
    { id: 'image', label: 'Tạo hình', done: false, detail: '0/11' },
    { id: 'video', label: 'Tạo video', done: false, detail: '0/11' },
  ],
  scenes: [],
};
ok(videoCardStatus({ status: 'in_prod', shotCount: 11, kfCount: 11, videoCount: 11 } as never, progress).label === 'Đang làm', '03 status from SoT not videoCount');
ok(studioNextAction({ status: 'in_prod', shotCount: 11, kfCount: 11, videoCount: 11 } as never, progress).need === 'Hoàn thiện bộ ảnh chuẩn cho Minh', '04b next from SoT');
ok(filterStudioBuilds([{ status: 'final', title: 'A', episodeCode: 'EP01', updatedAt: '2026-08-29', id: '1' } as never], 'done', '', { '1': { ...progress, tone: 'done' } }).length === 1, '04c filter done from SoT');
ok(videoCardProgress({ status: 'in_prod', shotCount: 11, kfCount: 11, videoCount: 11 } as never, progress) === 22, '05 progress from stages not kfCount');
ok(studioCardFromProgress(progress).checks.some((c) => c.label.includes('0/11')) && !studioCardFromProgress(progress).checks.some((c) => c.done && c.id === 'image'), '05b no ✓ Hình ảnh from legacy');
ok(studioCardFromProgress(progress).storyLine === '1 cảnh · 11 shot · 2 nhân vật', '05c story line');
ok(desk.includes('Tạo video mới') && desk.includes('DANH SÁCH VIDEO') && desk.includes('Tìm video') && desk.includes('Bắt đầu sản xuất') && desk.includes('onDelete'), '06 desk chrome');
ok(!desk.includes('POST /content/video-engine/production-shots'), '07 no new production shot');
ok(tab.includes('ContentFamixaSeriesDesk') && tab.includes("seriesDesk === 'list'"), '08 Series list first');
ok(tab.includes('createVideoFromDesk') && tab.includes("setProdTab('script')"), '09 create goes to script');
ok(shell.includes('← Video Studio') && shell.includes('Video Studio /'), '10 back + crumb');
ok(shell.includes('data-episode-title') && shell.includes('Đang mở tập:'), '10b episode title stays above tabs');
ok(desk.includes('studioBuildLabel') && desk.includes('studioBuildCounts'), '10c list card uses build counts');
const twins = [
  { id: 'a', episodeCode: 'EP01', title: 'Tập 01', shotCount: 6, kfCount: 2, videoCount: 1, voiceLines: 6 },
  { id: 'b', episodeCode: 'EP01', title: 'Tập 01', shotCount: 11, kfCount: 0, videoCount: 0, voiceLines: 11 },
  { id: 'c', episodeCode: 'EP01', title: 'Tập 01', shotCount: 24, kfCount: 0, videoCount: 0, voiceLines: 24 },
] as never[];
ok(studioBuildLabel(twins[0]!, twins) === 'EP01 · Tập 01 · 6 shot', '10d same title keeps shot count');
ok(studioBuildLabel(twins[1]!, twins) === 'EP01 · Tập 01 · 11 shot', '10e 11-shot card differs');
ok(studioBuildCounts(twins[0]!) === 'Thoại 6 · Hình 2/6 · Video 1/6', '10f counts from build row');
ok(!tab.includes('AUTO APPROVE') && !desk.includes('GENERATE'), '11 no auto generate');
ok(desk.includes('studioCardFromProgress') && !desk.includes('videoCardProgress('), '12 desk uses SoT card');

if (fail.length) {
  console.error(`FAMIXA_SERIES_DESK_V1 FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_SERIES_DESK_V1 PASS FAIL=0 P0=0 (file/SoT scan; no provider)');
