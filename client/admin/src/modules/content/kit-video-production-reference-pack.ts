/** CHAR-001 Minh Production Reference Pack V1 — uses locked Master + locked DNA. Not a new identity. */

import {
  CHARACTER_DNA_CODE,
  CHARACTER_DNA_VERSION,
  type CharacterDnaSpec,
  isDnaComplete,
} from './kit-video-character-dna';
import { isLegacyOrGoldenPath } from './kit-video-master-reference';
import { MASTER_CODE_V1 } from './kit-video-master-lock';

export const MINH_PRODUCTION_PACK_ID = 'CHAR-001_MINH_PRODUCTION_REFERENCE_PACK_V1';
export const PRODUCTION_PACK_CODE = 'CHAR-001-MINH-ERA01-PROD-REF-V1';
export const PRODUCTION_PACK_VERSION = 'V1';

export const MINH_PRODUCTION_PACK_REVIEW_ID = 'CHAR-001_MINH_PRODUCTION_REFERENCE_PACK_REVIEW_SPEC_V1';

export const PRP_REQUIRED_SECTIONS = [
  'masterReference',
  'characterDna',
  'identityAnchor',
  'faceReference',
  'eyesReference',
  'hairReference',
  'ageProportion',
  'expressionBaseline',
  'allowedVariation',
  'forbiddenVariation',
  'continuityRules',
  'identityConstraints',
  'productionRules',
  'shotRules',
  'cameraRules',
  'expressionRules',
  'wardrobeRules',
  'environmentRules',
  'productionContinuity',
  'forbiddenProduction',
  'stressTestSummary',
  'regressionStatus',
  'lockMetadata',
] as const;

export const PRP_REVIEW_SECTIONS = [
  'identityConstraints',
  'productionRules',
  'shotRules',
  'cameraRules',
  'expressionRules',
  'wardrobeRules',
  'environmentRules',
  'productionContinuity',
  'forbiddenProduction',
] as const;

export const PRP_PRODUCTION_ALLOWED = [
  'camera distance',
  'camera angle',
  'framing',
  'body pose',
  'hand position',
  'gaze direction',
  'facial expression within DNA',
  'lighting',
  'environment',
  'wardrobe within DNA',
  'props',
  'interaction',
  'scene composition',
] as const;

export const PRP_PRODUCTION_FORBIDDEN = [
  'đổi khuôn mặt',
  'đổi shape mắt',
  'đổi tỷ lệ khuôn mặt',
  'đổi kiểu tóc đặc trưng',
  'đổi tuổi biểu kiến',
  'đổi proportions',
  'generic AI boy',
  'anime',
  'photorealistic',
  'character redesign',
  'face blending',
  'face averaging',
  'identity swap',
  'identity drift',
  'face mutation',
  'tạo một Minh khác',
  'sử dụng character reference không được approve',
] as const;

export const PRP_IDENTITY_KEYS = [
  'identityAnchor',
  'faceReference',
  'eyesReference',
  'hairReference',
  'ageProportion',
  'expressionBaseline',
  'allowedVariation',
  'forbiddenVariation',
  'continuityRules',
  'masterSha256',
  'dnaSha256',
] as const;

export type ProductionPackSpec = {
  derived_from_master?: string;
  derived_from_dna?: string;
  masterSha256?: string;
  dnaSha256?: string;
  masterReference?: Record<string, unknown>;
  characterDna?: Record<string, unknown>;
  identityAnchor?: Record<string, unknown>;
  faceReference?: Record<string, unknown>;
  eyesReference?: Record<string, unknown>;
  hairReference?: Record<string, unknown>;
  ageProportion?: { age?: unknown; body?: unknown };
  expressionBaseline?: Record<string, unknown>;
  allowedVariation?: unknown;
  forbiddenVariation?: unknown;
  continuityRules?: Record<string, unknown>;
  identityInvariants?: Record<string, unknown>;
  style?: Record<string, unknown>;
  stressTestSummary?: Record<string, unknown>;
  regressionStatus?: Record<string, unknown>;
  lockMetadata?: Record<string, unknown>;
  identityConstraints?: Record<string, unknown>;
  productionRules?: Record<string, unknown>;
  shotRules?: Record<string, unknown>;
  cameraRules?: Record<string, unknown>;
  expressionRules?: Record<string, unknown>;
  wardrobeRules?: Record<string, unknown>;
  environmentRules?: Record<string, unknown>;
  productionContinuity?: Record<string, unknown>;
  forbiddenProduction?: unknown;
};

export type PrpCheckItem = { code: string; label: string; pass: boolean; reason?: string };

function check(code: string, label: string, pass: boolean, reason: string): PrpCheckItem {
  return { code, label, pass, reason: pass ? undefined : reason };
}

export function evaluateProductionPackGate(input: {
  masterLocked?: boolean;
  dnaLocked?: boolean;
  masterShaValid?: boolean;
  dnaShaValid?: boolean;
  identityPass?: boolean;
  stressPass?: boolean;
  p0?: number;
  directorApproval?: boolean;
}): PrpCheckItem[] {
  const director = input.directorApproval ?? (!!input.masterLocked && !!input.dnaLocked);
  return [
    check('master_locked', 'Master LOCKED', input.masterLocked === true, 'Master chưa LOCKED'),
    check('dna_locked', 'Character DNA V1 LOCKED', input.dnaLocked === true, 'Character DNA V1 chưa LOCKED'),
    check('master_sha', 'Master SHA256 khớp', input.masterShaValid === true, 'Master SHA256 không khớp'),
    check('dna_sha', 'DNA SHA256 tồn tại và khớp', input.dnaShaValid === true, 'DNA SHA256 thiếu hoặc không khớp'),
    check('identity_pass', 'Identity Test PASS', input.identityPass === true, 'Identity Test chưa PASS'),
    check('stress_pass', 'Identity Stress Test PASS', input.stressPass === true, 'Identity Stress Test chưa PASS'),
    check('p0_zero', 'P0 = 0', (input.p0 ?? 0) === 0, 'P0 phải = 0'),
    check('director_approval', 'Director Approval PASS', director === true, 'Director Approval chưa PASS'),
  ];
}

export function productionPackGatePass(items: PrpCheckItem[]) {
  return items.every((x) => x.pass);
}

function hasObject(value: unknown) {
  return !!value && typeof value === 'object' && !Array.isArray(value) && Object.keys(value as object).length > 0;
}

function hasList(value: unknown, min = 1) {
  return Array.isArray(value) && value.length >= min;
}

export function evaluateReviewChecks(spec?: ProductionPackSpec, dna?: CharacterDnaSpec): PrpCheckItem[] {
  const inheritOk = inheritsIdentity(spec, dna);
  const items: PrpCheckItem[] = [
    check('identity_inherit', 'Identity inheritance (Face/Eyes/Hair/Age/Expression/Proportion/Style)', inheritOk, 'PRP conflict với DNA. DNA thắng. Không tự sửa.'),
    check('identity_constraints', 'Identity Constraints', hasObject(spec?.identityConstraints), 'thiếu Identity Constraints'),
    check('production_rules', 'Production Rules', hasObject(spec?.productionRules), 'thiếu Production Rules'),
    check('shot_rules', 'Shot Rules', hasObject(spec?.shotRules), 'thiếu Shot Rules'),
    check('camera_rules', 'Camera Rules', hasObject(spec?.cameraRules), 'thiếu Camera Rules'),
    check('expression_rules', 'Expression Rules', hasObject(spec?.expressionRules), 'thiếu Expression Rules'),
    check('wardrobe_rules', 'Wardrobe Rules', hasObject(spec?.wardrobeRules), 'thiếu Wardrobe Rules'),
    check('environment_rules', 'Environment Rules', hasObject(spec?.environmentRules), 'thiếu Environment Rules'),
    check('continuity_rules', 'Continuity Rules', hasObject(spec?.productionContinuity) || hasObject(spec?.continuityRules), 'thiếu Continuity Rules'),
    check(
      'forbidden_production',
      'Forbidden Production',
      hasList(spec?.forbiddenProduction) || hasObject(spec?.forbiddenProduction) || hasList(spec?.forbiddenVariation),
      'thiếu Forbidden Production',
    ),
    check('lock_metadata', 'Regression / Lock Metadata', hasObject(spec?.lockMetadata) && hasObject(spec?.regressionStatus), 'thiếu Regression / Lock Metadata'),
  ];
  const validation = inheritOk && items.every((x) => x.pass) && isPackComplete(spec);
  items.push(check('prp_validation', 'PRP validation', validation, 'PRP validation FAIL'));
  return items;
}

export function evaluateDirectorReview(input: {
  spec?: ProductionPackSpec;
  dna?: CharacterDnaSpec;
  masterLocked?: boolean;
  dnaLocked?: boolean;
  masterShaValid?: boolean;
  dnaShaValid?: boolean;
  identityPass?: boolean;
  stressPass?: boolean;
  p0?: number;
  directorApproval?: boolean;
}): PrpCheckItem[] {
  return [...evaluateProductionPackGate(input), ...evaluateReviewChecks(input.spec, input.dna)];
}

export function directorReviewPass(items: PrpCheckItem[]) {
  return items.every((x) => x.pass);
}

export function canCreateProductionPack(input: {
  masterLocked?: boolean;
  dnaLocked?: boolean;
  masterShaValid?: boolean;
  dnaShaValid?: boolean;
  identityPass?: boolean;
  stressPass?: boolean;
  p0?: number;
  directorApproval?: boolean;
  lockedPack?: boolean;
}) {
  if (input.lockedPack) return { ok: false as const, blocked: 'PRP_LOCKED: V1 đã khóa.' };
  const gate = evaluateProductionPackGate(input);
  const fail = gate.find((x) => !x.pass);
  if (fail) {
    const prefix = fail.code === 'master_sha' || fail.code === 'dna_sha' ? 'PRP_INVALID' : 'PRP_GATE_NOT_SATISFIED';
    return { ok: false as const, blocked: `${prefix}: ${fail.reason}` };
  }
  return { ok: true as const, status: 'DRAFT' as const, approved: false as const, locked: false as const };
}

export function isPackComplete(spec?: ProductionPackSpec) {
  if (!spec) return false;
  if (PRP_REQUIRED_SECTIONS.some((key) => spec[key] == null)) return false;
  if (!spec.masterSha256 || spec.masterSha256.length < 32) return false;
  if (!spec.dnaSha256 || spec.dnaSha256.length < 32) return false;
  return true;
}

export function inheritsIdentity(spec?: ProductionPackSpec, dna?: CharacterDnaSpec) {
  if (!spec || !dna) return false;
  if (JSON.stringify(spec.faceReference) !== JSON.stringify(dna.face)) return false;
  if (JSON.stringify(spec.eyesReference) !== JSON.stringify(dna.eyes)) return false;
  if (JSON.stringify(spec.hairReference) !== JSON.stringify(dna.hair)) return false;
  if (JSON.stringify(spec.ageProportion?.age) !== JSON.stringify(dna.age)) return false;
  if (JSON.stringify(spec.ageProportion?.body) !== JSON.stringify(dna.body)) return false;
  if (JSON.stringify(spec.expressionBaseline) !== JSON.stringify(dna.expression)) return false;
  if (JSON.stringify(spec.allowedVariation) !== JSON.stringify(dna.allowedVariation)) return false;
  if (JSON.stringify(spec.forbiddenVariation) !== JSON.stringify(dna.forbiddenVariation)) return false;
  if (JSON.stringify(spec.continuityRules) !== JSON.stringify(dna.continuityRules)) return false;
  if (JSON.stringify(spec.identityInvariants) !== JSON.stringify(dna.identityInvariants)) return false;
  if (dna.style && JSON.stringify(spec.style) !== JSON.stringify(dna.style)) return false;
  return true;
}

export function identityMutated(original?: ProductionPackSpec, edited?: ProductionPackSpec) {
  if (!original || !edited) return true;
  return PRP_IDENTITY_KEYS.some((key) => JSON.stringify(original[key]) !== JSON.stringify(edited[key]));
}

export function canApproveProductionPack(input: {
  status?: string;
  spec?: ProductionPackSpec;
  dna?: CharacterDnaSpec;
  masterLocked?: boolean;
  dnaLocked?: boolean;
  masterShaValid?: boolean;
  dnaShaValid?: boolean;
  identityPass?: boolean;
  stressPass?: boolean;
  p0?: number;
  directorApproval?: boolean;
  actor?: string;
}) {
  if ((input.status || '').toUpperCase() === 'LOCKED') {
    return { ok: true as const, idempotent: true as const, version: PRODUCTION_PACK_VERSION };
  }
  const actor = (input.actor || '').trim();
  if (!actor || actor.toLowerCase() === 'anonymous') {
    return { ok: false as const, blocked: 'PRP_GATE_NOT_SATISFIED: chỉ Director được duyệt Production Reference Pack.' };
  }
  const create = canCreateProductionPack(input);
  if (!create.ok) return create;
  const review = evaluateDirectorReview(input);
  const fail = review.find((x) => !x.pass);
  if (fail || !isDnaComplete(input.dna)) {
    return { ok: false as const, blocked: `PRP_GATE_NOT_SATISFIED: ${fail?.reason || 'inheritance / pack complete thất bại.'}` };
  }
  return { ok: true as const, next: 'LOCKED' as const, approved: true as const, version: PRODUCTION_PACK_VERSION, gatePass: true as const };
}

export function canCreateProductionShot(input: {
  status?: string;
  spec?: ProductionPackSpec;
  dna?: CharacterDnaSpec;
  masterLocked?: boolean;
  dnaLocked?: boolean;
  masterShaValid?: boolean;
  dnaShaValid?: boolean;
  identityPass?: boolean;
  stressPass?: boolean;
  p0?: number;
  directorApproval?: boolean;
}) {
  if ((input.status || '').toUpperCase() !== 'LOCKED') {
    return { ok: false as const, blocked: 'PRP_GATE_NOT_SATISFIED: PRP chưa LOCKED. Không tạo Production Shot.', createShot: false as const };
  }
  const approve = canApproveProductionPack({ ...input, actor: 'director' });
  if (!approve.ok) return { ...approve, createShot: false as const };
  return { ok: false as const, blocked: 'PRP_GATE_NOT_SATISFIED: PRP Review không tạo Production Shot.', createShot: false as const, gatePass: true as const };
}

export function blockProductionOnShaMismatch(live?: string, locked?: string) {
  if (!live || !locked || live !== locked) {
    return { ok: false as const, blocked: 'PRP_GATE_NOT_SATISFIED: SHA không khớp. BLOCK PRODUCTION.' };
  }
  return { ok: true as const };
}

export function prpReviewReport(input: {
  regressionPass?: boolean;
  directorGatePass?: boolean;
  status?: string;
  masterLocked?: boolean;
  dnaLocked?: boolean;
  masterShaMatch?: boolean;
  dnaShaMatch?: boolean;
}) {
  return {
    REGRESSION: input.regressionPass ? 'PASS' : 'FAIL',
    'DIRECTOR GATE': input.directorGatePass ? 'PASS' : 'FAIL',
    'PRP STATUS': (input.status || 'DRAFT').toUpperCase(),
    MASTER: input.masterLocked ? 'LOCKED' : 'UNLOCKED',
    DNA: input.dnaLocked ? 'LOCKED' : 'UNLOCKED',
    'MASTER SHA': input.masterShaMatch ? 'MATCH' : 'MISMATCH',
    'DNA SHA': input.dnaShaMatch ? 'MATCH' : 'MISMATCH',
  };
}

export function rejectPackMutation(status?: string, action?: string) {
  if ((status || '').toUpperCase() !== 'LOCKED') return { ok: true as const };
  const a = (action || '').toUpperCase();
  if (['UPDATE', 'DELETE', 'EDIT', 'OVERWRITE', 'CHANGE_MASTER', 'CHANGE_SHA256', 'REGENERATE', 'CHANGE_DNA'].includes(a)) {
    return { ok: false as const, blocked: 'PRP_LOCKED: V1 không overwrite. Dùng CHAR-001-MINH-ERA01-PROD-REF-V2.' };
  }
  return { ok: true as const };
}

export function packApproveIdempotent(existingVersion?: string) {
  if (!existingVersion || existingVersion === PRODUCTION_PACK_VERSION) {
    return { ok: true as const, version: PRODUCTION_PACK_VERSION, createdV2: false as const };
  }
  return { ok: false as const, blocked: 'PRP_LOCKED: không tạo PROD-REF-V2 khi approve lại V1.' };
}

export function packCreatesPixels(action?: string) {
  return action === 'GENERATE' || action === 'REGENERATE';
}

export function packGoldenUntouched(path?: string) {
  return !isLegacyOrGoldenPath(path || '');
}

export function packMutatesMasterOrDna(action?: string) {
  return action === 'CHANGE_MASTER' || action === 'CHANGE_DNA' || action === 'CHANGE_SHA256';
}

export function buildMinhProductionPackV1Spec(input: {
  masterSha256: string;
  dnaSha256: string;
  dna: CharacterDnaSpec;
  identityPass?: boolean;
  stressPass?: boolean;
  p0?: number;
}): ProductionPackSpec {
  return {
    derived_from_master: MASTER_CODE_V1,
    derived_from_dna: CHARACTER_DNA_CODE,
    masterSha256: input.masterSha256,
    dnaSha256: input.dnaSha256,
    masterReference: { masterCode: MASTER_CODE_V1, status: 'LOCKED', sha256: input.masterSha256 },
    characterDna: { dnaCode: CHARACTER_DNA_CODE, status: 'LOCKED', sha256: input.dnaSha256, version: CHARACTER_DNA_VERSION },
    identityAnchor: {
      character: 'MINH',
      characterId: 'CHAR-001',
      era: 'ERA-01',
      sourceMaster: MASTER_CODE_V1,
      sourceDna: CHARACTER_DNA_CODE,
      notANewIdentity: true,
    },
    faceReference: input.dna.face,
    eyesReference: input.dna.eyes,
    hairReference: input.dna.hair,
    ageProportion: { age: input.dna.age, body: input.dna.body },
    expressionBaseline: input.dna.expression,
    allowedVariation: input.dna.allowedVariation,
    forbiddenVariation: input.dna.forbiddenVariation,
    continuityRules: input.dna.continuityRules,
    identityInvariants: input.dna.identityInvariants,
    style: input.dna.style,
    stressTestSummary: {
      identityPass: input.identityPass !== false,
      stressPass: input.stressPass !== false,
      p0: input.p0 ?? 0,
    },
    regressionStatus: {
      noGenerate: true,
      noGemini: true,
      noRunway: true,
      masterImmutable: true,
      dnaImmutable: true,
      noNewIdentity: true,
      noAutoApprove: true,
      noAutoLock: true,
    },
    lockMetadata: {
      packCode: PRODUCTION_PACK_CODE,
      packVersion: PRODUCTION_PACK_VERSION,
      derived_from_master: MASTER_CODE_V1,
      derived_from_dna: CHARACTER_DNA_CODE,
      master_sha256: input.masterSha256,
      dna_sha256: input.dnaSha256,
      immutableAfterLock: true,
      notANewIdentity: true,
    },
    ...buildProductionRules(input.masterSha256, input.dnaSha256),
  };
}

export function buildProductionRules(masterSha256: string, dnaSha256: string): Pick<
  ProductionPackSpec,
  | 'identityConstraints'
  | 'productionRules'
  | 'shotRules'
  | 'cameraRules'
  | 'expressionRules'
  | 'wardrobeRules'
  | 'environmentRules'
  | 'productionContinuity'
  | 'forbiddenProduction'
> {
  return {
    identityConstraints: {
      inherit: ['FACE', 'EYES', 'HAIR', 'AGE', 'EXPRESSION', 'PROPORTION', 'STYLE'],
      dnaWinsOnConflict: true,
      masterWinsOnMasterConflict: true,
      noAutoFix: true,
      notANewIdentity: true,
    },
    productionRules: {
      uses: ['video', 'shot', 'scene', 'camera', 'framing', 'pose', 'expression', 'lighting', 'wardrobe', 'environment', 'interaction', 'continuity'],
      mustFollowDna: true,
      allowed: [...PRP_PRODUCTION_ALLOWED],
    },
    shotRules: {
      fields: ['SHOT ID', 'SHOT TYPE', 'CAMERA', 'FRAMING', 'POSE', 'EXPRESSION', 'GAZE', 'LIGHTING', 'ENVIRONMENT', 'WARDROBE', 'CONTINUITY REQUIREMENT', 'IDENTITY RISK'],
      risk: ['LOW RISK', 'MEDIUM RISK', 'HIGH RISK'],
      highRiskRequiresIdentityCheck: true,
      noShotCreatedInReview: true,
    },
    cameraRules: {
      allowed: ['camera distance', 'camera angle', 'framing'],
      mustNotChangeFaceIdentity: true,
    },
    expressionRules: {
      withinDna: ['NEUTRAL', 'SAD', 'LIGHT SMILE'],
      expressionIsNotNewIdentity: true,
    },
    wardrobeRules: {
      wardrobeIsNotIdentity: true,
      allowedWithinDna: true,
    },
    environmentRules: {
      allowed: ['environment', 'lighting', 'scene composition', 'props'],
      mustNotBreakIdentity: true,
    },
    productionContinuity: {
      required: ['character_id', 'master_id', 'master_sha256', 'dna_id', 'dna_sha256', 'prp_id', 'prp_version'],
      character_id: 'CHAR-001',
      master_sha256: masterSha256,
      dna_sha256: dnaSha256,
      prp_version: PRODUCTION_PACK_VERSION,
      shaMismatchBlocksProduction: true,
    },
    forbiddenProduction: [...PRP_PRODUCTION_FORBIDDEN],
  };
}

export function attachMissingProductionRules(spec: ProductionPackSpec, masterSha256: string, dnaSha256: string): ProductionPackSpec {
  const overlay = buildProductionRules(masterSha256, dnaSha256);
  const next = { ...spec };
  for (const key of PRP_REVIEW_SECTIONS) {
    if (next[key] == null) (next as Record<string, unknown>)[key] = overlay[key];
  }
  return next;
}
