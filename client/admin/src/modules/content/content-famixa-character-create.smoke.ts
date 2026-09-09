import { parseFamixaCanon, type FamixaCharacterRecord } from './content-famixa-character-memory';
import {
  canGenerateMasterRef,
  canLockCharacter,
  characterQa,
  compileMasterRefPrompt,
  detectDuplicateCharacter,
  eraOfAge,
  findUnregisteredNames,
  lockedFieldBlocked,
  nextCharacterCode,
  nextMasterRefKind,
  nextVersionLabel,
  plannedEras,
  proposeVisualDna,
  resolveScriptCharacter,
  seedDraftCanon,
  workspaceProgress,
} from './content-famixa-character-create';

const fail: string[] = [];

const minhCanon = parseFamixaCanon({
  identity: { characterId: 'CHAR-001', name: 'Minh', role: 'Con', gender: 'male', currentAge: 11, currentEra: 'A11' },
  visualDna: {
    documentId: 'CHAR-001_MINH_VISUAL_DNA_V1',
    status: 'APPROVED',
    characterId: 'CHAR-001',
    whoVisually: 'designed Minh',
    hairLock: 'left tuft',
    silhouette: 'round head + tuft',
    headToBody: '1:5.5',
    face: 'round youthful face',
    hair: 'short black slightly messy hair',
    eyes: 'large natural dark eyes',
    build: 'slim child body',
    visualStyle: 'FAMIXA_VISUAL_STYLE',
  },
  famixaVisualStyle: { documentId: 'FAMIXA-VISUAL-STYLE-V1', status: 'APPROVED', summary: 'Famixa designed-character' },
  personalityDna: ['sensitive', 'curious'],
  personality: { core: ['sensitive'], needs: ['recognition'] },
  behaviorDna: [{ when: 'embarrassed', does: 'looks down' }],
  voiceDna: { voiceId: 'voice-minh', language: 'vi' },
  wardrobe: [{ id: 'OUTFIT-HOME-01', set: 'HOME', label: 'Áo nhà tối' }],
  relationships: [{ to: 'CHAR-003', type: 'Mother' }],
  evolution: { initialEra: 'A11', eras: plannedEras('A11') },
  workspace: { visualProposalStatus: 'approved' },
  references: [{ kind: 'FRONT', path: '/content/famixa/canon/CHAR-001-minh-master.png' }],
});

const minh: FamixaCharacterRecord = {
  id: '1',
  characterCode: 'CHAR-001',
  name: 'Minh',
  role: 'Con',
  universe: 'CORE',
  visual: 'frame',
  lifecycle: 'approved',
  currentEra: 'A11',
  version: 'V1',
  isCurrentCanon: true,
  canon: minhCanon,
  references: minhCanon.references ?? [],
  updatedAt: '',
};

if (eraOfAge(11) !== 'A11' || eraOfAge(16) !== 'A16') fail.push('era of age');
if (plannedEras('A11').some((e) => e.era === 'CHAR-015')) fail.push('evolution must not invent CHAR-015');
if (!plannedEras('A11').some((e) => e.era === 'A16' && e.status === 'planned')) fail.push('A16 must be planned on CHAR-001');

const dup = detectDuplicateCharacter([minh], { name: 'Minh', role: 'Con' });
if (!dup.duplicate || dup.existing[0]?.characterCode !== 'CHAR-001') fail.push('TEST 43 duplicate Minh');
if (dup.preferred !== 'USE_EXISTING') fail.push('duplicate should prefer USE EXISTING');

const resolved = resolveScriptCharacter([minh], 'Minh', 11);
if (!resolved.ok || resolved.characterCode !== 'CHAR-001' || resolved.era !== 'A11' || resolved.version !== 'V1') {
  fail.push('TEST 45 resolve Minh về nhà → CHAR-001 ERA-A11');
}
if (!resolved.ok || !resolved.masterPath) fail.push('TEST 45 master reference');

const missing = resolveScriptCharacter([minh], 'Hùng');
if (missing.ok || !missing.requestCreation) fail.push('TEST 33 unknown name must REQUEST CREATION');

if (findUnregisteredNames('Hùng bước vào phòng.', [minh])[0] !== 'Hùng') {
  fail.push('unregistered action name');
}
if (findUnregisteredNames('Minh về nhà.', [minh]).length) fail.push('Minh must resolve, not request creation');
if (findUnregisteredNames('sáng về nhà.', [minh]).length) fail.push('sáng về must not invent «áng»');
if (findUnregisteredNames('Sáng về nhà.', [minh]).length) fail.push('Sáng về is lighting, not a character');
if (findUnregisteredNames('Không nhìn camera.', [minh]).length) fail.push('Không nhìn must not invent a character');
if (findUnregisteredNames('ánh sáng nhìn vào phòng.', [minh]).length) fail.push('ánh sáng nhìn must not invent «áng»');

if (nextCharacterCode([minh, { characterCode: 'CHAR-004' }]) !== 'CHAR-005') fail.push('next code CHAR-005');
if (nextVersionLabel('V1') !== 'V2') fail.push('version V2 after V1');

const lockBlocked = lockedFieldBlocked('locked', 'visualDna.hair');
if (!lockBlocked) fail.push('TEST 44 locked hair must BLOCK');
if (lockedFieldBlocked('approved', 'visualDna.hair')) fail.push('approved may edit hair');

const qa = characterQa(minh);
if (qa.status === 'FAIL') fail.push(`seed Minh QA must not FAIL: ${qa.ticks.filter((t) => !t.ok).map((t) => t.key).join()}`);

const approvedLock = canLockCharacter(minh);
if (!approvedLock.ok) fail.push(`approved Minh with DNA+Style+FRONT may LOCK: ${approvedLock.ok === false ? approvedLock.blocked : ''}`);

const legacyMinh = {
  ...minh,
  canon: parseFamixaCanon({
    ...minhCanon,
    visualDna: { face: 'round youthful face', hair: 'short black', eyes: 'dark', build: 'slim', visualStyle: 'FAMIXA_VISUAL_STYLE' },
    famixaVisualStyle: { summary: 'old' },
  }),
};
if (canLockCharacter(legacyMinh).ok) fail.push('must not LOCK CHAR-001 from current/legacy sheet');
if (canGenerateMasterRef(legacyMinh).ok) fail.push('must not generate Master from legacy Minh sheet');

const draft = { ...minh, lifecycle: 'draft' as const };
if (canLockCharacter(draft).ok) fail.push('APPROVE ≠ LOCK — draft cannot lock');

const noFront = { ...minh, references: [], canon: { ...minhCanon, references: [] } };
if (characterQa(noFront).status !== 'FAIL') fail.push('missing FRONT must QA FAIL');
if (canLockCharacter({ ...noFront, lifecycle: 'approved' }).ok) fail.push('QA FAIL cannot LOCK');

const proposal = proposeVisualDna({ name: 'Minh', gender: 'male', currentAge: 11, role: 'Con' });
if (!proposal.face || !proposal.hair) fail.push('visual proposal');
if (proposal.visualStyle !== 'FAMIXA_VISUAL_STYLE') fail.push('proposal must inherit Famixa style');

const incomplete = {
  ...minh,
  canon: seedDraftCanon({ characterCode: 'CHAR-009', name: 'Test', initialAge: 11, characterType: 'BACKGROUND' }),
  references: [],
};
if (canGenerateMasterRef(incomplete).ok) fail.push('no master ref before definition complete');

if (nextMasterRefKind(minh) !== 'THREE_Q_LEFT') fail.push('next view after FRONT is 3/4 Left');
const prompt = compileMasterRefPrompt({ canon: minhCanon, kind: 'SIDE', inheritFrom: 'FRONT' });
if (!/CHAR-001|Minh|A11|inherit/i.test(prompt)) fail.push('master prompt must carry canon + inherit');
if (/random hairstyle|new face/i.test(prompt)) fail.push('master prompt must not invite random identity');

const progress = workspaceProgress(minh);
if (progress.total !== 12) fail.push('12 workspace steps');
if (progress.completed < 8) fail.push(`Minh definition should complete ≥8/12, got ${progress.completed}`);

if (fail.length) {
  console.error('CHARACTER CREATE FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('CHARACTER CREATE PASS · duplicate / resolve / QA / lock / evolution / master gate');
