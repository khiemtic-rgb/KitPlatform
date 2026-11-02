/** FAMIXA_SHARED_PERFORMANCE_CONTRACT_V1 Phase A. WHAT only. No providers. No UI cutover. */

import { createHash } from 'node:crypto';
import { actingLabel, inferActingDirection, type ActingEmotion, type LinePerformance } from './content-famixa-acting-law';
import type { ShotActingBeat } from './famixa-shot-production-timing';

export const PERFORMANCE_CONTRACT_DOCUMENT_ID = 'FAMIXA_SHARED_PERFORMANCE_CONTRACT_V1';
export const PERFORMANCE_CANON_ID = 'PERFORMANCE_CANON_V1';
export const PERFORMANCE_HASH_PREFIX = 'PERFORMANCE_HASH_V1:';
export const LINE_TEXT_PREFIX = 'LINE_TEXT_V1:';
export const PERFORMANCE_DRAFT_PREFIX = 'PERFORMANCE_DRAFT_V1:';
export const PERFORMANCE_TIMING_DOCUMENT_ID = 'FAMIXA_SHOT_TIMING_CONTRACT_V3';
export const PERFORMANCE_SCHEMA_VERSION = 1;

export type Participation = 'IN_FRAME' | 'OFF_FRAME' | 'VOICE_ONLY';
export type VisualRole = 'PRIMARY' | 'SECONDARY' | 'NONE';
export type SpeechRole = 'SPEAKER' | 'LISTENER' | 'NONE';

export type ShotPerformanceSlice = {
  before?: { action?: string; body?: string; prop?: string; gaze?: string; room?: string };
  during?: { emotion?: ActingEmotion; intensity?: 1 | 2 | 3 | 4 | 5 };
  after?: { action?: string; gaze?: string };
  gazeTarget?: string;
};

export type CharacterInShot = {
  characterId: string;
  participation: Participation;
  visualRole: VisualRole;
  speechRole: SpeechRole;
  emotionalState?: ActingEmotion;
  intensity?: 1 | 2 | 3 | 4 | 5;
  lineIds: string[];
};

export type PerformanceLine = {
  lineId: string;
  characterId: string;
  textIdentity: { text: string; textHash: string };
  delivery: {
    emotion: ActingEmotion;
    intensity: 1 | 2 | 3 | 4 | 5;
    pace: 'slow' | 'natural' | 'fast';
    volume: 'low' | 'mid' | 'high';
    pauseSec: number;
    label: string;
    emphasis?: string;
    locked: boolean;
  };
};

export type PerformanceTimingRef = { timingDocumentId: string; timingHash?: string };
export type PerformanceVisualRef = {
  visualAuthorityId?: string;
  visualContractHash?: string;
  characterIdentityIds: string[];
};
export type PerformanceStoryRef = {
  storyTextHash?: string;
  beatId?: string;
  dialogueSegmentIds: string[];
};

export type PerformanceDraft = {
  documentId: string;
  schemaVersion: number;
  buildId: string;
  shotCode: string;
  episodeCode: string;
  sceneCode: string;
  shotPerformance: ShotPerformanceSlice;
  characters: CharacterInShot[];
  lines: PerformanceLine[];
  timingRef: PerformanceTimingRef;
  visualRef: PerformanceVisualRef;
  storyRef: PerformanceStoryRef;
};

export type PerformanceSourceRef = {
  draftFingerprint?: string;
  actingBeatPresent: boolean;
  linePerformanceLocked: boolean[];
  derivedFromInfer: boolean;
};

export type PerformanceSnapshot = PerformanceDraft & {
  performanceSnapshotId: string;
  performanceHash: string;
  frozenAt: string;
  createdBy: string;
  source: PerformanceSourceRef;
  payloadJson: string;
};

export type PerformanceIssue = { code: string; path: string; message: string };
export type PerformanceValidation = { ok: boolean; voiceReady: boolean; issues: PerformanceIssue[] };

export type PerformanceLineSource = {
  lineId: string;
  characterId: string;
  text: string;
  performance?: LinePerformance;
};

export type PerformanceProjectionSource = {
  buildId: string;
  shotCode: string;
  episodeCode: string;
  sceneCode: string;
  characterIds: string[];
  dialogueSegmentIds: string[];
  lines: PerformanceLineSource[];
  actingBeat?: ShotActingBeat;
  gazeTarget?: string;
  offFrameCharacterIds?: string[];
  voiceOnlyCharacterIds?: string[];
  primaryCharacterIds?: string[];
  secondaryCharacterIds?: string[];
  characterEmotionalState?: Record<string, ActingEmotion>;
  timingHash?: string;
  visualAuthorityId?: string;
  visualContractHash?: string;
  storyTextHash?: string;
  beatId?: string;
};

const FORBIDDEN_KEYS = new Set([
  'voiceid', 'voice_id', 'provider', 'providerid', 'provider_id', 'model', 'modelid', 'model_id',
  'runway', 'veo', 'gemini', 'elevenlabs', 'fal', 'wan',
  'providerdurationsec', 'provider_duration_sec', 'providerduration',
  'previewurl', 'acceptedtake', 'motionurl',
]);
const VOICE_KEYS = new Set(['voiceid', 'voice_id']);
const DURATION_KEYS = new Set(['providerdurationsec', 'provider_duration_sec', 'providerduration']);

export function sha256HexUtf8(text: string) {
  return createHash('sha256').update(text, 'utf8').digest('hex');
}

export function lineTextHashOf(text: string) {
  return LINE_TEXT_PREFIX + sha256HexUtf8(text ?? '');
}

export function spokenTextOf(raw?: string) {
  let s = String(raw ?? '').replace(/\[[^\]]*\]\s*/g, '');
  s = s.replace(/\s+/g, ' ').trim();
  s = s.replace(/\s*[—–]\s+(?:KHOE|CHẠY|NHÌN|NGẬP|VỠ|BEAT|INSERT|CUT|\().+$/i, '').trim();
  s = s.replace(/\s*\(\d+\s*[–\-]\s*\d+\s*s?\)\s*$/i, '').trim();
  return s;
}

export function canonicalPerformanceJson(draft: PerformanceDraft) {
  return writeObject([
    ['documentId', draft.documentId],
    ['schemaVersion', draft.schemaVersion],
    ['buildId', (draft.buildId || '').toLowerCase()],
    ['shotCode', draft.shotCode],
    ['episodeCode', draft.episodeCode],
    ['sceneCode', draft.sceneCode],
    ['shotPerformance', shotCanon(draft.shotPerformance)],
    ['characters', draft.characters.map(characterCanon)],
    ['lines', draft.lines.map(lineCanon)],
    ['timingRef', timingCanon(draft.timingRef)],
    ['visualRef', visualCanon(draft.visualRef)],
    ['storyRef', storyCanon(draft.storyRef)],
  ]);
}

export function performanceHashOf(draft: PerformanceDraft) {
  return PERFORMANCE_HASH_PREFIX + sha256HexUtf8(canonicalPerformanceJson(draft));
}

export function draftFingerprintOf(draft: PerformanceDraft) {
  return PERFORMANCE_DRAFT_PREFIX + sha256HexUtf8(canonicalPerformanceJson(draft));
}

export function isValidPerformanceHash(hash?: string) {
  const h = (hash || '').trim();
  if (!h.startsWith(PERFORMANCE_HASH_PREFIX)) return false;
  const hex = h.slice(PERFORMANCE_HASH_PREFIX.length);
  return hex.length === 64 && /^[0-9a-f]+$/.test(hex);
}

export function rejectLegacyHash(hash?: string): PerformanceIssue[] {
  if (isValidPerformanceHash(hash)) return [];
  return [{ code: 'INVALID_PERFORMANCE_HASH', path: 'performanceHash', message: 'Legacy hash cannot be PerformanceHash.' }];
}

export function projectPerformanceDraft(src: PerformanceProjectionSource): PerformanceDraft {
  if (!src.buildId || !src.shotCode) throw new Error('MISSING_SCOPE');
  const dialogueIds = (src.dialogueSegmentIds ?? []).map((x) => x.trim()).filter(Boolean);
  const byId = new Map(src.lines.map((l) => [l.lineId, l]));
  if (dialogueIds.some((id) => !byId.has(id))) throw new Error('UNRESOLVED_PROJECTION');
  const ordered = dialogueIds.map((id) => byId.get(id)!);
  const off = new Set(src.offFrameCharacterIds ?? []);
  const voiceOnly = new Set(src.voiceOnlyCharacterIds ?? []);
  const characterIds = [...(src.characterIds ?? [])];
  for (const line of ordered) {
    const id = (line.characterId || '').trim();
    if (!id) continue;
    if (!characterIds.includes(id) && (voiceOnly.has(id) || id === 'CHAR-VO')) {
      characterIds.push(id);
      voiceOnly.add(id);
    }
  }
  const shot = mapBeat(src.actingBeat, src.gazeTarget);
  const characters = characterIds.map((id) => mapCharacter(id, ordered, src, off, voiceOnly));
  const lines = ordered.map((line) => mapLine(line, characters, shot));
  return {
    documentId: PERFORMANCE_CONTRACT_DOCUMENT_ID,
    schemaVersion: PERFORMANCE_SCHEMA_VERSION,
    buildId: src.buildId.toLowerCase(),
    shotCode: src.shotCode.trim(),
    episodeCode: (src.episodeCode || '').trim(),
    sceneCode: (src.sceneCode || '').trim(),
    shotPerformance: shot,
    characters,
    lines,
    timingRef: { timingDocumentId: PERFORMANCE_TIMING_DOCUMENT_ID, timingHash: emptyToUndef(src.timingHash) },
    visualRef: {
      visualAuthorityId: emptyToUndef(src.visualAuthorityId),
      visualContractHash: emptyToUndef(src.visualContractHash),
      characterIdentityIds: characters.map((c) => c.characterId),
    },
    storyRef: {
      storyTextHash: emptyToUndef(src.storyTextHash),
      beatId: emptyToUndef(src.beatId),
      dialogueSegmentIds: dialogueIds,
    },
  };
}

export function sourceRefOf(src: PerformanceProjectionSource, draft: PerformanceDraft): PerformanceSourceRef {
  return {
    draftFingerprint: draftFingerprintOf(draft),
    actingBeatPresent: Boolean(src.actingBeat),
    linePerformanceLocked: (src.dialogueSegmentIds ?? []).map((id) => Boolean(src.lines.find((l) => l.lineId === id)?.performance?.locked)),
    derivedFromInfer: draft.lines.some((l) => !l.delivery.locked),
  };
}

export function validatePerformanceDraft(draft: PerformanceDraft, extra?: unknown): PerformanceValidation {
  const issues: PerformanceIssue[] = [];
  if (draft.documentId !== PERFORMANCE_CONTRACT_DOCUMENT_ID) issues.push(issue('DOCUMENT', 'documentId', 'Wrong documentId.'));
  if (draft.schemaVersion !== PERFORMANCE_SCHEMA_VERSION) issues.push(issue('SCHEMA', 'schemaVersion', 'schemaVersion must be 1.'));
  if (!draft.buildId || !draft.shotCode) issues.push(issue('MISSING_SCOPE', 'buildId', 'buildId and shotCode required.'));
  if (draft.characters.filter((c) => c.visualRole === 'PRIMARY').length > 1) {
    issues.push(issue('DUPLICATE_PRIMARY', 'characters', 'Duplicate PRIMARY is not allowed.'));
  }
  const byId = new Map(draft.characters.map((c) => [c.characterId, c]));
  for (const line of draft.lines) {
    const owner = byId.get(line.characterId);
    if (!owner) issues.push(issue('INVALID_CHARACTER', `lines.${line.lineId}`, 'line.characterId must exist in characters[].'));
    else if (!owner.lineIds.includes(line.lineId)) {
      issues.push(issue('LINE_CHARACTER_MISMATCH', `characters.${owner.characterId}.lineIds`, 'lineIds must correspond to the character.'));
    }
    if (!line.textIdentity.text) issues.push(issue('EMPTY_SPOKEN_TEXT', `lines.${line.lineId}`, 'Spoken text is empty.'));
    if (line.textIdentity.textHash !== lineTextHashOf(line.textIdentity.text)) {
      issues.push(issue('UNRESOLVED_PROJECTION', `lines.${line.lineId}.textHash`, 'textHash does not match text.'));
    }
  }
  issues.push(...scanForbidden(extra));
  const spoken = draft.lines.length > 0;
  const unlocked = spoken && draft.lines.some((l) => !l.delivery.locked);
  if (unlocked) issues.push(issue('SPOKEN_LINE_UNLOCKED', 'lines', 'Spoken snapshot requires every line locked.'));
  const blocking = issues.filter((i) => i.code !== 'SPOKEN_LINE_UNLOCKED');
  return { ok: blocking.length === 0, voiceReady: spoken && !unlocked && blocking.length === 0, issues };
}

export function freezePerformanceSnapshot(draft: PerformanceDraft, createdBy: string, source?: PerformanceSourceRef): PerformanceSnapshot {
  const verdict = validatePerformanceDraft(draft);
  if (!verdict.ok || (draft.lines.length > 0 && !verdict.voiceReady)) {
    throw new Error(verdict.issues[0]?.code || 'SPOKEN_LINE_UNLOCKED');
  }
  const payloadJson = canonicalPerformanceJson(draft);
  const frozenDraft = JSON.parse(payloadJson) as PerformanceDraft;
  return {
    ...frozenDraft,
    performanceSnapshotId: cryptoRandomId(),
    performanceHash: performanceHashOf(draft),
    frozenAt: new Date().toISOString(),
    createdBy: (createdBy || '').trim(),
    source: source ?? { actingBeatPresent: false, linePerformanceLocked: draft.lines.map((l) => l.delivery.locked), derivedFromInfer: draft.lines.some((l) => !l.delivery.locked) },
    payloadJson,
  };
}

export function bindPerformanceSnapshot(snapshot: PerformanceSnapshot, buildId: string, shotCode: string) {
  if (snapshot.buildId.toLowerCase() !== buildId.toLowerCase()) throw new Error('CROSS_SCOPE_FORBIDDEN');
  if (snapshot.shotCode !== shotCode) throw new Error('CROSS_SHOT_FORBIDDEN');
}

function mapBeat(beat?: ShotActingBeat, gazeTarget?: string): ShotPerformanceSlice {
  return {
    before: omitEmpty({
      action: emptyToUndef(beat?.before?.action),
      body: emptyToUndef(beat?.before?.body),
      prop: emptyToUndef(beat?.before?.prop),
      gaze: emptyToUndef(beat?.before?.gaze),
      room: emptyToUndef(beat?.before?.room),
    }),
    during: omitEmpty({
      emotion: emptyToUndef(beat?.during?.emotion) as ActingEmotion | undefined,
      intensity: beat?.during?.intensity as 1 | 2 | 3 | 4 | 5 | undefined,
    }),
    after: omitEmpty({
      action: emptyToUndef(beat?.after?.action),
      gaze: emptyToUndef(beat?.after?.gaze),
    }),
    gazeTarget: emptyToUndef(gazeTarget),
  };
}

function mapCharacter(
  id: string,
  lines: PerformanceLineSource[],
  src: PerformanceProjectionSource,
  off: Set<string>,
  voiceOnly: Set<string>,
): CharacterInShot {
  const lineIds = lines.filter((l) => l.characterId === id).map((l) => l.lineId);
  const participation: Participation = voiceOnly.has(id) || id === 'CHAR-VO'
    ? 'VOICE_ONLY'
    : off.has(id)
      ? 'OFF_FRAME'
      : 'IN_FRAME';
  const visualRole = visualRoleOf(id, src);
  const speechRole: SpeechRole = lineIds.length
    ? 'SPEAKER'
    : participation === 'IN_FRAME'
      ? 'LISTENER'
      : 'NONE';
  return {
    characterId: id,
    participation,
    visualRole,
    speechRole,
    emotionalState: src.characterEmotionalState?.[id],
    lineIds,
  };
}

function visualRoleOf(id: string, src: PerformanceProjectionSource): VisualRole {
  if (src.primaryCharacterIds?.includes(id)) return 'PRIMARY';
  if (src.secondaryCharacterIds?.includes(id)) return 'SECONDARY';
  const inFrame = (src.characterIds ?? []).filter(
    (c) => !src.voiceOnlyCharacterIds?.includes(c) && !src.offFrameCharacterIds?.includes(c),
  );
  if (inFrame.length === 1 && inFrame[0] === id) return 'PRIMARY';
  return 'NONE';
}

function mapLine(line: PerformanceLineSource, characters: CharacterInShot[], shot: ShotPerformanceSlice): PerformanceLine {
  const text = spokenTextOf(line.text);
  const owner = characters.find((c) => c.characterId === line.characterId);
  const locked = Boolean(line.performance?.locked);
  let emotion = emptyToUndef(line.performance?.emotion) as ActingEmotion | undefined;
  let intensity = line.performance?.intensity;
  if (!emotion) emotion = owner?.emotionalState ?? (shot.during?.emotion as ActingEmotion | undefined);
  if (intensity == null) intensity = owner?.intensity ?? shot.during?.intensity;
  if (!emotion) {
    const inferred = inferActingDirection({ text, characterId: line.characterId });
    emotion = inferred.emotion;
    intensity = intensity ?? inferred.intensity;
  }
  if (!emotion) throw new Error('UNRESOLVED_PROJECTION');
  const pace = line.performance?.pace ?? 'natural';
  const volume = line.performance?.volume ?? 'mid';
  const pauseSec = line.performance?.pauseSec ?? 0.15;
  const label = line.performance?.label || actingLabel(emotion, intensity ?? 2);
  return {
    lineId: line.lineId,
    characterId: line.characterId,
    textIdentity: { text, textHash: lineTextHashOf(text) },
    delivery: {
      emotion,
      intensity: (intensity ?? 2) as 1 | 2 | 3 | 4 | 5,
      pace,
      volume,
      pauseSec,
      label,
      emphasis: emptyToUndef(line.performance?.emphasis),
      locked,
    },
  };
}

function scanForbidden(extra: unknown, path = '$'): PerformanceIssue[] {
  const issues: PerformanceIssue[] = [];
  if (extra && typeof extra === 'object' && !Array.isArray(extra)) {
    for (const [k, v] of Object.entries(extra as Record<string, unknown>)) {
      const child = `${path}.${k}`;
      const key = k.toLowerCase();
      if (VOICE_KEYS.has(key)) issues.push(issue('VOICE_ID_FORBIDDEN', child, 'VoiceId is HOW.'));
      else if (DURATION_KEYS.has(key)) issues.push(issue('PROVIDER_DURATION_FORBIDDEN', child, 'Provider duration is HOW.'));
      else if (FORBIDDEN_KEYS.has(key)) issues.push(issue('PROVIDER_FIELD_FORBIDDEN', child, 'Provider field is not allowed.'));
      issues.push(...scanForbidden(v, child));
    }
  } else if (Array.isArray(extra)) {
    extra.forEach((v, i) => issues.push(...scanForbidden(v, `${path}[${i}]`)));
  } else if (typeof extra === 'string' && /https?:\/\/|blob:/i.test(extra)) {
    issues.push(issue('PROVIDER_FIELD_FORBIDDEN', path, 'URL is not Performance WHAT.'));
  }
  return issues;
}

function shotCanon(shot: ShotPerformanceSlice) {
  return omitEmpty({
    before: omitEmpty({
      action: shot.before?.action,
      body: shot.before?.body,
      prop: shot.before?.prop,
      gaze: shot.before?.gaze,
      room: shot.before?.room,
    }),
    during: omitEmpty({
      emotion: shot.during?.emotion,
      intensity: shot.during?.intensity,
    }),
    after: omitEmpty({
      action: shot.after?.action,
      gaze: shot.after?.gaze,
    }),
    gazeTarget: shot.gazeTarget,
  });
}

function characterCanon(c: CharacterInShot) {
  return {
    characterId: c.characterId,
    participation: c.participation,
    visualRole: c.visualRole,
    speechRole: c.speechRole,
    emotionalState: c.emotionalState,
    intensity: c.intensity,
    lineIds: c.lineIds,
  };
}

function lineCanon(line: PerformanceLine) {
  return {
    lineId: line.lineId,
    characterId: line.characterId,
    textIdentity: { text: line.textIdentity.text, textHash: line.textIdentity.textHash },
    delivery: {
      emotion: line.delivery.emotion,
      intensity: line.delivery.intensity,
      pace: line.delivery.pace,
      volume: line.delivery.volume,
      pauseSec: line.delivery.pauseSec,
      label: line.delivery.label,
      emphasis: line.delivery.emphasis,
      locked: line.delivery.locked,
    },
  };
}

function timingCanon(ref: PerformanceTimingRef) {
  return { timingDocumentId: ref.timingDocumentId, timingHash: ref.timingHash };
}

function visualCanon(ref: PerformanceVisualRef) {
  return {
    visualAuthorityId: ref.visualAuthorityId,
    visualContractHash: ref.visualContractHash,
    characterIdentityIds: ref.characterIdentityIds,
  };
}

function storyCanon(ref: PerformanceStoryRef) {
  return { storyTextHash: ref.storyTextHash, beatId: ref.beatId, dialogueSegmentIds: ref.dialogueSegmentIds };
}

function writeObject(fields: [string, unknown][]): string {
  const parts: string[] = [];
  for (const [key, value] of fields) {
    const encoded = writeValue(value);
    if (encoded === undefined) continue;
    parts.push(`${jsonString(key)}:${encoded}`);
  }
  return `{${parts.join(',')}}`;
}

function writeValue(value: unknown): string | undefined {
  if (value == null) return undefined;
  if (typeof value === 'string') return value.trim() ? jsonString(value) : undefined;
  if (typeof value === 'number') {
    if (!Number.isFinite(value)) return undefined;
    if (Math.abs(value - Math.round(value)) < 1e-12) return String(Math.round(value));
    return String(Number(value.toFixed(10)).toString());
  }
  if (typeof value === 'boolean') return value ? 'true' : 'false';
  if (Array.isArray(value)) return `[${value.map((v) => writeValue(v) ?? jsonString('')).join(',')}]`;
  if (typeof value === 'object') {
    const inner = writeObject(Object.entries(value as Record<string, unknown>));
    return inner === '{}' ? undefined : inner;
  }
  return undefined;
}

function jsonString(value: string) {
  return JSON.stringify(value);
}

function omitEmpty<T extends Record<string, unknown>>(obj: T): T | undefined {
  const next = { ...obj };
  for (const key of Object.keys(next)) {
    const v = next[key];
    if (v == null || v === '') delete next[key];
  }
  return Object.keys(next).length ? next : undefined;
}

function emptyToUndef(value?: string) {
  const s = (value || '').trim();
  return s || undefined;
}

function issue(code: string, path: string, message: string): PerformanceIssue {
  return { code, path, message };
}

function cryptoRandomId() {
  if (typeof crypto !== 'undefined' && 'randomUUID' in crypto) return crypto.randomUUID();
  return '00000000-0000-4000-8000-000000000000'.replace(/0/g, () => Math.floor(Math.random() * 16).toString(16));
}
