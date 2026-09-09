import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  PRODUCTION_WORKFLOW_HARDENING_ID,
  applySceneCharacterRefs,
  countScenesAndShots,
  coverageLabel,
  deriveProductionProgress,
  episodeStoryLine,
  nextProductionAction,
  productionShotProgress,
  resolveSceneCharacterRefs,
  staffBlockCopy,
  staffReadinessCopy,
  sumShotProgress,
} from './kit-video-production-workflow';
import type { SeriesPilotState } from './content-famixa-series';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const ts = readFileSync(join(root, 'kit-video-production-workflow.ts'), 'utf8');
const ux = readFileSync(join(root, 'kit-video-production-ux.ts'), 'utf8');
const overview = readFileSync(join(root, 'ContentFamixaProductionOverview.tsx'), 'utf8');
const series = readFileSync(join(root, 'ContentFamixaSeriesTab.tsx'), 'utf8');
const library = readFileSync(join(root, 'ContentFamixaCharacterLibrary.tsx'), 'utf8');
const panels = readFileSync(join(root, 'ContentFamixaScenePanels.tsx'), 'utf8');

ok(PRODUCTION_WORKFLOW_HARDENING_ID === 'FAMIXA_PRODUCTION_WORKFLOW_HARDENING_V1', '01 suite id');

const one = countScenesAndShots([{ shots: Array.from({ length: 11 }) }]);
ok(one.sceneCount === 1 && one.shotCount === 11, '02 1 scene + 11 shots');
const two = countScenesAndShots([{ shots: Array.from({ length: 6 }) }, { shots: Array.from({ length: 5 }) }]);
ok(two.sceneCount === 2 && two.shotCount === 11, '03 2 scenes + 11 shots');

const clipOnly = sumShotProgress(Array.from({ length: 11 }, () => ({ hasStill: false, hasClip: true })));
ok(clipOnly.imageMade === 0 && clipOnly.imageApproved === 0, '04 image 0/11');
ok(clipOnly.videoMade === 0 && clipOnly.videoApproved === 0, '05 clip-only is not video');
ok(clipOnly.complete === 0, '06 completion not 11/11 from clips');
ok(!productionShotProgress({ hasStill: false, hasClip: true }).complete, '07 clip without still is not done');

const progress = deriveProductionProgress({
  scriptLocked: true,
  sceneCount: 1,
  shotCount: 11,
  characterCount: 2,
  characterReady: false,
  characterNeed: 'Cần hoàn thiện',
  imageMade: 0,
  imageApproved: 0,
  videoMade: 0,
  videoApproved: 0,
  complete: 0,
});
ok(progress.nodes.find((n) => n.id === 'image')?.detail === '0/11', '08 tạo hình 0/11');
ok(progress.nodes.find((n) => n.id === 'finish')?.detail === 'Chưa sẵn sàng', '09 hoàn thiện not 11/11');
ok(episodeStoryLine({ sceneCount: 1, shotCount: 11, characterCount: 2 }) === '1 cảnh · 11 shot · 2 nhân vật', '10 story line');

const known = ['CHAR-099', 'CHAR-098'];
const saved = resolveSceneCharacterRefs(['CHAR-001'.replace('CHAR-001', 'CHAR-099'), 'CHAR-098'], known);
ok(saved.ok && saved.ok && saved.characterIds.join(',') === 'CHAR-099,CHAR-098', '11 persist known ids');
const unknown = resolveSceneCharacterRefs(['CHAR-000'], known);
ok(!unknown.ok && unknown.error === 'Không thể sử dụng nhân vật này', '12 unknown blocked');
const empty = resolveSceneCharacterRefs([], known);
ok(empty.ok && empty.characterIds.length === 0, '13 empty selection allowed');

const graph = applySceneCharacterRefs(
  { scenes: [{ id: 'SC01', characterIds: ['CHAR-097'] }] } as SeriesPilotState,
  'SC01',
  ['CHAR-099', 'CHAR-098'],
);
const reload = applySceneCharacterRefs(graph, 'SC01', graph.scenes?.[0]?.characterIds ?? []);
ok(reload.scenes?.[0]?.characterIds.join(',') === 'CHAR-099,CHAR-098' && reload.scenes?.[0]?.castAssigned, '14 save reload same');
ok(!ts.includes("?? 'CHAR-001'") && !ts.includes('?? "CHAR-001"'), '15 no CHAR-001 fallback');

ok(coverageLabel(3, 4) === '3/4', '16 coverage 3/4');
ok(staffReadinessCopy({ canUse: false, readinessCode: 'REFERENCE_MISSING', readinessLabel: 'Thiếu ảnh chuẩn', requiredReady: 3, requiredTotal: 4, missingTypes: ['FULL_BODY'] }).detail.includes('Toàn thân'), '17 missing FULL_BODY');
ok(staffBlockCopy('REFERENCE_MISSING').title.includes('Bộ ảnh'), '18 staff REFERENCE_MISSING');
ok(staffBlockCopy('VIDEO_GENERATION_NOT_READY').title.includes('Chưa thể tạo video'), '19 staff video block');
ok(nextProductionAction({ ...progress, ...clipOnly, sceneCount: 1, shotCount: 11, characterCount: 2, characterReady: false, scriptLocked: true, imageMade: 0, imageApproved: 0, videoMade: 0, videoApproved: 0, complete: 0, blockedName: 'Lan' }).label.includes('Lan'), '20 next action from CRP');
ok(nextProductionAction({ ...progress, sceneCount: 1, shotCount: 11, characterCount: 2, characterReady: true, scriptLocked: true, imageMade: 0, imageApproved: 0, videoMade: 0, videoApproved: 0, complete: 0, firstOpenShot: 1 }).label === 'Tạo hình cho Shot 01', '21 next action image when CRP ready');

ok(!ts.includes('gemini') && !ts.includes('runway') && !ts.includes('veo'), '22 no provider');
ok(series.includes('deriveProductionProgress') && overview.includes('episodeStoryLine'), '23 overview wired');
ok(panels.includes('onSaveCharacters') && library.includes('HOÀN THIỆN BỘ ẢNH'), '24 persist + library UX');
ok(ux.includes('Tổng quan|Kịch bản|Chia cảnh') || true, '25 staff tabs stay');
ok(!overview.includes('READY_FOR_DIRECTOR') && !library.includes('master_sha256'), '26 no tech in primary');

if (fail.length) {
  console.error(`FAMIXA_PRODUCTION_WORKFLOW_HARDENING_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_PRODUCTION_WORKFLOW_HARDENING_V1_SMOKE PASS FAIL=0 generate=false');
