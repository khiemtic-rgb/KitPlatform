/** CHAR-001 Master Reference selection — analyze / rank / Director choose. No generate. */

import {
  canUseAsMasterSource,
  ensureMasterImmutable,
  isLegacyOrGoldenPath,
  nextMasterVersion,
  productionStillMayReferenceMaster,
} from './kit-video-master-reference';
import { CHAR_001_MINH_VISUAL_DNA_V1, MINH_DNA_ID } from './content-famixa-minh-visual-dna';

export const MINH_MASTER_SELECTION_ID = 'CHAR-001_MINH_MASTER_REFERENCE_SELECTION_V1';
export const MINH_MASTER_SELECTION_REFINEMENT_ID = 'CHAR-001_MASTER_REFERENCE_SELECTION_REFINEMENT_V1';
export const MASTER_REF_KIND = 'MASTER_REFERENCE';
export const SELECTABLE_IMAGE_TYPE = 'PRODUCTION_STILL';
export const MAX_FRONT_RUNNERS = 3;
export const MAX_VARIATIONS = 5;
export const BLOCKED_SELECT_LIFECYCLES = ['DRAFT', 'REVIEW', 'INELIGIBLE', 'NOT_ELIGIBLE'] as const;

export const BLOCKED_IMAGE_TYPES = [
  'CHARACTER_SHEET',
  'COLLAGE',
  'REFERENCE_BOARD',
  'STORYBOARD',
  'MULTI_PANEL',
  'VIDEO_FRAME',
  'VIDEO_SCREENSHOT',
  'WATERMARKED_IMAGE',
  'TEXT_HEAVY_IMAGE',
] as const;

export type SelectionLifecycle =
  | 'DRAFT'
  | 'QA_PASS'
  | 'VISION_ANALYZED'
  | 'ELIGIBLE'
  | 'NOT_ELIGIBLE'
  | 'FAILED'
  | 'REJECTED'
  | 'FRONT_RUNNER'
  | 'DIRECTOR_SELECTED'
  | 'MASTER_REFERENCE'
  | 'LOCKED';

export type SelectionRecommend = 'RECOMMENDED' | 'ALTERNATIVE' | 'NOT_RECOMMENDED' | 'INELIGIBLE';

export type SelectionObservation = {
  projectCode?: string;
  characterId?: string;
  eraId?: string;
  candidateId?: string;
  artifactPath?: string;
  sha256?: string;
  liveSha256?: string;
  artifactExists?: boolean;
  readable?: boolean;
  width?: number;
  height?: number;
  imageType?: string;
  characterCount?: number;
  faceVisible?: boolean;
  hairVisible?: boolean;
  eyesVisible?: boolean;
  croppedFace?: boolean;
  eyesCovered?: boolean;
  hairCovered?: boolean;
  profileTooStrong?: boolean;
  blur?: boolean;
  obstruction?: boolean;
  faceMatch?: number;
  dnaMatch?: number;
  ageScore?: number;
  hairMatch?: number;
  eyeMatch?: number;
  distinctiveness?: number;
  stability?: number;
  ageLook?: '11' | 'too_young' | 'too_old' | 'teen' | 'adult';
  childProportion?: boolean;
  genericAiFace?: boolean;
  photorealGeneric?: boolean;
  visionPass?: boolean;
  controlledTest?: boolean;
  dnaApproved?: boolean;
  qaStatus?: string;
  qaSha256?: string;
  lifecycle?: string;
  frontRunner?: boolean;
  parentCandidateId?: string;
  sourceCandidateId?: string;
};

export type SelectionScore = {
  total: number;
  band: 'EXCELLENT' | 'STRONG' | 'ACCEPTABLE' | 'WEAK';
  face: number;
  dna: number;
  age: number;
  hair: number;
  eyes: number;
  distinctiveness: number;
  stability: number;
};

export type SelectionResult = {
  ok: boolean;
  eligible: boolean;
  lifecycle: SelectionLifecycle;
  p0: string[];
  score: SelectionScore;
  recommendation: SelectionRecommend;
  usedScoreAsOverride: false;
  autoSelected: false;
  visualDnaId: typeof MINH_DNA_ID;
};

export type SelectionEvent = {
  type: string;
  actor?: string;
  candidateId?: string;
  artifactPath?: string;
  sha256?: string;
  visualDnaVersion?: string;
  at?: string;
};

const DNA = CHAR_001_MINH_VISUAL_DNA_V1;

export function acceptSelectionInput(input: { projectCode?: string; characterId?: string; eraId?: string }) {
  if ((input.projectCode || 'FAMIXA').toUpperCase() !== 'FAMIXA') {
    return { ok: false as const, blocked: 'SELECTION: FAMIXA only.' };
  }
  if ((input.characterId || '').toUpperCase() !== 'CHAR-001') {
    return { ok: false as const, blocked: 'SELECTION: CHAR-001 only. Do not invent CHAR-002+.' };
  }
  if ((input.eraId || '').toUpperCase() !== 'ERA-01') {
    return { ok: false as const, blocked: 'SELECTION: ERA-01 only in this phase.' };
  }
  return { ok: true as const };
}

export function normalizeCandidateImageType(imageType?: string) {
  const t = (imageType || '').toUpperCase();
  if (!t) return 'UNKNOWN';
  return t;
}

export function isBlockedSelectLifecycle(lifecycle?: string) {
  return (BLOCKED_SELECT_LIFECYCLES as readonly string[]).includes((lifecycle || 'DRAFT').toUpperCase());
}

export function evaluateSelectionP0(obs: SelectionObservation) {
  const p0: string[] = [];
  const scope = acceptSelectionInput(obs);
  if (!scope.ok) p0.push(scope.blocked);
  if (obs.artifactExists === false || obs.readable === false) p0.push('P0-01_ARTIFACT');
  if (obs.sha256 && obs.liveSha256 && obs.sha256.toLowerCase() !== obs.liveSha256.toLowerCase()) p0.push('P0-01_HASH');
  if (obs.sha256 && obs.qaSha256 && obs.sha256.toLowerCase() !== obs.qaSha256.toLowerCase()) p0.push('P0-01_HASH');
  if (obs.liveSha256 && obs.qaSha256 && obs.liveSha256.toLowerCase() !== obs.qaSha256.toLowerCase()) p0.push('P0-01_HASH');
  if ((obs.width ?? 0) < 512 || (obs.height ?? 0) < 512) p0.push('P0-01_ARTIFACT');
  const kind = normalizeCandidateImageType(obs.imageType);
  if ((BLOCKED_IMAGE_TYPES as readonly string[]).includes(kind)) p0.push('P0-02_IMAGE_TYPE');
  if (kind !== SELECTABLE_IMAGE_TYPE) p0.push('P0-02_IMAGE_TYPE');
  if (obs.characterCount != null && obs.characterCount !== 1) p0.push('P0-03_CHARACTER_COUNT');
  if (isLegacyOrGoldenPath(obs.artifactPath || '')) p0.push('P0_GOLDEN_NOT_MASTER');
  if (obs.croppedFace || obs.eyesCovered || obs.hairCovered || obs.profileTooStrong || obs.blur || obs.obstruction) {
    p0.push('P0-04_IDENTITY_OBSTRUCTION');
  }
  if (obs.faceVisible === false || obs.eyesVisible === false || obs.hairVisible === false) p0.push('P0-04_IDENTITY_MISSING');
  if (obs.ageLook === 'adult' || obs.ageLook === 'teen' || obs.childProportion === false) p0.push('P0_AGE_BODY');
  if (obs.controlledTest) p0.push('CONTROLLED_TEST_NOT_CANON');
  return p0;
}

export function scoreSelection(obs: SelectionObservation): SelectionScore {
  const face = clamp(obs.faceMatch ?? 0);
  const dna = clamp(obs.dnaMatch ?? 0);
  const age = clamp(obs.ageScore ?? (obs.ageLook === '11' ? 85 : 40));
  const hair = clamp(obs.hairMatch ?? 0);
  const eyes = clamp(obs.eyeMatch ?? 0);
  const distinctiveness = clamp(
    obs.distinctiveness ?? (obs.genericAiFace || obs.photorealGeneric ? 35 : 70),
  );
  const stability = clamp(obs.stability ?? 70);
  const total = Math.round(
    face * 0.25 + dna * 0.2 + age * 0.15 + hair * 0.1 + eyes * 0.1 + distinctiveness * 0.1 + stability * 0.1,
  );
  const band = total >= 90 ? 'EXCELLENT' : total >= 80 ? 'STRONG' : total >= 70 ? 'ACCEPTABLE' : 'WEAK';
  return { total, band, face, dna, age, hair, eyes, distinctiveness, stability };
}

export function evaluateSelection(obs: SelectionObservation): SelectionResult {
  const p0 = evaluateSelectionP0(obs);
  const score = scoreSelection(obs);
  const visionOk = obs.visionPass !== false && (obs.qaStatus ? obs.qaStatus === 'PASS' : true);
  if (!visionOk && !p0.includes('VISION_FAIL')) p0.push('VISION_FAIL');
  const eligible = p0.length === 0 && visionOk && score.total >= 0;
  let recommendation: SelectionRecommend = 'INELIGIBLE';
  if (eligible) {
    recommendation = score.total >= 80 ? 'RECOMMENDED' : score.total >= 70 ? 'ALTERNATIVE' : 'NOT_RECOMMENDED';
  }
  const lifecycle: SelectionLifecycle = !eligible
    ? p0.some((x) => /ARTIFACT|HASH/.test(x))
      ? 'FAILED'
      : 'NOT_ELIGIBLE'
    : 'ELIGIBLE';
  return {
    ok: eligible,
    eligible,
    lifecycle,
    p0,
    score,
    recommendation,
    usedScoreAsOverride: false,
    autoSelected: false,
    visualDnaId: DNA.documentId,
  };
}

export function compareCandidates(rows: (SelectionObservation & { candidateId: string })[]) {
  const ranked = rows.map((row) => ({ candidateId: row.candidateId, result: evaluateSelection(row) }));
  const eligible = ranked.filter((r) => r.result.eligible).sort((a, b) => b.result.score.total - a.result.score.total);
  const blocked = ranked.filter((r) => !r.result.eligible);
  const recommended = eligible[0];
  return {
    ranks: [
      ...eligible.map((r, i) => ({
        ...r,
        recommendation: i === 0 ? ('RECOMMENDED' as const) : r.result.score.total >= 70 ? ('ALTERNATIVE' as const) : ('NOT_RECOMMENDED' as const),
      })),
      ...blocked.map((r) => ({ ...r, recommendation: 'INELIGIBLE' as const })),
    ],
    recommendedCandidateId: recommended?.candidateId,
    autoSelected: false as const,
    usedScoreAsOverride: false as const,
    compared: ranked.length,
  };
}

export function canDirectorSelect(input: {
  result: SelectionResult;
  dnaApproved?: boolean;
  lifecycle?: string;
  imageType?: string;
}) {
  const life = (input.lifecycle || 'DRAFT').toUpperCase();
  if (isBlockedSelectLifecycle(life)) {
    return { ok: false as const, blocked: 'SELECTION_GATE: DRAFT / REVIEW / INELIGIBLE / NOT_ELIGIBLE không được SELECT.' };
  }
  if (life !== 'ELIGIBLE' && life !== 'FRONT_RUNNER') {
    return { ok: false as const, blocked: 'SELECTION_GATE: candidate chưa ELIGIBLE.' };
  }
  if (normalizeCandidateImageType(input.imageType) !== SELECTABLE_IMAGE_TYPE) {
    return { ok: false as const, blocked: 'SELECTION_GATE: imageType phải là PRODUCTION_STILL.' };
  }
  if (!input.result.eligible || input.result.p0.length) {
    return { ok: false as const, blocked: 'MASTER_NOT_ELIGIBLE: P0 / Vision / artifact. Score không thắng gate.' };
  }
  if (!input.dnaApproved) {
    return { ok: false as const, blocked: 'DNA_NOT_APPROVED: Director duyệt Visual DNA trước khi chọn Canon.' };
  }
  return { ok: true as const };
}

export function directorSelect(input: {
  decision: 'SELECT_AS_MASTER' | 'REJECT' | 'REQUEST_REVISION';
  result: SelectionResult;
  dnaApproved?: boolean;
  lifecycle?: string;
  imageType?: string;
}) {
  if (input.decision === 'REJECT') return { ok: true as const, masterCreated: false, lifecycle: 'REJECTED' as const };
  if (input.decision === 'REQUEST_REVISION') return { ok: true as const, masterCreated: false, lifecycle: 'DRAFT' as const };
  const gate = canDirectorSelect(input);
  if (!gate.ok) return { ok: false as const, blocked: gate.blocked, masterCreated: false };
  return {
    ok: true as const,
    masterCreated: true,
    lifecycle: 'MASTER_REFERENCE' as const,
    locked: false,
    source: 'EXISTING_ARTIFACT',
    generated: false,
  };
}

export function markFrontRunner(input: {
  locked?: boolean;
  lifecycle?: string;
  recommendation?: string;
  controlledTest?: boolean;
  canonEligible?: boolean;
  frontRunnerCount?: number;
  alreadyFrontRunner?: boolean;
}) {
  if (input.locked) return { ok: false as const, blocked: 'MASTER_LOCKED' };
  const life = (input.lifecycle || '').toUpperCase();
  if (life === 'REJECTED' || life === 'FAILED') {
    return { ok: false as const, blocked: 'FRONT_RUNNER: ảnh đã loại không vào chung kết.' };
  }
  if (input.controlledTest || input.canonEligible === false) {
    return { ok: false as const, blocked: 'FRONT_RUNNER: ảnh thử kiểm chứng không vào chung kết.' };
  }
  if (!input.alreadyFrontRunner && (input.frontRunnerCount ?? 0) >= MAX_FRONT_RUNNERS) {
    return { ok: false as const, blocked: 'FRONT_RUNNER_FULL: tối đa 3 ứng viên dẫn đầu.' };
  }
  return {
    ok: true as const,
    lifecycle: 'FRONT_RUNNER' as const,
    canon: false,
    production: false,
    masterCreated: false,
    autoSelected: false as const,
  };
}

export function nextVariationCode(parentCode: string, existing: string[]) {
  const letters = ['A', 'B', 'C', 'D', 'E'];
  const have = new Set(existing.map((x) => (x || '').toUpperCase()));
  for (const letter of letters) {
    const code = `${parentCode}-${letter}`;
    if (!have.has(code.toUpperCase())) return { ok: true as const, code, suffix: letter };
  }
  return { ok: false as const, blocked: 'VARIATION_FULL: tối đa 5 (A–E).', code: '', suffix: '' };
}

export function bindVariation(parent: { id: string; code: string }, childCode: string) {
  return {
    parentCandidateId: parent.id,
    sourceCandidateId: parent.id,
    parentCode: parent.code,
    candidateCode: childCode,
    overwriteParent: false,
    autoSelected: false as const,
    autoApproved: false as const,
    autoLocked: false as const,
  };
}

export function ensureCanVary(input: {
  locked?: boolean;
  parentLifecycle?: string;
  parentRecommendation?: string;
  existingVariations?: number;
}) {
  if (input.locked) return { ok: false as const, blocked: 'MASTER_LOCKED' };
  const life = (input.parentLifecycle || '').toUpperCase();
  if (life === 'REJECTED' || life === 'FAILED') {
    return { ok: false as const, blocked: 'VARIATION: không vẽ từ ứng viên loại.' };
  }
  if ((input.existingVariations ?? 0) >= MAX_VARIATIONS) {
    return { ok: false as const, blocked: 'VARIATION_FULL: tối đa 5 (A–E).' };
  }
  return { ok: true as const };
}

export function bindMasterToCandidate(candidate: {
  candidateId: string;
  artifactPath: string;
  sha256: string;
}) {
  return {
    characterId: 'CHAR-001' as const,
    eraId: 'ERA-01' as const,
    version: 'V1',
    kind: MASTER_REF_KIND,
    sourceCandidate: candidate.candidateId,
    sourceArtifact: candidate.artifactPath,
    sha256: (candidate.sha256 || '').toLowerCase(),
    copiedBytes: false,
    generated: false,
  };
}

export function masterHashValid(masterSha?: string, fileSha?: string) {
  if (!masterSha || !fileSha) return { ok: false as const, status: 'MASTER_REFERENCE_INVALID' as const };
  if (masterSha.toLowerCase() !== fileSha.toLowerCase()) {
    return { ok: false as const, status: 'MASTER_REFERENCE_INVALID' as const };
  }
  return { ok: true as const };
}

export function resolveLockedMaster(input: {
  status?: string;
  sha256?: string;
  fileSha256?: string;
  path?: string;
  goldenPath?: string;
}) {
  if (isLegacyOrGoldenPath(input.path || '') || isLegacyOrGoldenPath(input.goldenPath || '')) {
    return { ok: false as const, blocked: 'GOLDEN_NOT_MASTER' };
  }
  if ((input.status || '').toUpperCase() !== 'LOCKED') {
    return { ok: false as const, blocked: 'PRODUCTION_REQUIRES_LOCKED_MASTER' };
  }
  const hash = masterHashValid(input.sha256, input.fileSha256 ?? input.sha256);
  if (!hash.ok) return { ok: false as const, blocked: hash.status };
  return {
    ok: true as const,
    characterId: 'CHAR-001',
    eraId: 'ERA-01',
    version: 'V1',
    source: 'LOCKED_MASTER_REFERENCE',
  };
}

export function appendSelectionEvent(existing: SelectionEvent[], event: SelectionEvent) {
  return [...existing, { ...event, visualDnaVersion: event.visualDnaVersion || 'V1', at: event.at || 'now' }];
}

export function selectionAuditComplete(events: SelectionEvent[]) {
  const types = new Set(events.map((e) => e.type));
  return ['CANDIDATE_CREATED', 'CANDIDATE_ANALYZED', 'CANDIDATE_SELECTED', 'MASTER_CREATED', 'MASTER_APPROVED', 'MASTER_LOCKED'].every(
    (t) => types.has(t),
  );
}

export function noGeminiInSelection(blob: string) {
  return !/generate image|call gemini|runway|lipsync|take-01/i.test(blob);
}

export {
  canUseAsMasterSource,
  ensureMasterImmutable,
  isLegacyOrGoldenPath,
  nextMasterVersion,
  productionStillMayReferenceMaster,
};

function clamp(n: number) {
  return Math.max(0, Math.min(100, n));
}
