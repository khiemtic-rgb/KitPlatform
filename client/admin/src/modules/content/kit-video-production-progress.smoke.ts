import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  PRODUCTION_PROGRESS_SOT_ID,
  progressByBuildId,
  studioCardFromProgress,
} from './kit-video-production-progress';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const ts = readFileSync(join(root, 'kit-video-production-progress.ts'), 'utf8');
const desk = readFileSync(join(root, 'content-famixa-series-desk.ts'), 'utf8');
const ui = readFileSync(join(root, 'ContentFamixaSeriesDesk.tsx'), 'utf8');
const tab = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const overview = readFileSync(join(root, 'ContentFamixaProductionOverview.tsx'), 'utf8');

ok(PRODUCTION_PROGRESS_SOT_ID === 'FAMIXA_PRODUCTION_PROGRESS_SOURCE_OF_TRUTH_V1', '01 sot id');

const live = {
  buildId: 'fd760913-0eb8-46a5-b6eb-ec2990a4b0d0',
  seriesCode: 'FAMIXA',
  episodeCode: 'EP01',
  title: 'Tập 01',
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
  blockingReason: 'Bộ ảnh chuẩn của Minh chưa hoàn tất. Thiếu: Toàn thân.',
  storyLine: '1 cảnh · 11 shot · 2 nhân vật',
  completedStages: 2,
  totalStages: 9,
  tone: 'work',
  stages: [
    { id: 'script', label: 'Kịch bản', done: true, detail: 'Hoàn thành' },
    { id: 'scenes', label: 'Chia cảnh', done: true, detail: '1 cảnh · 11 shot' },
    { id: 'cast', label: 'Nhân vật', done: false, detail: 'Cần hoàn thiện' },
    { id: 'image', label: 'Tạo hình', done: false, detail: '0/11' },
    { id: 'imageReview', label: 'Duyệt hình', done: false, detail: '0/11' },
    { id: 'video', label: 'Tạo video', done: false, detail: '0/11' },
    { id: 'videoReview', label: 'Duyệt video', done: false, detail: '0/11' },
    { id: 'finish', label: 'Hoàn thiện', done: false, detail: 'Chưa sẵn sàng' },
    { id: 'publish', label: 'Xuất bản', done: false, detail: 'Chưa sẵn sàng' },
  ],
  scenes: [],
  generate: false,
};

const card = studioCardFromProgress(live);
ok(card.storyLine === '1 cảnh · 11 shot · 2 nhân vật', '02 story line');
ok(card.stageLine === '2/9 bước', '03 stage line not 88%');
ok(card.next === 'Hoàn thiện bộ ảnh chuẩn cho Minh', '04 next from SoT');
ok(card.checks.find((c) => c.id === 'image')?.label.includes('0/11'), '05 image 0/11');
ok(!card.checks.find((c) => c.id === 'image')?.done, '06 no checkmark from legacy still');
ok(progressByBuildId([live])[live.buildId].shotCount === 11, '07 map by build');

const statusBody = desk.slice(desk.indexOf('export function videoCardStatus'), desk.indexOf('export function videoCardProgress'));
const progressBody = desk.slice(desk.indexOf('export function videoCardProgress'), desk.indexOf('export function videoCardChecks'));
const doneBody = desk.slice(desk.indexOf("filter === 'done'"), desk.indexOf("filter === 'paused'"));
ok(!ts.includes('kfCount') && !ts.includes('videoCount'), '08 official progress view has no legacy counts');
ok(!statusBody.includes('kfCount') && !statusBody.includes('videoCount'), '08b status ignores inventory counts');
ok(!progressBody.includes('kfCount') && !progressBody.includes('videoCount'), '08c percent ignores inventory counts');
ok(!doneBody.includes('kfCount') && !doneBody.includes('videoCount'), '08d done filter ignores inventory counts');
ok(desk.includes("STUDIO_BUILD_COUNTS_AUTHORITY = 'INVENTORY'"), '08e build counts are inventory, not production progress');
ok(ui.includes('studioCardFromProgress') && ui.includes('progressById'), '09 desk consumes SoT');
ok(tab.includes('fetchProductionProgressList') && tab.includes('progressById'), '10 tab loads SoT');
ok(overview.includes('bước') && !overview.includes('kfCount'), '11 overview stage line');
ok(!ts.includes('gemini') && !ts.includes('runway') && !ts.includes('veo'), '12 no provider');

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_PROGRESS_SOURCE_OF_TRUTH_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_PROGRESS_SOURCE_OF_TRUTH_V1_SMOKE PASS FAIL=0 generate=false');
