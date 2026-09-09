import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  SCENE_PRODUCTION_WORKSPACE_ID,
  SCENE_WORK_STEPS,
  buildSceneViews,
  displayPersonName,
  nextSceneAction,
  pageSlice,
  sceneActionsFromMedia,
  sceneCastCopy,
  sceneCastFaceSource,
  sceneStepMarks,
  staffErrorCopy,
} from './kit-video-scene-workspace';
import type { FamixaSeriesShot, SeriesShotRun } from './content-famixa-series';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const ts = readFileSync(join(root, 'kit-video-scene-workspace.ts'), 'utf8');
const ui = readFileSync(join(root, 'ContentFamixaSceneWorkspace.tsx'), 'utf8');
const panels = readFileSync(join(root, 'ContentFamixaScenePanels.tsx'), 'utf8');
const boards = readFileSync(join(root, 'ContentFamixaBuildBoards.tsx'), 'utf8');

ok(SCENE_PRODUCTION_WORKSPACE_ID === 'SCENE_PRODUCTION_WORKSPACE_V1', '01 document id');
ok(
  SCENE_WORK_STEPS.map((s) => s.label).join('|') ===
    'Tổng quan|Kịch bản|Chia shot|Nhân vật|Tạo hình|Dựng video|Duyệt|Hoàn thiện',
  '02 staff steps',
);

const emptyRun = { status: 'story_locked' } as SeriesShotRun;
const shot = (id: string, scene: string, extras: Partial<FamixaSeriesShot> = {}): FamixaSeriesShot =>
  ({
    id,
    scene,
    shot: id,
    clock: '00:00',
    seconds: 5,
    story: extras.story || 'Shot',
    visual: '',
    characters: extras.characters || ['CHAR-099'],
    location: extras.location || '',
    motionPrompt: '',
    motionPromptVi: '',
    sceneId: extras.sceneId || scene,
    characterIds: extras.characterIds || extras.characters || ['CHAR-099'],
    beatId: extras.beatId,
    beatText: extras.beatText,
  }) as FamixaSeriesShot;

const scenes = buildSceneViews(
  [
    shot('s1', 'SC01', { story: 'Thức dậy', beatId: 'b1', beatText: 'Sáng sớm', sceneId: 'SC01' }),
    shot('s2', 'SC01', { story: 'Lấy balo', beatId: 'b2', beatText: 'Chuẩn bị', sceneId: 'SC01' }),
    shot('s3', 'SC02', { story: 'Gặp mẹ', sceneId: 'SC02', characters: ['CHAR-098'] }),
  ],
  (id) => (id === 's1' ? ({ status: 'keyframe_ready', keyframeDataUrl: 'data:image/png;base64,xx' } as SeriesShotRun) : emptyRun),
  [
    { id: 'SC01', title: 'Buổi sáng', content: 'Minh thức dậy.', characterIds: ['CHAR-099'], environment: 'Phòng ngủ' },
    { id: 'SC02', title: 'Gặp mẹ', characterIds: ['CHAR-098'], environment: '' },
  ],
);

ok(scenes.length === 2 && scenes[0].shots.length === 2 && scenes[1].shots.length === 1, '03 group scenes');
ok(scenes[0].beats.length >= 2, '04 beat grouping');
ok(scenes[0].status === 'imaging' && scenes[1].status === 'prep', '05 scene status');
ok(pageSlice(scenes[0].shots, 0, 1).length === 1, '06 pagination');

const marks = sceneStepMarks(scenes[0], [{ characterId: 'CHAR-099', canUse: false } as never], true);
const next = nextSceneAction(scenes[0], marks);
ok(next.shotId === 's2' && next.step === 'image', '07 next action first missing still');
ok(
  nextSceneAction(scenes[0], marks, [{ characterId: 'CHAR-099', canUse: false, displayName: 'Lan', name: 'Lan' } as never]).step === 'cast',
  '07b next action CRP blocked',
);
ok(sceneCastFaceSource({ frontPackId: 'p', frontItemId: 'i', masterLocked: false }) === 'crp', '07c CRP front wins');
ok(
  sceneCastFaceSource({ frontPackId: null, frontItemId: null, masterLocked: false }, { officialLocked: true }) ===
    'studio-master',
  '07d locked studio shows master',
);
ok(
  sceneCastCopy({ canUse: false, readinessReason: 'Chưa có bộ ảnh chuẩn.', readinessLabel: 'Cần bổ sung' }, { officialLocked: true })
    .mark === '✓ Đã khóa hồ sơ',
  '07e locked studio is not missing-face copy',
);
ok(
  sceneCastCopy({ canUse: false, readinessReason: 'Chưa có bộ ảnh chuẩn.', readinessLabel: 'Cần bổ sung' }).mark ===
    '⚠ Cần bổ sung',
  '07f unlocked missing CRP stays warning',
);
ok(!sceneActionsFromMedia({ hasStill: false, hasClip: false, needsFix: false }).canCreateImage, '08 no generate action');
ok(staffErrorCopy('SHOT_CONTRACT_IDENTITY_CONFLICT').hint.includes('nhân vật'), '09 staff error copy');
ok(displayPersonName('CHAR-099', [{ id: 'CHAR-099', name: 'Lan' }]) === 'Lan', '10 name from graph');
ok(displayPersonName('', [], []) === 'Nhân vật', '11 no fallback character');

ok(!ts.includes("?? 'CHAR-001'") && !ts.includes('?? "CHAR-001"'), '12 no CHAR-001 fallback');
ok(!ts.includes("'Minh'") && !ui.includes("'Minh'") && !panels.includes("'Minh'"), '13 no Minh hardcode');
ok(!ts.includes('evaluateIdentity') && !ui.includes('evaluateIdentity'), '14 no frontend evaluator');
ok(!ui.includes('gemini') && !ui.includes('runway') && !ui.includes('veo') && !ui.includes('|| true'), '15 no provider / bypass');
ok(
  (ui.includes('Chi tiết kỹ thuật') ||
    panels.includes('Chi tiết kỹ thuật') ||
    panels.includes('Chi tiết sản xuất')) &&
    ui.includes('Việc tiếp theo') &&
    ui.includes('fx-scw'),
  '16 staff surfaces',
);
ok(boards.includes('ContentFamixaSceneWorkspace') && boards.includes('ContentFamixaBuildSceneList'), '17 wired to scene tab');

const clipOnly = buildSceneViews(
  [shot('c1', 'SC01', { sceneId: 'SC01' })],
  () => ({ status: 'preview_ready', previewUrl: 'http://x' } as SeriesShotRun),
  [{ id: 'SC01', title: 'Phòng', characterIds: ['CHAR-099'] }],
);
ok(sceneStepMarks(clipOnly[0]).find((m) => m.id === 'image')?.detail === '0/1', '18 clip-only is not image');
ok(sceneStepMarks(clipOnly[0]).find((m) => m.id === 'finish')?.detail === 'Chưa sẵn sàng', '19 clip-only is not complete');

if (fail.length) {
  console.error(`SCENE_PRODUCTION_WORKSPACE_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('SCENE_PRODUCTION_WORKSPACE_V1_SMOKE PASS FAIL=0');
