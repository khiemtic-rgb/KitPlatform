/** KIT Video Engine — generic Master Reference on video_asset. Famixa CHAR-001 consumes it. Not a FamixaMasterEngine. */

import { distinguishFrameKind } from './kit-video-visual-system';
import { MINH_DNA_ID } from './content-famixa-minh-visual-dna';
import { MINH_BRIEF_ID } from './content-famixa-character-brief';
import { FAMIXA_VISUAL_STYLE_SYSTEM_V1 } from './kit-video-visual-system';

export const KIT_VIDEO_MASTER_REFERENCE = 'KIT-VIDEO-MASTER-REFERENCE-V1';
export const MINH_MASTER_SPEC_ID = 'CHAR-001_MINH_MASTER_REFERENCE_SPEC_V1';
export const MINH_BRIEF_SPEC_ALIAS = 'CHAR-001_MINH_CHARACTER_BRIEF_V1';
export const MASTER_ERA_01 = 'ERA-01';
export const MASTER_VERSION = 'V1';

export type MasterRefStatus = 'DRAFT' | 'CREATING' | 'REVIEW' | 'APPROVED' | 'LOCKED';
export type MasterQaStatus = 'PENDING' | 'PASS' | 'FAIL' | 'BLOCK' | 'DIAGNOSE';
export type MasterImageType = 'UNKNOWN' | 'PRODUCTION_STILL' | 'CHARACTER_SHEET' | 'COLLAGE' | 'MULTI_PANEL' | 'REFERENCE_BOARD';
export type MasterRole =
  | 'IDENTITY'
  | 'FACE'
  | 'FRONT'
  | 'THREE_Q_LEFT'
  | 'THREE_Q_RIGHT'
  | 'SIDE'
  | 'NEUTRAL'
  | 'HAPPY'
  | 'SAD'
  | 'HURT'
  | 'ANGRY'
  | 'BODY'
  | 'SHEET';

export const MASTER_STEPS = [
  'IDENTITY_EXPLORATION',
  'CANDIDATE_SELECTION',
  'FACE_CONSISTENCY',
  'POSE_ANGLE_VALIDATION',
  'EXPRESSION_VALIDATION',
  'BODY_SILHOUETTE_VALIDATION',
  'MASTER_ASSEMBLY',
  'DIRECTOR_APPROVAL',
  'LOCK',
] as const;

export const MASTER_PACKAGE_ROLES: MasterRole[] = [
  'FRONT',
  'THREE_Q_LEFT',
  'THREE_Q_RIGHT',
  'SIDE',
  'NEUTRAL',
  'HAPPY',
  'SAD',
  'HURT',
  'ANGRY',
  'BODY',
];

export const MASTER_SOURCES = [
  FAMIXA_VISUAL_STYLE_SYSTEM_V1.documentId,
  MINH_BRIEF_ID,
  MINH_BRIEF_SPEC_ALIAS,
  MINH_DNA_ID,
  MASTER_ERA_01,
] as const;

export type MasterCandidate = {
  candidateId: string;
  generationAttempt: number;
  artifactPath: string;
  sha256: string;
  visualStyleVersion: string;
  characterId: string;
  eraId: string;
  role: MasterRole | string;
  imageType?: MasterImageType | string;
  status?: MasterRefStatus | string;
  qaStatus?: MasterQaStatus | string;
  fingerprint?: string;
};

export type MasterQaObservation = {
  artifactExists?: boolean;
  sha256?: string;
  persistedSha256?: string;
  approvalSha256?: string;
  characterId?: string;
  expectedCharacterId?: string;
  era?: string;
  expectedEra?: string;
  age?: number;
  expectedAge?: number;
  identityMatch?: boolean;
  styleMatch?: boolean;
  imageType?: MasterImageType | string;
  unwantedText?: boolean;
  watermark?: boolean;
  corrupt?: boolean;
  photoreal?: boolean;
  anime?: boolean;
  cartoonExcess?: boolean;
  childProportion?: boolean;
  beautyScore?: number;
  angles?: { role: string; identityMatch?: boolean }[];
  expressions?: { role: string; identityMatch?: boolean }[];
};

export type MasterQaResult = {
  ok: boolean;
  status: MasterQaStatus;
  p0: string[];
  p1: string[];
  p2: string[];
  usedBeautyScore: false;
};

export const CHAR_001_MINH_MASTER_REFERENCE_SPEC_V1 = {
  documentId: MINH_MASTER_SPEC_ID,
  status: 'DRAFT' as MasterRefStatus,
  projectCode: 'FAMIXA',
  assetKind: 'CHARACTER' as const,
  assetCode: 'CHAR-001',
  characterId: 'CHAR-001',
  era: MASTER_ERA_01,
  age: 11,
  version: MASTER_VERSION,
  purpose: 'Visual Source of Truth for Minh — consistency over beauty, identity over realism, emotion over perfection.',
  sources: [...MASTER_SOURCES],
  notProductionStill: true,
  notI2vSource: true,
  steps: [...MASTER_STEPS],
  packageRoles: [...MASTER_PACKAGE_ROLES],
  wardrobeAssets: ['WARDROBE-001', 'WARDROBE-002'],
  lighting: 'soft, face-clear, stable — no cinematic darkness, neon, strong rim, extreme shadow',
  background: 'neutral simple controlled — no living room, school, street, extras, props',
  providerIndependent: true,
  providers: [] as string[],
  generateEnabled: false,
  forbiddenSources: [
    'random pretty still as Canon',
    'SH01-01 / Golden Shot / take-01.mp4',
    'legacy /content/famixa/canon CHAR sheets',
    'screenshot / thumbnail / collage / watermark',
  ],
  lockRule: 'LOCKED is immutable. Change = V2. Do not overwrite V1.',
};

export function isMinhMasterSpec(raw?: { documentId?: string; characterId?: string } | null) {
  return raw?.documentId === MINH_MASTER_SPEC_ID && (!raw.characterId || raw.characterId === 'CHAR-001');
}

export function nextCandidateCode(existing: string[], era = MASTER_ERA_01) {
  const prefix = era === MASTER_ERA_01 ? 'MINH-E01-CANDIDATE-' : `MINH-${era}-CANDIDATE-`;
  let max = 0;
  for (const id of existing) {
    const m = id.match(/CANDIDATE-(\d+)$/i);
    if (m) max = Math.max(max, Number(m[1]));
  }
  return `${prefix}${String(max + 1).padStart(3, '0')}`;
}

export function registerCandidate(
  existing: MasterCandidate[],
  input: Omit<MasterCandidate, 'candidateId'> & { candidateId?: string },
) {
  const candidateId = input.candidateId || nextCandidateCode(existing.map((c) => c.candidateId), input.eraId || MASTER_ERA_01);
  if (existing.some((c) => c.candidateId === candidateId)) {
    return { ok: false as const, blocked: 'CANDIDATE_EXISTS: do not overwrite.', candidate: existing.find((c) => c.candidateId === candidateId)! };
  }
  if (!input.characterId) return { ok: false as const, blocked: 'CANDIDATE_ID: character ID required.' };
  if (!input.eraId) return { ok: false as const, blocked: 'CANDIDATE_ERA: era required.' };
  const candidate: MasterCandidate = {
    ...input,
    candidateId,
    generationAttempt: input.generationAttempt || 1,
    visualStyleVersion: input.visualStyleVersion || FAMIXA_VISUAL_STYLE_SYSTEM_V1.version,
    sha256: (input.sha256 || '').toLowerCase(),
    artifactPath: input.artifactPath || '',
    status: input.status || 'DRAFT',
    qaStatus: input.qaStatus || 'PENDING',
  };
  return { ok: true as const, candidate, candidates: [...existing, candidate] };
}

export function isLegacyOrGoldenPath(path: string) {
  return /famixa\/canon\/CHAR-001|take-01|SH01-01|Golden|golden-sh01/i.test(path || '');
}

export function canUseAsMasterSource(path: string) {
  if (!path.trim()) return { ok: false as const, blocked: 'ARTIFACT_MISSING' };
  if (isLegacyOrGoldenPath(path)) return { ok: false as const, blocked: 'LEGACY_OR_GOLDEN_NOT_MASTER' };
  return { ok: true as const };
}

export function evaluateMasterQa(obs: MasterQaObservation): MasterQaResult {
  const p0: string[] = [];
  const p1: string[] = [];
  const p2: string[] = [];
  if (obs.artifactExists === false || !(obs.sha256 || obs.persistedSha256 || obs.artifactExists)) {
    p0.push('ARTIFACT_MISSING');
  }
  if (obs.artifactExists === false) p0.push('ARTIFACT_MISSING');
  if (obs.sha256 && obs.persistedSha256 && obs.sha256.toLowerCase() !== obs.persistedSha256.toLowerCase()) {
    p0.push('HASH_MISMATCH');
  }
  if (obs.approvalSha256 && obs.sha256 && obs.approvalSha256.toLowerCase() !== obs.sha256.toLowerCase()) {
    p0.push('HASH_MISMATCH');
  }
  if (obs.expectedCharacterId && obs.characterId && obs.characterId !== obs.expectedCharacterId) p0.push('IDENTITY_FAIL');
  if (obs.identityMatch === false) p0.push('IDENTITY_FAIL');
  if (obs.expectedEra && obs.era && obs.era !== obs.expectedEra) p0.push('ERA_FAIL');
  if (obs.expectedAge != null && obs.age != null && obs.age !== obs.expectedAge) p0.push('AGE_FAIL');
  if (obs.styleMatch === false) p0.push('STYLE_FAIL');
  if (obs.photoreal) p0.push('STYLE_FAIL:photoreal');
  if (obs.anime) p0.push('STYLE_FAIL:anime');
  if (obs.cartoonExcess) p0.push('STYLE_FAIL:cartoon');
  if (obs.childProportion === false) p0.push('BODY_FAIL');
  if (obs.corrupt) p0.push('CORRUPT');
  if (obs.unwantedText) p0.push('UNWANTED_TEXT');
  if (obs.watermark) p0.push('WATERMARK');
  const kind = distinguishFrameKind((obs.imageType === 'CHARACTER_SHEET' ? 'CHARACTER_SHEET' : 'PRODUCTION_STILL') as 'PRODUCTION_STILL' | 'CHARACTER_SHEET');
  if (obs.imageType === 'CHARACTER_SHEET' || obs.imageType === 'COLLAGE' || obs.imageType === 'MULTI_PANEL') {
    if (!kind.i2vAllowed) p0.push('SHEET_NOT_PRODUCTION_STILL');
  }
  for (const a of obs.angles || []) {
    if (a.identityMatch === false) p1.push(`ANGLE_INCONSISTENT:${a.role}`);
  }
  for (const e of obs.expressions || []) {
    if (e.identityMatch === false) p1.push(`EXPRESSION_INCONSISTENT:${e.role}`);
  }
  if (typeof obs.beautyScore === 'number' && obs.beautyScore < 70) p2.push('AESTHETIC');
  const status: MasterQaStatus = p0.length ? (p0.includes('ARTIFACT_MISSING') || p0.includes('HASH_MISMATCH') ? 'BLOCK' : 'FAIL') : p1.length ? 'FAIL' : 'PASS';
  return { ok: status === 'PASS', status, p0, p1, p2, usedBeautyScore: false };
}

export function canPromoteToProductionStill(imageType?: string) {
  const t = (imageType || '').toUpperCase();
  if (t === 'CHARACTER_SHEET' || t === 'COLLAGE' || t === 'MULTI_PANEL' || t === 'REFERENCE_BOARD') {
    return { ok: false as const, blocked: 'CHARACTER_SHEET cannot become a Production Still or I2V source.' };
  }
  return { ok: t === 'PRODUCTION_STILL' };
}

export function canApproveMaster(input: {
  status?: string;
  candidates: MasterCandidate[];
  qa: MasterQaResult[];
  directorApprove?: boolean;
}) {
  if ((input.status || '').toUpperCase() === 'LOCKED') return { ok: false as const, blocked: 'Already LOCKED.' };
  if (!input.candidates.length) return { ok: false as const, blocked: 'No candidates.' };
  if (input.qa.some((q) => !q.ok)) return { ok: false as const, blocked: 'QA not PASS.' };
  if (!input.directorApprove) return { ok: false as const, blocked: 'Director APPROVE required. AI cannot LOCK.' };
  return { ok: true as const };
}

export function canLockMaster(input: {
  status?: string;
  directorApproved?: boolean;
  artifactExists?: boolean;
  hashes: { artifact?: string; reference?: string; approval?: string };
  visionPass?: boolean;
  identityPass?: boolean;
  stylePass?: boolean;
  multiAnglePass?: boolean;
  expressionPass?: boolean;
  bodyPass?: boolean;
}) {
  const missing: string[] = [];
  if ((input.status || '').toUpperCase() === 'LOCKED') missing.push('ALREADY_LOCKED');
  if (!input.directorApproved) missing.push('DIRECTOR_APPROVE');
  if (input.artifactExists === false) missing.push('ARTIFACT');
  if (!input.hashes.artifact || !input.hashes.reference || !input.hashes.approval) missing.push('HASH_LINK');
  if (input.hashes.artifact && input.hashes.reference && input.hashes.artifact !== input.hashes.reference) missing.push('HASH_MISMATCH');
  if (input.hashes.artifact && input.hashes.approval && input.hashes.artifact !== input.hashes.approval) missing.push('HASH_MISMATCH');
  if (input.visionPass === false) missing.push('VISION');
  if (input.identityPass === false) missing.push('IDENTITY');
  if (input.stylePass === false) missing.push('STYLE');
  if (input.multiAnglePass === false) missing.push('MULTI_ANGLE');
  if (input.expressionPass === false) missing.push('EXPRESSION');
  if (input.bodyPass === false) missing.push('BODY');
  if ((input.status || '').toUpperCase() !== 'APPROVED' && input.directorApproved) {
    /* director may approve first; lock still needs APPROVED */
  }
  if ((input.status || '').toUpperCase() !== 'APPROVED') missing.push('NOT_APPROVED');
  return missing.length ? { ok: false as const, blocked: missing } : { ok: true as const };
}

export function ensureMasterImmutable(current: { status: string; version: string }, next: { version: string; overwrite?: boolean }) {
  if ((current.status || '').toUpperCase() === 'LOCKED' && current.version === next.version && next.overwrite !== false) {
    throw new Error('MASTER_LOCKED: V1 không overwrite. Tạo MASTER-REF-V2.');
  }
}

export function nextMasterVersion(current = MASTER_VERSION) {
  const n = Number((current || 'V1').replace(/\D/g, '')) || 1;
  return `V${n + 1}`;
}

export function diagnoseRepair(fail: string) {
  const f = fail.toUpperCase();
  if (/HAIR/.test(f)) return { region: 'HAIR' as const, instruction: 'Revise hair silhouette / hairline only.' };
  if (/FACE|IDENTITY|EYE|NOSE|MOUTH/.test(f)) return { region: 'FACE' as const, instruction: 'Revise face identity only.' };
  if (/STYLE|PHOTOREAL|ANIME|CARTOON/.test(f)) return { region: 'STYLE' as const, instruction: 'Revise style instruction only.' };
  if (/BODY|AGE|PROPORTION/.test(f)) return { region: 'BODY' as const, instruction: 'Revise child proportion only.' };
  return { region: 'DIAGNOSE' as const, instruction: 'Diagnose before a new attempt. Do not rewrite the whole prompt.' };
}

export function doNotBlindRetry(a: { fingerprint?: string; sha256?: string; prompt?: string; config?: string }, b: typeof a) {
  const same =
    !!a.fingerprint &&
    a.fingerprint === b.fingerprint &&
    (a.sha256 || '') === (b.sha256 || '') &&
    (a.prompt || '') === (b.prompt || '') &&
    (a.config || '') === (b.config || '');
  return same
    ? { ok: false as const, blocked: 'DO_NOT_BLIND_RETRY' }
    : { ok: true as const };
}

export function persistenceFailMeansRevalidate(reason: string) {
  return /persist|metadata|database|ui|hash/i.test(reason);
}

export function productionStillMayReferenceMaster(master: { status?: string; path?: string }, still: { path?: string }) {
  return {
    ok: true as const,
    stillIsNotMaster: still.path !== master.path,
    referenced: Boolean(master.path),
    source: 'LOCKED_MASTER_REFERENCE',
    usable: (master.status || '').toUpperCase() === 'LOCKED',
  };
}

export function compileMasterCandidatePrompt(input: {
  role: MasterRole | string;
  dnaApproved?: boolean;
  styleReady?: boolean;
}) {
  if (!input.styleReady) return { ok: false as const, blocked: 'Style System / Visual Style must be REVIEW/APPROVED first.', prompt: '' };
  if (!input.dnaApproved) return { ok: false as const, blocked: 'CHAR-001 Visual DNA V1 must be APPROVED before Master candidates.', prompt: '' };
  const prompt = [
    'SUBJECT: CHAR-001 Minh ERA-01 age 11 — Stylized Cinematic Human from Visual DNA V1',
    `VIEW: ${input.role}`,
    'EXPRESSION: default neutral unless role is HAPPY/SAD/HURT/ANGRY',
    'ENVIRONMENT: simple neutral background, no room, no school, no extras, no props, no text',
    'LIGHTING: soft, face-clear, stable',
    'STYLE: FAMIXA VISUAL STYLE SYSTEM V1',
    'CONTINUITY: same face, hair silhouette, 11yo proportion',
    'NEGATIVE: photoreal child, anime, cartoon, idol, adult body, golden shot, character sheet collage, watermark, logo',
  ].join('\n');
  return { ok: true as const, prompt, provider: 'none', generateEnabled: false };
}

export function refuseGenerate(controlledTest?: boolean) {
  if (!controlledTest) return { ok: false as const, blocked: 'GEMINI_NOT_CALLED: Master generation is off. Compile only.' };
  return { ok: false as const, blocked: 'GEMINI_NOT_CALLED: controlled test must not become Canon.' };
}

export function isProviderIndependent() {
  const blob = JSON.stringify(CHAR_001_MINH_MASTER_REFERENCE_SPEC_V1);
  return CHAR_001_MINH_MASTER_REFERENCE_SPEC_V1.providerIndependent && CHAR_001_MINH_MASTER_REFERENCE_SPEC_V1.providers.length === 0 && !/gemini|runway/i.test(blob);
}

export function eraRemainsChar001(characterId: string, era: string) {
  const id = (characterId || '').toUpperCase();
  const e = (era || '').toUpperCase();
  if (/^CHAR-00[5-9]/.test(id)) return { ok: false as const, blocked: 'Do not invent CHAR-005+ for Minh older.' };
  return { ok: id === 'CHAR-001' && (e === 'ERA-01' || e === 'ERA-02' || e === 'ERA-03'), characterId: 'CHAR-001', era: e };
}
