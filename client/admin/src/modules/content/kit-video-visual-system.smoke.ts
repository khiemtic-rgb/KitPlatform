import { KIT_VIDEO_ENGINE } from './kit-video-engine';
import {
  distinguishFrameKind,
  enforcePromptHierarchy,
  eraBelongsToCharacterIdentity,
  ensureVisualSystemImmutable,
  FAMIXA_VISUAL_STYLE_SYSTEM_V1,
  isProviderIndependent,
  KIT_VIDEO_VISUAL_SYSTEM,
  loadVisualSystem,
  missingAssetIsBlocked,
  visualSystemBelongsToProject,
  visualSystemRef,
  VISUAL_STYLE_SYSTEM_CODE,
} from './kit-video-visual-system';

const fail: string[] = [];
const ok = (cond: boolean, name: string) => {
  if (!cond) fail.push(name);
};

const doc = loadVisualSystem('FAMIXA', 'V1');
ok(doc.documentId === 'FAMIXA_VISUAL_STYLE_SYSTEM_V1' && doc.version === 'V1', '01 Visual System V1 can be loaded');
ok(visualSystemBelongsToProject(doc, 'FAMIXA') && doc.projectCode === 'FAMIXA', '02 belongs to Project = FAMIXA');
ok(doc.status === 'proposed' && KIT_VIDEO_VISUAL_SYSTEM === 'KIT-VIDEO-VISUAL-SYSTEM-V1', '01 proposed not auto-locked');

try {
  ensureVisualSystemImmutable({ status: 'locked', version: 'V1' }, { version: 'V1' });
  fail.push('03 locked V1 must be immutable');
} catch (e) {
  ok(/VISUAL_SYSTEM_LOCKED/.test(String(e)), '03 Visual System version immutable once locked');
}

const hairBlock = enforcePromptHierarchy({
  prompt: 'Give Minh blonde long hair and a new face',
  lockedCharacter: { hair: 'short black left tuft', identity: 'CHAR-001 Minh' },
});
ok(!hairBlock.ok && hairBlock.blocked.some((x) => /Character Canon/.test(x)), '04 prompt cannot override locked Character Canon');

const locBlock = enforcePromptHierarchy({
  prompt: 'Change the living room to a beach villa',
  lockedLocation: { name: 'SC01 living room night' },
});
ok(!locBlock.ok && locBlock.blocked.some((x) => /Location Canon/.test(x)), '05 prompt cannot override locked Location Canon');

const still = distinguishFrameKind('PRODUCTION_STILL');
const sheet = distinguishFrameKind('CHARACTER_SHEET');
ok(still.isProductionStill && !still.isCharacterSheet && still.i2vAllowed, '06 production still');
ok(sheet.isCharacterSheet && !sheet.i2vAllowed && sheet.forbiddenInProduction.includes('collage'), '06 character sheet ≠ production still');

ok(doc.forbiddenDrift.length > 0 && doc.forbiddenGeneration.includes('invent characters'), '07 forbidden visual requirements represented');

const era = eraBelongsToCharacterIdentity('CHAR-001', 'ERA-03');
ok(era.ok && era.characterId === 'CHAR-001' && !era.newCharacter, '08 era belongs to Character Identity');
const invented = eraBelongsToCharacterIdentity('CHAR-005', 'ERA-01');
ok(!invented.ok && invented.newCharacter, '08 era is not a new Character Identity');

ok(isProviderIndependent(doc) && doc.providers.length === 0, '09 Visual System is provider-independent');
ok(visualSystemRef('FAMIXA').systemCode === VISUAL_STYLE_SYSTEM_CODE, '09 generic ref not Famixa engine');
ok(KIT_VIDEO_ENGINE === 'KIT-VIDEO-ENGINE-V1', '10 engine id unchanged');

const missing = missingAssetIsBlocked(['LOCATION:living-room']);
ok(!missing.ok && /CREATION REQUIRED/.test(missing.blocked[0] || ''), 'missing asset BLOCKED not invented');

const allowed = enforcePromptHierarchy({
  prompt: 'Subtle natural movement. Minh remains seated.',
  lockedCharacter: { hair: 'short black left tuft', identity: 'CHAR-001 Minh' },
  lockedLocation: { name: 'SC01 living room night' },
});
ok(allowed.ok, 'motion-only prompt does not override Canon');

if (fail.length) {
  console.error('KIT VIDEO VISUAL SYSTEM FAIL');
  for (const f of fail) console.error(' -', f);
  process.exit(1);
}
console.log('KIT VIDEO VISUAL SYSTEM PASS · tests 01–09 · FAMIXA V1 proposed · no provider lock-in');
