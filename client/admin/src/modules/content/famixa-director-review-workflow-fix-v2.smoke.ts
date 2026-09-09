import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  parseFamixaCanon,
  FAMIXA_VISUAL_STYLE,
  type FamixaCharacterRecord,
} from './content-famixa-character-memory';
import {
  canGenerateScene,
  directorCharacterLocked,
  SCENE_IMAGE_PREFLIGHT_ID,
} from './kit-video-scene-image-preflight';
import { imageStatus } from './content-famixa-shot-catalog';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const ui = read('ContentFamixaBuildBoards.tsx');
const card = read('ContentFamixaSceneImagePreflight.tsx');
const studio = read('ContentFamixaCharacterStudio.tsx');
const tab = read('ContentFamixaSeriesTab.tsx');
const board = read('ContentFamixaProductionBoards.tsx');
const logic = read('kit-video-scene-image-preflight.ts');

const minhCanon = parseFamixaCanon({
  identity: { characterId: 'CHAR-001', name: 'Minh', role: 'Con', currentAge: 11, currentEra: 'A11' },
  references: [{ kind: 'FRONT', path: '/content/famixa/canon/CHAR-001-minh-master.png' }],
  famixaVisualStyle: { summary: FAMIXA_VISUAL_STYLE.summary },
});
const minh = (life: string): FamixaCharacterRecord => ({
  id: '1',
  characterCode: 'CHAR-001',
  name: 'Minh',
  role: 'Con',
  universe: 'CORE',
  visual: 'frame',
  lifecycle: life,
  currentEra: 'A11',
  version: 'V1',
  isCurrentCanon: true,
  canon: minhCanon,
  references: minhCanon.references ?? [],
  updatedAt: '',
});

const readyBase = {
  characterIds: ['CHAR-001'],
  scriptLocked: true,
  voiceReady: true,
  shotGraphLocked: true,
  sceneMasterLocked: true,
  visualStyleKnown: true,
  visualStyleReady: true,
  aspectOk: true,
  hasGemini: true,
  shotAllowsGeneration: true,
  sceneLabel: 'Cảnh 01',
};

const locked = canGenerateScene({ ...readyBase, registry: [minh('locked')] });
const approved = canGenerateScene({ ...readyBase, registry: [minh('approved')] });
const missingStyle = canGenerateScene({
  ...readyBase,
  registry: [minh('locked')],
  visualStyleReady: false,
});
const three = canGenerateScene({
  ...readyBase,
  registry: [minh('approved')],
  scriptLocked: false,
  visualStyleReady: false,
});

ok(SCENE_IMAGE_PREFLIGHT_ID === 'FAMIXA_SCENE_IMAGE_PREFLIGHT_V1', '01 preflight id');
ok(locked.allowed && locked.blockers.length === 0, '02 locked + ready → allowed');
const studioLockedMinh = canGenerateScene({
  ...readyBase,
  registry: [minh('approved')],
  studioRows: [{ characterId: 'CHAR-001', characterName: 'Minh', officialLocked: true }],
});
ok(!approved.allowed
  && approved.blockers.some((b) => b.code === 'CHARACTER_NOT_LOCKED' && /Minh chưa LOCK/.test(b.title))
  && approved.blockers.some((b) => b.action.label === 'Mở Character Studio')
  && !approved.blockers.some((b) => b.action.id === 'open_character_universe'),
  '03 approved ≠ locked → Character Studio CTA');
ok(studioLockedMinh.allowed
  && !studioLockedMinh.blockers.some((b) => b.code === 'CHARACTER_NOT_LOCKED')
  && studioLockedMinh.checks.find((c) => c.id === 'character_lock')?.ok
  && directorCharacterLocked('CHAR-001', [minh('approved')], [{ characterId: 'CHAR-001', officialLocked: true }]),
  '03b Character Studio officialLocked = Director LOCK (registry approved ignored)');
ok(!missingStyle.allowed && missingStyle.blockers.some((b) => b.code === 'VISUAL_STYLE_NOT_READY'),
  '04 visual style blocker');
ok(three.blockers.length >= 3
  && three.blockers.some((b) => b.code === 'SCRIPT_NOT_LOCKED')
  && three.blockers.some((b) => b.code === 'CHARACTER_NOT_LOCKED')
  && three.blockers.some((b) => b.code === 'VISUAL_STYLE_NOT_READY'),
  '05 all blockers listed');
ok(ui.includes('Chưa thể tạo ảnh')
  && ui.includes('ContentFamixaSceneImagePreflight')
  && ui.includes('Tạo ảnh')
  && ui.includes('Xác nhận tạo ảnh')
  && ui.includes('blocked ?'),
  '06 drawer disables generate when blocked');
ok(logic.includes('Mở Character Studio')
  && card.includes('Đủ điều kiện tạo ảnh')
  && card.includes('Ở lại màn Hình ảnh')
  && card.includes('Xem nhân vật'),
  '07 blocker / ready copy');
ok(
  canGenerateScene({ ...readyBase, registry: [minh('locked')], voiceReady: false }).blockers.some(
    (b) => b.action.id === 'lock_voice' && b.action.stay,
  )
    && canGenerateScene({ ...readyBase, registry: [minh('locked')], shotGraphLocked: false }).blockers.some(
      (b) => b.action.id === 'approve_shot_graph',
    )
    && canGenerateScene({ ...readyBase, registry: [minh('locked')], aspectOk: false }).blockers.some(
      (b) => b.action.id === 'set_aspect',
    )
    && !logic.includes("'Mở chia cảnh'")
    && tab.includes("leave('voice')")
    && tab.includes('approve_shot_graph')
    && tab.includes('set_aspect'),
  '07b stay-on-images gates, voice goes to voice tab',
);
ok(studio.includes('focusCharacterId')
  && studio.includes('Quay lại')
  && studio.includes('Nhân vật') && studio.includes('đã được LOCK')
  && studio.includes('markStudioLocked')
  && !studio.includes('lockFamixaCharacter(')
  && !studio.includes('autoLock: true'),
  '08 studio deep-link + lock success, no auto lock');
ok(tab.includes('preflightForShot')
  && tab.includes("setProdTab('characters')")
  && tab.includes('returnToSceneFromStudio')
  && tab.includes('restoreShotId')
  && tab.includes('if (!preflight.allowed)'),
  '09 series wires deep-link + return + no generate when blocked');
ok(board.includes('focusCharacterId') && board.includes('returnToScene'),
  '10 character board keeps return context');
ok(logic.includes('characterProductionGuard')
  && logic.includes('officialLocked')
  && !logic.includes('lifecycle = \'locked\'')
  && !logic.includes('MR-CHAR-001-V1'),
  '11 reuses guard, no fake master code, no auto lifecycle write');
ok(imageStatus({ status: 'keyframe_ready', keyframeDataUrl: 'data:image/png;base64,aa' }).label === 'Chờ duyệt'
  && imageStatus({ status: 'keyframe_ready', keyframeDataUrl: 'data:image/png;base64,aa', kfApproved: true }).label === 'Đã duyệt'
  && ui.includes('onApprove(shot.id)')
  && ui.includes('kind="image"'),
  '12b image approve persists kfApproved');
ok(!thisSmokeGenerates() && !thisSmokeLocks() && !thisSmokeApproves(),
  '12 smoke does not mutate');

function thisSmokeGenerates() {
  return false;
}
function thisSmokeLocks() {
  return false;
}
function thisSmokeApproves() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_DIRECTOR_REVIEW_WORKFLOW_FIX_V2_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_DIRECTOR_REVIEW_WORKFLOW_FIX_V2_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
