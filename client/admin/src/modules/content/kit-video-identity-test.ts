/** CHAR-001 Minh Identity Test — casting validation before Master. Not Canon. */

import { isLegacyOrGoldenPath } from './kit-video-master-reference';

export const MINH_IDENTITY_TEST_ID = 'CHAR-001_MINH_IDENTITY_TEST_SPEC_V1';
export const IDENTITY_TEST_KIND = 'IDENTITY_TEST';
export const IDENTITY_TEST_IMAGE_TYPE = 'IDENTITY_TEST';

export const VIEW_VARIANTS = ['FRONT', 'THREE_QUARTER_LEFT', 'THREE_QUARTER_RIGHT', 'PROFILE'] as const;
export const EMOTION_VARIANTS = ['NEUTRAL', 'SAD', 'LIGHT_SMILE'] as const;
export const REQUIRED_VARIANTS = [...VIEW_VARIANTS, ...EMOTION_VARIANTS] as const;
export type IdentityTestVariant = (typeof REQUIRED_VARIANTS)[number];
export type IdentityTestType = 'VIEW' | 'EMOTION';

export const IDENTITY_TEST_STATUSES = ['PENDING', 'RUNNING', 'PASS', 'CONDITIONAL', 'FAIL', 'BLOCKED'] as const;
export type IdentityTestStatus = (typeof IDENTITY_TEST_STATUSES)[number];

export const DIRECTOR_DECISIONS = ['PASS', 'CONDITIONAL', 'FAIL', 'REJECT', 'PROMOTE_TO_MASTER_REVIEW'] as const;
export type IdentityDirectorDecision = (typeof DIRECTOR_DECISIONS)[number];

export const PRIORITY_CANDIDATES = ['MINH-E01-CANDIDATE-004', 'MINH-E01-CANDIDATE-004-C', 'MINH-E01-CANDIDATE-004-D'] as const;

export function testTypeOf(variant: string): IdentityTestType {
  return (EMOTION_VARIANTS as readonly string[]).includes(variant) ? 'EMOTION' : 'VIEW';
}

export function acceptIdentityScope(input: { projectCode?: string; characterId?: string; eraId?: string }) {
  if ((input.projectCode || 'FAMIXA').toUpperCase() !== 'FAMIXA') {
    return { ok: false as const, blocked: 'IDENTITY_TEST: FAMIXA only.' };
  }
  if ((input.characterId || '').toUpperCase() !== 'CHAR-001') {
    return { ok: false as const, blocked: 'IDENTITY_TEST: CHAR-001 only.' };
  }
  if ((input.eraId || '').toUpperCase() !== 'ERA-01') {
    return { ok: false as const, blocked: 'IDENTITY_TEST: ERA-01 only.' };
  }
  return { ok: true as const };
}

export function dnaAllowsIdentityTest(status?: string) {
  return (status || '').toUpperCase() === 'APPROVED';
}

export function canOpenIdentityTest(input: {
  dnaStatus?: string;
  candidateCode?: string;
  frontRunner?: boolean;
  designated?: boolean;
  lockedMaster?: boolean;
}) {
  if (!dnaAllowsIdentityTest(input.dnaStatus)) {
    return { ok: false as const, blocked: 'IDENTITY_TEST_BLOCKED: Visual DNA phải APPROVED.' };
  }
  const code = (input.candidateCode || '').toUpperCase();
  if (/CANDIDATE-001$/.test(code) && !/CANDIDATE-001-/.test(code)) {
    return { ok: false as const, blocked: 'IDENTITY_TEST: Candidate-001 không vào vòng test này.' };
  }
  if (!input.frontRunner && !input.designated) {
    return { ok: false as const, blocked: 'IDENTITY_TEST: chỉ FRONT_RUNNER hoặc Director chỉ định.' };
  }
  return { ok: true as const, canon: false, locked: false };
}

export function identityTestKindGuard(kind?: string) {
  const t = (kind || '').toUpperCase();
  if (t === 'MASTER_REFERENCE' || t === 'CANON' || t === 'PRODUCTION_STILL' || t === 'LOCKED_REFERENCE') {
    return { ok: false as const, blocked: 'IDENTITY_TEST: test artifact không phải Master / Canon / Production Still.' };
  }
  return { ok: true as const, kind: IDENTITY_TEST_KIND };
}

export function compileIdentityTestPrompt(input: {
  variant: string;
  candidateCode: string;
  sourceSha256: string;
}) {
  const variant = input.variant.toUpperCase();
  const type = testTypeOf(variant);
  const viewLine =
    type === 'VIEW'
      ? `[VIEW] ${variant}. Change viewpoint only. Same boy. Same age 11. Same hair. Same face.`
      : '[VIEW] FRONT. Keep the same boy. Change expression only.';
  const emotionLine =
    type === 'EMOTION'
      ? `[EXPRESSION] ${variant}. Change emotion only. Identity must not change.`
      : '[EXPRESSION] NEUTRAL. Subtle. Identity must not change.';
  const prompt = [
    '[STYLE] Famixa stylized cinematic human. Not photoreal. Not cartoon. Not anime.',
    `[CHARACTER DNA] Minh 11. Same boy as ${input.candidateCode}. Soft slightly oval child face. Hair silhouette Canon.`,
    '[BRIEF] Identity Test. Prove it is the same Minh. Not a new character.',
    '[ERA] ERA-01 age 11.',
    `[REFERENCE] Source candidate ${input.candidateCode} hash ${input.sourceSha256.slice(0, 12)}. Visual DNA APPROVED. No Golden. No SH01-01.`,
    viewLine,
    emotionLine,
    '[WARDROBE] HOME baseline T-shirt. Wardrobe is not identity.',
    '[IMAGE CONTRACT] single_character single_image single_subject single_composition no_panels no_collage no_storyboard no_watermark no_logo no_unrequested_text',
    '[FORBIDDEN] character sheet, collage, storyboard, multi-panel, watermark, logo, photoreal, anime, adult body, new face',
  ].join('\n');
  if (prompt.includes('{')) {
    return { ok: false as const, blocked: 'IDENTITY_TEST: prompt must not dump JSON.', prompt: '', fingerprint: '' };
  }
  return { ok: true as const, prompt, type, variant };
}

export function fingerprintIdentityTest(input: {
  candidateId: string;
  testType: string;
  testVariant: string;
  dnaVersion?: string;
  sourceSha256: string;
}) {
  return [
    input.candidateId,
    input.testType,
    input.testVariant,
    input.dnaVersion || 'V1',
    (input.sourceSha256 || '').toLowerCase(),
  ].join('|');
}

export type IdentityObservation = {
  artifactExists?: boolean;
  readable?: boolean;
  sha256?: string;
  liveSha256?: string;
  qaSha256?: string;
  imageType?: string;
  characterCount?: number;
  minhPresent?: boolean;
  faceVisible?: boolean;
  croppedFace?: boolean;
  viewpoint?: string;
  requestedVariant?: string;
  emotionOk?: boolean;
  identitySame?: boolean;
  ageLook?: '11' | 'too_young' | 'too_old' | 'teen' | 'adult';
  hairSame?: boolean;
  sheet?: boolean;
  goldenPath?: string;
  score?: number;
};

export function evaluateIdentityP0(obs: IdentityObservation) {
  const p0: string[] = [];
  const diagnosis: string[] = [];
  if (obs.artifactExists === false || obs.readable === false) p0.push('ARTIFACT_FAILED');
  if (obs.sha256 && obs.liveSha256 && obs.sha256.toLowerCase() !== obs.liveSha256.toLowerCase()) p0.push('P0-01_HASH');
  if (obs.sha256 && obs.qaSha256 && obs.sha256.toLowerCase() !== obs.qaSha256.toLowerCase()) p0.push('P0-01_HASH');
  if (obs.sheet || obs.imageType === 'CHARACTER_SHEET' || obs.imageType === 'COLLAGE') {
    p0.push('IMAGE_TYPE_FAIL');
    diagnosis.push('CHARACTER_SHEET');
  }
  if (obs.characterCount != null && obs.characterCount !== 1) p0.push('P0-03_CHARACTER_COUNT');
  if (obs.minhPresent === false) p0.push('IDENTITY_MISSING');
  if (obs.faceVisible === false || obs.croppedFace) {
    p0.push('P0-04_IDENTITY_OBSTRUCTION');
    if (obs.croppedFace) diagnosis.push('FACE_CROP');
  }
  if (obs.identitySame === false) {
    p0.push('IDENTITY_DRIFT');
    diagnosis.push('IDENTITY_DRIFT');
  }
  if (obs.ageLook && obs.ageLook !== '11') {
    p0.push('AGE_DRIFT');
    diagnosis.push('AGE_DRIFT');
  }
  if (obs.hairSame === false) diagnosis.push('HAIR_DRIFT');
  const requested = (obs.requestedVariant || '').toUpperCase();
  if (requested && obs.viewpoint && obs.viewpoint.toUpperCase() !== requested && testTypeOf(requested) === 'VIEW') {
    p0.push('VIEWPOINT_FAILURE');
    diagnosis.push('VIEWPOINT_FAILURE');
  }
  if (testTypeOf(requested) === 'EMOTION' && obs.emotionOk === false && obs.identitySame !== false) {
    diagnosis.push('EMOTION_FAILURE');
  }
  if (isLegacyOrGoldenPath(obs.goldenPath || '')) p0.push('P0_GOLDEN_NOT_MASTER');
  return { p0, diagnosis };
}

export function evaluateIdentityArtifact(obs: IdentityObservation) {
  const { p0, diagnosis } = evaluateIdentityP0(obs);
  const score = Math.max(0, Math.min(100, obs.score ?? 70));
  const fail = p0.length > 0;
  return {
    ok: !fail,
    status: fail ? ('FAIL' as const) : ('PASS' as const),
    p0,
    diagnosis,
    score,
    usedScoreAsOverride: false as const,
    autoSelected: false as const,
    kind: IDENTITY_TEST_KIND,
  };
}

export function identityTestCompleteness(rows: { variant: string; sha256?: string; fingerprint?: string; vision?: string }[]) {
  const have = new Set(rows.filter((r) => r.sha256 && r.fingerprint && r.vision).map((r) => r.variant.toUpperCase()));
  const missing = REQUIRED_VARIANTS.filter((v) => !have.has(v));
  return {
    required: REQUIRED_VARIANTS.length,
    have: have.size,
    missing,
    complete: missing.length === 0,
    status: missing.length === 0 ? ('IDENTITY_TEST_COMPLETE' as const) : ('INCOMPLETE' as const),
  };
}

export function rollupIdentityTest(input: {
  artifacts: { variant: string; sha256?: string; fingerprint?: string; vision?: string; p0?: string[] }[];
  dnaApproved?: boolean;
}) {
  if (!input.dnaApproved) return { status: 'BLOCKED' as const, complete: false, autoSelected: false as const, canon: false };
  const done = identityTestCompleteness(input.artifacts);
  if (!done.complete) return { status: 'INCOMPLETE' as const, complete: false, autoSelected: false as const, canon: false };
  const anyP0 = input.artifacts.some((a) => (a.p0 || []).length > 0 || a.vision === 'FAIL');
  return {
    status: anyP0 ? ('FAIL' as const) : ('PASS' as const),
    complete: true,
    autoSelected: false as const,
    canon: false,
    locked: false,
  };
}

export function directorIdentityDecision(input: {
  decision: IdentityDirectorDecision;
  complete?: boolean;
  p0?: string[];
}) {
  if (input.decision === 'PROMOTE_TO_MASTER_REVIEW') {
    return {
      ok: true as const,
      masterCreated: false,
      locked: false,
      canon: false,
      promoteToMasterReview: true,
      selectMaster: false,
    };
  }
  return { ok: true as const, masterCreated: false, locked: false, canon: false, promoteToMasterReview: false, selectMaster: false };
}

export function reuseOrRetry(input: {
  fingerprint: string;
  existingFingerprint?: string;
  existingSha?: string;
  existingValid?: boolean;
  regenerate?: boolean;
}) {
  if (input.regenerate) {
    return { action: 'NEW_ATTEMPT' as const, reuse: false, blindRetry: false };
  }
  if (input.existingFingerprint === input.fingerprint && input.existingValid && input.existingSha) {
    return { action: 'REUSE_EXISTING_ARTIFACT' as const, reuse: true, blindRetry: false };
  }
  if (input.existingFingerprint === input.fingerprint && !input.existingValid) {
    return { action: 'DO_NOT_BLIND_RETRY' as const, reuse: false, blindRetry: true };
  }
  return { action: 'GENERATE' as const, reuse: false, blindRetry: false };
}

export function noRunwayInIdentityTest(blob: string) {
  return !/runway|i2v|lipsync|take-01|SH01-01/i.test(blob);
}

export function identityTestCreatesCanon(events: { type: string }[]) {
  return events.some((e) => /MASTER_CREATED|MASTER_LOCKED|CANON_LOCK|SELECT_AS_MASTER/.test(e.type));
}
