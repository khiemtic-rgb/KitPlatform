/** CHAR-001 Minh Production Shot Specification V1 — uses locked Master + DNA + PRP. Not an image. */

import { CHARACTER_DNA_CODE, type CharacterDnaSpec, isDnaComplete } from './kit-video-character-dna';
import { isLegacyOrGoldenPath } from './kit-video-master-reference';
import { MASTER_CODE_V1 } from './kit-video-master-lock';
import { PRODUCTION_PACK_CODE } from './kit-video-production-reference-pack';

export const MINH_PRODUCTION_SHOT_ID = 'CHAR-001_MINH_PRODUCTION_SHOT_SPEC_V1';
export const PRODUCTION_SHOT_PREFIX = 'CHAR-001-MINH-ERA01-SHOT';
export const PRODUCTION_SHOT_VERSION = 'V1';

export const SHOT_REQUIRED_SECTIONS = [
  'source',
  'scene',
  'camera',
  'framing',
  'pose',
  'expression',
  'lighting',
  'wardrobe',
  'environment',
  'interaction',
  'continuity',
  'identityRisk',
  'forbiddenConditions',
  'identity',
  'identityCheck',
  'directorDecision',
] as const;

export const SHOT_IDENTITY_KEYS = [
  'identity',
  'faceReference',
  'eyesReference',
  'hairReference',
  'ageProportion',
  'expressionBaseline',
  'style',
  'masterSha256',
  'dnaSha256',
  'prpSha256',
  'character_id',
] as const;

export const SHOT_ALLOWED_VARIATION = [
  'camera angle',
  'camera distance',
  'framing',
  'body pose',
  'body orientation',
  'gaze',
  'expression intensity',
  'lighting',
  'environment',
  'props',
  'wardrobe within PRP',
  'interaction',
  'composition',
] as const;

export const SHOT_FORBIDDEN = [
  'generic AI child',
  'different child identity',
  'face redesign',
  'face replacement',
  'face blending',
  'identity averaging',
  'age change',
  'hair redesign',
  'anime',
  'photorealistic conversion',
  'character-sheet presentation',
  'collage',
  'text embedded in image',
  'unapproved character reference',
  'unapproved identity source',
  'identity drift',
  'face drift',
  'age drift',
  'hair identity drift',
  'proportion drift',
  'style drift',
] as const;

export type ProductionShotSpec = {
  shot_id?: string;
  character_id?: string;
  era_id?: string;
  master_id?: string;
  master_sha256?: string;
  dna_id?: string;
  dna_sha256?: string;
  prp_id?: string;
  prp_sha256?: string;
  source?: Record<string, unknown>;
  scene?: Record<string, unknown>;
  camera?: Record<string, unknown>;
  framing?: Record<string, unknown>;
  pose?: Record<string, unknown>;
  expression?: Record<string, unknown>;
  lighting?: Record<string, unknown>;
  wardrobe?: Record<string, unknown>;
  environment?: Record<string, unknown>;
  interaction?: { peopleCount?: number; minhDistinct?: boolean; noBlend?: boolean; noSwap?: boolean; kind?: string };
  continuity?: Record<string, unknown>;
  identityRisk?: { level?: string; identityCheckRequired?: boolean; reason?: string };
  forbiddenConditions?: unknown;
  identity?: Record<string, unknown>;
  identityCheck?: { pass?: boolean; run?: boolean; required?: boolean };
  directorDecision?: Record<string, unknown>;
  allowedVariation?: unknown;
  faceReference?: unknown;
  eyesReference?: unknown;
  hairReference?: unknown;
  ageProportion?: unknown;
  expressionBaseline?: unknown;
  style?: unknown;
  engineAction?: string;
};

export type ShotCheckItem = { code: string; label: string; pass: boolean; reason?: string };

function check(code: string, label: string, pass: boolean, reason: string): ShotCheckItem {
  return { code, label, pass, reason: pass ? undefined : reason };
}

export function shotCode(seq: number) {
  return `${PRODUCTION_SHOT_PREFIX}-${String(seq).padStart(3, '0')}`;
}

export function evaluateShotSourceGate(input: {
  masterLocked?: boolean;
  dnaLocked?: boolean;
  prpLocked?: boolean;
  masterShaValid?: boolean;
  dnaShaValid?: boolean;
  prpShaValid?: boolean;
  identityPass?: boolean;
  stressPass?: boolean;
}): ShotCheckItem[] {
  return [
    check('master_locked', 'Master LOCKED', input.masterLocked === true, 'Master chưa LOCKED'),
    check('dna_locked', 'DNA LOCKED', input.dnaLocked === true, 'DNA chưa LOCKED'),
    check('prp_locked', 'PRP LOCKED', input.prpLocked === true, 'PRP chưa LOCKED'),
    check('master_sha', 'Master SHA MATCH', input.masterShaValid === true, 'Master SHA256 không khớp'),
    check('dna_sha', 'DNA SHA MATCH', input.dnaShaValid === true, 'DNA SHA256 không khớp'),
    check('prp_sha', 'PRP SHA MATCH', input.prpShaValid === true, 'PRP SHA256 không khớp'),
    check('identity_pass', 'Identity PASS', input.identityPass === true, 'Identity Test chưa PASS'),
    check('stress_pass', 'Stress PASS', input.stressPass === true, 'Identity Stress Test chưa PASS'),
  ];
}

export function shotGatePass(items: ShotCheckItem[]) {
  return items.every((x) => x.pass);
}

export function canCreateProductionShot(input: {
  masterLocked?: boolean;
  dnaLocked?: boolean;
  prpLocked?: boolean;
  masterShaValid?: boolean;
  dnaShaValid?: boolean;
  prpShaValid?: boolean;
  identityPass?: boolean;
  stressPass?: boolean;
}) {
  const gate = evaluateShotSourceGate(input);
  const fail = gate.find((x) => !x.pass);
  if (fail) {
    const prefix = fail.code.endsWith('_sha') ? 'SHOT_INVALID' : 'SHOT_GATE_NOT_SATISFIED';
    return { ok: false as const, blocked: `${prefix}: ${fail.reason}` };
  }
  return { ok: true as const, status: 'DRAFT' as const, approved: false as const, locked: false as const };
}

export function isShotComplete(spec?: ProductionShotSpec) {
  if (!spec) return false;
  if (SHOT_REQUIRED_SECTIONS.some((key) => spec[key] == null)) return false;
  if (!spec.master_sha256 || spec.master_sha256.length < 32) return false;
  if (!spec.dna_sha256 || spec.dna_sha256.length < 32) return false;
  if (!spec.prp_sha256 || spec.prp_sha256.length < 32) return false;
  return true;
}

export function inheritsShotIdentity(spec?: ProductionShotSpec, dna?: CharacterDnaSpec) {
  if (!spec?.identity || !dna) return false;
  const ident = spec.identity as Record<string, unknown>;
  if (JSON.stringify(ident.face) !== JSON.stringify(dna.face)) return false;
  if (JSON.stringify(ident.eyes) !== JSON.stringify(dna.eyes)) return false;
  if (JSON.stringify(ident.hair) !== JSON.stringify(dna.hair)) return false;
  if (JSON.stringify(ident.age) !== JSON.stringify(dna.age)) return false;
  if (JSON.stringify(ident.expression) !== JSON.stringify(dna.expression)) return false;
  if (JSON.stringify(ident.body) !== JSON.stringify(dna.body)) return false;
  if (dna.style && JSON.stringify(ident.style) !== JSON.stringify(dna.style)) return false;
  return true;
}

export function identityMutated(original?: ProductionShotSpec, edited?: ProductionShotSpec) {
  if (!original || !edited) return true;
  return SHOT_IDENTITY_KEYS.some((key) => JSON.stringify(original[key]) !== JSON.stringify(edited[key]));
}

export function assessShotRisk(spec?: ProductionShotSpec): 'LOW' | 'MEDIUM' | 'HIGH' {
  const people = spec?.interaction?.peopleCount ?? 1;
  const text = [spec?.camera?.angle, spec?.camera?.distance, spec?.framing?.type, spec?.lighting?.style, spec?.interaction?.kind]
    .join(' ')
    .toLowerCase();
  if (
    people > 1 ||
    /profile extreme|heavy occlusion|extreme lighting|fast motion|unusual camera|small face|multiple/.test(text)
  ) {
    return 'HIGH';
  }
  if (/strong angle|motion|dramatic|partial occlusion|unusual expression/.test(text)) return 'MEDIUM';
  return (spec?.identityRisk?.level as 'LOW' | 'MEDIUM' | 'HIGH') || 'LOW';
}

export function identityCheckRecorded(spec?: ProductionShotSpec) {
  return spec?.identityCheck?.pass === true;
}

function multiPersonOk(spec?: ProductionShotSpec) {
  const people = spec?.interaction?.peopleCount ?? 1;
  if (people <= 1) return true;
  return !!(spec?.interaction?.minhDistinct && spec.interaction.noBlend && spec.interaction.noSwap && spec.identity);
}

export function evaluateShotChecks(spec?: ProductionShotSpec, dna?: CharacterDnaSpec): ShotCheckItem[] {
  const inheritOk = inheritsShotIdentity(spec, dna);
  const risk = assessShotRisk(spec);
  const high = risk === 'HIGH';
  const checkPass = identityCheckRecorded(spec);
  const items: ShotCheckItem[] = [
    check('identity_inherit', 'Identity inheritance', inheritOk, 'Shot conflict với DNA. DNA thắng. Không tự sửa.'),
    check('continuity', 'Continuity PASS', !!spec?.continuity && Object.keys(spec.continuity).length > 0, 'thiếu Continuity reference'),
    check('forbidden', 'Forbidden conditions PASS', Array.isArray(spec?.forbiddenConditions) && spec!.forbiddenConditions!.length > 0, 'thiếu Forbidden conditions'),
    check('identity_risk', 'Identity risk assessed', ['LOW', 'MEDIUM', 'HIGH'].includes(risk), 'Identity risk chưa đánh giá'),
    check('identity_check', high ? 'Identity Check REQUIRED (HIGH)' : 'Identity Check', checkPass, high ? 'HIGH RISK bắt buộc Identity Check PASS' : 'Identity Check chưa PASS'),
    check('multi_person', 'Multi-person policy', multiPersonOk(spec), 'blend / swap / thiếu identity anchor'),
    check('no_pixels', 'Không tạo ảnh / video / Gemini / Runway', spec?.engineAction !== 'GENERATE' && spec?.engineAction !== 'RUNWAY', 'không gọi production engine'),
  ];
  items.push(check('shot_validation', 'Shot validation', inheritOk && items.every((x) => x.pass) && isShotComplete(spec), 'SHOT validation FAIL'));
  return items;
}

export function evaluateDirectorShotReview(input: {
  spec?: ProductionShotSpec;
  dna?: CharacterDnaSpec;
  masterLocked?: boolean;
  dnaLocked?: boolean;
  prpLocked?: boolean;
  masterShaValid?: boolean;
  dnaShaValid?: boolean;
  prpShaValid?: boolean;
  identityPass?: boolean;
  stressPass?: boolean;
}): ShotCheckItem[] {
  return [...evaluateShotSourceGate(input), ...evaluateShotChecks(input.spec, input.dna)];
}

export function canApproveProductionShot(input: {
  status?: string;
  spec?: ProductionShotSpec;
  dna?: CharacterDnaSpec;
  actor?: string;
  masterLocked?: boolean;
  dnaLocked?: boolean;
  prpLocked?: boolean;
  masterShaValid?: boolean;
  dnaShaValid?: boolean;
  prpShaValid?: boolean;
  identityPass?: boolean;
  stressPass?: boolean;
}) {
  if ((input.status || '').toUpperCase() === 'LOCKED') {
    return { ok: true as const, idempotent: true as const, version: PRODUCTION_SHOT_VERSION };
  }
  const actor = (input.actor || '').trim();
  if (!actor || actor.toLowerCase() === 'anonymous') {
    return { ok: false as const, blocked: 'SHOT_GATE_NOT_SATISFIED: chỉ Director được duyệt Production Shot.' };
  }
  const status = (input.status || '').toUpperCase();
  if (!['DIRECTOR_REVIEW', 'IDENTITY_CHECK', 'APPROVED'].includes(status)) {
    return { ok: false as const, blocked: 'SHOT_GATE_NOT_SATISFIED: không bỏ qua gate. Chạy Identity Check rồi Director Review.' };
  }
  const review = evaluateDirectorShotReview(input);
  const fail = review.find((x) => !x.pass);
  if (fail || !isDnaComplete(input.dna)) {
    return { ok: false as const, blocked: `SHOT_GATE_NOT_SATISFIED: ${fail?.reason || 'shot complete thất bại.'}` };
  }
  return { ok: true as const, next: 'LOCKED' as const, approved: true as const, version: PRODUCTION_SHOT_VERSION };
}

export function rejectShotMutation(status?: string, action?: string) {
  if ((status || '').toUpperCase() !== 'LOCKED') return { ok: true as const };
  const a = (action || '').toUpperCase();
  if (['UPDATE', 'DELETE', 'EDIT', 'OVERWRITE', 'CHANGE_MASTER', 'CHANGE_SHA256', 'REGENERATE', 'CHANGE_DNA', 'CHANGE_PRP'].includes(a)) {
    return { ok: false as const, blocked: 'SHOT_LOCKED: V1 không overwrite. Dùng SHOT-V2.' };
  }
  return { ok: true as const };
}

export function shotCreatesPixels(action?: string) {
  return action === 'GENERATE' || action === 'REGENERATE' || action === 'GEMINI' || action === 'RUNWAY';
}

export function shotGoldenUntouched(path?: string) {
  return !isLegacyOrGoldenPath(path || '');
}

export function shotMutatesSources(action?: string) {
  return action === 'CHANGE_MASTER' || action === 'CHANGE_DNA' || action === 'CHANGE_PRP' || action === 'CHANGE_SHA256';
}

export function buildMinhProductionShotV1Spec(input: {
  seq: number;
  masterSha256: string;
  dnaSha256: string;
  prpSha256: string;
  dna: CharacterDnaSpec;
  previousShotId?: string;
}): ProductionShotSpec {
  return {
    shot_id: shotCode(input.seq),
    character_id: 'CHAR-001',
    era_id: 'ERA-01',
    master_sha256: input.masterSha256,
    dna_sha256: input.dnaSha256,
    prp_sha256: input.prpSha256,
    source: {
      master: MASTER_CODE_V1,
      dna: CHARACTER_DNA_CODE,
      prp: PRODUCTION_PACK_CODE,
      master_sha256: input.masterSha256,
      dna_sha256: input.dnaSha256,
      prp_sha256: input.prpSha256,
      notANewIdentity: true,
      notAnImage: true,
    },
    scene: { scene_id: 'SC-01', shot_type: 'MEDIUM' },
    camera: { angle: 'eye-level 3/4', distance: 'medium', lens_intent: '50mm cinematic still' },
    framing: { type: 'medium', face: 'clear', composition: 'single subject Minh' },
    pose: { body: 'natural standing', orientation: '3/4' },
    expression: { baseline: 'NEUTRAL', intensity: 'low', gaze: 'slightly off-camera' },
    lighting: { style: 'normal soft', direction: 'front-side' },
    wardrobe: { withinPrp: true, wardrobeIsNotIdentity: true },
    environment: { kind: 'interior era-01', allowed: true },
    interaction: { kind: 'none', peopleCount: 1, minhDistinct: true, noBlend: true, noSwap: true, noAverage: true },
    continuity: {
      character_id: 'CHAR-001',
      master_sha256: input.masterSha256,
      dna_sha256: input.dnaSha256,
      prp_sha256: input.prpSha256,
      scene_id: 'SC-01',
      previous_shot_id: input.previousShotId || null,
    },
    identityRisk: { level: 'LOW', reason: 'front / 3/4 · normal lighting · clear face · single person', identityCheckRequired: false },
    forbiddenConditions: [...SHOT_FORBIDDEN],
    allowedVariation: [...SHOT_ALLOWED_VARIATION],
    identity: {
      face: input.dna.face,
      eyes: input.dna.eyes,
      hair: input.dna.hair,
      age: input.dna.age,
      expression: input.dna.expression,
      body: input.dna.body,
      style: input.dna.style,
      notRedefined: true,
    },
    faceReference: input.dna.face,
    eyesReference: input.dna.eyes,
    hairReference: input.dna.hair,
    ageProportion: { age: input.dna.age, body: input.dna.body },
    expressionBaseline: input.dna.expression,
    style: input.dna.style,
    identityCheck: { pass: false, run: false, required: false },
    directorDecision: { status: 'PENDING', noAutoApprove: true, noAutoLock: true },
    engineAction: 'SPEC',
  };
}

export function markShotIdentityCheck(spec: ProductionShotSpec, pass: boolean, risk: 'LOW' | 'MEDIUM' | 'HIGH'): ProductionShotSpec {
  return {
    ...spec,
    identityCheck: { pass, run: true, required: risk === 'HIGH' },
    identityRisk: { ...(spec.identityRisk || {}), level: risk, identityCheckRequired: risk === 'HIGH' },
    directorDecision: { status: pass ? 'READY' : 'BLOCKED', noAutoApprove: true, noAutoLock: true },
  };
}

export function shotReviewReport(input: {
  regressionPass?: boolean;
  directorGatePass?: boolean;
  status?: string;
  masterLocked?: boolean;
  dnaLocked?: boolean;
  prpLocked?: boolean;
  allShaMatch?: boolean;
}) {
  return {
    REGRESSION: input.regressionPass ? 'PASS' : 'FAIL',
    'DIRECTOR GATE': input.directorGatePass ? 'PASS' : 'FAIL',
    'SHOT STATUS': (input.status || 'DRAFT').toUpperCase(),
    MASTER: input.masterLocked ? 'LOCKED' : 'UNLOCKED',
    DNA: input.dnaLocked ? 'LOCKED' : 'UNLOCKED',
    PRP: input.prpLocked ? 'LOCKED' : 'UNLOCKED',
    'ALL SHA': input.allShaMatch ? 'MATCH' : 'MISMATCH',
  };
}
