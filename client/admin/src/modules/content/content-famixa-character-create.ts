/** Famixa Character Creation — workspace, QA, resolver, duplicate. AI may propose; user Approves/Locks. */

import { canonRowOf, FAMIXA_CHAR_CANON, normCanonId } from './content-famixa-char-canon';
import {
  FAMIXA_VISUAL_STYLE,
  hasFrontRef,
  parseFamixaCanon,
  type FamixaCharacterCanon,
  type FamixaCharacterRecord,
  type FamixaQaStatus,
  type FamixaRefKind,
  type FamixaVisualDna,
  type FamixaWorkspaceStepId,
} from './content-famixa-character-memory';
import { styleApproved, styleReadyForMaster } from './content-famixa-visual-style';
import {
  CHAR_001_MINH_VISUAL_DNA_V1,
  dnaApproved,
  dnaReadyForMaster,
  MINH_DNA_ID,
  MINH_DNA_ID_LEGACY,
  minhDnaCompilerLines,
} from './content-famixa-minh-visual-dna';

export const CHARACTER_CREATION_SPEC = 'FAMIXA-CHAR-CREATE-V1';

export const WORKSPACE_STEPS: { id: FamixaWorkspaceStepId; n: number; title: string }[] = [
  { id: 'identity', n: 1, title: 'Identity' },
  { id: 'visualDna', n: 2, title: 'Visual DNA' },
  { id: 'personality', n: 3, title: 'Personality' },
  { id: 'behavior', n: 4, title: 'Behavior' },
  { id: 'voice', n: 5, title: 'Voice' },
  { id: 'wardrobe', n: 6, title: 'Wardrobe' },
  { id: 'relationships', n: 7, title: 'Relationships' },
  { id: 'evolution', n: 8, title: 'Age & Evolution' },
  { id: 'references', n: 9, title: 'Reference Generation' },
  { id: 'qa', n: 10, title: 'QA' },
  { id: 'review', n: 11, title: 'Review' },
  { id: 'lock', n: 12, title: 'Lock' },
];

export const CORE_MASTER_VIEWS: { kind: FamixaRefKind; label: string }[] = [
  { kind: 'FRONT', label: '01 Front' },
  { kind: 'THREE_Q_LEFT', label: '02 3/4 Left' },
  { kind: 'THREE_Q_RIGHT', label: '03 3/4 Right' },
  { kind: 'SIDE', label: '04 Side' },
  { kind: 'FULL_BODY', label: '05 Full Body' },
  { kind: 'NEUTRAL', label: '06 Neutral' },
  { kind: 'EXPRESSION', label: '07 Expression Sheet' },
];

export const WARDROBE_SETS = ['SCHOOL', 'HOME', 'CASUAL', 'SPORTS', 'SPECIAL'] as const;

export const CHARACTER_TYPES = ['CORE', 'RECURRING', 'SUPPORTING', 'BACKGROUND'] as const;

const FUTURE_ERAS = ['A13', 'A16', 'A18', 'A21', 'A23'] as const;

export function eraOfAge(age?: number | null) {
  const n = Number(age);
  if (!Number.isFinite(n) || n < 1) return 'A11';
  return `A${Math.round(n)}`;
}

export function plannedEras(initialEra: string) {
  const start = initialEra.toUpperCase();
  const eras = [{ era: start, status: 'active' as const }];
  for (const era of FUTURE_ERAS) {
    if (era === start) continue;
    const later = Number(era.slice(1)) > Number(start.replace(/\D/g, '') || 0);
    if (later) eras.push({ era, status: 'planned' });
  }
  return eras;
}

export function normPersonName(raw: string) {
  return (raw ?? '').trim().replace(/\s+/g, ' ').toLowerCase();
}

export function nextCharacterCode(existing: { characterCode: string }[]) {
  let max = 0;
  for (const row of existing) {
    const m = normCanonId(row.characterCode).match(/^CHAR-(\d+)$/i);
    if (m) max = Math.max(max, Number(m[1]));
  }
  return `CHAR-${String(max + 1).padStart(3, '0')}`;
}

export function detectDuplicateCharacter(
  registry: Pick<FamixaCharacterRecord, 'characterCode' | 'name' | 'role' | 'universe'>[],
  input: { name: string; role?: string; familyRole?: string },
) {
  const name = normPersonName(input.name);
  if (!name) return { duplicate: false as const };
  const hits = registry.filter((r) => {
    const sameName = normPersonName(r.name) === name;
    const canon = canonRowOf(r.characterCode) || canonRowOf(r.name);
    const alias = canon ? canon.aliases.includes(name) || normPersonName(canon.name) === name : false;
    return sameName || alias;
  });
  if (!hits.length) return { duplicate: false as const };
  const sameIdentity = hits.find((r) => {
    const role = normPersonName(r.role);
    const want = normPersonName(input.role ?? '');
    const family = normPersonName(input.familyRole ?? '');
    return !want || role === want || role === family || (want && role.includes(want));
  });
  return {
    duplicate: true as const,
    preferred: 'USE_EXISTING' as const,
    existing: hits,
    sameIdentity: Boolean(sameIdentity || hits.length === 1),
    message: `A character with this identity already exists (${hits.map((h) => h.characterCode).join(', ')}).`,
  };
}

export function resolveCharacterByName(
  registry: Pick<FamixaCharacterRecord, 'characterCode' | 'name'>[],
  raw: string,
) {
  const token = (raw ?? '').trim();
  if (!token) return undefined;
  const id = normCanonId(token);
  const byCode = registry.find((r) => normCanonId(r.characterCode) === id);
  if (byCode) return byCode;
  const key = normPersonName(token);
  const byName = registry.find((r) => normPersonName(r.name) === key);
  if (byName) return byName;
  const roster = canonRowOf(token);
  if (!roster) return undefined;
  return registry.find((r) => normCanonId(r.characterCode) === roster.id);
}

/** Script “Minh về nhà.” → CHAR-001 + current era + current canon + master. Never invent. */
export function resolveScriptCharacter(
  registry: FamixaCharacterRecord[],
  raw: string,
  age?: number,
) {
  const hit = resolveCharacterByName(registry, raw);
  if (!hit) {
    return {
      ok: false as const,
      blocked: `Character “${raw.trim()}” has not been registered.`,
      requestCreation: true as const,
    };
  }
  const era = age != null ? eraOfAge(age) : hit.currentEra || hit.canon.identity.currentEra || 'A11';
  const refs = hit.references?.length ? hit.references : hit.canon.references ?? [];
  const master = refs.find((r) => r.kind.toUpperCase() === 'FRONT');
  return {
    ok: true as const,
    characterCode: hit.characterCode,
    era,
    version: hit.version || 'V1',
    masterPath: master?.path,
    lifecycle: hit.lifecycle,
    requestCreation: false as const,
  };
}

/** JS `\b` + `[A-ZÀ-Ỵ]` splits Vietnamese mid-word (`sáng về` → «áng»). */
const CAST_ACTION_VERB = 'bước|đi|nói|nhìn|về|đứng|ngồi|chạy|vào|ra';
const VI_PROPER_NAME = new RegExp(
  `(?:^|[^\\p{L}])([A-ZÀÁẢÃẠĂẰẮẲẴẶÂẦẤẨẪẬĐÈÉẺẼẸÊỀẾỂỄỆÌÍỈĨỊÒÓỎÕỌÔỒỐỔỖỘƠỜỚỞỠỢÙÚỦŨỤƯỪỨỬỮỰỲÝỶỸỴ][\\p{Ll}]{2,16})\\s+(?:${CAST_ACTION_VERB})(?=\\P{L}|$)`,
  'gu',
);
const VI_NAME_STOP = new Set(
  [
    'khi',
    'một',
    'người',
    'cậu',
    'cô',
    'anh',
    'chị',
    'em',
    'không',
    'sáng',
    'ánh',
    'khoảng',
    'buổi',
    'trong',
    'ngoài',
    'phòng',
    'nhà',
    'cửa',
    'bàn',
    'mặt',
    'rồi',
    'sao',
    'cái',
  ].map((s) => s.toLowerCase()),
);

export function findUnregisteredNames(text: string, registry: Pick<FamixaCharacterRecord, 'characterCode' | 'name'>[]) {
  const missing: string[] = [];
  const src = text ?? '';
  VI_PROPER_NAME.lastIndex = 0;
  let m: RegExpExecArray | null;
  while ((m = VI_PROPER_NAME.exec(src))) {
    const name = (m[1] ?? '').trim();
    if (!name || VI_NAME_STOP.has(normPersonName(name))) continue;
    if (resolveCharacterByName(registry, name) || canonRowOf(name)) continue;
    if (!missing.some((x) => normPersonName(x) === normPersonName(name))) missing.push(name);
  }
  return missing;
}

export function relationshipMustUseExisting(
  registry: Pick<FamixaCharacterRecord, 'characterCode' | 'name'>[],
  raw: string,
) {
  const hit = resolveCharacterByName(registry, raw);
  if (hit) return { ok: true as const, characterCode: hit.characterCode };
  return { ok: false as const, blocked: `Character “${raw.trim()}” has not been registered. Chọn Character đã có — không tạo bản sao.` };
}

function filled(v?: string | number | null) {
  if (typeof v === 'number') return Number.isFinite(v);
  return Boolean((v ?? '').toString().trim());
}

function dnaFilled(dna?: FamixaVisualDna) {
  if (!dna) return false;
  if (dna.documentId === MINH_DNA_ID || dna.documentId === MINH_DNA_ID_LEGACY) {
    return filled(dna.whoVisually) && filled(dna.hairLock) && filled(dna.silhouette) && filled(dna.headToBody);
  }
  return [dna.face, dna.hair, dna.eyes, dna.build].filter((x) => filled(x)).length >= 3;
}

export function workspaceStepDone(record: FamixaCharacterRecord, step: FamixaWorkspaceStepId): boolean {
  const c = record.canon;
  const marked = c.workspace?.completed?.[step];
  if (marked === false) return false;
  if (marked === true && step !== 'lock' && step !== 'references' && step !== 'qa') return true;
  switch (step) {
    case 'identity':
      return filled(c.identity.characterId) && filled(c.identity.name) && filled(c.identity.currentEra || record.currentEra);
    case 'visualDna':
      return dnaFilled(c.visualDna) && (
        c.workspace?.visualProposalStatus === 'approved' ||
        record.lifecycle === 'approved' ||
        record.lifecycle === 'locked'
      );
    case 'personality':
      return (c.personalityDna?.length ?? 0) > 0 || (c.personality?.core?.length ?? 0) > 0;
    case 'behavior':
      return (c.behaviorDna?.length ?? 0) > 0;
    case 'voice':
      return record.visual === 'voice' || Boolean((c.voiceDna?.voiceId ?? '').trim()) || record.universe === 'BACKGROUND';
    case 'wardrobe':
      return record.universe === 'BACKGROUND' || (c.wardrobe?.length ?? 0) > 0;
    case 'relationships':
      return record.universe === 'BACKGROUND' || (c.relationships?.length ?? 0) > 0 || marked === true;
    case 'evolution':
      return Boolean(c.evolution?.initialEra || c.identity.currentEra || record.currentEra);
    case 'references':
      return record.visual !== 'frame' || hasFrontRef(record);
    case 'qa':
      return (c.workspace?.qa?.status ?? c.qa?.status) === 'PASS' || (c.workspace?.qa?.status ?? c.qa?.status) === 'WARNING';
    case 'review':
      return record.lifecycle === 'approved' || record.lifecycle === 'locked' || marked === true;
    case 'lock':
      return record.lifecycle === 'locked';
    default:
      return false;
  }
}

export function workspaceProgress(record: FamixaCharacterRecord) {
  const done = WORKSPACE_STEPS.filter((s) => workspaceStepDone(record, s.id));
  return { completed: done.length, total: WORKSPACE_STEPS.length, steps: done.map((s) => s.id) };
}

export function definitionComplete(record: FamixaCharacterRecord) {
  return (['identity', 'visualDna', 'personality', 'behavior', 'voice', 'wardrobe', 'relationships', 'evolution'] as const).every(
    (id) => workspaceStepDone(record, id),
  );
}

export function canGenerateMasterRef(record: FamixaCharacterRecord) {
  if (record.characterCode === 'CHAR-001') {
    if (!styleReadyForMaster(record.canon.famixaVisualStyle)) {
      return { ok: false as const, blocked: 'CHAR-001: FAMIXA VISUAL STYLE V1 phải REVIEW/APPROVED trước khi tạo Master. Không dùng ảnh Minh hiện tại.' };
    }
    if (!dnaReadyForMaster(record.canon.visualDna)) {
      return { ok: false as const, blocked: 'CHAR-001: Visual DNA V1 phải REVIEW/APPROVED trước khi tạo Master. Không LOCK từ sheet cũ.' };
    }
  }
  if (!definitionComplete(record)) {
    return { ok: false as const, blocked: 'Character Definition chưa xong. Không tạo Master Reference trước bước 01–08.' };
  }
  if (record.lifecycle === 'locked') {
    return { ok: false as const, blocked: 'LOCKED: UNLOCK + version mới trước khi tạo lại Master.' };
  }
  return { ok: true as const };
}

export function nextMasterRefKind(record: FamixaCharacterRecord): FamixaRefKind | undefined {
  const refs = record.references?.length ? record.references : record.canon.references ?? [];
  const have = new Set(refs.map((r) => r.kind.toUpperCase()));
  return CORE_MASTER_VIEWS.find((v) => !have.has(v.kind))?.kind;
}

export function proposeVisualDna(identity: {
  name: string;
  gender?: string;
  currentAge?: number;
  role?: string;
  characterType?: string;
}): FamixaVisualDna {
  if (/minh/i.test(identity.name) || identity.characterType === 'CHAR-001') {
    return minhDnaCompilerLines({ ...CHAR_001_MINH_VISUAL_DNA_V1, status: 'DRAFT' });
  }
  const age = identity.currentAge ?? 11;
  const male = /male|nam|boy|bé trai/i.test(identity.gender ?? '');
  const child = age <= 14;
  return {
    face: child ? 'round youthful Vietnamese face' : 'natural Vietnamese adult face',
    eyes: 'natural dark eyes',
    eyebrows: 'natural dark eyebrows',
    nose: child ? 'small youthful nose' : 'natural nose',
    mouth: child ? 'child mouth' : 'natural mouth',
    hair: male ? (child ? 'short black slightly messy hair' : 'short black hair') : 'shoulder-length black hair',
    hairStyle: male ? 'short' : 'natural',
    skin: 'warm Vietnamese skin',
    body: child ? 'child proportion' : 'adult proportion',
    bodyProportion: child ? 'child' : 'adult',
    height: child ? 'short for age' : 'average Vietnamese height',
    build: child ? 'slim child body' : 'average build',
    distinctiveFeatures: `${identity.name} ${identity.role ?? ''} ${age}`.trim(),
    visualStyle: 'FAMIXA_VISUAL_STYLE',
  };
}

export function compileMasterRefPrompt(opts: {
  canon: FamixaCharacterCanon;
  kind: FamixaRefKind;
  inheritFrom?: string;
}) {
  const id = opts.canon.identity;
  const v = opts.canon.visualDna ?? {};
  const style = opts.canon.famixaVisualStyle?.summary || FAMIXA_VISUAL_STYLE.summary;
  const view =
    opts.kind === 'FRONT'
      ? 'front portrait, face fully visible, neutral expression'
      : opts.kind === 'THREE_Q_LEFT'
        ? 'three-quarter left, same face as master front'
        : opts.kind === 'THREE_Q_RIGHT'
          ? 'three-quarter right, same face as master front'
          : opts.kind === 'SIDE'
            ? 'true side profile, same identity as master front'
            : opts.kind === 'FULL_BODY'
              ? 'full body standing, same face and wardrobe as master front'
              : opts.kind === 'NEUTRAL'
                ? 'neutral expression, same identity as master front'
                : 'expression sheet, same identity, only expression changes';
  return [
    `${id.characterId} ${id.name}`.trim(),
    id.currentEra ? `era ${id.currentEra}` : '',
    id.currentAge != null ? `age ${id.currentAge}` : '',
    v.whoVisually,
    v.headShape,
    v.hairLock,
    v.eyeShape,
    v.headToBody,
    v.silhouette,
    v.face,
    v.hair,
    v.eyes,
    v.build,
    v.distinctiveFeatures,
    style,
    view,
    opts.inheritFrom ? `inherit identity from previous master ${opts.inheritFrom}` : '',
    'Do not invent a new hairstyle, clothes, age, or facial features.',
    'Do not add text or watermark.',
  ]
    .filter((s) => (s ?? '').trim())
    .join('. ');
}

export type FamixaQaTick = { key: string; ok: boolean; note?: string };

export function characterQa(record: FamixaCharacterRecord): {
  status: FamixaQaStatus;
  ticks: FamixaQaTick[];
} {
  const c = record.canon;
  const refs = record.references?.length ? record.references : c.references ?? [];
  const kinds = new Set(refs.map((r) => r.kind.toUpperCase()));
  const frame = record.visual === 'frame';
  const core = record.universe === 'CORE';
  const ticks: FamixaQaTick[] = [
    { key: 'identity', ok: filled(c.identity.characterId) && filled(c.identity.name) },
    { key: 'face', ok: filled(c.visualDna?.face) },
    { key: 'hair', ok: filled(c.visualDna?.hair) },
    { key: 'eyes', ok: filled(c.visualDna?.eyes) },
    { key: 'age', ok: filled(c.identity.currentAge) || filled(c.identity.currentEra) },
    { key: 'body', ok: filled(c.visualDna?.build) || filled(c.visualDna?.body) },
    { key: 'style', ok: Boolean(c.famixaVisualStyle?.summary) && c.visualDna?.visualStyle !== 'CUSTOM_UNRELATED' },
    { key: 'clothing', ok: !frame || (c.wardrobe?.length ?? 0) > 0 || record.universe === 'BACKGROUND' },
    { key: 'personality', ok: (c.personalityDna?.length ?? 0) > 0 || (c.personality?.core?.length ?? 0) > 0 || !core },
    { key: 'behavior', ok: (c.behaviorDna?.length ?? 0) > 0 || !core },
    { key: 'voice', ok: record.visual !== 'frame' || Boolean((c.voiceDna?.voiceId ?? '').trim()) || record.universe === 'BACKGROUND' },
    { key: 'relationships', ok: (c.relationships?.length ?? 0) > 0 || record.universe === 'BACKGROUND' || !core },
    { key: 'front', ok: !frame || kinds.has('FRONT') },
    { key: 'threeQ', ok: !core || !frame || kinds.has('THREE_Q_LEFT') || kinds.has('THREE_Q_RIGHT') },
    { key: 'side', ok: !core || !frame || kinds.has('SIDE') },
    { key: 'fullBody', ok: !core || !frame || kinds.has('FULL_BODY') },
    { key: 'expression', ok: !core || !frame || kinds.has('EXPRESSION') || kinds.has('NEUTRAL') },
  ];
  const failKeys = new Set(['identity', 'front']);
  const failed = ticks.filter((t) => !t.ok && failKeys.has(t.key));
  const warned = ticks.filter((t) => !t.ok && !failKeys.has(t.key));
  const status: FamixaQaStatus = failed.length ? 'FAIL' : warned.length ? 'WARNING' : 'PASS';
  return { status, ticks };
}

export function canApproveCharacter(record: FamixaCharacterRecord) {
  if (record.lifecycle === 'locked') {
    return { ok: false as const, blocked: 'LOCKED. UNLOCK + version mới trước khi duyệt lại.' };
  }
  const qa = characterQa(record);
  if (qa.status === 'FAIL') return { ok: false as const, blocked: 'QA FAIL — không APPROVE.', qa };
  return { ok: true as const, qa };
}

export function canLockCharacter(record: FamixaCharacterRecord) {
  if (record.lifecycle === 'locked') return { ok: false as const, blocked: 'Đã LOCK.' };
  if (record.lifecycle !== 'approved') {
    return { ok: false as const, blocked: 'APPROVE trước. APPROVE ≠ LOCK.' };
  }
  if (record.characterCode === 'CHAR-001') {
    if (!styleApproved(record.canon.famixaVisualStyle) || !dnaApproved(record.canon.visualDna)) {
      return {
        ok: false as const,
        blocked: 'CHAR-001: không LOCK từ ảnh hiện tại. Cần Style V1 APPROVED + DNA V1 APPROVED + Master sinh từ DNA.',
      };
    }
  }
  const qa = characterQa(record);
  if (qa.status === 'FAIL') return { ok: false as const, blocked: 'QA FAIL — không LOCK.', qa };
  return { ok: true as const, qa, warning: qa.status === 'WARNING' };
}

export function lockedFieldBlocked(lifecycle: string, field: string) {
  if ((lifecycle || '').toLowerCase() !== 'locked') return undefined;
  const blocked = /visualDna|hair|face|voice|personality|reference|master/i.test(field);
  return blocked
    ? `LOCKED: không sửa ${field} trực tiếp. UNLOCK REQUEST → CREATE NEW VERSION → REVIEW → APPROVE → LOCK.`
    : undefined;
}

export function nextVersionLabel(current?: string) {
  const m = (current || 'V1').toUpperCase().match(/^V(\d+)$/);
  return `V${(m ? Number(m[1]) : 1) + 1}`;
}

export function seedDraftCanon(input: {
  characterCode: string;
  name: string;
  role?: string;
  gender?: string;
  initialAge?: number;
  characterType?: string;
  description?: string;
  familyRole?: string;
}): FamixaCharacterCanon {
  const age = input.initialAge ?? 11;
  const era = eraOfAge(age);
  return parseFamixaCanon({
    identity: {
      characterId: input.characterCode,
      name: input.name,
      role: input.role,
      gender: input.gender,
      currentAge: age,
      currentEra: era,
      familyRole: input.familyRole,
      biography: input.description,
      characterType: input.characterType,
      initialAge: age,
    },
    visualDna: {},
    personalityDna: [],
    personality: {},
    behaviorDna: [],
    voiceDna: { voiceId: '', language: 'vi', provider: '' },
    wardrobe: [],
    relationships: [],
    evolution: { initialEra: era, eras: plannedEras(era) },
    continuityRules: [],
    famixaVisualStyle: { summary: FAMIXA_VISUAL_STYLE.summary },
    references: [],
    workspace: { completed: { identity: true }, visualProposalStatus: 'draft' },
  });
}

export function rosterHint(name: string) {
  return FAMIXA_CHAR_CANON.find((r) => r.aliases.includes(normPersonName(name)) || normPersonName(r.name) === normPersonName(name));
}
