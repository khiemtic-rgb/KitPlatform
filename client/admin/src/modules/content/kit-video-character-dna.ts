/** CHAR-001 Minh Character DNA V1 — identity spec derived from locked Master. Not an image. */

import { isLegacyOrGoldenPath } from './kit-video-master-reference';
import { MASTER_CODE_V1 } from './kit-video-master-lock';

export const MINH_CHARACTER_DNA_ID = 'CHAR-001_MINH_CHARACTER_DNA_V1';
export const CHARACTER_DNA_CODE = 'CHAR-001-MINH-ERA01-DNA-V1';
export const CHARACTER_DNA_VERSION = 'V1';

export const DNA_ALLOWED_VARIATION = [
  'clothing',
  'background',
  'environment',
  'lighting',
  'time_of_day',
  'camera_distance',
  'camera_angle',
  'pose',
  'gaze_direction',
  'expression',
  'body_position',
  'scene_context',
] as const;

export const DNA_FORBIDDEN_VARIATION = [
  'different face identity',
  'different eye structure',
  'different facial proportions',
  'different age',
  'different hair identity',
  'different recognizable facial features',
  'generic AI face',
  'model-like face',
  'adult appearance',
  'younger-child appearance',
  'anime transformation',
  'photorealistic transformation',
  'character-sheet transformation',
  'identity blending',
  'identity averaging',
  'face swap',
  'identity merge',
] as const;

export const DNA_INVARIANTS = {
  CRITICAL: ['age appearance', 'face identity', 'overall facial structure', 'eye identity', 'major facial proportions'],
  HIGH: ['hairstyle identity', 'eyebrow character', 'nose character', 'mouth character', 'ear character'],
  VARIABLE: ['expression', 'gaze', 'pose', 'clothing', 'lighting', 'background', 'scene'],
} as const;

export const DNA_REVIEW_GROUPS = ['face', 'eyes', 'hair', 'age', 'expression', 'body', 'style'] as const;
export const DNA_EXTRA_SECTIONS = ['variationBoundaries', 'continuityRules', 'stressRules', 'regressionRules', 'lockMetadata'] as const;

export const DNA_SECTIONS = [
  'identityCore',
  'face',
  'eyes',
  'eyebrows',
  'nose',
  'mouth',
  'ears',
  'hair',
  'skin',
  'age',
  'body',
  'expression',
  'style',
] as const;

export type CharacterDnaSpec = {
  derived_from_master?: string;
  master_sha256?: string;
  identityCore?: Record<string, unknown>;
  face?: Record<string, unknown>;
  eyes?: Record<string, unknown>;
  eyebrows?: Record<string, unknown>;
  nose?: Record<string, unknown>;
  mouth?: Record<string, unknown>;
  ears?: Record<string, unknown>;
  hair?: Record<string, unknown>;
  skin?: Record<string, unknown>;
  age?: Record<string, unknown>;
  body?: Record<string, unknown>;
  expression?: Record<string, unknown>;
  style?: Record<string, unknown>;
  allowedVariation?: string[];
  forbiddenVariation?: string[];
  identityInvariants?: { CRITICAL?: string[]; HIGH?: string[]; VARIABLE?: string[] };
  variationBoundaries?: Record<string, unknown>;
  continuityRules?: Record<string, unknown>;
  stressRules?: Record<string, unknown>;
  regressionRules?: Record<string, unknown>;
  lockMetadata?: Record<string, unknown>;
};

export function isLockedMaster(status?: string, masterCode?: string) {
  return (masterCode || '') === MASTER_CODE_V1 && (status || '') === 'MASTER_REFERENCE_LOCKED';
}

export function canCreateCharacterDna(input: {
  masterExists?: boolean;
  masterLocked?: boolean;
  masterCode?: string;
  shaValid?: boolean;
  lockedDna?: boolean;
}) {
  if (input.lockedDna) return { ok: false as const, blocked: 'DNA_LOCKED: V1 đã khóa.' };
  if (!input.masterExists) {
    return { ok: false as const, blocked: 'DNA_GATE_NOT_SATISFIED: master exists thất bại.' };
  }
  if (!input.masterLocked || !isLockedMaster('MASTER_REFERENCE_LOCKED', input.masterCode || MASTER_CODE_V1)) {
    return { ok: false as const, blocked: 'DNA_GATE_NOT_SATISFIED: Master chưa LOCKED. Không tạo APPROVED DNA.' };
  }
  if (input.shaValid === false) {
    return { ok: false as const, blocked: 'DNA_INVALID: Master SHA không khớp. DNA không tự cập nhật.' };
  }
  return { ok: true as const, status: 'DRAFT' as const, approved: false as const };
}

export type DnaCheckItem = { code: string; label: string; pass: boolean; reason?: string };

function hasList(value: unknown, min = 1) {
  return Array.isArray(value) && value.length >= min;
}

function groupHas(spec: CharacterDnaSpec | undefined, group: keyof CharacterDnaSpec, field: 'invariant' | 'allowed' | 'forbidden' | 'tested') {
  const g = spec?.[group];
  if (!g || typeof g !== 'object') return false;
  return hasList((g as Record<string, unknown>)[field]);
}

function hasObject(value: unknown) {
  return !!value && typeof value === 'object' && !Array.isArray(value) && Object.keys(value as object).length > 0;
}

export function evaluateSpecChecks(spec?: CharacterDnaSpec): DnaCheckItem[] {
  const check = (code: string, label: string, pass: boolean, reason: string): DnaCheckItem => ({
    code,
    label,
    pass,
    reason: pass ? undefined : reason,
  });
  return [
    check('face_invariant', 'Face invariant', groupHas(spec, 'face', 'invariant'), 'thiếu Face invariant'),
    check('eyes_invariant', 'Eyes invariant', groupHas(spec, 'eyes', 'invariant'), 'thiếu Eyes invariant'),
    check('hair_invariant', 'Hair invariant', groupHas(spec, 'hair', 'invariant'), 'thiếu Hair invariant'),
    check('age_invariant', 'Age invariant', groupHas(spec, 'age', 'invariant'), 'thiếu Age invariant'),
    check(
      'expression_rules',
      'Expression rules',
      groupHas(spec, 'expression', 'invariant') || groupHas(spec, 'expression', 'tested'),
      'thiếu Expression rules',
    ),
    check('proportion_invariant', 'Proportion invariant', groupHas(spec, 'body', 'invariant'), 'thiếu Proportion invariant'),
    check('style_invariant', 'Style invariant', groupHas(spec, 'style', 'invariant'), 'thiếu Style invariant'),
    check('allowed_variation', 'Allowed variation', hasList(spec?.allowedVariation), 'thiếu Allowed variation'),
    check('forbidden_variation', 'Forbidden variation', hasList(spec?.forbiddenVariation), 'thiếu Forbidden variation'),
    check('continuity_rules', 'Continuity rules', hasObject(spec?.continuityRules), 'thiếu Continuity rules'),
    check('stress_rules', 'Stress rules', hasObject(spec?.stressRules), 'thiếu Stress rules'),
    check('regression_rules', 'Regression rules', hasObject(spec?.regressionRules), 'thiếu Regression rules'),
  ];
}

export function evaluateDirectorGate(input: {
  spec?: CharacterDnaSpec;
  masterExists?: boolean;
  masterLocked?: boolean;
  shaValid?: boolean;
  identityPass?: boolean;
  stressPass?: boolean;
  p0?: number;
}): DnaCheckItem[] {
  const check = (code: string, label: string, pass: boolean, reason: string): DnaCheckItem => ({
    code,
    label,
    pass,
    reason: pass ? undefined : reason,
  });
  const specChecks = evaluateSpecChecks(input.spec);
  return [
    check('master_locked', 'Master LOCKED', !!input.masterExists && !!input.masterLocked, 'Master chưa LOCKED'),
    check('master_sha', 'Master SHA khớp', input.shaValid === true, 'Master SHA không khớp'),
    check('identity_pass', 'Identity Test PASS', input.identityPass === true, 'Identity Test chưa PASS'),
    check('stress_pass', 'Stress Test PASS', input.stressPass === true, 'Stress Test chưa PASS'),
    check('p0_zero', 'P0 = 0', (input.p0 ?? 0) === 0, 'P0 phải = 0'),
    ...specChecks,
    check('dna_validation', 'DNA validation', specChecks.every((x) => x.pass) && isDnaComplete(input.spec), 'DNA validation FAIL'),
  ];
}

export function directorGatePass(items: DnaCheckItem[]) {
  return items.every((x) => x.pass);
}

export function isDnaComplete(spec?: CharacterDnaSpec) {
  if (!spec) return false;
  if (DNA_SECTIONS.some((key) => !spec[key] || typeof spec[key] !== 'object')) return false;
  if (DNA_EXTRA_SECTIONS.some((key) => !spec[key] || typeof spec[key] !== 'object')) return false;
  if (
    DNA_REVIEW_GROUPS.some((key) => {
      const g = spec[key] as { invariant?: unknown[]; allowed?: unknown[]; forbidden?: unknown[] } | undefined;
      return (g?.invariant?.length ?? 0) < 1 || (g?.allowed?.length ?? 0) < 1 || (g?.forbidden?.length ?? 0) < 1;
    })
  ) {
    return false;
  }
  if ((spec.allowedVariation?.length ?? 0) < DNA_ALLOWED_VARIATION.length) return false;
  if ((spec.forbiddenVariation?.length ?? 0) < DNA_FORBIDDEN_VARIATION.length) return false;
  const inv = spec.identityInvariants;
  if ((inv?.CRITICAL?.length ?? 0) < DNA_INVARIANTS.CRITICAL.length) return false;
  if ((inv?.HIGH?.length ?? 0) < DNA_INVARIANTS.HIGH.length) return false;
  if ((inv?.VARIABLE?.length ?? 0) < DNA_INVARIANTS.VARIABLE.length) return false;
  return true;
}

export function canApproveCharacterDna(input: {
  status?: string;
  spec?: CharacterDnaSpec;
  masterExists?: boolean;
  masterLocked?: boolean;
  shaValid?: boolean;
  identityPass?: boolean;
  stressPass?: boolean;
  p0?: number;
}) {
  if ((input.status || '').toUpperCase() === 'LOCKED') {
    return { ok: true as const, idempotent: true as const, version: CHARACTER_DNA_VERSION };
  }
  const gate = evaluateDirectorGate(input);
  const fail = gate.find((x) => !x.pass);
  if (fail) {
    const prefix = fail.code === 'master_sha' ? 'DNA_INVALID' : 'DNA_GATE_NOT_SATISFIED';
    return { ok: false as const, blocked: `${prefix}: ${fail.reason}` };
  }
  return { ok: true as const, next: 'LOCKED' as const, approved: true as const, version: CHARACTER_DNA_VERSION, gatePass: true as const };
}

export function rejectDnaMutation(status?: string, action?: string) {
  if ((status || '').toUpperCase() !== 'LOCKED') return { ok: true as const };
  const a = (action || '').toUpperCase();
  if (['UPDATE', 'DELETE', 'EDIT', 'OVERWRITE', 'CHANGE_MASTER', 'CHANGE_SHA256', 'REGENERATE'].includes(a)) {
    return { ok: false as const, blocked: 'DNA_LOCKED: V1 không overwrite. Dùng CHAR-001-MINH-ERA01-DNA-V2.' };
  }
  return { ok: true as const };
}

export function dnaApproveIdempotent(existingVersion?: string) {
  if (!existingVersion || existingVersion === CHARACTER_DNA_VERSION) {
    return { ok: true as const, version: CHARACTER_DNA_VERSION, createdV2: false as const };
  }
  return { ok: false as const, blocked: 'DNA_LOCKED: không tạo DNA-V2 khi approve lại V1.' };
}

export function dnaCreatesPixels(action?: string) {
  return action === 'GENERATE' || action === 'REGENERATE';
}

export function dnaGoldenUntouched(path?: string) {
  return !isLegacyOrGoldenPath(path || '');
}

export function dnaShaInvalid(masterSha?: string, dnaSha?: string, liveSha?: string) {
  if (!masterSha || !dnaSha) return true;
  if (masterSha !== dnaSha) return true;
  if (liveSha && liveSha !== masterSha) return true;
  return false;
}

export function buildMinhDnaV1Spec(masterSha256: string): CharacterDnaSpec {
  const trait = (value: string, importance: string, invariance: string) => ({
    value,
    importance,
    invariance,
    notes: 'Master V1 không cung cấp số đo pixel. Mô tả cấu trúc nhận diện.',
  });
  return {
    derived_from_master: MASTER_CODE_V1,
    master_sha256: masterSha256,
    identityCore: {
      character: 'Minh',
      characterId: 'CHAR-001',
      era: 'ERA-01',
      ageAppearance: 'approximately 11 years old',
      visualIdentity: 'Vietnamese boy',
      style: 'Famixa illustration style',
      source: MASTER_CODE_V1,
    },
    face: {
      invariant: ['face identity', 'overall facial structure', 'major facial proportions'],
      allowed: ['camera angle', 'lighting', 'expression'],
      forbidden: ['different face identity', 'generic AI face', 'face swap', 'identity merge'],
      face_shape: trait('soft slightly oval child face — not fully round, not teen-angular', 'CRITICAL', 'HIGH'),
      facial_balance: trait('recognizable Minh across angles', 'CRITICAL', 'HIGH'),
    },
    eyes: {
      invariant: ['eye identity', 'eye structure'],
      allowed: ['gaze direction', 'expression', 'camera angle', 'lighting'],
      forbidden: ['different eye structure', 'anime transformation'],
      eye_shape: trait('relatively large natural eyes, not anime', 'CRITICAL', 'HIGH'),
    },
    eyebrows: { shape: trait('natural, slightly soft', 'HIGH', 'HIGH') },
    nose: { nose_shape: trait('small soft child nose', 'HIGH', 'HIGH') },
    mouth: { mouth_shape: trait('small-to-medium natural mouth', 'HIGH', 'HIGH') },
    ears: { ear_shape: trait('natural child ears', 'MEDIUM', 'HIGH') },
    hair: {
      invariant: ['hairstyle identity', 'hairline character', 'hair silhouette'],
      allowed: ['minor strand variation', 'wind', 'movement', 'lighting'],
      forbidden: ['different hair identity'],
      hair_style: trait('modern child cut; silhouette locked', 'HIGH', 'HIGH'),
    },
    skin: { skin_tone: trait('natural soft stylized complexion', 'HIGH', 'HIGH') },
    age: {
      invariant: ['age appearance approximately 11 years old'],
      allowed: ['camera angle', 'pose', 'expression', 'lighting', 'clothing'],
      forbidden: ['different age', 'adult appearance', 'younger-child appearance'],
      age_appearance: trait('approximately 11 years old', 'CRITICAL', 'HIGH'),
    },
    body: {
      invariant: ['11-year-old proportion', 'head-to-body relationship', 'overall silhouette'],
      allowed: ['pose', 'body_position', 'clothing', 'camera_distance'],
      forbidden: ['adult appearance', 'younger-child appearance'],
      body_age_character: trait('clear 11-year-old body character', 'CRITICAL', 'HIGH'),
    },
    expression: {
      invariant: ['same identity across NEUTRAL / SAD / LIGHT SMILE'],
      allowed: ['NEUTRAL', 'SAD', 'LIGHT_SMILE'],
      forbidden: ['treat expression as a new identity'],
      tested: ['NEUTRAL', 'SAD', 'LIGHT_SMILE'],
      schema: ['happy', 'surprised', 'curious', 'concerned', 'laughing', 'speaking'],
    },
    style: {
      invariant: ['FAMIXA_ILLUSTRATION'],
      allowed: ['lighting motivated by scene'],
      forbidden: ['photorealistic transformation', 'anime transformation', 'character-sheet transformation'],
      visual_style: 'FAMIXA_ILLUSTRATION',
      not_photorealistic: true,
      not_anime: true,
    },
    allowedVariation: [...DNA_ALLOWED_VARIATION],
    forbiddenVariation: [...DNA_FORBIDDEN_VARIATION],
    identityInvariants: {
      CRITICAL: [...DNA_INVARIANTS.CRITICAL],
      HIGH: [...DNA_INVARIANTS.HIGH],
      VARIABLE: [...DNA_INVARIANTS.VARIABLE],
    },
    variationBoundaries: { mayChange: [...DNA_ALLOWED_VARIATION], mustNotBreak: [...DNA_INVARIANTS.CRITICAL] },
    continuityRules: { sameIdentityAcrossShots: true, wardrobeIsNotIdentity: true, masterIsImmutable: true, noGoldenFaceLock: true },
    stressRules: { identity7of7: true, stress10of10: true, st10MustPass: true, p0MustBeZero: true },
    regressionRules: { noGenerate: true, noGemini: true, noRunway: true, goldenUntouched: true, noAutoApprove: true, noAutoLock: true },
    lockMetadata: { derived_from_master: MASTER_CODE_V1, master_sha256: masterSha256, dna_version: CHARACTER_DNA_VERSION, immutableAfterLock: true },
  };
}
