/** CHAR-001 Minh Identity Stress Test Implementation V1 — QA layer, not Master. */

import { isLegacyOrGoldenPath } from './kit-video-master-reference';

export const MINH_IDENTITY_STRESS_ID = 'CHAR-001_MINH_IDENTITY_STRESS_TEST_IMPLEMENTATION_V1';
export const STRESS_KIND = 'IDENTITY_STRESS';
export const STRESS_ALLOWED_CANDIDATE = 'MINH-E01-CANDIDATE-004-D';
export const STRESS_SCORE_THRESHOLD = 80;
export const STRESS_PROMPT_VERSION = 'V1';

export const STRESS_CASES = [
  { code: 'ST-01', group: 'CAMERA', key: 'EXTREME_3_4', label: 'Extreme 3/4', prompt: 'Strong three-quarter view. Same Minh. Face structure must stay the same boy.' },
  { code: 'ST-02', group: 'CAMERA', key: 'HIGH_ANGLE', label: 'High angle', prompt: 'Camera above eye level. Same eyes nose mouth head proportion. No warp.' },
  { code: 'ST-03', group: 'CAMERA', key: 'LOW_ANGLE', label: 'Low angle', prompt: 'Camera below eye level. Keep jaw nose eye spacing hair silhouette. Same Minh 11.' },
  { code: 'ST-04', group: 'CAMERA', key: 'LOOKING_UP_DOWN', label: 'Looking up / down', prompt: 'Minh looking up or down. Same face. Not a new boy.' },
  { code: 'ST-05', group: 'EMOTION', key: 'STRONG_EMOTION', label: 'Strong emotion', prompt: 'Strong but not extreme emotion: worried or surprised or sad or happy. Identity must not change.' },
  { code: 'ST-06', group: 'POSE', key: 'MOTION_POSE', label: 'Motion pose', prompt: 'Minh in motion: walking fast or turning. Not a portrait pose. Same boy.' },
  { code: 'ST-07', group: 'LIGHTING', key: 'LIGHTING_CHANGE', label: 'Lighting change', prompt: 'Changed light: indoor soft or warm or cool or outdoor. Lighting must not change identity.' },
  { code: 'ST-08', group: 'OCCLUSION', key: 'PARTIAL_OCCLUSION', label: 'Partial occlusion', prompt: 'Partial occlusion by hair or hand or foreground object. Face still readable as Minh.' },
  { code: 'ST-09', group: 'SCENE', key: 'SCENE_CONTEXT', label: 'Scene context', prompt: 'Minh sitting at a simple study desk. Single scene. Not a sheet. Same boy.' },
  { code: 'ST-10', group: 'SCENE', key: 'MULTI_PERSON', label: 'Multi person', prompt: 'Minh with one other person. Identify Minh correctly. No face merge. No duplicate Minh. Not a Master.' },
] as const;

export const STRESS_GROUPS = ['CAMERA', 'EMOTION', 'POSE', 'LIGHTING', 'OCCLUSION', 'SCENE'] as const;

export function stressCase(code: string) {
  return STRESS_CASES.find((c) => c.code === code);
}

export function isAllowedStressCandidate(code?: string) {
  return (code || '').toUpperCase() === STRESS_ALLOWED_CANDIDATE;
}

export function canOpenStressTest(input: {
  characterId?: string;
  eraId?: string;
  candidateCode?: string;
  dnaStatus?: string;
  identityEligible?: boolean;
  identityTestComplete?: boolean;
  artifactOk?: boolean;
  artifactReadable?: boolean;
  sha256?: string;
  liveSha256?: string;
  visionPass?: boolean;
  lifecycle?: string;
  lockedMaster?: boolean;
}) {
  if ((input.characterId || '').toUpperCase() !== 'CHAR-001') {
    return { ok: false as const, blocked: 'STRESS_TEST_BLOCKED: CHAR-001 only.' };
  }
  if ((input.eraId || 'ERA-01').toUpperCase() !== 'ERA-01') {
    return { ok: false as const, blocked: 'STRESS_TEST_BLOCKED: ERA-01 only.' };
  }
  if (!isAllowedStressCandidate(input.candidateCode)) {
    return { ok: false as const, blocked: 'STRESS_TEST_BLOCKED: chỉ MINH-E01-CANDIDATE-004-D.' };
  }
  if ((input.dnaStatus || '').toUpperCase() !== 'APPROVED') {
    return { ok: false as const, blocked: 'STRESS_TEST_BLOCKED: Visual DNA phải APPROVED.' };
  }
  if (input.lockedMaster) return { ok: false as const, blocked: 'STRESS_TEST_BLOCKED: Master đã LOCKED.' };
  if (input.lifecycle === 'INELIGIBLE' || input.lifecycle === 'NOT_ELIGIBLE' || input.lifecycle === 'REJECTED') {
    return { ok: false as const, blocked: 'STRESS_TEST_BLOCKED: candidate không eligible.' };
  }
  if (input.identityEligible === false) return { ok: false as const, blocked: 'STRESS_TEST_BLOCKED: identity candidate chưa eligible.' };
  if (!input.identityTestComplete) return { ok: false as const, blocked: 'STRESS_TEST_BLOCKED: Identity Test 7/7 chưa COMPLETE.' };
  if (input.artifactOk === false || input.artifactReadable === false || !input.sha256) {
    return { ok: false as const, blocked: 'STRESS_TEST_BLOCKED: artifact / SHA256 chưa đủ.' };
  }
  if (input.liveSha256 && input.sha256.toLowerCase() !== input.liveSha256.toLowerCase()) {
    return { ok: false as const, blocked: 'STRESS_TEST_BLOCKED: source SHA mismatch.' };
  }
  if (input.visionPass === false) return { ok: false as const, blocked: 'STRESS_TEST_BLOCKED: Vision chưa PASS.' };
  return { ok: true as const, canon: false, locked: false, autoSelect: false, productionStill: false };
}

export function sourceStillValid(input: { storedSha?: string; liveSha?: string; storedFingerprint?: string; liveFingerprint?: string }) {
  if (input.storedSha && input.liveSha && input.storedSha.toLowerCase() !== input.liveSha.toLowerCase()) {
    return { ok: false as const, blocked: 'STRESS_TEST_INVALIDATED: source SHA changed.' };
  }
  if (input.storedFingerprint && input.liveFingerprint && input.storedFingerprint !== input.liveFingerprint) {
    return { ok: false as const, blocked: 'STRESS_TEST_INVALIDATED: source fingerprint changed.' };
  }
  return { ok: true as const };
}

export function compileStressPrompt(input: { caseCode: string; candidateCode: string; sourceSha256: string }) {
  const row = stressCase(input.caseCode);
  if (!row) return { ok: false as const, blocked: 'STRESS: case không hợp lệ.', prompt: '' };
  const people = row.code === 'ST-10' ? 'two_people minh_plus_one' : 'single_character single_subject';
  const prompt = [
    '[STYLE] Famixa stylized cinematic human. Not photoreal. Not cartoon. Not anime.',
    `[CHARACTER DNA] Minh 11. Same boy as ${STRESS_ALLOWED_CANDIDATE}. Soft slightly oval child face. Hair silhouette Canon.`,
    `[STRESS] ${row.code} ${row.key} ${row.label}. ${row.prompt}`,
    '[ERA] ERA-01 age 11.',
    `[REFERENCE] Source ${input.candidateCode} hash ${input.sourceSha256.slice(0, 12)}. Visual DNA APPROVED. Identity Test V1. No Golden. No SH01-01.`,
    `[IMAGE CONTRACT] singleFrame singleScene singleComposition ${people} no_panels no_collage no_storyboard no_watermark no_logo no_unrequested_text`,
    '[FORBIDDEN] character sheet, collage, storyboard, multi-panel, watermark, logo, photoreal, anime, new face, teen, adult, face merge, duplicate Minh',
  ].join('\n');
  if (prompt.includes('{')) return { ok: false as const, blocked: 'STRESS: prompt must not dump JSON.', prompt: '' };
  return { ok: true as const, prompt, group: row.group, code: row.code, key: row.key };
}

export function fingerprintStress(input: {
  characterId?: string;
  candidateId: string;
  caseCode: string;
  sourceFingerprint?: string;
  sourceSha256?: string;
  stressTestVersion?: string;
  promptVersion?: string;
}) {
  return [
    input.characterId || 'CHAR-001',
    input.candidateId,
    input.stressTestVersion || 'V1',
    input.caseCode,
    (input.sourceFingerprint || input.sourceSha256 || '').toLowerCase(),
    input.promptVersion || STRESS_PROMPT_VERSION,
  ].join('|');
}

export type StressScores = {
  identityScore: number;
  facialStructureScore: number;
  hairScore: number;
  eyeScore: number;
  ageConsistencyScore: number;
  styleConsistencyScore: number;
  sceneComplianceScore: number;
};

export const EMPTY_STRESS_SCORES: StressScores = {
  identityScore: 0,
  facialStructureScore: 0,
  hairScore: 0,
  eyeScore: 0,
  ageConsistencyScore: 0,
  styleConsistencyScore: 0,
  sceneComplianceScore: 0,
};

export function clampScore(n?: number) {
  return Math.max(0, Math.min(100, n ?? 0));
}

export type StressObservation = {
  artifactExists?: boolean;
  artifactReadable?: boolean;
  sha256?: string;
  liveSha256?: string;
  faceSame?: boolean;
  ageLook?: '11' | 'too_young' | 'too_old' | 'teen' | 'adult';
  hairSame?: boolean;
  proportionSame?: boolean;
  distinctiveKept?: boolean;
  differentPerson?: boolean;
  missingSubject?: boolean;
  faceDeformed?: boolean;
  duplicateMinh?: boolean;
  faceMerge?: boolean;
  sheet?: boolean;
  collage?: boolean;
  multiPanel?: boolean;
  textHeavy?: boolean;
  logo?: boolean;
  watermark?: boolean;
  visionFail?: boolean;
  conditionSatisfied?: boolean;
  goldenPath?: string;
  scores?: Partial<StressScores>;
};

export function evaluateStressP0(obs: StressObservation) {
  const p0: string[] = [];
  if (obs.artifactExists === false || obs.artifactReadable === false) p0.push('ARTIFACT_FAILED');
  if (obs.sha256 && obs.liveSha256 && obs.sha256.toLowerCase() !== obs.liveSha256.toLowerCase()) p0.push('P0-01_HASH');
  if (obs.sheet || obs.collage || obs.multiPanel || obs.textHeavy || obs.logo || obs.watermark) p0.push('IMAGE_TYPE_FAIL');
  if (obs.missingSubject) p0.push('MISSING_SUBJECT');
  if (obs.faceSame === false || obs.differentPerson) p0.push('IDENTITY_MISMATCH');
  if (obs.faceDeformed) p0.push('FACE_DEFORMATION');
  if (obs.duplicateMinh) p0.push('DUPLICATE_MINH');
  if (obs.faceMerge) p0.push('FACE_MERGE');
  if (obs.ageLook && obs.ageLook !== '11') p0.push('AGE_DRIFT');
  if (obs.hairSame === false) p0.push('HAIR_IDENTITY_BREAK');
  if (obs.proportionSame === false) p0.push('FACIAL_PROPORTION_BREAK');
  if (obs.distinctiveKept === false) p0.push('DISTINCTIVE_FEATURE_LOSS');
  if (isLegacyOrGoldenPath(obs.goldenPath || '')) p0.push('P0_GOLDEN_NOT_MASTER');
  return { p0 };
}

export function evaluateStressCase(obs: StressObservation) {
  const { p0 } = evaluateStressP0(obs);
  const scores: StressScores = {
    identityScore: clampScore(obs.scores?.identityScore),
    facialStructureScore: clampScore(obs.scores?.facialStructureScore),
    hairScore: clampScore(obs.scores?.hairScore),
    eyeScore: clampScore(obs.scores?.eyeScore),
    ageConsistencyScore: clampScore(obs.scores?.ageConsistencyScore),
    styleConsistencyScore: clampScore(obs.scores?.styleConsistencyScore),
    sceneComplianceScore: clampScore(obs.scores?.sceneComplianceScore),
  };
  const below = scores.identityScore < STRESS_SCORE_THRESHOLD;
  const condition = obs.conditionSatisfied !== false;
  const visionOk = obs.visionFail !== true;
  const fail = p0.length > 0 || below || !condition || !visionOk || obs.artifactExists === false;
  return {
    ok: !fail,
    status: fail ? ('FAIL' as const) : ('PASS' as const),
    p0,
    scores,
    usedScoreAsOverride: false as const,
    allowMasterReview: !fail && p0.length === 0,
    kind: STRESS_KIND,
    productionStill: false as const,
    locked: false as const,
  };
}

export function rollupStress(cases: { code: string; p0?: string[]; sha256?: string; status?: string; scores?: Partial<StressScores> }[]) {
  const have = cases.filter((c) => c.sha256).length;
  const p0 = cases.reduce((n, c) => n + (c.p0?.length || 0), 0);
  const passed = cases.filter((c) => c.status === 'PASS' && !(c.p0?.length)).length;
  const complete = have === STRESS_CASES.length;
  let status: 'NOT_RUN' | 'RUNNING' | 'BLOCKED' | 'PASS' | 'FAIL' = 'NOT_RUN';
  if (have === 0) status = 'NOT_RUN';
  else if (!complete) status = 'RUNNING';
  else if (p0 > 0 || passed < STRESS_CASES.length) status = 'FAIL';
  else status = 'PASS';
  const avg = (key: keyof StressScores) => {
    const vals = cases.map((c) => c.scores?.[key]).filter((n): n is number => typeof n === 'number');
    if (!vals.length) return 0;
    return Math.round(vals.reduce((a, b) => a + b, 0) / vals.length);
  };
  return {
    have,
    required: STRESS_CASES.length,
    complete,
    p0,
    passed,
    status,
    scores: {
      identityScore: avg('identityScore'),
      facialStructureScore: avg('facialStructureScore'),
      hairScore: avg('hairScore'),
      eyeScore: avg('eyeScore'),
      ageConsistencyScore: avg('ageConsistencyScore'),
      styleConsistencyScore: avg('styleConsistencyScore'),
      sceneComplianceScore: avg('sceneComplianceScore'),
    },
    allowMasterReview: status === 'PASS',
    autoSelect: false as const,
    canon: false,
    locked: false,
    productionStill: false,
  };
}

export function reuseOrRepair(input: { fingerprint: string; existingFingerprint?: string; existingValid?: boolean; regenerate?: boolean; diagnosis?: string }) {
  if (input.existingFingerprint === input.fingerprint && input.existingValid) {
    return { action: 'DO_NOT_BLIND_RETRY' as const, blindRetry: true, reuse: true };
  }
  if (input.regenerate && !(input.diagnosis || '').trim()) {
    return { action: 'DO_NOT_BLIND_RETRY' as const, blindRetry: true };
  }
  if (input.regenerate && (input.diagnosis || '').trim()) return { action: 'NEW_ATTEMPT' as const, blindRetry: false };
  return { action: 'GENERATE' as const, blindRetry: false };
}

export function directorCanApprove(stressStatus?: string) {
  if (stressStatus !== 'PASS') {
    return { ok: false as const, blocked: 'DIRECTOR: không approve khi Stress FAIL / chưa đủ 10 PASS.', master: false };
  }
  return { ok: true as const, master: false, locked: false, readyForMasterReview: true };
}

export function directorStressDecision(decision: 'PASS' | 'REJECT' | 'REPAIR' | 'PROMOTE_TO_MASTER_REVIEW', stressStatus?: string) {
  if (decision === 'PASS' && stressStatus && stressStatus !== 'PASS') {
    return { ok: false as const, blocked: 'DIRECTOR: không approve test FAIL.', masterCreated: false, locked: false };
  }
  return {
    ok: true as const,
    decision,
    masterCreated: false,
    locked: false,
    canon: false,
    selectMaster: false,
    productionStill: false,
    readyForMasterReview: decision === 'PASS' || decision === 'PROMOTE_TO_MASTER_REVIEW',
    promoteToMasterReview: decision === 'PROMOTE_TO_MASTER_REVIEW',
  };
}

export function canPromoteAfterStress(input: {
  identityComplete?: boolean;
  stressStatus?: string;
  directorPass?: boolean;
  artifactOk?: boolean;
  dnaApproved?: boolean;
}) {
  if (!input.identityComplete || input.stressStatus !== 'PASS' || !input.directorPass || !input.artifactOk || !input.dnaApproved) {
    return { ok: false as const, blocked: 'MASTER_GATE: Identity PASS + Stress PASS + Director PASS.', master: false, locked: false };
  }
  return { ok: true as const, master: false, locked: false, readyForMasterReview: true };
}

export function stressArtifactKindGuard(kind?: string) {
  const t = (kind || '').toUpperCase();
  if (t === 'PRODUCTION_STILL' || t === 'MASTER_REFERENCE' || t === 'CANON' || t === 'LOCKED_REFERENCE') {
    return { ok: false as const, blocked: 'STRESS: artifact không phải Production Still / Master / Canon.' };
  }
  return { ok: true as const, kind: STRESS_KIND };
}

export function noRunwayInStress(blob: string) {
  return !/runway|i2v|lipsync|take-01|SH01-01|phase.?07|film assembly/i.test(blob);
}

const COLLAPSED_P0 = [
  'IDENTITY_MISMATCH',
  'FACE_DEFORMATION',
  'AGE_DRIFT',
  'HAIR_IDENTITY_BREAK',
  'FACIAL_PROPORTION_BREAK',
  'DISTINCTIVE_FEATURE_LOSS',
] as const;

export function diagnoseCollapsedStressQa(qa?: Record<string, unknown> | null) {
  const p0 = Array.isArray(qa?.p0) ? (qa.p0 as string[]) : [];
  const score = typeof qa?.identityScore === 'number' ? qa.identityScore : -1;
  const vision = String(qa?.vision || '');
  const collapsed = score === 40 && vision === 'FAIL' && p0.length === 6 && COLLAPSED_P0.every((x) => p0.includes(x));
  if (!collapsed && qa?.diagnostic && typeof qa.diagnostic === 'object') {
    return qa.diagnostic as Record<string, unknown>;
  }
  if (!collapsed) return null;
  return {
    collapsedCascade: true,
    root: 'VISION_OVERALL_FAIL_CASCADE',
    sourceCandidate: STRESS_ALLOWED_CANDIDATE,
    referenceAttached: false,
    visionTarget: 'FULL_FRAME',
    minhCrop: false,
    contractPurpose: 'MASTER_REFERENCE',
    imageContract: 'single_character single_image',
    scoreMode: 'HARDCODED_40',
    p0Provenance: {
      IDENTITY_MISMATCH: 'same=false because vision.Overall==FAIL',
      FACE_DEFORMATION: 'vision.Overall==FAIL && minh',
      AGE_DRIFT: 'ageOk copied from same',
      HAIR_IDENTITY_BREAK: 'hairSame copied from same',
      FACIAL_PROPORTION_BREAK: 'proportionSame copied from same',
      DISTINCTIVE_FEATURE_LOSS: 'distinctiveKept copied from same',
    },
    note: 'P0=6 và ID 40 không phải 6 feature độc lập. Evaluator cũ nhân 1 Vision FAIL. ST-10 có 2 người nhưng contract chỉ 1. Vision không nhận ảnh 004-D.',
  };
}
