/** CHAR-001 Master Reference creation — Gemini is one generator, not the engine. */

import {
  canLockMaster,
  canPromoteToProductionStill,
  diagnoseRepair,
  doNotBlindRetry,
  ensureMasterImmutable,
  evaluateMasterQa,
  nextMasterVersion,
  productionStillMayReferenceMaster,
  type MasterQaObservation,
} from './kit-video-master-reference';
import { CHAR_001_MINH_VISUAL_DNA_V1 } from './content-famixa-minh-visual-dna';
import { FAMIXA_VISUAL_STYLE_SYSTEM_V1 } from './kit-video-visual-system';
import { MINH_BRIEF_ID } from './content-famixa-character-brief';

export const MINH_MASTER_CREATION_ID = 'CHAR-001_MINH_MASTER_REFERENCE_CREATION_V1';
export const MAX_CANDIDATE_BATCH = 4;
export const MAX_AUTO_ATTEMPTS = 3;

export type MasterGenerationPurpose = 'MASTER_REFERENCE';
export type MasterView = 'FRONT' | 'THREE_Q_LEFT' | 'THREE_Q_RIGHT' | 'SIDE' | 'FULL_BODY';
export type MasterExpression = 'NEUTRAL' | 'HAPPY' | 'SAD' | 'HURT' | 'ANGRY';

export type MasterGenerationContract = {
  characterId: 'CHAR-001';
  eraId: 'ERA-01';
  age: 11;
  visualStyleVersion: string;
  visualDnaVersion: string;
  masterReferenceSpecVersion: string;
  generationPurpose: MasterGenerationPurpose;
  candidateId: string;
  attemptNumber: number;
  requiredView: MasterView;
  requiredExpression: MasterExpression;
  forbiddenElements: string[];
  referenceInputs: string[];
};

export function buildMasterContract(input: {
  candidateId: string;
  attemptNumber: number;
  view?: MasterView;
  expression?: MasterExpression;
}): MasterGenerationContract {
  return {
    characterId: 'CHAR-001',
    eraId: 'ERA-01',
    age: 11,
    visualStyleVersion: FAMIXA_VISUAL_STYLE_SYSTEM_V1.version,
    visualDnaVersion: 'V1',
    masterReferenceSpecVersion: 'V1',
    generationPurpose: 'MASTER_REFERENCE',
    candidateId: input.candidateId,
    attemptNumber: input.attemptNumber,
    requiredView: input.view || 'FRONT',
    requiredExpression: input.expression || 'NEUTRAL',
    forbiddenElements: [
      'character sheet',
      'collage',
      'storyboard',
      'multi-panel',
      'watermark',
      'logo',
      'dialogue',
      'photoreal child',
      'anime',
      'cartoon',
      'adult body',
    ],
    referenceInputs: [FAMIXA_VISUAL_STYLE_SYSTEM_V1.documentId, CHAR_001_MINH_VISUAL_DNA_V1.documentId, MINH_BRIEF_ID, 'ERA-01'],
  };
}

export function compileMasterCreationPrompt(contract: MasterGenerationContract) {
  if (contract.characterId !== 'CHAR-001' || contract.eraId !== 'ERA-01') {
    return { ok: false as const, blocked: 'CHAR-001 ERA-01 only.', prompt: '', fingerprint: '' };
  }
  const dna = CHAR_001_MINH_VISUAL_DNA_V1;
  const prompt = [
    '[STYLE] Famixa stylized cinematic human. Not photoreal. Not cartoon. Not anime.',
    `[CHARACTER DNA] Minh 11. Face: ${dna.headShape}. Eyes: ${dna.eyes}. Hair lock: silhouette Canon, black/dark-brown modern child cut. Body: clear 11yo, never adult body + child face.`,
    '[BRIEF] Everyday boy, holds emotion before showing it. Not hot boy, not ad-child.',
    '[ERA] ERA-01 age 11.',
    '[REFERENCE] Use Visual Style + Visual DNA only. No Golden Shot. No SH01-01. No random still.',
    `[VIEW] ${contract.requiredView} upper body unless FULL_BODY. Single boy. Simple neutral background.`,
    `[EXPRESSION] ${contract.requiredExpression}. Subtle. Identity must not change.`,
    '[WARDROBE] HOME baseline T-shirt. Wardrobe is not identity.',
    '[IMAGE CONTRACT] single_character single_image single_subject single_composition no_panels no_collage no_storyboard no_watermark no_logo no_unrequested_text',
    `[FORBIDDEN] ${contract.forbiddenElements.join(', ')}`,
  ].join('\n');
  if (/\b(Minh said|dialogue:|\{)/i.test(prompt) && prompt.includes('{')) {
    /* compiler never dumps JSON */
  }
  const fingerprint = fingerprintOf(prompt, contract);
  return { ok: true as const, prompt, fingerprint, provider: 'IImageGenerator', sections: ['STYLE', 'CHARACTER DNA', 'BRIEF', 'ERA', 'REFERENCE', 'VIEW', 'EXPRESSION', 'WARDROBE', 'IMAGE CONTRACT', 'FORBIDDEN'] };
}

export function fingerprintOf(prompt: string, contract: MasterGenerationContract) {
  const raw = [prompt, contract.characterId, contract.eraId, contract.requiredView, contract.requiredExpression, contract.visualDnaVersion, contract.visualStyleVersion].join('|');
  let h = 0;
  for (let i = 0; i < raw.length; i += 1) h = (h * 31 + raw.charCodeAt(i)) >>> 0;
  return `mr-${h.toString(16)}`;
}

export function precheckGenerate(input: {
  confirmed?: boolean;
  batchCount: number;
  attemptsOnFingerprint: number;
  sameFingerprint?: boolean;
  diagnosis?: string;
  locked?: boolean;
}) {
  if (input.locked) return { ok: false as const, blocked: 'MASTER_LOCKED' };
  if (!input.confirmed) return { ok: false as const, blocked: 'CREDIT_GATE: chưa xác nhận — không gọi generator.' };
  if (input.batchCount >= MAX_CANDIDATE_BATCH) return { ok: false as const, blocked: 'BATCH_FULL: tối đa 4 ảnh thử / đợt.' };
  if (input.sameFingerprint && !input.diagnosis) return { ok: false as const, blocked: 'DO_NOT_BLIND_RETRY' };
  if (input.attemptsOnFingerprint >= MAX_AUTO_ATTEMPTS) return { ok: false as const, blocked: 'DIRECTOR_REVIEW_REQUIRED: MAX_AUTO_ATTEMPTS=3' };
  return { ok: true as const };
}

export function artifactFailed(valid: { exists?: boolean; readable?: boolean; mime?: string; width?: number; height?: number }) {
  if (valid.exists === false || valid.readable === false) return { ok: false as const, status: 'ARTIFACT_FAILED' as const };
  if (valid.mime && !/jpeg|png|jpg/i.test(valid.mime)) return { ok: false as const, status: 'ARTIFACT_FAILED' as const };
  if ((valid.width ?? 0) < 512 || (valid.height ?? 0) < 512) return { ok: false as const, status: 'ARTIFACT_FAILED' as const };
  return { ok: true as const };
}

export function imageTypeFail(imageType?: string) {
  const t = (imageType || '').toUpperCase();
  if (['CHARACTER_SHEET', 'COLLAGE', 'STORYBOARD', 'MULTI_PANEL', 'REFERENCE_BOARD', 'CONTACT_SHEET', 'TEXT_HEAVY'].includes(t)) {
    return { ok: false as const, status: 'IMAGE_TYPE_FAIL' as const };
  }
  return { ok: true as const };
}

export function rankMasterCandidates(rows: { id: string; p0: string[]; score?: number }[]) {
  const eligible = rows.filter((r) => r.p0.length === 0);
  const ranked = [...eligible].sort((a, b) => (b.score ?? 0) - (a.score ?? 0));
  return {
    winner: ranked[0]?.id,
    rejectedHighScore: rows.filter((r) => r.p0.length > 0 && (r.score ?? 0) >= 98).map((r) => r.id),
    usedScoreAsOverride: false,
  };
}

export function identityScore(parts: {
  face?: number;
  hair?: number;
  body?: number;
  age?: number;
  style?: number;
  expression?: number;
  artifact?: number;
}) {
  return (
    (parts.face ?? 0) * 0.35 +
    (parts.hair ?? 0) * 0.15 +
    (parts.body ?? 0) * 0.15 +
    (parts.age ?? 0) * 0.1 +
    (parts.style ?? 0) * 0.15 +
    (parts.expression ?? 0) * 0.05 +
    (parts.artifact ?? 0) * 0.05
  );
}

export function productionAcceptsMaster(status?: string) {
  return (status || '').toUpperCase() === 'LOCKED';
}

export function directorDecision(decision: 'APPROVE' | 'REJECT' | 'REQUEST_REVISION') {
  if (decision === 'REJECT') return { masterCreated: false, status: 'REJECTED' as const };
  if (decision === 'REQUEST_REVISION') return { masterCreated: false, status: 'REVIEW' as const };
  return { masterCreated: true, status: 'APPROVED' as const, locked: false };
}

export function evaluateCreationQa(obs: MasterQaObservation & { hairMatch?: boolean; faceMatch?: boolean; imageType?: string }) {
  const typed = imageTypeFail(obs.imageType);
  if (!typed.ok) {
    return { ok: false as const, status: 'FAIL' as const, p0: ['IMAGE_TYPE_FAIL'], p1: [], p2: [], usedBeautyScore: false as const };
  }
  const next = { ...obs };
  if (obs.faceMatch === false) next.identityMatch = false;
  const qa = evaluateMasterQa(next);
  if (obs.hairMatch === false && !qa.p0.includes('HAIR_FAIL')) qa.p0.push('HAIR_FAIL');
  if (obs.faceMatch === false && !qa.p0.includes('FACE_FAIL')) qa.p0.push('FACE_FAIL');
  if (qa.p0.length && qa.status === 'PASS') return { ...qa, ok: false as const, status: 'FAIL' as const };
  if (qa.p0.includes('HAIR_FAIL') || qa.p0.includes('FACE_FAIL') || qa.p0.includes('BODY_FAIL') || qa.p0.includes('AGE_FAIL') || qa.p0.includes('STYLE_FAIL') || qa.p0.includes('IDENTITY_FAIL')) {
    return { ...qa, ok: false as const, status: qa.status === 'BLOCK' ? qa.status : 'FAIL' };
  }
  return qa;
}

export function noRunwayInCreation(blob: string) {
  return !/runway|i2v|lipsync|phase 07/i.test(blob);
}

export { canLockMaster, canPromoteToProductionStill, diagnoseRepair, doNotBlindRetry, ensureMasterImmutable, nextMasterVersion, productionStillMayReferenceMaster };
