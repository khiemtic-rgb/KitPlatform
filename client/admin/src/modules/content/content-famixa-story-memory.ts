/** Long-form Famixa memory on the Series graph. KIT tracks; Story Director writes. */

import type { FamixaCharacter, FamixaSeriesEpisode, SeriesPilotState } from './content-famixa-series';

function episodeCodeOf(raw?: string) {
  return raw?.match(/EP\s*\d+/i)?.[0]?.replace(/\s+/g, '').toUpperCase() || '';
}

export const STORY_BEATS = [
  'CONFLICT',
  'CONSEQUENCE',
  'ESCALATION',
  'DAMAGE',
  'LOSS',
  'REALIZATION',
  'REGRET',
  'ACTION',
  'REPAIR',
  'CHANGE',
] as const;

export type StoryBeat = (typeof STORY_BEATS)[number];
export type StoryThreadStatus = 'OPEN' | 'RESOLVED';

export type FamixaStoryThread = {
  id: string;
  name: string;
  status: StoryThreadStatus;
  createdEpisode: string;
  resolvedEpisode?: string;
  cause?: string;
  consequence?: string;
  potentialResolution?: string;
};

export type FamixaCharacterState = {
  characterId: string;
  goal?: string;
  fear?: string;
  belief?: string;
  emotion?: string;
  knowledge?: string;
  secrets?: string;
  internalConflict?: string;
  arcPosition?: string;
  behavior?: string;
  /** Semantic trust toward another CHAR id — not a score. */
  trust?: Record<string, string>;
};

export type FamixaRelationshipState = {
  id: string;
  a: string;
  b: string;
  trust?: string;
  communication?: string;
  conflict?: string;
  distance?: string;
  affection?: string;
  unresolved?: string;
};

export type FamixaEpisodeNarrative = {
  episode: string;
  title?: string;
  whatHappened?: string;
  whatChanged?: string;
  characterChanges?: string;
  relationshipChanges?: string;
  newConflicts?: string;
  unresolvedConflicts?: string;
  consequences?: string;
  secretsRevealed?: string;
  secretsHidden?: string;
  promises?: string;
  objects?: string;
  locations?: string;
  timeline?: string;
  hook?: string;
  approved: boolean;
  approvedAt?: string;
};

export type FamixaArcNote = {
  premise?: string;
  currentBeat?: string;
};

export type FamixaStoryMemory = {
  seriesArc: FamixaArcNote;
  season: string;
  seasonArc: FamixaArcNote;
  episodeNarrative?: FamixaEpisodeNarrative;
  ledger: FamixaEpisodeNarrative[];
  characterStates: FamixaCharacterState[];
  relationships: FamixaRelationshipState[];
  threads: FamixaStoryThread[];
  inheritReviewed: boolean;
  inheritFromEpisode?: string;
};

const DEFAULT_PAIRS: [string, string][] = [
  ['CHAR-001', 'CHAR-002'],
  ['CHAR-001', 'CHAR-003'],
  ['CHAR-002', 'CHAR-003'],
];

export function relationshipId(a: string, b: string) {
  return [a, b].sort().join('|');
}

export function emptyStoryMemory(): FamixaStoryMemory {
  return {
    seriesArc: {},
    season: 'Season 01',
    seasonArc: {},
    ledger: [],
    characterStates: [],
    relationships: [],
    threads: [],
    inheritReviewed: true,
  };
}

export function emptyEpisodeNarrative(episode: string, title?: string): FamixaEpisodeNarrative {
  return { episode, title, approved: false };
}

function charIdsOf(characters: FamixaCharacter[]) {
  const ids = characters.map((c) => c.id).filter(Boolean);
  if (ids.length) return [...new Set(ids)];
  return ['CHAR-001', 'CHAR-002', 'CHAR-003'];
}

export function ensureStoryMemory(
  raw: FamixaStoryMemory | undefined,
  characters: FamixaCharacter[],
  episode?: FamixaSeriesEpisode,
): FamixaStoryMemory {
  const mem = raw ?? emptyStoryMemory();
  const ids = charIdsOf(characters);
  const characterStates = [...mem.characterStates];
  for (const id of ids) {
    if (!characterStates.some((s) => s.characterId === id)) characterStates.push({ characterId: id });
  }
  const relationships = [...mem.relationships];
  const pairs = ids.length >= 2 ? DEFAULT_PAIRS.filter(([a, b]) => ids.includes(a) && ids.includes(b)) : [];
  for (const [a, b] of pairs) {
    const id = relationshipId(a, b);
    if (!relationships.some((r) => r.id === id)) relationships.push({ id, a, b });
  }
  const ep = episodeCodeOf(episode?.episode || episode?.title) || episode?.episode || '';
  const episodeNarrative =
    mem.episodeNarrative && (!ep || mem.episodeNarrative.episode === ep)
      ? mem.episodeNarrative
      : ep
        ? emptyEpisodeNarrative(ep, episode?.title)
        : mem.episodeNarrative;
  return {
    ...mem,
    season: mem.season || 'Season 01',
    seriesArc: mem.seriesArc ?? {},
    seasonArc: mem.seasonArc ?? {},
    ledger: Array.isArray(mem.ledger) ? mem.ledger : [],
    threads: Array.isArray(mem.threads) ? mem.threads : [],
    characterStates,
    relationships,
    episodeNarrative,
    inheritReviewed: Boolean(mem.inheritReviewed) || mem.ledger.length === 0,
  };
}

export function openThreads(memory?: FamixaStoryMemory) {
  return (memory?.threads ?? []).filter((t) => t.status === 'OPEN');
}

export function lastApprovedEpisode(memory?: FamixaStoryMemory) {
  return [...(memory?.ledger ?? [])].reverse().find((e) => e.approved);
}

export function needsInheritanceReview(state: Pick<SeriesPilotState, 'episode' | 'storyMemory'>) {
  const mem = state.storyMemory;
  if (!mem?.ledger.length) return false;
  const current = episodeCodeOf(state.episode?.episode || state.episode?.title);
  const last = lastApprovedEpisode(mem);
  if (!current || !last) return false;
  if (current === last.episode) return false;
  return !mem.inheritReviewed;
}

export function inheritStoryMemory(prev: FamixaStoryMemory | undefined, nextEpisode: string, title?: string): FamixaStoryMemory {
  const mem = prev ?? emptyStoryMemory();
  const from = lastApprovedEpisode(mem)?.episode || mem.episodeNarrative?.episode;
  return {
    ...mem,
    episodeNarrative: emptyEpisodeNarrative(nextEpisode, title),
    inheritReviewed: false,
    inheritFromEpisode: from || mem.inheritFromEpisode,
    threads: mem.threads.map((t) =>
      t.status === 'OPEN' ? { ...t, status: 'OPEN', resolvedEpisode: undefined } : t,
    ),
  };
}

export function approveEpisodeNarrative(memory: FamixaStoryMemory, episode: FamixaSeriesEpisode | undefined): FamixaStoryMemory {
  const code = episodeCodeOf(episode?.episode || episode?.title) || memory.episodeNarrative?.episode || 'EP01';
  const draft: FamixaEpisodeNarrative = {
    ...(memory.episodeNarrative ?? emptyEpisodeNarrative(code, episode?.title)),
    episode: code,
    title: episode?.title || memory.episodeNarrative?.title,
    approved: true,
    approvedAt: new Date().toISOString(),
  };
  const ledger = [...memory.ledger.filter((e) => e.episode !== code), draft];
  return {
    ...memory,
    episodeNarrative: draft,
    ledger,
  };
}

export function addStoryThread(
  memory: FamixaStoryMemory,
  input: { name: string; createdEpisode: string; cause?: string; consequence?: string; potentialResolution?: string },
): FamixaStoryMemory {
  const name = input.name.trim();
  if (!name) return memory;
  const n = memory.threads.reduce((max, t) => {
    const m = t.id.match(/THREAD-(\d+)/i);
    return Math.max(max, m ? Number(m[1]) : 0);
  }, 0);
  const row: FamixaStoryThread = {
    id: `THREAD-${String(n + 1).padStart(3, '0')}`,
    name,
    status: 'OPEN',
    createdEpisode: input.createdEpisode,
    cause: input.cause?.trim() || undefined,
    consequence: input.consequence?.trim() || undefined,
    potentialResolution: input.potentialResolution?.trim() || undefined,
  };
  return { ...memory, threads: [...memory.threads, row] };
}

export function resolveStoryThread(memory: FamixaStoryMemory, threadId: string, episode: string): FamixaStoryMemory {
  return {
    ...memory,
    threads: memory.threads.map((t) =>
      t.id === threadId && t.status === 'OPEN' ? { ...t, status: 'RESOLVED', resolvedEpisode: episode } : t,
    ),
  };
}

export function reopenStoryThread(memory: FamixaStoryMemory, threadId: string): FamixaStoryMemory {
  return {
    ...memory,
    threads: memory.threads.map((t) =>
      t.id === threadId ? { ...t, status: 'OPEN', resolvedEpisode: undefined } : t,
    ),
  };
}

export type InheritanceReview = {
  fromEpisode?: string;
  toEpisode?: string;
  whatHappened?: string;
  whatChanged?: string;
  characterChanges?: string;
  relationshipChanges?: string;
  openConflicts?: string;
  openThreads: FamixaStoryThread[];
  consequences?: string;
  inheritLines: string[];
};

export function inheritanceReview(
  state: Pick<SeriesPilotState, 'episode' | 'storyMemory' | 'characters'>,
): InheritanceReview {
  const mem = state.storyMemory ?? emptyStoryMemory();
  const last = lastApprovedEpisode(mem);
  const to = episodeCodeOf(state.episode?.episode || state.episode?.title);
  const open = openThreads(mem);
  const inheritLines = [
    last ? `Kế thừa ${last.episode}${last.title ? ` — ${last.title}` : ''}` : 'Chưa có tập đã khóa trạng thái',
    `${open.length} thread OPEN — KIT không đóng`,
    mem.characterStates.some((s) => s.emotion || s.secrets || s.internalConflict || Object.keys(s.trust ?? {}).length)
      ? 'Character State đi tiếp'
      : 'Character State còn trống (Story Director điền, KIT không bịa)',
    mem.relationships.some((r) => r.trust || r.conflict || r.unresolved)
      ? 'Relationship State đi tiếp'
      : 'Relationship State còn trống (Story Director điền)',
  ];
  return {
    fromEpisode: last?.episode,
    toEpisode: to || undefined,
    whatHappened: last?.whatHappened,
    whatChanged: last?.whatChanged,
    characterChanges: last?.characterChanges,
    relationshipChanges: last?.relationshipChanges,
    openConflicts: last?.unresolvedConflicts,
    openThreads: open,
    consequences: last?.consequences,
    inheritLines,
  };
}

export function storyContinuityWarnings(state: Pick<SeriesPilotState, 'episode' | 'storyMemory'>) {
  const warnings: string[] = [];
  const mem = state.storyMemory;
  if (!mem) return warnings;
  const open = openThreads(mem);
  const current = episodeCodeOf(state.episode?.episode || state.episode?.title);
  if (needsInheritanceReview(state)) {
    warnings.push(
      `Duyệt kế thừa từ ${mem.inheritFromEpisode || lastApprovedEpisode(mem)?.episode || 'tập trước'} trước khi sản xuất.`,
    );
  }
  if (open.length) {
    warnings.push(`${open.length} thread còn OPEN — KIT không tự xin lỗi / tha thứ / đóng conflict.`);
  }
  const draft = mem.episodeNarrative;
  if (draft?.approved && open.length && !(draft.unresolvedConflicts || '').trim()) {
    warnings.push('Tập đã khóa trạng thái nhưng còn thread OPEN mà chưa ghi conflict chưa giải.');
  }
  if (current && draft && draft.episode && draft.episode !== current) {
    warnings.push(`Narrative đang ở ${draft.episode} trong khi pack là ${current}.`);
  }
  return warnings;
}

export function patchStoryMemory(state: SeriesPilotState, patch: Partial<FamixaStoryMemory>): SeriesPilotState {
  const base = ensureStoryMemory(state.storyMemory, state.characters ?? [], state.episode);
  return { ...state, storyMemory: { ...base, ...patch } };
}
