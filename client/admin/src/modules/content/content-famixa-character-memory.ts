/** Structured Character Memory — not a prompt dump. Compiler reads a subset. */

import { isOffFrameCanon, normCanonId } from './content-famixa-char-canon';
import { FAMIXA_VISUAL_STYLE as STYLE_TOKEN } from './content-famixa-visual-style';

export const CHARACTER_PROMPT_COMPILER = 'FAMIXA-CHAR-PROMPT-V1';
export const FAMIXA_VISUAL_STYLE = STYLE_TOKEN;

export type FamixaCharacterLifecycle =
  | 'draft'
  | 'designing'
  | 'review'
  | 'approved'
  | 'locked'
  | 'archived';

export type FamixaRefKind =
  | 'FRONT'
  | 'THREE_Q_LEFT'
  | 'THREE_Q_RIGHT'
  | 'SIDE'
  | 'FULL_BODY'
  | 'NEUTRAL'
  | 'EXPRESSION';

export type FamixaWorkspaceStepId =
  | 'identity'
  | 'visualDna'
  | 'personality'
  | 'behavior'
  | 'voice'
  | 'wardrobe'
  | 'relationships'
  | 'evolution'
  | 'references'
  | 'qa'
  | 'review'
  | 'lock';

export type FamixaQaStatus = 'PASS' | 'WARNING' | 'FAIL';

export type FamixaCharacterIdentity = {
  characterId: string;
  name: string;
  role?: string;
  gender?: string;
  currentAge?: number;
  currentEra?: string;
  familyRole?: string;
  biography?: string;
  characterType?: string;
  initialAge?: number;
};

export type FamixaVisualDna = {
  documentId?: string;
  status?: 'DRAFT' | 'REVIEW' | 'APPROVED' | string;
  characterId?: string;
  era?: string;
  age?: number;
  version?: string;
  face?: string;
  hair?: string;
  hairStyle?: string;
  eyes?: string;
  eyebrows?: string;
  nose?: string;
  mouth?: string;
  skin?: string;
  body?: string;
  bodyProportion?: string;
  height?: string;
  build?: string;
  distinctiveFeatures?: string;
  defaultClothing?: string;
  visualStyle?: string;
  role?: string;
  identityPrinciple?: string;
  whoVisually?: string;
  headShape?: string;
  eyeShape?: string;
  noseShape?: string;
  mouthShape?: string;
  hairLock?: string;
  posture?: string;
  emotionalSignature?: string;
  wardrobeBaseline?: string;
  colorPersonality?: string;
  recognitionTests?: string[];
  generationPriority?: string[];
  signatureExpression?: string;
  headToBody?: string;
  lineStyle?: string;
  dimension?: string;
  texture?: string;
  lighting?: string;
  color?: string;
  realism?: string;
  emotionRead?: string;
  cuteLevel?: string;
  silhouette?: string;
  immutableTraits?: string[];
  forbidden?: string[];
};

export type FamixaPersonality = {
  core?: string[];
  strengths?: string[];
  weaknesses?: string[];
  fears?: string[];
  needs?: string[];
  desires?: string[];
  values?: string[];
  triggers?: string[];
  emotionalPatterns?: string[];
};

export type FamixaOutfit = {
  id: string;
  set: string;
  label?: string;
  description?: string;
  referenceImage?: string;
  era?: string;
  status?: string;
};

export type FamixaCharacterRef = { kind: string; path: string; label?: string };
export type FamixaEraRow = { era: string; status: 'active' | 'planned' | 'draft' | 'locked'; masterReady?: boolean };
export type FamixaEvolution = { initialEra: string; eras: FamixaEraRow[] };

export type FamixaQaResult = { status: FamixaQaStatus; ticks?: { key: string; ok: boolean; note?: string }[] };

export type FamixaWorkspaceState = {
  completed?: Partial<Record<FamixaWorkspaceStepId, boolean>>;
  visualProposal?: FamixaVisualDna;
  visualProposalStatus?: 'draft' | 'approved';
  revisionReason?: string;
  qa?: FamixaQaResult;
};

export type FamixaCharacterCanon = {
  identity: FamixaCharacterIdentity;
  visualDna?: FamixaVisualDna;
  personality?: FamixaPersonality;
  personalityDna?: string[];
  behaviorDna?: { when: string; does: string }[];
  voiceDna?: {
    voiceId?: string;
    provider?: string;
    language?: string;
    age?: string;
    gender?: string;
    tone?: string;
    pitch?: string;
    speed?: string;
    speakingStyle?: string;
    emotionalRange?: string;
    ageCharacter?: string;
  };
  wardrobe?: FamixaOutfit[];
  relationships?: { to: string; type?: string }[];
  evolution?: FamixaEvolution;
  continuityRules?: string[];
  famixaVisualStyle?: {
    documentId?: string;
    status?: 'DRAFT' | 'REVIEW' | 'APPROVED' | string;
    summary?: string;
    world?: string;
    recognition?: string;
    line?: string;
    dimension?: string;
    texture?: string;
    lighting?: string;
    color?: string;
    realism?: string;
    emotion?: string;
    cuteness?: string;
    silhouetteLaw?: string;
    notCompetingWith?: string[];
    immutable?: string[];
    forbidden?: string[];
  };
  references?: FamixaCharacterRef[];
  workspace?: FamixaWorkspaceState;
  qa?: FamixaQaResult;
  brief?: {
    documentId: string;
    characterId: string;
    status: 'BRIEF';
    readyFor?: string;
    statement?: string;
    oneLine?: string;
    emotionalCore?: string;
    notA?: string[];
    notLocked?: string[];
    nextStep?: string;
    visualDirection?: string;
    familyNote?: string;
  };
  plannedFamily?: { role: string; label: string; characterCode?: string }[];
};

export type FamixaCharacterRecord = {
  id: string;
  characterCode: string;
  name: string;
  role: string;
  universe: string;
  visual: 'frame' | 'mention' | 'voice' | string;
  lifecycle: FamixaCharacterLifecycle | string;
  currentVersionId?: string;
  currentEra: string;
  version: string;
  isCurrentCanon: boolean;
  approvedAt?: string;
  approvedBy?: string;
  canon: FamixaCharacterCanon;
  references: FamixaCharacterRef[];
  updatedAt: string;
};

export type FamixaGuardItem = {
  characterCode: string;
  lifecycle: string;
  era: string;
  version: string;
  wardrobeId?: string;
  refIds: string[];
  frontOk: boolean;
};

export type FamixaCharacterGuard = {
  ok: boolean;
  blocked: string[];
  resolved: FamixaGuardItem[];
  compilerVersion: string;
};

function strList(raw: unknown) {
  return Array.isArray(raw) ? raw.map(String).filter((s) => s.trim()) : [];
}

export function parseFamixaCanon(raw: unknown): FamixaCharacterCanon {
  const o = raw && typeof raw === 'object' ? (raw as Record<string, unknown>) : {};
  const id = (o.identity && typeof o.identity === 'object' ? o.identity : {}) as Record<string, unknown>;
  const personality = o.personality && typeof o.personality === 'object' ? (o.personality as Record<string, unknown>) : undefined;
  const evolution = o.evolution && typeof o.evolution === 'object' ? (o.evolution as Record<string, unknown>) : undefined;
  const workspace = o.workspace && typeof o.workspace === 'object' ? (o.workspace as Record<string, unknown>) : undefined;
  const qaRaw = (workspace?.qa ?? o.qa) && typeof (workspace?.qa ?? o.qa) === 'object' ? ((workspace?.qa ?? o.qa) as Record<string, unknown>) : undefined;
  return {
    identity: {
      characterId: String(id.characterId ?? ''),
      name: String(id.name ?? ''),
      role: id.role != null ? String(id.role) : undefined,
      gender: id.gender != null ? String(id.gender) : undefined,
      currentAge: typeof id.currentAge === 'number' ? id.currentAge : undefined,
      currentEra: id.currentEra != null ? String(id.currentEra) : undefined,
      familyRole: id.familyRole != null ? String(id.familyRole) : undefined,
      biography: id.biography != null ? String(id.biography) : undefined,
      characterType: id.characterType != null ? String(id.characterType) : undefined,
      initialAge: typeof id.initialAge === 'number' ? id.initialAge : undefined,
    },
    visualDna: o.visualDna && typeof o.visualDna === 'object' ? (o.visualDna as FamixaVisualDna) : undefined,
    personality: personality
      ? {
          core: strList(personality.core),
          strengths: strList(personality.strengths),
          weaknesses: strList(personality.weaknesses),
          fears: strList(personality.fears),
          needs: strList(personality.needs),
          desires: strList(personality.desires),
          values: strList(personality.values),
          triggers: strList(personality.triggers),
          emotionalPatterns: strList(personality.emotionalPatterns),
        }
      : undefined,
    personalityDna: strList(o.personalityDna),
    behaviorDna: Array.isArray(o.behaviorDna)
      ? o.behaviorDna
          .map((b) => {
            const row = b && typeof b === 'object' ? (b as Record<string, unknown>) : {};
            return { when: String(row.when ?? ''), does: String(row.does ?? '') };
          })
          .filter((b) => b.when)
      : [],
    voiceDna: o.voiceDna && typeof o.voiceDna === 'object' ? (o.voiceDna as FamixaCharacterCanon['voiceDna']) : undefined,
    wardrobe: Array.isArray(o.wardrobe)
      ? o.wardrobe
          .map((w) => {
            const row = w && typeof w === 'object' ? (w as Record<string, unknown>) : {};
            return {
              id: String(row.id ?? ''),
              set: String(row.set ?? ''),
              label: row.label != null ? String(row.label) : undefined,
              description: row.description != null ? String(row.description) : undefined,
              referenceImage: row.referenceImage != null ? String(row.referenceImage) : undefined,
              era: row.era != null ? String(row.era) : undefined,
              status: row.status != null ? String(row.status) : undefined,
            };
          })
          .filter((w) => w.id)
      : [],
    relationships: Array.isArray(o.relationships)
      ? o.relationships
          .map((r) => {
            const row = r && typeof r === 'object' ? (r as Record<string, unknown>) : {};
            return { to: String(row.to ?? ''), type: row.type != null ? String(row.type) : undefined };
          })
          .filter((r) => r.to)
      : [],
    evolution: evolution
      ? {
          initialEra: String(evolution.initialEra ?? id.currentEra ?? 'A11'),
          eras: Array.isArray(evolution.eras)
            ? evolution.eras
                .map((e) => {
                  const row = e && typeof e === 'object' ? (e as Record<string, unknown>) : {};
                  const status = String(row.status ?? 'planned');
                  return {
                    era: String(row.era ?? ''),
                    status: (status === 'active' || status === 'locked' || status === 'draft' ? status : 'planned') as FamixaEraRow['status'],
                    masterReady: row.masterReady === true,
                  };
                })
                .filter((e) => e.era)
            : [],
        }
      : undefined,
    continuityRules: strList(o.continuityRules),
    famixaVisualStyle:
      o.famixaVisualStyle && typeof o.famixaVisualStyle === 'object'
        ? (o.famixaVisualStyle as FamixaCharacterCanon['famixaVisualStyle'])
        : { summary: FAMIXA_VISUAL_STYLE.summary, documentId: 'FAMIXA-VISUAL-STYLE-V1', status: 'DRAFT' },
    references: Array.isArray(o.references)
      ? o.references
          .map((r) => {
            const row = r && typeof r === 'object' ? (r as Record<string, unknown>) : {};
            return { kind: String(row.kind ?? ''), path: String(row.path ?? ''), label: row.label != null ? String(row.label) : undefined };
          })
          .filter((r) => r.kind && r.path && !r.path.startsWith('data:'))
      : [],
    workspace: workspace
      ? {
          completed: workspace.completed && typeof workspace.completed === 'object' ? (workspace.completed as FamixaWorkspaceState['completed']) : undefined,
          visualProposal:
            workspace.visualProposal && typeof workspace.visualProposal === 'object'
              ? (workspace.visualProposal as FamixaVisualDna)
              : undefined,
          visualProposalStatus: workspace.visualProposalStatus === 'approved' ? 'approved' : 'draft',
          revisionReason: workspace.revisionReason != null ? String(workspace.revisionReason) : undefined,
          qa: qaRaw
            ? {
                status: qaRaw.status === 'PASS' || qaRaw.status === 'WARNING' || qaRaw.status === 'FAIL' ? qaRaw.status : 'FAIL',
                ticks: Array.isArray(qaRaw.ticks) ? (qaRaw.ticks as FamixaQaResult['ticks']) : undefined,
              }
            : undefined,
        }
      : undefined,
    qa: qaRaw
      ? {
          status: qaRaw.status === 'PASS' || qaRaw.status === 'WARNING' || qaRaw.status === 'FAIL' ? qaRaw.status : 'FAIL',
          ticks: Array.isArray(qaRaw.ticks) ? (qaRaw.ticks as FamixaQaResult['ticks']) : undefined,
        }
      : undefined,
    brief:
      o.brief && typeof o.brief === 'object'
        ? (() => {
            const b = o.brief as Record<string, unknown>;
            return {
              documentId: String(b.documentId ?? ''),
              characterId: String(b.characterId ?? ''),
              status: 'BRIEF' as const,
              readyFor: b.readyFor != null ? String(b.readyFor) : undefined,
              statement: b.statement != null ? String(b.statement) : undefined,
              oneLine: b.oneLine != null ? String(b.oneLine) : undefined,
              emotionalCore: b.emotionalCore != null ? String(b.emotionalCore) : undefined,
              notA: strList(b.notA),
              notLocked: strList(b.notLocked),
              nextStep: b.nextStep != null ? String(b.nextStep) : undefined,
              visualDirection: b.visualDirection != null ? String(b.visualDirection) : undefined,
              familyNote: b.familyNote != null ? String(b.familyNote) : undefined,
            };
          })()
        : undefined,
    plannedFamily: Array.isArray(o.plannedFamily)
      ? o.plannedFamily
          .map((r) => {
            const row = r && typeof r === 'object' ? (r as Record<string, unknown>) : {};
            return {
              role: String(row.role ?? ''),
              label: String(row.label ?? ''),
              characterCode: row.characterCode != null ? String(row.characterCode) : undefined,
            };
          })
          .filter((r) => r.role)
      : undefined,
  };
}

export function serializeFamixaCanon(canon: FamixaCharacterCanon): Record<string, unknown> {
  return {
    identity: canon.identity,
    visualDna: canon.visualDna ?? {},
    personality: canon.personality ?? {},
    personalityDna: canon.personalityDna ?? [],
    behaviorDna: canon.behaviorDna ?? [],
    voiceDna: canon.voiceDna ?? {},
    wardrobe: canon.wardrobe ?? [],
    relationships: canon.relationships ?? [],
    evolution: canon.evolution,
    continuityRules: canon.continuityRules ?? [],
    famixaVisualStyle: canon.famixaVisualStyle ?? { summary: FAMIXA_VISUAL_STYLE.summary },
    references: (canon.references ?? []).filter((r) => r.path && !r.path.startsWith('data:')),
    workspace: canon.workspace,
    qa: canon.qa ?? canon.workspace?.qa,
    brief: canon.brief,
    plannedFamily: canon.plannedFamily,
  };
}

export function hasFrontRef(record: FamixaCharacterRecord) {
  const refs = record.references?.length ? record.references : record.canon.references ?? [];
  return refs.some((r) => r.kind.toUpperCase() === 'FRONT' && r.path && !r.path.startsWith('data:'));
}

export function resolveWardrobeId(canon: FamixaCharacterCanon, shotOverride?: string) {
  if (shotOverride?.trim()) return shotOverride.trim();
  const home = (canon.wardrobe ?? []).find((w) => w.set.toUpperCase() === 'HOME');
  return home?.id || canon.wardrobe?.[0]?.id;
}

/** Identity + era + outfit + style only. Emotion/Action stay on the Shot. */
export function compileCharacterPromptSubset(canon: FamixaCharacterCanon, outfitId?: string) {
  const id = canon.identity;
  const v = canon.visualDna ?? {};
  const outfit =
    (canon.wardrobe ?? []).find((w) => w.id === outfitId) ||
    (canon.wardrobe ?? []).find((w) => w.set.toUpperCase() === 'HOME');
  const style = canon.famixaVisualStyle?.summary || FAMIXA_VISUAL_STYLE.summary;
  return [
    `${id.characterId} ${id.name}`.trim(),
    id.currentEra ? `era ${id.currentEra}` : '',
    id.currentAge != null ? `age ${id.currentAge}` : '',
    v.face,
    v.hair,
    v.build,
    outfit ? `wardrobe ${outfit.id}${outfit.label ? ` ${outfit.label}` : ''}` : '',
    style,
  ]
    .filter((s) => (s ?? '').trim())
    .join('. ');
}

export function characterProductionGuard(opts: {
  registry: FamixaCharacterRecord[];
  characterIds: string[];
  hasDialogue?: Record<string, boolean>;
  voiceIds?: Record<string, string>;
  unknownNames?: string[];
}): FamixaCharacterGuard {
  const byCode = new Map(opts.registry.map((r) => [normCanonId(r.characterCode), r]));
  const blocked: string[] = [];
  const resolved: FamixaGuardItem[] = [];
  const extraIds: string[] = [];
  for (const raw of opts.unknownNames ?? []) {
    const name = raw.trim();
    if (!name) continue;
    const hit = [...byCode.values()].find(
      (r) => r.name.trim().toLowerCase() === name.toLowerCase() || r.characterCode.toUpperCase() === name.toUpperCase(),
    );
    if (hit) {
      extraIds.push(hit.characterCode);
      continue;
    }
    blocked.push(`REQUEST CREATION: «${name}» chưa có trong Character Registry. Không tự tạo Canon.`);
  }
  const seen = new Set<string>();
  for (const raw of [...opts.characterIds, ...extraIds]) {
    const code = normCanonId(raw);
    if (!code || seen.has(code)) continue;
    seen.add(code);
    const row = byCode.get(code);
    if (!row) {
      blocked.push(`REQUEST CREATION: ${code} chưa có. Không tự invent nhân vật.`);
      continue;
    }
    const frame = row.visual === 'frame' && !isOffFrameCanon(row.characterCode, row.name);
    const front = hasFrontRef(row);
    const life = (row.lifecycle || '').toLowerCase();
    if (frame && life !== 'locked') {
      blocked.push(`${code}: Character chưa LOCK — không vào production (${row.lifecycle}).`);
    } else if (!frame && life !== 'approved' && life !== 'locked') {
      blocked.push(`${code}: Canon chưa APPROVE/LOCK (${row.lifecycle}).`);
    }
    if (frame && !front) blocked.push(`${code}: thiếu Reference FRONT — BLOCK.`);
    const voiceId = (row.canon.voiceDna?.voiceId ?? '').trim() || (opts.voiceIds?.[code] ?? '').trim();
    if (opts.hasDialogue?.[code] && !voiceId) {
      blocked.push(`${code}: có thoại nhưng chưa Voice ID.`);
    }
    const wardrobeId = resolveWardrobeId(row.canon);
    const refs = (row.references?.length ? row.references : row.canon.references ?? []).map((r) => `${r.kind}:${r.path}`);
    resolved.push({
      characterCode: code,
      lifecycle: row.lifecycle,
      era: row.currentEra || row.canon.identity.currentEra || 'A11',
      version: row.version || 'V1',
      wardrobeId,
      refIds: refs,
      frontOk: front || !frame,
    });
  }
  return { ok: blocked.length === 0, blocked, resolved, compilerVersion: CHARACTER_PROMPT_COMPILER };
}

export function resolveFamixaCharacters(registry: FamixaCharacterRecord[], ids: string[]) {
  const by = new Map(registry.map((r) => [normCanonId(r.characterCode), r]));
  return ids.map((id) => by.get(normCanonId(id))).filter((r): r is FamixaCharacterRecord => Boolean(r));
}

export function characterAuditOf(guard: FamixaCharacterGuard) {
  return {
    characterCodes: guard.resolved.map((r) => r.characterCode),
    era: guard.resolved[0]?.era || 'A11',
    version: guard.resolved[0]?.version || 'V1',
    refIds: guard.resolved.flatMap((r) => r.refIds),
    compilerVersion: guard.compilerVersion || CHARACTER_PROMPT_COMPILER,
    wardrobeIds: guard.resolved.map((r) => r.wardrobeId).filter((id): id is string => Boolean(id)),
  };
}
