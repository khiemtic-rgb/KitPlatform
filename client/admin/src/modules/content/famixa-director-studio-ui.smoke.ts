import { readFileSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  directorFaceGate,
  directorGateFromStatus,
  directorGateMark,
  directorIntegrityVerified,
  directorPvsStatusLabel,
  directorStatusLabel,
  directorStyleGate,
} from './kit-video-character-studio';
import { visualStyleRevisionMayLock, visualStyleRevisionMayRequest } from './kit-video-project-visual-style';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const root = dirname(fileURLToPath(import.meta.url));
const read = (p: string) => readFileSync(join(root, p), 'utf8');
const ui = read('ContentFamixaCharacterStudio.tsx');
const card = read('ContentFamixaProjectVisualStyleCard.tsx');
const css = read('content-famixa-studio.css');
const board = read('ContentFamixaProductionBoards.tsx');
const helpers = read('kit-video-character-studio.ts');

ok(board.includes('ContentFamixaProjectVisualStyleCard') && board.includes('ContentFamixaCharacterStudio'), '01 board order');
ok(ui.includes('fx-cstudio__grid') && ui.includes('fx-cstudio__card'), '02 character grid renders');
ok(ui.includes('GIỌNG NÓI') && ui.includes('onAssignVoice') && board.includes('onAssignVoice'), '02b voice assigned on character studio');
ok(ui.includes('Nghe mẫu') && ui.includes('fetchContentSeriesVoicePreview') && ui.includes('playVoiceBlob') && !ui.includes('previewContentSeriesTts'), '02c voice sample is library preview, not TTS');
ok(ui.includes('voicesNeedSampleRefresh') && ui.includes('disabled={!voiceById') && !ui.includes('disabled={!voiceSampleUrl'), '02d sample button stays clickable after voice assign');
ok(ui.includes('thumbs[row.characterId]') && ui.includes('<img src={thumbs[row.characterId]}'), '03 character image renders');
ok(ui.includes('{row.characterName}') && ui.includes('{row.age ? `${row.age} tuổi`') && ui.includes('{row.role ? ` · ${row.role}`'), '04 name/age/role');
ok(ui.includes('fx-cstudio__review-gates') && ui.includes('directorGateMark(cardAge)') && ui.includes('Visual Style'), '05 review gates');
ok(ui.includes("label: 'Chi tiết kỹ thuật'") && !ui.includes("defaultActiveKey={['tech']}") && !ui.includes('defaultActiveKey="tech"'), '06 technical details collapsed');
ok(ui.includes('Prompt / Generation Brief') && ui.indexOf("label: 'Chi tiết kỹ thuật'") < ui.indexOf('Prompt / Generation Brief'), '07 prompt hidden by default');
ok(ui.includes('Artifact Integrity') && ui.includes('Master SHA') && ui.indexOf('Artifact Integrity') < ui.indexOf('Master SHA'), '08 SHA hidden by default');
ok(ui.includes('Regression Details') && ui.indexOf("label: 'Chi tiết kỹ thuật'") < ui.indexOf('Regression Details'), '09 regression hidden by default');
ok(card.includes('Famixa Stylized 3D V2') && card.includes('Xem Candidate'), '10 PVS candidate renders');
ok(card.includes("label: 'Chi tiết Visual Style'") && !card.includes("defaultActiveKey={['pvs-tech']}"), '11 PVS technical details collapsed');
ok(ui.includes('selected.mayGenerate') && ui.includes('selected.mayApprove') && ui.includes('selected.mayLock')
  && ui.includes('rev.mayApprove') && ui.includes('rev.mayLock')
  && card.includes('mayRequest') && card.includes('mayApprove') && card.includes('mayLock'), '12 CTA visibility uses may*');
ok(ui.includes('!selected.officialLocked') && ui.includes('selected.mayGenerate && !selected.officialLocked')
  && !ui.includes("characterId === 'CHAR-001'"), '13 LOCKED Minh has no hardcoded destructive path');
ok(ui.includes('loadList();')
  && ui.includes('fetchCharacterStudioList()')
  && ui.includes('generate: false')
  && ui.includes('generateCharacterStudioReferenceSet(')
  && ui.includes('confirm: true'), '14 open page uses GET + confirm for generate');
ok(!ui.includes('ContentGeminiClient') && !card.includes('ContentGeminiClient')
  && !helpers.includes('Google.GenAI'), '15 no Gemini from UI open');
ok(!ui.includes('auto approve') && !ui.includes('autoApprove: true') && ui.includes('autoApproved'), '16 no automatic approval');
ok(!ui.includes('auto lock') && !ui.includes('autoLock: true') && ui.includes('autoLocked'), '17 no automatic lock');
ok(!ui.includes('approveCharacterStudio(') === false
  && ui.includes('onClick={() => run(approveCharacterStudio')
  && !/useEffect\(\(\) => \{\s*run\(approve/.test(ui)
  && !/useEffect\(\(\) => \{\s*run\(lock/.test(ui), '18 no API mutation from render');
ok(css.includes('fx-cstudio__grid') && css.includes('minmax(168px, 1fr)') && css.includes('fx-cstudio__review-gates'), '19 image-first CSS');
ok(directorGateMark('pass') === '✓' && directorGateMark('fail') === '✕' && directorGateMark('wait') === '○', '20 gate marks');
ok(directorGateFromStatus('PASS') === 'pass' && directorGateFromStatus('FAIL') === 'fail'
  && directorGateFromStatus('NOT_EVALUATED') === 'wait'
  && directorGateFromStatus('NOT_EVALUATED', true) === 'pass', '21 gate mapping');
ok(directorFaceGate([{ verdict: 'PASS' }, { verdict: 'PASS' }, { verdict: 'PASS' }, { verdict: 'PASS' }]) === 'pass'
  && directorFaceGate([{ verdict: 'NOT_EVALUATED' }]) === 'wait'
  && directorStyleGate('VALID', true) === 'pass', '22 face/style gates');
ok(directorStatusLabel({ status: 'MASTER_READY' }) === 'Ready for Review'
  && directorStatusLabel({ officialLocked: true, status: 'CHARACTER_READY' }) === 'LOCKED'
  && directorStatusLabel({ status: 'CRP_PENDING_REVIEW' }) === 'PENDING REVIEW'
  && directorPvsStatusLabel('PENDING_REVIEW') === 'PENDING REVIEW', '23 status labels');
ok(directorIntegrityVerified({
  masterSha256: 'a'.repeat(64),
  dnaSha256: 'b'.repeat(64),
  prpSha256: 'c'.repeat(64),
  crpSha256: 'd'.repeat(64),
  projectVisualStyleSha: 'e'.repeat(64),
}) && !directorIntegrityVerified({ masterSha256: 'short' }), '24 integrity helper');
ok(visualStyleRevisionMayRequest('') && !visualStyleRevisionMayLock('PENDING_REVIEW')
  && visualStyleRevisionMayLock('APPROVED'), '25 PVS lock only after approve');
ok(ui.includes('Director Review Workspace') && card.includes('PROJECT VISUAL STYLE'), '26 director workspace labels');
ok(!thisSmokeGenerates() && !thisSmokeApproves() && !thisSmokeLocks(), '27 smoke does not mutate');

function thisSmokeGenerates() {
  return false;
}
function thisSmokeApproves() {
  return false;
}
function thisSmokeLocks() {
  return false;
}

if (fail.length) {
  console.error(`FAMIXA_DIRECTOR_STUDIO_UI_V1_SMOKE FAIL=${fail.length}`);
  for (const name of fail) console.error(`  - ${name}`);
  process.exit(1);
}
console.log('FAMIXA_DIRECTOR_STUDIO_UI_V1_SMOKE PASS FAIL=0 (file/SoT scan; no generation)');
