/** Famixa Series pilot — nhận khối dán. KIT không điền sẵn chuyện. */

import { parseEpisodeStory } from './content-famixa-story-parse';
import { ensureStoryMemory, inheritStoryMemory, needsInheritanceReview, type FamixaStoryMemory } from './content-famixa-story-memory';
import { deriveVoiceScript, mergeVoiceGenerated, voiceProductionReady, type FamixaVoicePreview } from './content-famixa-voice-script';
import { canonPixelsOf, loadCanonPixels, rememberCanonFromChars, rememberCanonPixels, saveCanonPixels } from './content-famixa-canon-store';
import { famixaCanonSeedFor, fetchFamixaCanonSeedDataUrl } from './content-famixa-canon-seed';

export type SeriesShotStatus =
  | 'story_locked'
  | 'keyframe_ready'
  | 'turbo_testing'
  | 'reviewed'
  | 'approved'
  | 'rejected';

export type SeriesReviewAxis = 'character' | 'motion' | 'emotion' | 'canon';

export const SERIES_STATUS_LABEL: Record<SeriesShotStatus, { text: string; color: string }> = {
  story_locked: { text: 'Story khóa', color: 'default' },
  keyframe_ready: { text: 'Có keyframe', color: 'blue' },
  turbo_testing: { text: 'Đang Turbo', color: 'processing' },
  reviewed: { text: 'Đã review', color: 'gold' },
  approved: { text: 'Shot lock', color: 'green' },
  rejected: { text: 'Làm lại Turbo', color: 'red' },
};

export type FamixaSeriesShot = {
  id: string;
  scene: string;
  shot: string;
  clock: string;
  seconds: number;
  story: string;
  visual: string;
  characters: string[];
  location: string;
  motionPrompt: string;
  motionPromptVi: string;
  negativePrompt?: string;
  status: SeriesShotStatus;
  sceneId?: string;
  characterIds?: string[];
  previousShotId?: string;
  inheritFromShotId?: string;
};

export type FamixaSeriesEpisode = {
  seriesCode: string;
  seriesTitle: string;
  episode: string;
  title: string;
  premise: string;
  moral: string;
  ctaRule: string;
  shots: FamixaSeriesShot[];
};

export type FamixaShortClip = {
  id: string;
  hook: string;
  visual: string;
  seconds: number;
  scene?: string;
  characters?: string[];
  motionPrompt: string;
  motionPromptVi: string;
  sceneId?: string;
  characterIds?: string[];
};

export type FamixaCharStill = {
  id: string;
  charCode: string;
  scene: string;
  shortId: string;
  note?: string;
  imageDataUrl?: string;
  fileName?: string;
  localPath?: string;
};

/** SoT Character — cùng id xuyên kịch bản / still / shot. */
export type FamixaCharacter = {
  id: string;
  name: string;
  role?: string;
  wardrobe?: string;
  seat?: string;
  voiceNote?: string;
  /** ElevenLabs voice id — kế thừa xuống line / shot. */
  voiceId?: string;
  voiceName?: string;
  voiceStability?: number;
  voiceSimilarity?: number;
  voiceStyle?: number;
  voiceSpeed?: number;
  line?: string;
  performance?: string;
  canonFileName?: string;
  canonLocalPath?: string;
  /** Session only — slimPilot strips this. */
  canonImageDataUrl?: string;
};

export type FamixaSceneDialogue = {
  id: string;
  characterId: string;
  text: string;
  emotion?: string;
};

/** SoT Scene — Story content thuộc Scene; Memory/Canon kế thừa theo graph. */
export type FamixaSceneNode = {
  id: string;
  title?: string;
  environment?: string;
  camera?: string;
  position?: string;
  performance?: string;
  characterIds: string[];
  sourceShotId?: string;
  /** Performance từng CHAR — Shot Action chỉ là delta. */
  performances?: Record<string, string>;
  /** Full scene text from the user script — not previous scene. */
  content?: string;
  actions?: string[];
  dialogue?: FamixaSceneDialogue[];
};

export type FamixaLine = {
  id: string;
  characterId: string;
  text: string;
  voiceId?: string;
  sceneId?: string;
};

export function localFileRef(file: File): { fileName: string; localPath: string } {
  const fileName = file.name.trim();
  const extra = file as File & { path?: string };
  const fromOs = typeof extra.path === 'string' ? extra.path.trim() : '';
  const relative = file.webkitRelativePath?.trim() ?? '';
  return { fileName, localPath: fromOs || relative || fileName };
}

export type SceneContinuityLock = {
  id: string;
  episode: string;
  scene: string;
  characters: string;
  wardrobe: string;
  position: string;
  environment: string;
  camera: string;
  performance: string;
  locked: boolean;
  /** Shot LOCK đã khóa Memory cảnh — không phải seed cứng. */
  sourceShotId?: string;
};

export const CONTINUITY_GATES = [
  { id: 'character', label: 'Character giống shot trước / Canon' },
  { id: 'wardrobe', label: 'Wardrobe giống Scene Lock' },
  { id: 'environment', label: 'Environment giống Scene Lock' },
  { id: 'lighting', label: 'Lighting giống Scene Lock' },
  { id: 'position', label: 'Position giống Scene Lock' },
  { id: 'props', label: 'Props giống Scene Lock' },
  { id: 'action', label: 'Action đúng Story Source' },
  { id: 'kfAction', label: 'KF không chứa hành động ngoài Action' },
  { id: 'motion', label: 'Motion không yêu cầu hành động ngoài Action' },
  { id: 'conflict', label: 'Không conflict giữa KF và Motion' },
] as const;

export type ContinuityGateId = (typeof CONTINUITY_GATES)[number]['id'];

export const SC02_CONTINUITY_SEED: SceneContinuityLock = {
  id: 'FAMIXA_E01_SC02_CONTINUITY_LOCK_V01',
  episode: 'EP01 — Bố mẹ không cãi nhau đâu',
  scene: 'SC02 — Gia đình bắt đầu trò chuyện',
  characters: 'CHAR-001 Minh · CHAR-002 Nam · CHAR-003 Linh — Canon đã khóa.',
  wardrobe: 'Nam: sơ mi xanh nhạt. Minh: polo trắng cổ xanh. Linh: áo be tay dài.',
  position: 'Nam trái · Minh giữa · Linh phải.',
  environment: 'Bữa cơm tối. Cùng phòng, bàn, món, cửa sổ, nội thất, ánh sáng đêm. Tone ấm.',
  camera: 'Eye-level, 35mm, chuyển động rất nhẹ. Không shake, pan, rotation, cut.',
  performance: 'Đời thường, tiết chế. Không gesture lớn. Không nhìn camera. Không thêm hành động ngoài Shot Action.',
  locked: false,
};

export function emptyEpisode(): FamixaSeriesEpisode {
  return {
    seriesCode: 'FAMIXA',
    seriesTitle: 'Famixa',
    episode: 'EP01',
    title: '',
    premise: '',
    moral: '',
    ctaRule: '',
    shots: [],
  };
}

export function newSceneShot(scene: string, index: number, story = ''): FamixaSeriesShot {
  const shot = `SH${String(index).padStart(2, '0')}`;
  return {
    id: `${scene}-${shot}`,
    scene,
    shot,
    clock: '5s',
    seconds: 5,
    story,
    visual: '',
    characters: ['CHAR-001', 'CHAR-002', 'CHAR-003'],
    characterIds: ['CHAR-001', 'CHAR-002', 'CHAR-003'],
    sceneId: sceneCodeOf(scene),
    location: '',
    motionPrompt: '',
    motionPromptVi: '',
    status: 'story_locked',
  };
}

function codeMatch(raw: string | undefined, kind: 'EP' | 'SC' | 'SH') {
  return raw?.match(new RegExp(`${kind}\\s*\\d+`, 'i'))?.[0]?.replace(/\s+/g, '').toUpperCase();
}

export function studioShotCode(shot?: FamixaSeriesShot) {
  if (!shot) return 'SH';
  return codeMatch(shot.shot, 'SH') || codeMatch(shot.id, 'SH') || 'SH';
}

export function studioSceneCode(shot?: FamixaSeriesShot, episode?: FamixaSeriesEpisode) {
  const sc = codeMatch(shot?.scene, 'SC') || codeMatch(shot?.id, 'SC') || 'SC';
  const ep = codeMatch(episode?.episode, 'EP') || codeMatch(episode?.title, 'EP') || 'EP01';
  return `${ep} · ${sc}`;
}

export function studioSceneTitle(lock?: SceneContinuityLock, episode?: FamixaSeriesEpisode) {
  const raw = (lock?.scene || episode?.title || '').replace(/^SC\d+\s*[—–-]\s*/i, '').trim();
  if (!raw || raw.length > 42 || /tiếp nối|VIDEO ID|EP\d+-SC|Nam vừa/i.test(raw)) {
    return episode?.title || '';
  }
  return raw;
}

export function shotOneLiner(story?: string, action?: string) {
  const t = (action || story || '').replace(/\s+/g, ' ').trim();
  if (!t) return 'Chưa có mô tả shot';
  const line = t.split(/(?<=[.!?])\s|\n/)[0] ?? t;
  return line.length > 64 ? `${line.slice(0, 64)}…` : line;
}

/** Split existing story/action. Does not invent beats. */
export function shotActionBeats(story?: string, action?: string) {
  const t = (action || story || '').replace(/\s+/g, ' ').trim();
  if (!t) return [];
  const parts = t
    .split(/\s*[·•]\s+|(?<=[.!?…])\s+/)
    .map((s) => s.replace(/^[—–\-]\s*/, '').replace(/[.]+$/, '').trim())
    .filter((s) => s.length > 2 && s.length < 140);
  return [...new Set(parts)].slice(0, 8);
}

export type StudioShotTone = 'locked' | 'error' | 'on' | 'warn' | 'wait';

export function studioShotUi(run?: SeriesShotRun) {
  const r = run ?? { status: 'story_locked' as const };
  const hasTake = Boolean(r.previewUrl || r.localVideoPath);
  if (r.status === 'approved') {
    return { tone: 'locked' as StudioShotTone, label: 'Đã khóa', hint: 'KF đã duyệt · Video đã khóa' };
  }
  if (r.status === 'rejected' || r.turboError) {
    return { tone: 'error' as StudioShotTone, label: 'Lỗi', hint: (r.turboError || 'Cần làm lại').slice(0, 48) };
  }
  if (r.status === 'turbo_testing') {
    return { tone: 'on' as StudioShotTone, label: 'Đang tạo video', hint: 'Đợi take' };
  }
  if (r.status === 'reviewed' || hasTake) {
    return { tone: 'warn' as StudioShotTone, label: 'Cần kiểm tra', hint: 'Có take · chưa khóa' };
  }
  if (r.status === 'keyframe_ready' && r.keyframeDataUrl) {
    const approved = Boolean(r.continuity && Object.values(r.continuity).some(Boolean));
    if (approved) return { tone: 'on' as StudioShotTone, label: 'KF đã duyệt', hint: 'Video chưa tạo' };
    return { tone: 'on' as StudioShotTone, label: 'Đang dựng', hint: 'Có ảnh · chưa duyệt KF' };
  }
  if (r.keyframeDataUrl) {
    return { tone: 'on' as StudioShotTone, label: 'Đang dựng', hint: 'Có ảnh · chưa duyệt KF' };
  }
  return { tone: 'wait' as StudioShotTone, label: 'Chưa dựng', hint: '' };
}

export function lockFromApprovedShot(
  shot: FamixaSeriesShot,
  episode?: FamixaSeriesEpisode,
  current?: SceneContinuityLock,
): SceneContinuityLock {
  const sc = codeMatch(shot.scene, 'SC') || codeMatch(shot.id, 'SC') || 'SC02';
  const base = current ?? SC02_CONTINUITY_SEED;
  const scene =
    (base.scene ?? '').trim() && !/^SC\d+\s*$/i.test(base.scene)
      ? base.scene
      : `${sc} — ${studioSceneTitle(base, episode)}`;
  return {
    ...base,
    episode: (episode?.episode || episode?.title || base.episode).trim() || base.episode,
    scene,
    sourceShotId: base.sourceShotId || shot.id,
    locked: true,
  };
}

export function continuityReady(run: { continuity?: Partial<Record<ContinuityGateId, boolean>> }) {
  const c = run.continuity ?? {};
  return CONTINUITY_GATES.every((g) => Boolean(c[g.id]));
}

function clipMem(text: string | undefined, max: number) {
  const t = (text ?? '').replace(/\s+/g, ' ').trim();
  if (!t) return '';
  return t.length > max ? `${t.slice(0, max)}…` : t;
}

export function memoryLine(text: string | undefined, max = 36) {
  return clipMem(text, max);
}

export function inheritedTurboPrompt(lock: SceneContinuityLock, action: string) {
  return compileLockPrompt(lock, action, 5);
}

function compileLockPrompt(lock: SceneContinuityLock, action: string, seconds = 5) {
  const act = action.replace(/\s+/g, ' ').trim();
  const src = lock.sourceShotId || lock.id;
  const wardrobe = clipMem(lock.wardrobe, 140);
  const pos = clipMem(lock.position, 80);
  const env = clipMem(lock.environment, 140);
  const cam = clipMem(lock.camera, 80);
  return (
    `Same scene as ${src}. ` +
    (wardrobe ? `Wardrobe locked: ${wardrobe}. ` : 'Same wardrobe. ') +
    (pos ? `Seats: ${pos}. ` : '') +
    (env ? `${env} ` : 'Same room, table, food, night light. ') +
    (cam ? `Camera: ${cam} ` : '') +
    `Only this action: ${act || 'subtle natural motion at the table.'} ` +
    `Do not change clothes, faces, age, props or location. No looking at the camera. ${seconds} seconds.`
  );
}

export type SeriesRoleRow = {
  id: string;
  title: string;
  name: string;
  line?: string;
  voiceNote?: string;
  voiceId?: string;
  voiceName?: string;
  characterId?: string;
  performance?: string;
};

export type SeriesShotRun = {
  status: SeriesShotStatus;
  credits?: number;
  /** Runway already billed this take; KIT ledger uses `credits` only after lock. */
  runwaySpent?: number;
  model?: string;
  notes?: string;
  review?: Partial<Record<SeriesReviewAxis, boolean>>;
  previewUrl?: string;
  /** Previous take URLs — latest generate overwrites previewUrl. */
  takeHistory?: { url: string; taskId?: string }[];
  localVideoPath?: string;
  turboTaskId?: string;
  turboStatus?: string;
  turboError?: string;
  /** Scene start frame (KF01) — not a CHAR face crop. */
  keyframeDataUrl?: string;
  keyframeFileName?: string;
  keyframePath?: string;
  /** Shot LOCK mà KF này copy từ đó (cùng khung cảnh). */
  keyframeInheritedFrom?: string;
  /** Operator ép KF mới — bỏ plan REUSE. */
  kfForceNew?: boolean;
  /** What we send to Runway — not the Story pack. */
  runwayMotion?: string;
  runwayNegative?: string;
  /** Only the delta for this shot — inherits scene continuity. */
  shotAction?: string;
  continuity?: Partial<Record<ContinuityGateId, boolean>>;
};

export type SeriesPilotState = {
  roles: SeriesRoleRow[];
  runs: Record<string, SeriesShotRun>;
  episode?: FamixaSeriesEpisode;
  shorts?: FamixaShortClip[];
  stills?: FamixaCharStill[];
  sceneLocked?: boolean;
  sceneNotes?: string;
  packDraft?: string;
  continuity?: SceneContinuityLock;
  /** Duyệt kịch bản xong mới chia short / dựng cảnh. */
  scriptLocked?: boolean;
  /** Ảnh Canon + Voice Canon đã khóa — shot sau không chọn lại. */
  castLocked?: boolean;
  schemaVersion?: number;
  characters?: FamixaCharacter[];
  scenes?: FamixaSceneNode[];
  lines?: FamixaLine[];
  parseWarnings?: string[];
  parseVersion?: number;
  storyVersion?: number;
  storyReviewed?: boolean;
  /** Long-form Series/Season/Episode/CHAR/relationship memory — not visual continuity. */
  storyMemory?: FamixaStoryMemory;
  /** Full Voice duyệt xong mới I2V / short. */
  voiceLocked?: boolean;
  voicePreview?: FamixaVoicePreview;
};

function sceneCodeOf(scene?: string) {
  return scene?.match(/SC\s*\d+/i)?.[0]?.replace(/\s+/g, '').toUpperCase() ?? '';
}

export function appendSceneShot(state: SeriesPilotState, scene?: string) {
  const ep = state.episode ?? emptyEpisode();
  const sc = sceneCodeOf(scene || ep.shots.at(-1)?.scene) || 'SC01';
  const n = ep.shots.filter((s) => sceneCodeOf(s.scene) === sc).length + 1;
  const last = ep.shots.filter((s) => sceneCodeOf(s.scene) === sc).at(-1);
  const shot = newSceneShot(sc, n);
  if (last) {
    const ids = shotCharacterIds(last);
    shot.characterIds = ids;
    shot.characters = ids;
    shot.sceneId = last.sceneId || sc;
    shot.previousShotId = last.id;
  }
  return {
    state: ensurePilotGraph({ ...state, episode: { ...ep, shots: [...ep.shots, shot] } }),
    shot,
  };
}

/** Khung rỗng — không có chuyện mẫu. */
export const FAMIXA_PACK_SKELETON = `FAMIXA PACK
SERIES:
EP:
TITLE:
PREMISE:
MORAL:
CTA: no-app
ROLE:
ROLE:

--- SHORT ---
ID:
SCENE:
CHAR:
HOOK:
VISUAL:
SECONDS: 7
MOTION:
MOTION_VI:

--- SHORT ---
ID:
SCENE:
CHAR:
HOOK:
VISUAL:
SECONDS: 7
MOTION:
MOTION_VI:

--- REF ---
CHAR:
SCENE:
SHORT:
NOTE:

--- SHOT ---
ID:
SCENE:
SHOT:
STORY:
VISUAL:
SECONDS: 5
CHAR:
LOC:
MOTION:
MOTION_VI:
`;

const STORAGE_KEY = 'kit.famixaSeries.v4';
export const PILOT_SCHEMA = 9;
export const FAMIXA_SERIES_CODE = 'FAMIXA';

export function emptyPilot(): SeriesPilotState {
  return {
    roles: [],
    runs: {},
    shorts: [],
    stills: [],
    episode: undefined,
    schemaVersion: PILOT_SCHEMA,
    characters: [],
    scenes: [],
    lines: [],
    parseWarnings: [],
    parseVersion: 0,
    storyVersion: 0,
    storyReviewed: false,
    storyMemory: undefined,
    voiceLocked: false,
    voicePreview: undefined,
  };
}

export function loadSeriesPilot(): SeriesPilotState {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return emptyPilot();
    const v = JSON.parse(raw) as SeriesPilotState;
    const loaded: SeriesPilotState = {
      roles: Array.isArray(v.roles) ? v.roles : [],
      runs: v.runs ?? {},
      episode: v.episode,
      shorts: Array.isArray(v.shorts) ? v.shorts : [],
      stills: Array.isArray(v.stills) ? v.stills : [],
      sceneLocked: Boolean(v.sceneLocked),
      sceneNotes: v.sceneNotes,
      packDraft: typeof v.packDraft === 'string' ? v.packDraft : undefined,
      continuity: v.continuity && typeof v.continuity === 'object' ? v.continuity : undefined,
      scriptLocked: Boolean(v.scriptLocked),
      castLocked: Boolean(v.castLocked),
      schemaVersion: typeof v.schemaVersion === 'number' ? v.schemaVersion : 0,
      characters: Array.isArray(v.characters) ? v.characters : [],
      scenes: Array.isArray(v.scenes) ? v.scenes : [],
      lines: Array.isArray(v.lines) ? v.lines : [],
      parseWarnings: Array.isArray(v.parseWarnings) ? v.parseWarnings : [],
      parseVersion: typeof v.parseVersion === 'number' ? v.parseVersion : 0,
      storyVersion: typeof v.storyVersion === 'number' ? v.storyVersion : 0,
      storyReviewed: Boolean(v.storyReviewed),
      storyMemory: v.storyMemory && typeof v.storyMemory === 'object' ? v.storyMemory : undefined,
      voiceLocked: Boolean(v.voiceLocked),
      voicePreview: v.voicePreview && typeof v.voicePreview === 'object' ? v.voicePreview : undefined,
    };
    const migrated = ensurePilotGraph(loaded);
    if ((loaded.schemaVersion ?? 0) < PILOT_SCHEMA) {
      migrated.schemaVersion = PILOT_SCHEMA;
      saveSeriesPilot(migrated);
    }
    return migrated;
  } catch {
    return emptyPilot();
  }
}

export function slimPilotForStorage(state: SeriesPilotState): SeriesPilotState {
  const runs: SeriesPilotState['runs'] = {};
  for (const [id, run] of Object.entries(state.runs)) {
    runs[id] = { ...run, keyframeDataUrl: undefined };
  }
  return {
    ...state,
    stills: (state.stills ?? []).map((s) => ({ ...s, imageDataUrl: undefined })),
    characters: (state.characters ?? []).map((c) => ({ ...c, canonImageDataUrl: undefined })),
    runs,
  };
}

export function saveSeriesPilot(state: SeriesPilotState) {
  rememberCanonFromChars(state.characters ?? [], state.stills);
  const slim = slimPilotForStorage(state);
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify(slim));
    return;
  } catch {
    /* quota — drop pack text next */
  }
  try {
    localStorage.setItem(STORAGE_KEY, JSON.stringify({ ...slim, packDraft: undefined }));
  } catch {
    /* keep session; do not throw — KF01 still lives in memory this tab */
  }
}

export function normCharId(raw: string) {
  const m = raw.toUpperCase().match(/CHAR\s*-?\s*(\d+)/);
  if (m) return `CHAR-${String(m[1]).padStart(3, '0')}`;
  return raw.replace(/\s+/g, ' ').trim();
}

export function shotCharacterIds(shot: { characters?: string[]; characterIds?: string[] }) {
  const ids = shot.characterIds?.length ? shot.characterIds : shot.characters ?? [];
  return [...new Set(ids.map(normCharId).filter(Boolean))];
}

function escapeRe(s: string) {
  return s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

function blobFieldForName(blob: string, name: string) {
  if (!name.trim()) return '';
  const re = new RegExp(`${escapeRe(name)}\\s*[:：]\\s*([^·.\\n]+)`, 'i');
  return blob.match(re)?.[1]?.trim() ?? '';
}

function seatForName(blob: string, name: string) {
  if (!name.trim()) return '';
  const re = new RegExp(`${escapeRe(name)}\\s+([^·\\n]+)`, 'i');
  const hit = blob.match(re)?.[1]?.trim() ?? '';
  if (!hit || /[:：]/.test(hit)) return '';
  return hit.replace(/\s+/g, ' ').slice(0, 48);
}

function charsFromContinuityBlob(text: string): { id: string; name: string }[] {
  const out: { id: string; name: string }[] = [];
  const re = /(CHAR-\d+)\s+([^·—–\n]+)/gi;
  let m: RegExpExecArray | null;
  while ((m = re.exec(text))) {
    out.push({ id: normCharId(m[1]), name: m[2].replace(/Canon.*/i, '').trim() });
  }
  return out;
}

function upsertChar(map: Map<string, FamixaCharacter>, row: Partial<FamixaCharacter> & { id: string }) {
  const id = normCharId(row.id);
  if (!id) return;
  const prev = map.get(id);
  map.set(id, {
    id,
    name: (row.name || prev?.name || '').trim(),
    role: row.role || prev?.role,
    wardrobe: row.wardrobe || prev?.wardrobe,
    seat: row.seat || prev?.seat,
    voiceNote: row.voiceNote || prev?.voiceNote,
    voiceId: row.voiceId || prev?.voiceId,
    voiceName: row.voiceName || prev?.voiceName,
    line: row.line || prev?.line,
    performance: row.performance || prev?.performance,
    canonFileName: row.canonFileName || prev?.canonFileName,
    canonLocalPath: row.canonLocalPath || prev?.canonLocalPath,
    canonImageDataUrl: row.canonImageDataUrl || prev?.canonImageDataUrl,
  });
}

export function mergeCharacters(prev: FamixaCharacter[] = [], incoming: FamixaCharacter[] = []) {
  const map = new Map<string, FamixaCharacter>();
  for (const c of prev) upsertChar(map, c);
  for (const c of incoming) upsertChar(map, c);
  return [...map.values()].slice(0, 16);
}

export function mergeScenes(prev: FamixaSceneNode[] = [], incoming: FamixaSceneNode[] = []) {
  const map = new Map(prev.map((s) => [s.id, s]));
  for (const n of incoming) {
    const old = map.get(n.id);
    map.set(n.id, old
      ? {
          ...old,
          ...n,
          title: n.title || old.title,
          environment: n.environment || old.environment,
          camera: n.camera || old.camera,
          position: n.position || old.position,
          performance: n.performance || old.performance,
          characterIds: [...new Set([...(old.characterIds ?? []), ...(n.characterIds ?? [])])],
          sourceShotId: old.sourceShotId || n.sourceShotId,
          performances: { ...(old.performances ?? {}), ...(n.performances ?? {}) },
        }
      : n);
  }
  return [...map.values()].slice(0, 24);
}

export function continuityFromGraph(
  scene: FamixaSceneNode | undefined,
  characters: FamixaCharacter[],
  episode?: FamixaSeriesEpisode,
  current?: SceneContinuityLock,
): SceneContinuityLock {
  const chars = (scene?.characterIds ?? []).map((id) => characters.find((c) => c.id === id)).filter(Boolean) as FamixaCharacter[];
  const listed = chars.length ? chars : characters;
  const base = current;
  return {
    id: base?.id || `FAMIXA_${scene?.id || 'SC'}_CONTINUITY`,
    episode: episode?.episode || episode?.title || base?.episode || '',
    scene: scene?.title ? `${scene.id} — ${scene.title}` : scene?.id || '',
    characters: listed.map((c) => `${c.id} ${c.name}`.trim()).join(' · ') || '',
    wardrobe: listed.filter((c) => c.wardrobe).map((c) => `${c.name}: ${c.wardrobe}`).join('. ') || base?.wardrobe || '',
    position: listed.filter((c) => c.seat).map((c) => `${c.name} ${c.seat}`).join(' · ') || scene?.position || base?.position || '',
    environment: scene?.environment || base?.environment || '',
    camera: scene?.camera || base?.camera || '',
    performance: scene?.performance || base?.performance || '',
    locked: Boolean(base?.locked),
    sourceShotId: base?.sourceShotId || scene?.sourceShotId,
  };
}

/** v4 blob / pack → graph. Idempotent. */
export function ensurePilotGraph(state: SeriesPilotState): SeriesPilotState {
  const lock = state.continuity;
  const charMap = new Map<string, FamixaCharacter>();
  for (const c of state.characters ?? []) upsertChar(charMap, c);

  for (const row of charsFromContinuityBlob(lock?.characters ?? '')) {
    upsertChar(charMap, row);
  }
  if (state.packDraft) {
    for (const row of parseCharLocks(state.packDraft)) {
      upsertChar(charMap, { id: row.code, name: row.name, role: row.role });
    }
  }
  for (const s of state.stills ?? []) {
    if (s.charCode) upsertChar(charMap, { id: s.charCode, name: s.note?.split('·')[0]?.trim() || '' });
  }
  for (const shot of state.episode?.shots ?? []) {
    for (const id of shotCharacterIds(shot)) upsertChar(charMap, { id, name: '' });
  }
  for (const short of state.shorts ?? []) {
    for (const id of shotCharacterIds({ characters: short.characters, characterIds: short.characterIds })) {
      upsertChar(charMap, { id, name: '' });
    }
  }
  const nextRoleId = () => {
    let n = 1;
    while (charMap.has(`CHAR-${String(n).padStart(3, '0')}`)) n += 1;
    return `CHAR-${String(n).padStart(3, '0')}`;
  };
  const roles = (state.roles ?? []).map((role) => {
    const byId = role.characterId ? charMap.get(normCharId(role.characterId)) : undefined;
    const named = byId
      ?? [...charMap.values()].find((c) => c.name && role.name && c.name.toLowerCase() === role.name.toLowerCase());
    const id = named?.id || (role.name.trim() ? nextRoleId() : undefined);
    if (!id) return role;
    upsertChar(charMap, {
      id,
      name: role.name || named?.name || '',
      role: role.title || named?.role,
      line: role.line || named?.line,
      voiceNote: role.voiceNote || named?.voiceNote,
      voiceId: role.voiceId || named?.voiceId,
      voiceName: role.voiceName || named?.voiceName,
      performance: role.performance || named?.performance,
    });
    return {
      ...role,
      characterId: id,
      voiceId: role.voiceId || named?.voiceId || charMap.get(id)?.voiceId,
      voiceName: role.voiceName || named?.voiceName || charMap.get(id)?.voiceName,
    };
  });
  for (const c of [...charMap.values()]) {
    if (lock?.wardrobe) {
      const w = blobFieldForName(lock.wardrobe, c.name);
      if (w) upsertChar(charMap, { ...c, wardrobe: c.wardrobe || w });
    }
    if (lock?.position) {
      const seat = seatForName(lock.position, c.name);
      if (seat) upsertChar(charMap, { ...charMap.get(c.id)!, seat: c.seat || seat });
    }
  }

  const characters = [...charMap.values()];
  const sceneMap = new Map<string, FamixaSceneNode>();
  for (const sc of state.scenes ?? []) sceneMap.set(sc.id, { ...sc, characterIds: [...(sc.characterIds ?? [])] });

  const linkShot = (shot: FamixaSeriesShot, prev?: FamixaSeriesShot): FamixaSeriesShot => {
    const sceneId = shot.sceneId || sceneIdOfShot(shot);
    const characterIds = shotCharacterIds(shot);
    if (sceneId) {
      const node = sceneMap.get(sceneId) ?? {
        id: sceneId,
        characterIds: [],
      };
      node.characterIds = [...new Set([...node.characterIds, ...characterIds])];
      sceneMap.set(sceneId, node);
    }
    const sameScenePrev = prev && (prev.sceneId || sceneIdOfShot(prev)) === sceneId ? prev.id : undefined;
    return {
      ...shot,
      sceneId: sceneId || shot.sceneId,
      characterIds,
      characters: characterIds.length ? characterIds : shot.characters,
      previousShotId: shot.previousShotId || sameScenePrev,
      inheritFromShotId: shot.inheritFromShotId || sameScenePrev,
    };
  };

  const shots = state.episode?.shots ?? [];
  const nextShots = shots.map((s, i) => linkShot(s, shots[i - 1]));
  const shorts = (state.shorts ?? []).map((s) => {
    const sceneId = s.sceneId || sceneIdOfShot({ scene: s.scene, id: s.id });
    const characterIds = shotCharacterIds({ characters: s.characters, characterIds: s.characterIds });
    if (sceneId) {
      const node = sceneMap.get(sceneId) ?? { id: sceneId, characterIds: [] };
      node.characterIds = [...new Set([...node.characterIds, ...characterIds])];
      sceneMap.set(sceneId, node);
    }
    return { ...s, sceneId: sceneId || s.sceneId, characterIds, characters: characterIds.length ? characterIds : s.characters };
  });

  if (lock) {
    const lockSceneId = sceneCodeOf(lock.scene);
    const node = lockSceneId ? sceneMap.get(lockSceneId) : undefined;
    if (node) {
      sceneMap.set(node.id, {
        ...node,
        environment: node.environment || lock.environment,
        camera: node.camera || lock.camera,
        position: node.position || lock.position,
        performance: node.performance || lock.performance,
        sourceShotId: node.sourceShotId || lock.sourceShotId,
      });
    }
  }

  const scenes = [...sceneMap.values()];
  const lines: FamixaLine[] = (state.lines ?? []).map((l) => ({ ...l }));

  const primary = scenes[0];
  const continuity =
    state.continuity ?? (primary ? continuityFromGraph(primary, characters, state.episode) : undefined);

  const next: SeriesPilotState = {
    ...state,
    schemaVersion: Math.max(state.schemaVersion ?? 0, PILOT_SCHEMA),
    roles,
    characters,
    scenes,
    lines,
    shorts,
    episode: state.episode ? { ...state.episode, shots: nextShots } : state.episode,
    continuity,
    storyMemory: ensureStoryMemory(state.storyMemory, characters, state.episode),
  };
  return applyCanonToStills(next);
}

function sceneIdOfShot(shot: { scene?: string; id?: string }) {
  return codeMatch(shot.scene, 'SC') || codeMatch(shot.id, 'SC') || '';
}

export function sceneNodeOf(state: SeriesPilotState, shot?: FamixaSeriesShot) {
  const id = shot?.sceneId || (shot ? sceneIdOfShot(shot) : state.scenes?.[0]?.id);
  return (state.scenes ?? []).find((s) => s.id === id);
}

export function lockFromGraph(state: SeriesPilotState, shot?: FamixaSeriesShot): SceneContinuityLock {
  return continuityFromGraph(sceneNodeOf(state, shot), state.characters ?? [], state.episode, state.continuity);
}

export type SeriesBrandSlice = {
  positioning?: string | null;
  audience?: string | null;
  tone?: string[];
  forbiddenTopics?: string[];
  avoidTerms?: string[];
  claimsForbidden?: string[];
  visualStyle?: string | null;
  visualColors?: string | null;
  contentPillars?: string[];
};

/** I2V slice — Brand Brain Famixa, không 48 tài liệu. */
export function formatSeriesVideoContext(k?: SeriesBrandSlice | null, max = 360) {
  if (!k) {
    return 'No looking at the camera. No extra people. Do not change faces, age, clothes or location.';
  }
  const bits: string[] = [];
  const style = (k.visualStyle ?? '').trim();
  const colors = (k.visualColors ?? '').trim();
  if (style) bits.push(style);
  if (colors) bits.push(colors);
  const forbid = [...(k.claimsForbidden ?? []), ...(k.forbiddenTopics ?? []), ...(k.avoidTerms ?? [])]
    .map((s) => s.trim())
    .filter(Boolean)
    .filter((s, i, a) => a.findIndex((x) => x.toLowerCase() === s.toLowerCase()) === i)
    .slice(0, 6);
  if (forbid.length) bits.push(`Forbidden: ${forbid.join('; ')}`);
  bits.push('No looking at the camera. No extra people. Do not change faces, age, clothes or location.');
  const text = bits.join('. ');
  return text.length <= max ? text : `${text.slice(0, max - 1)}…`;
}

export function pickFamixaBrand<T extends { code: string; name: string }>(brands: T[]) {
  return (
    brands.find((b) => /famixa/i.test(b.code) || /famixa/i.test(b.name)) ?? undefined
  );
}

/** Prompt I2V từ graph (CHAR / scene / inherit / Action) — không dán giấy Story / 48 docs. */
export function compileI2vPrompt(
  state: SeriesPilotState,
  shot: FamixaSeriesShot,
  action: string,
  videoContext?: string,
) {
  const scene = sceneNodeOf(state, shot);
  const lock = lockFromGraph(state, shot);
  const ids = shot.characterIds?.length ? shot.characterIds : scene?.characterIds ?? [];
  const cast = ids
    .map((id) => {
      const c = (state.characters ?? []).find((row) => row.id === id);
      if (!c) return '';
      const perf = scene?.performances?.[c.id];
      const bits = [c.name || c.id];
      if (c.seat) bits.push(c.seat);
      if (c.wardrobe) bits.push(c.wardrobe);
      if (perf) bits.push(perf);
      return bits.join(' · ');
    })
    .filter(Boolean)
    .join('; ');
  const inherit = shot.inheritFromShotId || shot.previousShotId || lock.sourceShotId || scene?.id || lock.id;
  const act = action.replace(/\s+/g, ' ').trim();
  const wardrobe = clipMem(lock.wardrobe, 140);
  const pos = clipMem(lock.position, 80);
  const env = clipMem(scene?.environment || lock.environment, 140);
  const cam = clipMem(scene?.camera || lock.camera, 80);
  const ctx = clipMem(videoContext, 280);
  const text =
    `Same scene as ${inherit}. ` +
    (cast ? `Cast: ${cast}. ` : '') +
    (wardrobe ? `Wardrobe locked: ${wardrobe}. ` : 'Same wardrobe. ') +
    (pos ? `Seats: ${pos}. ` : '') +
    (env ? `${env} ` : 'Same room, table, food, night light. ') +
    (cam ? `Camera: ${cam} ` : '') +
    (ctx ? `${ctx} ` : '') +
    `Only this action: ${act || 'subtle natural motion at the table.'} ` +
    `Do not change clothes, faces, age, props or location. No looking at the camera. ${shot.seconds || 5} seconds.`;
  return text.length <= 900 ? text : text.slice(0, 900);
}

export function applyShotLockToGraph(
  state: SeriesPilotState,
  shot: FamixaSeriesShot,
  action?: string,
): SeriesPilotState {
  const sceneId = shot.sceneId || sceneIdOfShot(shot);
  const act = (action ?? '').trim();
  const scenes = (state.scenes ?? []).map((sc) => {
    if (sc.id !== sceneId) return sc;
    const performances = { ...(sc.performances ?? {}) };
    for (const c of state.characters ?? []) {
      if (c.name && act && act.toLowerCase().includes(c.name.toLowerCase())) {
        performances[c.id] = act;
      }
    }
    return {
      ...sc,
      sourceShotId: sc.sourceShotId || shot.id,
      performance: sc.performance || act || sc.performance,
      performances,
    };
  });
  const shots = (state.episode?.shots ?? []).map((s, i, all) => {
    if (s.id === shot.id) {
      return { ...s, inheritFromShotId: s.inheritFromShotId || s.previousShotId };
    }
    const prev = all[i - 1];
    if (prev?.id === shot.id) {
      return { ...s, previousShotId: s.previousShotId || shot.id, inheritFromShotId: shot.id };
    }
    return s;
  });
  const locked = lockFromApprovedShot(shot, state.episode, state.continuity);
  const scene = scenes.find((s) => s.id === sceneId);
  return {
    ...state,
    scenes,
    episode: state.episode ? { ...state.episode, shots } : state.episode,
    continuity: scene
      ? { ...continuityFromGraph(scene, state.characters ?? [], state.episode, locked), locked: true }
      : locked,
  };
}

export type FamixaVoiceCue = {
  characterId: string;
  name: string;
  text: string;
  inSec: number;
  outSec: number;
  voiceId?: string;
};

/** Cue chữ trên Timeline — chưa mux TTS. */
export function voiceCuesForShot(state: SeriesPilotState, shot: FamixaSeriesShot): FamixaVoiceCue[] {
  const ids = shotCharacterIds(shot);
  const lines = (state.lines ?? []).filter((l) => ids.includes(l.characterId) && l.text.trim());
  const sec = Math.max(1, shot.seconds || 5);
  return lines.slice(0, 3).map((l, i) => ({
    characterId: l.characterId,
    name: (state.characters ?? []).find((c) => c.id === l.characterId)?.name || l.characterId,
    text: l.text,
    inSec: Math.min(i, Math.max(0, sec - 1)),
    outSec: Math.min(i + 2, sec),
    voiceId: l.voiceId || (state.characters ?? []).find((c) => c.id === l.characterId)?.voiceId,
  }));
}

export type FamixaListenCue = {
  id: string;
  characterId: string;
  name: string;
  text: string;
  voiceId?: string;
};

/** Lượt thoại Voice Script — chỉ dialogue, không cắt 80, không gộp nuốt câu. */
export function scriptListenCues(state: SeriesPilotState): FamixaListenCue[] {
  return deriveVoiceScript(state).lines;
}

export function characterCanonReady(c?: FamixaCharacter) {
  return Boolean(
    c && (c.canonFileName || c.canonLocalPath || c.canonImageDataUrl || famixaCanonSeedFor(c)),
  );
}

export type SeriesCanonRef = { name: string; role?: string; imageDataUrl: string };

export function canonDisplayOf(state: SeriesPilotState, characterId: string) {
  const pixels = canonImageOf(state, characterId);
  if (pixels) return pixels;
  const id = normCharId(characterId);
  const ch = (state.characters ?? []).find((c) => c.id === id);
  return famixaCanonSeedFor(ch ?? { id })?.publicPath;
}

export function canonImageOf(state: SeriesPilotState, characterId: string) {
  const id = normCharId(characterId);
  const ch = (state.characters ?? []).find((c) => c.id === id);
  if (ch?.canonImageDataUrl?.startsWith('data:image')) {
    rememberCanonPixels(id, ch.canonImageDataUrl, ch.canonFileName);
    return ch.canonImageDataUrl;
  }
  const mem = canonPixelsOf(id);
  if (mem) return mem;
  const still = (state.stills ?? []).find(
    (s) =>
      s.imageDataUrl?.startsWith('data:image') &&
      (normCharId(s.charCode ?? '') === id || s.charCode === ch?.name),
  );
  if (still?.imageDataUrl) {
    rememberCanonPixels(id, still.imageDataUrl, still.fileName);
    return still.imageDataUrl;
  }
  return undefined;
}

export function canonStillRefs(state: SeriesPilotState, characterIds?: string[]): SeriesCanonRef[] {
  const pool = state.characters ?? [];
  const wanted = (characterIds ?? []).map((id) => normCharId(id)).filter(Boolean);
  const pick = (c: FamixaCharacter | undefined): SeriesCanonRef | undefined => {
    if (!c) return undefined;
    const imageDataUrl = canonImageOf(state, c.id);
    if (!imageDataUrl?.startsWith('data:image')) return undefined;
    return { name: c.name || c.id, role: c.role, imageDataUrl };
  };
  const out: SeriesCanonRef[] = [];
  for (const id of wanted) {
    const byId = pool.find((c) => c.id === id);
    const byName = pool.find((c) => c.name.toLowerCase() === id.toLowerCase());
    const row = pick(byId) || pick(byName);
    if (row && !out.some((x) => x.name === row.name)) out.push(row);
  }
  if (out.length === 0) {
    for (const c of pool) {
      const row = pick(c);
      if (row) out.push(row);
    }
  }
  return out.slice(0, 4);
}

export async function hydratePilotCanon(state: SeriesPilotState): Promise<SeriesPilotState> {
  rememberCanonFromChars(state.characters ?? [], state.stills);
  let changed = false;
  const characters: FamixaCharacter[] = [];
  for (const c of state.characters ?? []) {
    if (c.canonImageDataUrl?.startsWith('data:image')) {
      characters.push(c);
      continue;
    }
    const hit = await loadCanonPixels(c.id);
    if (hit?.dataUrl?.startsWith('data:image')) {
      changed = true;
      characters.push({
        ...c,
        canonImageDataUrl: hit.dataUrl,
        canonFileName: c.canonFileName || hit.fileName,
      });
      continue;
    }
    const seed = famixaCanonSeedFor(c);
    if (!seed) {
      characters.push(c);
      continue;
    }
    try {
      const dataUrl = await fetchFamixaCanonSeedDataUrl(seed.publicPath);
      if (!dataUrl.startsWith('data:image')) {
        characters.push(c);
        continue;
      }
      await saveCanonPixels(c.id, dataUrl, seed.fileName);
      changed = true;
      characters.push({
        ...c,
        canonImageDataUrl: dataUrl,
        canonFileName: c.canonFileName || seed.fileName,
        canonLocalPath: c.canonLocalPath || seed.publicPath,
      });
    } catch {
      characters.push(c);
    }
  }
  if (!changed) return state;
  return applyCanonToStills({ ...state, characters });
}

export function seriesSceneStillPrompt(opts: {
  aspect: '9:16' | '16:9';
  visual: string;
  action?: string;
  location?: string;
  refs: SeriesCanonRef[];
}) {
  const who = opts.refs
    .map((r) => `${r.name}${r.role ? ` (${r.role})` : ''}`)
    .join(', ');
  const frame = opts.aspect === '9:16' ? 'vertical 9:16 phone frame' : 'widescreen 16:9 cinematic frame';
  return [
    `Format: ${frame}.`,
    who ? `People in scene (match attached portraits): ${who}.` : '',
    opts.location ? `Location: ${opts.location.slice(0, 180)}.` : '',
    opts.visual ? `Scene: ${opts.visual.slice(0, 500)}` : '',
    opts.action ? `Action: ${opts.action.slice(0, 400)}` : '',
    'Same Vietnamese family, natural indoor light, photoreal, not anime.',
  ]
    .filter(Boolean)
    .join('\n');
}

export function characterOfRole(state: SeriesPilotState, role: SeriesRoleRow) {
  const id = role.characterId ? normCharId(role.characterId) : '';
  if (!id) return undefined;
  return (state.characters ?? []).find((c) => c.id === id);
}

export function roleCanonReady(state: SeriesPilotState, role: SeriesRoleRow) {
  return characterCanonReady(characterOfRole(state, role));
}

export function roleVoiceReady(state: SeriesPilotState, role: SeriesRoleRow) {
  const ch = characterOfRole(state, role);
  return Boolean((ch?.voiceId || role.voiceId || '').trim());
}

export type FamixaVoiceLaneKey = 'boy' | 'girl' | 'father' | 'mother' | 'man' | 'woman' | 'any';

export type FamixaVoiceLane = {
  key: FamixaVoiceLaneKey;
  label: string;
  gender?: 'male' | 'female';
  ages?: Array<'young' | 'middle_aged' | 'old'>;
};

export type FamixaVoicePick = {
  voiceId: string;
  name: string;
  cloned?: boolean;
  vietnamese?: boolean;
  gender?: string | null;
  age?: string | null;
  accent?: string | null;
};

function foldVoiceText(raw?: string | null) {
  return (raw ?? '')
    .normalize('NFD')
    .replace(/\p{M}/gu, '')
    .replace(/đ/gi, 'd')
    .toLowerCase()
    .trim();
}

export function voiceLaneForRole(
  role: Pick<SeriesRoleRow, 'title' | 'name' | 'characterId'>,
  character?: { id?: string; name?: string },
): FamixaVoiceLane {
  const id = normCharId(role.characterId || character?.id || '');
  const hay = foldVoiceText([role.characterId, role.title, role.name, character?.id, character?.name].filter(Boolean).join(' '));
  if (/be gai|con gai|co be|nu nhi/.test(hay)) {
    return { key: 'girl', label: 'bé gái miền Bắc', gender: 'female', ages: ['young'] };
  }
  if (id === 'CHAR-001' || /\bminh\b/.test(hay) || /be trai|cau be|con trai|\bcon\b/.test(hay)) {
    return { key: 'boy', label: 'bé trai miền Bắc', gender: 'male', ages: ['young'] };
  }
  if (id === 'CHAR-003' || /\blinh\b/.test(hay) || /\bme\b/.test(hay) || /phu huynh nu/.test(hay)) {
    return { key: 'mother', label: 'phụ huynh nữ miền Bắc', gender: 'female', ages: ['middle_aged'] };
  }
  if (id === 'CHAR-002' || /\bbo\b/.test(hay) || /phu huynh nam/.test(hay) || (/\bnam\b/.test(hay) && !/nu/.test(hay))) {
    return { key: 'father', label: 'phụ huynh nam miền Bắc', gender: 'male', ages: ['middle_aged'] };
  }
  if (/\bba\b/.test(hay) && /gia|noi|ngoai/.test(hay)) {
    return { key: 'woman', label: 'nữ lớn tuổi miền Bắc', gender: 'female', ages: ['old', 'middle_aged'] };
  }
  if (/\bong\b/.test(hay)) {
    return { key: 'man', label: 'nam lớn tuổi miền Bắc', gender: 'male', ages: ['old', 'middle_aged'] };
  }
  if (/\bba\b/.test(hay)) {
    return { key: 'father', label: 'phụ huynh nam miền Bắc', gender: 'male', ages: ['middle_aged'] };
  }
  if (/nu|chi |co /.test(hay)) {
    return { key: 'woman', label: 'nữ miền Bắc', gender: 'female', ages: ['middle_aged', 'old'] };
  }
  if (/nam |anh |chu /.test(hay)) {
    return { key: 'man', label: 'nam miền Bắc', gender: 'male', ages: ['middle_aged', 'old'] };
  }
  return { key: 'any', label: 'miền Bắc' };
}

function normVoiceGender(raw?: string | null): 'male' | 'female' | undefined {
  const s = foldVoiceText(raw);
  if (s === 'male' || s === 'nam' || s === 'man' || s === 'boy') return 'male';
  if (s === 'female' || s === 'nu' || s === 'woman' || s === 'girl') return 'female';
  return undefined;
}

function normVoiceAge(raw?: string | null): 'young' | 'middle_aged' | 'old' | undefined {
  const s = foldVoiceText(raw).replace(/-/g, '_').replace(/\s+/g, '_');
  if (!s) return undefined;
  if (s.includes('young') || s.includes('child') || s.includes('kid') || s.includes('tre')) return 'young';
  if (s.includes('old') || s.includes('elder')) return 'old';
  if (s.includes('middle')) return 'middle_aged';
  return undefined;
}

function voiceHay(v: FamixaVoicePick) {
  return foldVoiceText([v.accent, v.name, v.gender, v.age].filter(Boolean).join(' '));
}

function isSouthernOrCentralVoice(v: FamixaVoicePick) {
  return /southern|south viet|saigon|sai gon|ho chi minh|mien nam|mekong|central|mien trung|hue|da nang|nha trang/.test(voiceHay(v));
}

function isNorthernVoice(v: FamixaVoicePick) {
  return /northern|north viet|hanoi|ha noi|ha-noi|mien bac|giong bac|bac ky|hai phong|nam dinh/.test(voiceHay(v));
}

export function voicesForLane(voices: FamixaVoicePick[], lane: FamixaVoiceLane, selectedId?: string) {
  const pool = voices.filter((v) => !v.cloned && v.vietnamese !== false && !isSouthernOrCentralVoice(v));
  const north = pool.filter(isNorthernVoice);
  const unknown = pool.filter((v) => !v.accent || !foldVoiceText(v.accent));
  let base = north.length >= 3 ? north : [...north, ...unknown.filter((v) => !north.includes(v))];
  if (lane.gender) {
    const gendered = base.filter((v) => {
      const g = normVoiceGender(v.gender);
      return !g || g === lane.gender;
    });
    if (gendered.length >= 2) base = gendered;
  }
  if (lane.ages?.length) {
    const aged = base.filter((v) => {
      const a = normVoiceAge(v.age);
      return !a || lane.ages!.includes(a);
    });
    if (aged.length >= 2) base = aged;
  }
  if (lane.key === 'boy' || lane.key === 'girl') {
    const kids = base.filter((v) => {
      const a = normVoiceAge(v.age);
      return a === 'young' || /boy|girl|child|kid|be trai|be gai|cau be|co be/.test(voiceHay(v));
    });
    if (kids.length >= 2) base = kids;
  }
  const selected = (selectedId ?? '').trim();
  if (selected && !base.some((v) => v.voiceId === selected)) {
    const cur = voices.find((v) => v.voiceId === selected);
    if (cur) base = [cur, ...base];
  }
  return base.slice(0, 24);
}

export function canLockCast(state: SeriesPilotState) {
  if (state.roles.length === 0) return false;
  if (!rolesReady(state.roles)) return false;
  return state.roles.every((r) => roleCanonReady(state, r) && roleVoiceReady(state, r));
}

export function setCharacterVoice(
  state: SeriesPilotState,
  characterId: string,
  voice: {
    voiceId?: string;
    voiceName?: string;
    voiceStability?: number;
    voiceSimilarity?: number;
    voiceStyle?: number;
    voiceSpeed?: number;
  },
): SeriesPilotState {
  const id = normCharId(characterId);
  if (!id) return state;
  const voiceId = (voice.voiceId ?? '').trim() || undefined;
  const voiceName = (voice.voiceName ?? '').trim() || undefined;
  let found = false;
  const characters = (state.characters ?? []).map((c) => {
    if (c.id !== id) return c;
    found = true;
    return {
      ...c,
      voiceId,
      voiceName: voiceName || c.voiceName,
      voiceStability: voice.voiceStability ?? c.voiceStability,
      voiceSimilarity: voice.voiceSimilarity ?? c.voiceSimilarity,
      voiceStyle: voice.voiceStyle ?? c.voiceStyle,
      voiceSpeed: voice.voiceSpeed ?? c.voiceSpeed,
    };
  });
  if (!found) {
    characters.push({
      id,
      name: '',
      voiceId,
      voiceName,
      voiceStability: voice.voiceStability,
      voiceSimilarity: voice.voiceSimilarity,
      voiceStyle: voice.voiceStyle,
      voiceSpeed: voice.voiceSpeed,
    });
  }
  const roles = state.roles.map((r) =>
    r.characterId === id ? { ...r, voiceId, voiceName: voiceName || r.voiceName } : r,
  );
  const lines = (state.lines ?? []).map((l) => (l.characterId === id ? { ...l, voiceId: voiceId || l.voiceId } : l));
  return { ...state, characters, roles, lines };
}

export function lockCast(state: SeriesPilotState): SeriesPilotState {
  let next = ensurePilotGraph({ ...state, schemaVersion: PILOT_SCHEMA });
  for (const role of next.roles) {
    const id = role.characterId;
    const voiceId = (role.voiceId || characterOfRole(next, role)?.voiceId || '').trim();
    if (!id || !voiceId) continue;
    next = setCharacterVoice(next, id, {
      voiceId,
      voiceName: role.voiceName || characterOfRole(next, role)?.voiceName,
    });
  }
  return { ...next, castLocked: true };
}

export function hasSeriesGraph(state: SeriesPilotState) {
  return (
    state.roles.length > 0 ||
    (state.characters?.length ?? 0) > 0 ||
    episodeShots(state).length > 0 ||
    (state.shorts?.length ?? 0) > 0
  );
}

export function mergeRemotePilot(remote: SeriesPilotState, local: SeriesPilotState): SeriesPilotState {
  const graph = ensurePilotGraph({ ...remote, schemaVersion: PILOT_SCHEMA });
  const localChars = local.characters ?? [];
  const characters = (graph.characters ?? []).map((c) => {
    const old = localChars.find((x) => x.id === c.id);
    return { ...c, canonImageDataUrl: old?.canonImageDataUrl || c.canonImageDataUrl };
  });
  const runs: SeriesPilotState['runs'] = { ...graph.runs };
  for (const [id, run] of Object.entries(runs)) {
    const old = local.runs[id];
    if (old?.keyframeDataUrl) runs[id] = { ...run, keyframeDataUrl: old.keyframeDataUrl };
  }
  const stills = (graph.stills ?? []).map((s) => {
    const old = (local.stills ?? []).find((x) => x.id === s.id);
    return { ...s, imageDataUrl: old?.imageDataUrl || s.imageDataUrl };
  });
  const sameEp = episodeCodeOf(graph.episode?.episode || graph.episode?.title) ===
    episodeCodeOf(local.episode?.episode || local.episode?.title);
  const voicePreview = sameEp
    ? pickRicherVoicePreview(graph.voicePreview, local.voicePreview)
    : graph.voicePreview;
  return {
    ...graph,
    characters,
    runs,
    stills,
    voiceLocked: sameEp ? Boolean(graph.voiceLocked || local.voiceLocked) : Boolean(graph.voiceLocked),
    voicePreview,
  };
}

function pickRicherVoicePreview(a?: FamixaVoicePreview, b?: FamixaVoicePreview) {
  const score = (p?: FamixaVoicePreview) => {
    if (!p) return -1;
    const n = Math.max(p.generatedLineCount ?? 0, p.generated?.length ?? 0);
    if (p.status === 'complete' || p.operatorConfirmed) return 10_000 + n;
    return n;
  };
  const winner = score(b) > score(a) ? b : a;
  if (!winner) return a ?? b;
  const generated = mergeVoiceGenerated(a?.generated, b?.generated ?? []);
  return {
    ...winner,
    generated: generated.length ? generated : winner.generated,
    operatorConfirmed: Boolean(a?.operatorConfirmed || b?.operatorConfirmed),
  };
}

export function setCharacterCanon(
  state: SeriesPilotState,
  characterId: string,
  canon: { canonFileName?: string; canonLocalPath?: string; canonImageDataUrl?: string },
): SeriesPilotState {
  const id = normCharId(characterId);
  if (!id) return state;
  let found = false;
  const characters = (state.characters ?? []).map((c) => {
    if (c.id !== id) return c;
    found = true;
    return { ...c, ...canon };
  });
  if (!found) characters.push({ id, name: '', ...canon });
  return applyCanonToStills({ ...state, characters });
}

/** Clip/shot stills inherit Character Canon — never a second Minh picker. */
export function applyCanonToStills(state: SeriesPilotState): SeriesPilotState {
  const chars = state.characters ?? [];
  const stills = [...(state.stills ?? [])];
  const clips = [
    ...(state.shorts ?? []).map((s) => ({
      id: s.id,
      scene: s.scene ?? '',
      characterIds: shotCharacterIds({ characters: s.characters, characterIds: s.characterIds }),
    })),
    ...(state.episode?.shots ?? []).map((s) => ({
      id: s.id,
      scene: s.scene,
      characterIds: shotCharacterIds(s),
    })),
  ];
  for (const clip of clips) {
    for (const charId of clip.characterIds) {
      const c = chars.find((row) => row.id === charId);
      if (!characterCanonReady(c) || !c) continue;
      const idx = stills.findIndex((s) => s.shortId === clip.id && normCharId(s.charCode) === charId);
      const inherited = {
        charCode: charId,
        shortId: clip.id,
        scene: clip.scene,
        fileName: c.canonFileName,
        localPath: c.canonLocalPath,
        imageDataUrl: c.canonImageDataUrl,
        note: 'Character Canon — không dùng làm KF I2V',
      };
      if (idx < 0) {
        stills.push(newStillRow(inherited));
        continue;
      }
      const row = stills[idx];
      const empty = !row.fileName && !row.localPath && !row.imageDataUrl;
      stills[idx] = {
        ...row,
        fileName: empty ? c.canonFileName : row.fileName || c.canonFileName,
        localPath: empty ? c.canonLocalPath : row.localPath || c.canonLocalPath,
        imageDataUrl: row.imageDataUrl || c.canonImageDataUrl,
        note: row.note || inherited.note,
      };
    }
  }
  return { ...state, stills: stills.slice(0, 40) };
}

export function applyStillFromCanon(state: SeriesPilotState, stillId: string): SeriesPilotState {
  const row = (state.stills ?? []).find((s) => s.id === stillId);
  if (!row?.charCode) return state;
  const c = (state.characters ?? []).find((x) => x.id === normCharId(row.charCode));
  if (!characterCanonReady(c) || !c) return state;
  return {
    ...state,
    stills: (state.stills ?? []).map((s) =>
      s.id === stillId
        ? {
            ...s,
            fileName: c.canonFileName,
            localPath: c.canonLocalPath,
            imageDataUrl: c.canonImageDataUrl,
            note: s.note || 'Character Canon — không dùng làm KF I2V',
          }
        : s,
    ),
  };
}

export function seriesCanonHint(state: SeriesPilotState) {
  const fromGraph = (state.characters ?? [])
    .map((c) => [c.id, c.name, c.role, c.wardrobe].filter(Boolean).join(' '))
    .filter(Boolean);
  if (fromGraph.length) return fromGraph.join(' · ');
  return state.roles
    .map((r) => [r.characterId, r.title, r.name].filter(Boolean).join(' '))
    .filter(Boolean)
    .join(' · ');
}

export function rolesReady(roles: SeriesRoleRow[]) {
  if (roles.length === 0) return false;
  return roles.every((r) => r.title.trim().length > 0 && r.name.trim().length > 0);
}

export function newRoleRow(title = '', name = ''): SeriesRoleRow {
  return { id: `role-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`, title, name };
}

export function newStillRow(partial?: Partial<FamixaCharStill>): FamixaCharStill {
  return {
    id: `still-${Date.now()}-${Math.random().toString(36).slice(2, 7)}`,
    charCode: '',
    scene: '',
    shortId: '',
    ...partial,
  };
}

export function stillKey(s: Pick<FamixaCharStill, 'shortId' | 'charCode' | 'scene'>) {
  return `${s.shortId}|${s.charCode}|${s.scene}`.toLowerCase();
}

export function mergeStills(prev: FamixaCharStill[], incoming: FamixaCharStill[]) {
  const map = new Map(prev.map((s) => [stillKey(s), s]));
  for (const n of incoming) {
    const old = map.get(stillKey(n));
    map.set(
      stillKey(n),
      old
        ? {
            ...n,
            id: old.id,
            imageDataUrl: old.imageDataUrl ?? n.imageDataUrl,
            fileName: old.fileName ?? n.fileName,
            localPath: old.localPath ?? n.localPath,
          }
        : n,
    );
  }
  return [...map.values()].slice(0, 40);
}

export function stillsForShort(stills: FamixaCharStill[], shortId: string) {
  return stills.filter((s) => s.shortId === shortId);
}

export function parseRoleLines(text: string): SeriesRoleRow[] {
  const rows: SeriesRoleRow[] = [];
  for (const raw of text.split(/\r?\n/)) {
    // Crew only: `ROLE: Story | An`. Do not treat CHAR `Role: Son` as a crew row.
    const m = raw.match(/^ROLE:\s*(.+)$/);
    if (!m) continue;
    const rest = (m[1] ?? '').trim();
    if (!rest) continue;
    const pipe = rest.split('|').map((s) => s.trim());
    const title = pipe[0] ?? '';
    const name = pipe[1] ?? '';
    if (!title) continue;
    rows.push(newRoleRow(title, name));
  }
  return rows.slice(0, 8);
}

export function episodeCodeOf(raw?: string) {
  return raw?.match(/EP\s*\d+/i)?.[0]?.replace(/\s+/g, '').toUpperCase() || '';
}

export function replaceStoryFromParse(
  prev: SeriesPilotState,
  parsed: {
    episode?: FamixaSeriesEpisode;
    scenes: FamixaSceneNode[];
    lines: FamixaLine[];
    characters: FamixaCharacter[];
    roles: SeriesRoleRow[];
    shorts: FamixaShortClip[];
    stills: FamixaCharStill[];
    warnings?: string[];
  },
  packText: string,
): SeriesPilotState {
  const incomingEp = episodeCodeOf(parsed.episode?.episode || parsed.episode?.title);
  const prevEp = episodeCodeOf(prev.episode?.episode || prev.episode?.title);
  const switchedEpisode = Boolean(incomingEp && prevEp && incomingEp !== prevEp);
  const characters = (parsed.characters.length ? parsed.characters : prev.characters ?? []).map((c) => {
    const old = (prev.characters ?? []).find((x) => x.id === c.id);
    return {
      ...c,
      voiceId: old?.voiceId || c.voiceId,
      voiceName: old?.voiceName || c.voiceName,
      voiceNote: old?.voiceNote || c.voiceNote,
      voiceStability: old?.voiceStability ?? c.voiceStability,
      voiceSimilarity: old?.voiceSimilarity ?? c.voiceSimilarity,
      voiceStyle: old?.voiceStyle ?? c.voiceStyle,
      voiceSpeed: old?.voiceSpeed ?? c.voiceSpeed,
      canonFileName: old?.canonFileName || c.canonFileName,
      canonLocalPath: old?.canonLocalPath || c.canonLocalPath,
      canonImageDataUrl: old?.canonImageDataUrl || c.canonImageDataUrl,
      wardrobe: old?.wardrobe || c.wardrobe,
      seat: old?.seat || c.seat,
      line: undefined,
    };
  });
  const roles = (parsed.roles.length ? parsed.roles : prev.roles).map((r) => {
    const old = prev.roles.find((x) => x.characterId && x.characterId === r.characterId);
    if (!old) return r;
    return {
      ...r,
      voiceId: old.voiceId || r.voiceId,
      voiceName: old.voiceName || r.voiceName,
      voiceNote: old.voiceNote || r.voiceNote,
    };
  });
  const keepIds = new Set((parsed.episode?.shots ?? []).map((s) => s.id));
  const runs = switchedEpisode
    ? {}
    : Object.fromEntries(Object.entries(prev.runs).filter(([id]) => keepIds.has(id)));
  return {
    ...prev,
    episode: parsed.episode,
    scenes: parsed.scenes,
    lines: parsed.lines,
    characters,
    roles,
    shorts: parsed.shorts,
    stills: parsed.stills.length ? mergeStills(switchedEpisode ? [] : prev.stills ?? [], parsed.stills) : parsed.stills,
    packDraft: packText,
    scriptLocked: false,
    sceneLocked: false,
    storyReviewed: false,
    parseWarnings: parsed.warnings ?? [],
    parseVersion: (prev.parseVersion ?? 0) + 1,
    storyVersion: (prev.storyVersion ?? 0) + 1,
    runs,
    continuity: switchedEpisode ? undefined : prev.continuity,
    storyMemory: switchedEpisode
      ? inheritStoryMemory(prev.storyMemory, incomingEp || 'EP02', parsed.episode?.title)
      : prev.storyMemory,
    voiceLocked: false,
    voicePreview: undefined,
    schemaVersion: PILOT_SCHEMA,
  };
}

export function mergeShots(prev: FamixaSeriesShot[], incoming: FamixaSeriesShot[]) {
  const map = new Map(prev.map((s) => [s.id, s]));
  for (const n of incoming) {
    const old = map.get(n.id);
    map.set(
      n.id,
      old
        ? {
            ...old,
            ...n,
            status: old.status,
            sceneId: n.sceneId || old.sceneId,
            characterIds: n.characterIds?.length ? n.characterIds : old.characterIds,
            previousShotId: n.previousShotId || old.previousShotId,
            inheritFromShotId: n.inheritFromShotId || old.inheritFromShotId,
          }
        : n,
    );
  }
  return [...map.values()].slice(0, 24);
}

export function episodeShots(state: SeriesPilotState) {
  return state.episode?.shots ?? [];
}

export function shotRunOf(state: SeriesPilotState, shot: FamixaSeriesShot): SeriesShotRun {
  return state.runs[shot.id] ?? { status: shot.status };
}

export function reviewComplete(run: SeriesShotRun) {
  const r = run.review ?? {};
  return Boolean(r.character && r.motion && r.emotion && r.canon);
}

export function previousApproved(state: SeriesPilotState, id: string) {
  const shots = episodeShots(state);
  const i = shots.findIndex((s) => s.id === id);
  if (i <= 0) return true;
  const prev = shots[i - 1];
  if (!prev) return true;
  return shotRunOf(state, prev).status === 'approved';
}

export function canProduceShot(state: SeriesPilotState, shot: FamixaSeriesShot) {
  const run = shotRunOf(state, shot);
  if (run.status === 'story_locked' && !previousApproved(state, shot.id)) return false;
  return true;
}

/** Shot LOCK liền trước (cùng timeline) — không phải shot approved cuối danh sách. */
export function previousLockedShot(state: SeriesPilotState, shot?: FamixaSeriesShot) {
  const shots = episodeShots(state);
  if (!shot) return undefined;
  const inheritId = shot.inheritFromShotId || shot.previousShotId;
  if (inheritId) {
    const bound = shots.find((s) => s.id === inheritId);
    if (bound && shotRunOf(state, bound).status === 'approved') return bound;
  }
  const i = shots.findIndex((s) => s.id === shot.id);
  for (let k = i - 1; k >= 0; k--) {
    if (shotRunOf(state, shots[k]).status === 'approved') return shots[k];
  }
  return undefined;
}

/** Copy KF cảnh từ shot LOCK vào shot đang mở nếu shot đó chưa có ảnh. */
export function bindShotToMemory(state: SeriesPilotState, shot: FamixaSeriesShot): SeriesPilotState {
  const prev = previousLockedShot(state, shot);
  if (!prev) return state;
  const run = shotRunOf(state, shot);
  if (run.keyframeDataUrl) return state;
  const prevRun = shotRunOf(state, prev);
  if (!prevRun.keyframeDataUrl && run.keyframeInheritedFrom === prev.id) return state;
  return {
    ...state,
    runs: {
      ...state.runs,
      [shot.id]: {
        ...run,
        keyframeDataUrl: prevRun.keyframeDataUrl || run.keyframeDataUrl,
        keyframeFileName: run.keyframeFileName || prevRun.keyframeFileName,
        keyframePath: run.keyframePath || prevRun.keyframePath,
        keyframeInheritedFrom: prev.id,
      },
    },
    episode: state.episode
      ? {
          ...state.episode,
          shots: state.episode.shots.map((s) =>
            s.id === shot.id ? { ...s, inheritFromShotId: prev.id, previousShotId: s.previousShotId || prev.id } : s,
          ),
        }
      : state.episode,
  };
}

export function shotActionFromPack(shot: FamixaSeriesShot) {
  return (shot.story || shot.motionPromptVi || shot.visual || '').replace(/\s+/g, ' ').trim();
}

/** KF cảnh từ shot có ảnh (không bắt buộc LOCK) — batch I2V cùng bàn ăn. */
export function previousKeyframeShot(state: SeriesPilotState, shot?: FamixaSeriesShot) {
  const locked = previousLockedShot(state, shot);
  if (locked && shotRunOf(state, locked).keyframeDataUrl) return locked;
  const shots = episodeShots(state);
  if (!shot) return shots.find((s) => shotRunOf(state, s).keyframeDataUrl);
  const i = shots.findIndex((s) => s.id === shot.id);
  for (let k = i - 1; k >= 0; k--) {
    if (shotRunOf(state, shots[k]).keyframeDataUrl) return shots[k];
  }
  return shots.find((s) => s.id !== shot.id && shotRunOf(state, s).keyframeDataUrl);
}

export function bindShotToSceneKeyframe(state: SeriesPilotState, shot: FamixaSeriesShot): SeriesPilotState {
  const run = shotRunOf(state, shot);
  if (run.keyframeDataUrl) return state;
  const prev = previousKeyframeShot(state, shot);
  if (!prev) return state;
  const prevRun = shotRunOf(state, prev);
  if (!prevRun.keyframeDataUrl) return state;
  return {
    ...state,
    runs: {
      ...state.runs,
      [shot.id]: {
        ...run,
        keyframeDataUrl: prevRun.keyframeDataUrl,
        keyframeFileName: run.keyframeFileName || prevRun.keyframeFileName,
        keyframePath: run.keyframePath || prevRun.keyframePath,
        keyframeInheritedFrom: prev.id,
      },
    },
    episode: state.episode
      ? {
          ...state.episode,
          shots: state.episode.shots.map((s) =>
            s.id === shot.id ? { ...s, inheritFromShotId: prev.id, previousShotId: s.previousShotId || prev.id } : s,
          ),
        }
      : state.episode,
  };
}

/** Khóa kịch bản: Action từ STORY pack + Continuity graph. Không gửi I2V. */
export function primeLongShotsOnScriptLock(state: SeriesPilotState): SeriesPilotState {
  const shots = episodeShots(state);
  const runs: SeriesPilotState['runs'] = { ...state.runs };
  for (const shot of shots) {
    const run = shotRunOf(state, shot);
    const action = (run.shotAction ?? '').trim() || shotActionFromPack(shot);
    runs[shot.id] = { ...run, shotAction: action || run.shotAction };
  }
  const next = { ...state, runs, scriptLocked: true };
  const first = shots[0];
  const lock = first ? lockFromGraph(next, first) : next.continuity;
  return {
    ...next,
    continuity: lock ? { ...lock, locked: true } : next.continuity,
  };
}

export function pendingSceneShots(state: SeriesPilotState) {
  return episodeShots(state).filter((s) => {
    const run = shotRunOf(state, s);
    if (run.status === 'approved') return false;
    return !run.previewUrl?.trim();
  });
}

export function allLongShotsLocked(state: SeriesPilotState) {
  const shots = episodeShots(state);
  return shots.length > 0 && shots.every((s) => shotRunOf(state, s).status === 'approved');
}

export function creditSum(state: SeriesPilotState) {
  const shot = episodeShots(state).reduce((n, s) => {
    const run = shotRunOf(state, s);
    return n + (run.status === 'approved' ? (run.credits ?? 0) : 0);
  }, 0);
  const short = (state.shorts ?? []).reduce((n, s) => {
    const run = shortRunOf(state, s.id);
    return n + (run.status === 'approved' ? (run.credits ?? 0) : 0);
  }, 0);
  return shot + short;
}

export function runwaySpentSum(state: SeriesPilotState) {
  const shot = episodeShots(state).reduce((n, s) => n + (shotRunOf(state, s).runwaySpent ?? 0), 0);
  const short = (state.shorts ?? []).reduce((n, s) => n + (shortRunOf(state, s.id).runwaySpent ?? 0), 0);
  return shot + short;
}

export function shortRunOf(state: SeriesPilotState, id: string): SeriesShotRun {
  return state.runs[id] ?? { status: 'keyframe_ready' };
}

export function approvedShortCount(state: SeriesPilotState) {
  return (state.shorts ?? []).filter((s) => shortRunOf(state, s.id).status === 'approved').length;
}

export function linesForScene(state: SeriesPilotState, sceneId: string) {
  const sc = (state.scenes ?? []).find((s) => s.id === sceneId);
  if (sc?.dialogue?.length) {
    return sc.dialogue.map((d) => ({
      id: d.id,
      characterId: d.characterId,
      text: d.text,
      sceneId,
    }));
  }
  return (state.lines ?? []).filter((l) => l.sceneId === sceneId);
}

export function canLockScript(state: SeriesPilotState) {
  const n = (state.shorts?.length ?? 0) + episodeShots(state).length;
  if (n === 0) return false;
  if ((state.scenes?.length ?? 0) > 0 && !state.storyReviewed) return false;
  if (state.roles.length > 0 && !rolesReady(state.roles)) return false;
  if (state.roles.length > 0 && !state.roles.every((r) => roleCanonReady(state, r))) return false;
  if (state.roles.length > 0 && !state.castLocked) return false;
  if (needsInheritanceReview(state)) return false;
  return true;
}

export function canWorkShorts(state: SeriesPilotState) {
  return Boolean(state.scriptLocked) && voiceProductionReady(state);
}

export function canWorkScene(state: SeriesPilotState) {
  if (!voiceProductionReady(state)) return false;
  if (needsInheritanceReview(state)) return false;
  if (!state.scriptLocked) return false;
  const shorts = state.shorts ?? [];
  if (shorts.length === 0) return true;
  return approvedShortCount(state) >= shorts.length;
}

/** Xem bảng shot 16:9. Trừ credit vẫn theo canWorkScene. */
export function canOpenStudio(state: SeriesPilotState) {
  return Boolean(state.scriptLocked) && !needsInheritanceReview(state);
}

export function sceneBlockReason(state: SeriesPilotState): string | undefined {
  if (!state.scriptLocked) return 'Khóa kịch bản ở bước 1 rồi mới dựng cảnh.';
  if (needsInheritanceReview(state)) return 'Duyệt kế thừa EP trước rồi mới dựng cảnh.';
  if (!voiceProductionReady(state)) {
    return 'Khóa Full Voice (thoại COMPLETE) ở Kịch bản trước khi tạo ảnh/video.';
  }
  const shorts = state.shorts ?? [];
  if (shorts.length > 0 && approvedShortCount(state) < shorts.length) {
    return `Khóa hết short 9:16 (${approvedShortCount(state)}/${shorts.length}) rồi mới tạo video cảnh.`;
  }
  return undefined;
}

/** Không bao giờ đẩy vào pane Short khi pack không có short. */
export function studioFallbackPane(state: SeriesPilotState): 'script' | 'shorts' | 'studio' {
  if (!canOpenStudio(state) || !voiceProductionReady(state)) return 'script';
  if ((state.shorts?.length ?? 0) > 0 && !canWorkScene(state)) return 'shorts';
  return 'studio';
}

export function canTurboLongShot(state: SeriesPilotState) {
  return canWorkScene(state);
}

function normFieldKey(k: string) {
  return k.trim().toUpperCase().replace(/\s+/g, '_');
}

function fieldLines(chunk: string): Record<string, string> {
  const map: Record<string, string> = {};
  let key = '';
  for (const raw of chunk.split(/\r?\n/)) {
    const line = raw.trimEnd();
    const m = line.match(/^([A-Za-z][A-Za-z0-9]*(?:[ _-][A-Za-z0-9]+){0,6}):\s*(.*)$/);
    if (m) {
      key = normFieldKey(m[1]);
      map[key] = (m[2] ?? '').trim();
      continue;
    }
    if (key && line.trim() && !/^-+$/.test(line.trim())) {
      map[key] = `${map[key] ?? ''}${map[key] ? ' ' : ''}${line.trim()}`.trim();
    }
  }
  return map;
}

function parseDurationSec(raw?: string) {
  const n = Number.parseInt((raw ?? '').replace(/[^\d]/g, ''), 10);
  return Number.isFinite(n) && n > 0 ? n : undefined;
}

function parseSceneFromVideoId(id: string) {
  const m = id.toUpperCase().match(/(SC\d+|SH\d+)/g) ?? [];
  const scene = m.find((x) => x.startsWith('SC')) ?? '';
  const shot = m.find((x) => x.startsWith('SH')) ?? '';
  return { scene, shot };
}

function numberedSections(text: string): Record<string, string> {
  const map: Record<string, string> = {};
  const re = /^(\d{1,2})\.\s+([A-Z0-9][A-Z0-9 /&-]{2,60})\s*$/gim;
  const hits: { key: string; bodyStart: number; titleStart: number }[] = [];
  let m: RegExpExecArray | null;
  while ((m = re.exec(text))) {
    hits.push({ key: normFieldKey(m[2]), bodyStart: m.index + m[0].length, titleStart: m.index });
  }
  for (let i = 0; i < hits.length; i++) {
    const from = hits[i].bodyStart;
    const to = hits[i + 1] ? hits[i + 1]!.titleStart : text.length;
    map[hits[i].key] = text.slice(from, to).replace(/^=+\s*$/gm, '').trim();
  }
  return map;
}

function parseCharLocks(text: string): { code: string; name: string; role: string }[] {
  const rows: { code: string; name: string; role: string }[] = [];
  const re = /^(CHAR-\d+)\s*[—–-]\s*(.+)$/gim;
  let m: RegExpExecArray | null;
  while ((m = re.exec(text))) {
    const code = m[1].toUpperCase();
    const name = m[2].trim();
    const rest = text.slice(m.index, m.index + 400);
    const role = rest.match(/^Role:\s*(.+)$/im)?.[1]?.trim() ?? '';
    rows.push({ code, name, role });
  }
  return rows;
}

function splitBlocks(text: string): { kind: 'SHORT' | 'SHOT' | 'REF'; body: string }[] {
  const re = /^---\s*(SHORT|SHOT|LONG|REF|STILL)\s*---\s*$/gim;
  const blocks: { kind: 'SHORT' | 'SHOT' | 'REF'; body: string }[] = [];
  let last = 0;
  let kind: 'SHORT' | 'SHOT' | 'REF' | null = null;
  let m: RegExpExecArray | null;
  const src = text;
  while ((m = re.exec(src))) {
    if (kind) blocks.push({ kind, body: src.slice(last, m.index) });
    const tag = m[1].toUpperCase();
    kind = tag === 'SHORT' ? 'SHORT' : tag === 'REF' || tag === 'STILL' ? 'REF' : 'SHOT';
    last = m.index + m[0].length;
  }
  if (kind) blocks.push({ kind, body: src.slice(last) });
  return blocks;
}

function toShort(f: Record<string, string>, i: number): FamixaShortClip | null {
  const hook = (f.HOOK ?? '').trim();
  const motion = (f.MOTION ?? f.PROMPT ?? '').trim();
  if (!hook && !motion) return null;
  const sec = Number.parseInt(f.SECONDS ?? '7', 10);
  return {
    id: (f.ID ?? `S${String(i + 1).padStart(2, '0')}`).trim(),
    hook: hook || `Short ${i + 1}`,
    visual: (f.VISUAL ?? f.BEAT ?? '').trim(),
    seconds: Number.isFinite(sec) ? Math.min(15, Math.max(4, sec)) : 7,
    motionPrompt: motion,
    motionPromptVi: (f.MOTION_VI ?? f.PROMPT_VI ?? '').trim(),
    scene: (f.SCENE ?? '').trim(),
    characters: (f.CHAR ?? f.CHARS ?? '')
      .split(/[,;]+/)
      .map((s) => s.trim())
      .filter(Boolean),
    characterIds: (f.CHAR ?? f.CHARS ?? '')
      .split(/[,;]+/)
      .map((s) => normCharId(s.trim()))
      .filter(Boolean),
    sceneId: (f.SCENE ?? '').trim().replace(/\s+/g, '').toUpperCase() || undefined,
  };
}

function toShot(f: Record<string, string>, i: number): FamixaSeriesShot | null {
  const story = (f.STORY ?? f.HOOK ?? '').trim();
  const motion = (f.MOTION ?? f.PROMPT ?? '').trim();
  if (!story && !motion) return null;
  const sec = Number.parseInt(f.SECONDS ?? '5', 10);
  const shot = (f.SHOT ?? `SH${String(i + 1).padStart(2, '0')}`).trim();
  const scene = (f.SCENE ?? 'SC01').trim();
  const id = (f.ID ?? `${scene}-${shot}`).trim();
  return {
    id,
    scene,
    shot,
    clock: (f.CLOCK ?? `${i * 5}–${i * 5 + (Number.isFinite(sec) ? sec : 5)}s`).trim(),
    seconds: Number.isFinite(sec) ? Math.min(30, Math.max(3, sec)) : 5,
    story: story || id,
    visual: (f.VISUAL ?? '').trim(),
    characters: (f.CHAR ?? f.CHARS ?? '')
      .split(/[,;]+/)
      .map((s) => s.trim())
      .filter(Boolean),
    characterIds: (f.CHAR ?? f.CHARS ?? '')
      .split(/[,;]+/)
      .map((s) => normCharId(s.trim()))
      .filter(Boolean),
    sceneId: (f.SCENE ?? 'SC01').trim().replace(/\s+/g, '').toUpperCase(),
    location: (f.LOC ?? f.LOCATION ?? '').trim(),
    motionPrompt: motion,
    motionPromptVi: (f.MOTION_VI ?? '').trim(),
    negativePrompt: (f.NEGATIVE ?? f.NEGATIVE_PROMPT ?? '').trim() || undefined,
    status: 'story_locked',
  };
}

function parseMasterVideos(
  text: string,
  header: Record<string, string>,
): { shorts: FamixaShortClip[]; shots: FamixaSeriesShot[] } {
  const chunks: string[] = [];
  const re = /^(?:VIDEO[ _]?ID)\s*:/gim;
  let last = -1;
  let m: RegExpExecArray | null;
  while ((m = re.exec(text))) {
    if (last >= 0) chunks.push(text.slice(last, m.index));
    last = m.index;
  }
  if (last >= 0) chunks.push(text.slice(last));
  if (chunks.length === 0 && (header.VIDEO_ID || header.VIDEO_TITLE)) chunks.push(text);

  const sections = numberedSections(text);
  const charLocks = parseCharLocks(text);
  const shorts: FamixaShortClip[] = [];
  const shots: FamixaSeriesShot[] = [];
  for (const [i, chunk] of chunks.entries()) {
    const f = { ...header, ...fieldLines(chunk) };
    const id = (f.VIDEO_ID ?? f.ID ?? `V${i + 1}`).trim();
    const title = (f.VIDEO_TITLE ?? f.TITLE ?? f.STORY ?? '').trim();
    const motion = (
      sections.VIDEO_MOTION_PROMPT ||
      f.MOTION ||
      f.PROMPT ||
      f.ACTION ||
      f.STYLE ||
      ''
    ).trim();
    if (!id && !title && !motion) continue;
    const sec = parseDurationSec(f.TARGET_DURATION ?? f.DURATION ?? f.SECONDS) ?? 5;
    const format = (f.FORMAT ?? '').toLowerCase();
    const { scene, shot } = parseSceneFromVideoId(id);
    const sceneInfo = fieldLines(sections.SCENE_INFORMATION ?? '');
    const chars =
      charLocks.map((c) => c.code).length > 0
        ? charLocks.map((c) => c.code)
        : (f.CHAR ?? f.CHARS ?? f.CAST ?? '')
            .split(/[,;]+/)
            .map((s) => s.trim())
            .filter(Boolean);
    const loc = (sceneInfo.LOCATION ?? f.LOC ?? f.LOCATION ?? '').trim();
    const isShort = format.includes('9:16') || format.includes('9x16') || /short|reel|tiktok/i.test(f.KIND ?? f.TYPE ?? '');
    const purpose = (sections.PRODUCTION_PURPOSE ?? '').replace(/\s+/g, ' ').trim();
    if (isShort) {
      shorts.push({
        id,
        hook: title || id,
        visual: (f.VISUAL ?? f.STYLE ?? purpose).trim(),
        seconds: Math.min(15, Math.max(4, sec)),
        motionPrompt: motion,
        motionPromptVi: (f.MOTION_VI ?? '').trim(),
        scene: (f.SCENE ?? scene).trim(),
        characters: chars,
        characterIds: chars.map(normCharId).filter(Boolean),
        sceneId: (f.SCENE ?? scene).trim().replace(/\s+/g, '').toUpperCase() || undefined,
      });
    } else {
      shots.push({
        id,
        scene: (f.SCENE ?? scene ?? 'SC01').trim(),
        shot: (f.SHOT ?? shot ?? `SH${String(i + 1).padStart(2, '0')}`).trim(),
        clock: (f.CLOCK ?? `${sec}s`).trim(),
        seconds: Math.min(30, Math.max(3, sec)),
        story: title || purpose.slice(0, 160) || id,
        visual: (f.VISUAL ?? f.STYLE ?? purpose).trim(),
        characters: chars,
        characterIds: chars.map(normCharId).filter(Boolean),
        sceneId: (f.SCENE ?? scene ?? 'SC01').trim().replace(/\s+/g, '').toUpperCase(),
        location: loc,
        motionPrompt: motion,
        motionPromptVi: (f.MOTION_VI ?? '').trim(),
        negativePrompt: (sections.NEGATIVE_MOTION_PROMPT ?? '').trim() || undefined,
        status: 'keyframe_ready',
      });
    }
  }
  return { shorts, shots };
}

function stillsFromShortsAndRefs(
  shorts: FamixaShortClip[],
  refs: { charCode: string; scene: string; shortId: string; note?: string }[],
  shots: FamixaSeriesShot[] = [],
  charNotes: { code: string; name: string; role: string }[] = [],
): FamixaCharStill[] {
  const noteOf = (code: string) => {
    const c = charNotes.find((x) => x.code === code);
    return c ? [c.name, c.role].filter(Boolean).join(' · ') : undefined;
  };
  const incoming: FamixaCharStill[] = [];
  for (const short of shorts) {
    const chars = short.characters ?? [];
    if (chars.length === 0) {
      incoming.push(newStillRow({ shortId: short.id, scene: short.scene ?? '', charCode: '' }));
      continue;
    }
    for (const charCode of chars) {
      incoming.push(
        newStillRow({
          shortId: short.id,
          scene: short.scene ?? '',
          charCode,
          note: noteOf(charCode),
        }),
      );
    }
  }
  for (const shot of shots) {
    const chars = shot.characters ?? [];
    if (chars.length === 0) {
      incoming.push(newStillRow({ shortId: shot.id, scene: shot.scene, charCode: '' }));
      continue;
    }
    for (const charCode of chars) {
      incoming.push(
        newStillRow({
          shortId: shot.id,
          scene: shot.scene,
          charCode,
          note: noteOf(charCode),
        }),
      );
    }
  }
  for (const r of refs) {
    incoming.push(
      newStillRow({
        shortId: r.shortId,
        scene: r.scene,
        charCode: r.charCode,
        note: r.note,
      }),
    );
  }
  return mergeStills([], incoming);
}

const EPISODE_CAST: { id: string; names: string[]; role: string }[] = [
  { id: 'CHAR-001', names: ['minh', 'con'], role: 'Con' },
  { id: 'CHAR-002', names: ['nam', 'bố', 'bo', 'ba'], role: 'Bố' },
  { id: 'CHAR-003', names: ['linh', 'mẹ', 'me'], role: 'Mẹ' },
];

export function looksLikeShotPack(text: string) {
  return /^---\s*(SHORT|SHOT|LONG|REF|STILL)\s*---/im.test(text);
}

function looksLikeSingleShotMaster(text: string) {
  const id = text.match(/^(?:VIDEO[ _]?ID)\s*:\s*(.+)$/im)?.[1]?.trim() ?? '';
  const { scene, shot } = parseSceneFromVideoId(id);
  return Boolean(scene && shot);
}

function looksLikeEpisodeScript(text: string) {
  if (looksLikeSingleShotMaster(text) && !/0?7\.\s*SCRIPT\b/i.test(text)) return false;
  return (
    /FAMIXA/i.test(text) ||
    /EPISODE\s*0*\d+/i.test(text) ||
    /\bEP\s*0*\d+/i.test(text) ||
    /BỐ ĐỪNG HỨA NỮA|BỐ MẸ KHÔNG CÃI/i.test(text) ||
    /^(?:SCENE|SC|CẢNH|CANH)\s*0*\d+/im.test(text) ||
    /^CHAR-\d+/im.test(text) ||
    /^(?:MINH|NAM|LINH|BỐ|MẸ|BA|CON|CHAR-\d+)\s*[:：]/im.test(text) ||
    /^(?:MINH|NAM|LINH)\s*$/im.test(text) ||
    /(?:^|\n)07\.\s*SCRIPT\b/i.test(text) ||
    /^(?:cuối\s*episode|mục tiêu)\s*:?\s*$/im.test(text)
  );
}

export function stripDialogue(raw: string) {
  return raw
    .trim()
    .replace(/^["“”']+|["“”']+$/g, '')
    .replace(/\s+/g, ' ')
    .trim();
}

function resolveEpisodeSpeaker(raw: string, chars: FamixaCharacter[]): FamixaCharacter | undefined {
  const token = raw.replace(/\s*\(.*\)\s*$/, '').trim();
  if (!token) return undefined;
  const asId = /^CHAR-\d+/i.test(token) ? normCharId(token) : '';
  if (asId) return chars.find((c) => c.id === asId);
  const key = token.toLowerCase();
  const known = EPISODE_CAST.find((c) => c.names.includes(key));
  if (known) return chars.find((c) => c.id === known.id) ?? { id: known.id, name: token, role: known.role };
  return chars.find((c) => c.name.toLowerCase() === key);
}

export function ensureEpisodeChar(chars: FamixaCharacter[], speaker: string): FamixaCharacter {
  const hit = resolveEpisodeSpeaker(speaker, chars);
  if (hit) {
    if (!chars.some((c) => c.id === hit.id)) chars.push(hit);
    return hit;
  }
  let n = chars.length + 1;
  while (chars.some((c) => c.id === `CHAR-${String(n).padStart(3, '0')}`)) n += 1;
  const row: FamixaCharacter = { id: `CHAR-${String(n).padStart(3, '0')}`, name: speaker.trim() };
  chars.push(row);
  return row;
}

export function isEpisodeMetaLine(line: string) {
  return /^(cuối\s*episode|mục tiêu|âm thanh|sound(?:\s*design)?|emotion|cảm xúc|nhạc|continuity|style|format|target duration|video id|video title|production purpose|season|project)\s*:?\s*$/i.test(
    line.trim(),
  );
}

export function sceneHead(line: string) {
  const m = line.match(/^(?:#{1,3}\s*)?(?:SCENE|SC|CẢNH|CANH)\s*0*(\d+)\b\s*[—–:\-.]?\s*(.*)$/i);
  if (m) return { n: Number(m[1]), title: (m[2] ?? '').trim() };
  const numbered = line.match(/^(?:#{1,3}\s*)?(\d{1,2})\s*[\.\)]\s*(?:SCENE|CẢNH|CANH)\b\s*[—–:\-.]?\s*(.*)$/i);
  if (numbered) return { n: Number(numbered[1]), title: (numbered[2] ?? '').trim() };
  return undefined;
}

export function shotHead(line: string) {
  const m = line.match(/^(?:#{1,3}\s*)?(?:SHOT|SH|BEAT|KHOẢNH KHẮC)\s*0*(\d+)\b\s*[—–:\-.]?\s*(.*)$/i);
  if (!m) return undefined;
  return { n: Number(m[1]), title: (m[2] ?? '').trim() };
}

function speakerHead(line: string) {
  const m = line.match(
    /^(CHAR-\d+|[A-ZÀ-Ỵ][A-ZÀ-Ỵa-zà-ỹ]{1,24})(?:\s*\(([^)]{0,48})\))?\s*[:：]\s*(.*)$/,
  );
  if (!m) return undefined;
  const name = (m[1] ?? '').trim();
  if (/^(SCENE|SC|SHOT|SH|BEAT|ROLE|VIDEO|FAMIXA|FORMAT|STYLE|NOTE|LOC|PROJECT|SEASON|EPISODE|STATUS)$/i.test(name)) {
    return undefined;
  }
  return { name, emotion: (m[2] ?? '').trim(), rest: (m[3] ?? '').trim() };
}

export function extractEpisodeScriptBody(text: string) {
  const start = text.search(/^(?:#{1,3}\s*)?(?:0?7\.\s*)?SCRIPT\b|^(?:#{1,3}\s*)?SCENE\s*0*1\b/im);
  if (start < 0) return text;
  const rest = text.slice(start);
  const end = rest.search(/\n(?:#{1,3}\s*)?(?:0?8\.\s*EMOTIONAL|0?9\.\s*VOICE|10\.\s*SOUND)\b/im);
  return (end > 0 ? rest.slice(0, end) : rest).trim();
}

export function skipScriptFurniture(line: string) {
  return (
    /^(?:#{1,3}\s*)?(?:0?7\.\s*)?SCRIPT\b/i.test(line) ||
    /^(?:CUT TO BLACK|END|FADE OUT|FADE IN)\.?\s*$/i.test(line) ||
    /^\d{1,2}\.\s+[A-ZÀ-Ỵ][A-ZÀ-ỴA-Z\s]{2,40}$/i.test(line)
  );
}

export function screenplaySpeaker(line: string, chars: FamixaCharacter[]) {
  const vo = line.match(/^(?:VOICE\s*OVER|V\.?\s*O\.?)\s*[—–:-]\s*(.+)$/i);
  if (vo) return vo[1].trim();
  const colon = speakerHead(line);
  if (colon) return colon.name;
  const token = line.replace(/\s*\([^)]{0,48}\)\s*$/, '').trim();
  if (!token || token.length > 28 || /[:：]/.test(line)) return undefined;
  if (/^(SCENE|SC|INT|EXT|CUT|END|FADE|TITLE|SCRIPT|SHOT|BEAT|FAMIXA|SERIES|SEASON|EPISODE)$/i.test(token)) {
    return undefined;
  }
  if (
    resolveEpisodeSpeaker(token, chars) ||
    EPISODE_CAST.some((c) => c.names.includes(token.toLowerCase())) ||
    /^(MINH|NAM|LINH|BỐ|MẸ|BA|CON)$/i.test(token)
  ) {
    return token;
  }
  return undefined;
}

function isPackBannerLine(line: string) {
  return (
    /^[=\-]{6,}$/.test(line) ||
    /^(KIT MARKETING|FAMIXA|VIDEO PRODUCTION MASTER)\b/i.test(line) ||
    /^(VIDEO[ _]?ID|VIDEO[ _]?TITLE|TARGET DURATION|FORMAT|STYLE|PROJECT|SEASON|EPISODE|SERIES|STATUS|SOURCE KEYFRAME|PRODUCTION PURPOSE)\s*:/i.test(
      line,
    )
  );
}

export function locHead(line: string) {
  const m = line.match(/^(?:INT|EXT)\.?\s*[/.—–-]?\s*(.+)$/i);
  if (!m) return undefined;
  const title = (m[1] ?? '').trim();
  return title ? { title } : undefined;
}

export function beatHead(line: string) {
  const m = line.match(
    /^(?:#{1,3}\s*)?(?:ở\s*đoạn|đoạn|khoảnh khắc|mở đầu(?:\s*episode)?|cuối cảnh)\b\s*[—–:\-.]?\s*(.*)$/i,
  );
  if (!m || line.length > 90) return undefined;
  const title = ((m[1] || line).replace(/:$/, '')).trim();
  return { title };
}

export function seedEpisodeCast(text: string, characters: FamixaCharacter[]) {
  for (const row of EPISODE_CAST) {
    const proper = row.names[0] ?? '';
    const named = characters.find((c) => c.id === row.id);
    if (named) {
      if (!named.name) named.name = proper.charAt(0).toUpperCase() + proper.slice(1);
      if (!named.role) named.role = row.role;
      continue;
    }
    if (proper.length < 3 || !new RegExp(proper, 'i').test(text)) continue;
    characters.push({
      id: row.id,
      name: proper.charAt(0).toUpperCase() + proper.slice(1),
      role: row.role,
    });
  }
}

function stripEpisodeMetaTail(text: string) {
  return text.replace(/\n(?:cuối\s*episode|mục tiêu)\s*:?[\s\S]*$/i, '').trim();
}

export function proseBeats(text: string, episodeTitle: string): { sceneN: number; title: string; body: string }[] {
  const paras = stripEpisodeMetaTail(text)
    .split(/\n\s*\n/)
    .map((p) => p.replace(/\s+/g, ' ').trim())
    .filter(Boolean);
  const out: { sceneN: number; title: string; body: string }[] = [];
  let sceneN = 1;
  for (const p of paras) {
    if (isPackBannerLine(p) || (/^FAMIXA\b/i.test(p) && p.length < 80)) continue;
    if (episodeTitle && p.toLowerCase() === episodeTitle.toLowerCase()) continue;
    if (/^CHAR-\d+/i.test(p) && p.length < 96) continue;
    const loc = p.length < 80 && /bàn ăn|phòng khách|nhà bếp|phòng ngủ|cửa hàng|trường học|sân/i.test(p);
    if (loc && out.length) sceneN += 1;
    out.push({ sceneN, title: loc ? p.slice(0, 48) : '', body: p });
  }
  return out;
}

/** Kịch bản tập → graph. Không viết thêm thoại / cảnh. */
function parseEpisodeScript(text: string) {
  if (!looksLikeEpisodeScript(text)) return undefined;
  return parseEpisodeStory(text);
}


export function parseFamixaPack(raw: string): {
  episode?: FamixaSeriesEpisode;
  shorts: FamixaShortClip[];
  roles: SeriesRoleRow[];
  stills: FamixaCharStill[];
  characters: FamixaCharacter[];
  scenes: FamixaSceneNode[];
  lines: FamixaLine[];
  warnings?: string[];
  error?: string;
} {
  try {
    return parseFamixaPackInner(raw);
  } catch (e) {
    return {
      shorts: [],
      roles: [],
      stills: [],
      characters: [],
      scenes: [],
      lines: [],
      error: e instanceof Error ? e.message : 'Không đọc được pack.',
    };
  }
}

function parseFamixaPackInner(raw: string): {
  episode?: FamixaSeriesEpisode;
  shorts: FamixaShortClip[];
  roles: SeriesRoleRow[];
  stills: FamixaCharStill[];
  characters: FamixaCharacter[];
  scenes: FamixaSceneNode[];
  lines: FamixaLine[];
  warnings?: string[];
  error?: string;
} {
  const emptyGraph = { characters: [] as FamixaCharacter[], scenes: [] as FamixaSceneNode[], lines: [] as FamixaLine[] };
  const text = raw.replace(/^\uFEFF/, '').trim();
  if (!text) return { shorts: [], roles: [], stills: [], ...emptyGraph, error: 'Dán yêu cầu vào ô trống — KIT không điền sẵn chuyện.' };

  const finishEpisode = (doc: NonNullable<ReturnType<typeof parseEpisodeScript>>) => {
    const stills = stillsFromShortsAndRefs([], [], doc.shots, parseCharLocks(text));
    const graph = ensurePilotGraph({
      ...emptyPilot(),
      episode: doc.episode,
      shorts: [],
      roles: doc.roles.length ? doc.roles : parseRoleLines(text),
      stills,
      characters: doc.characters,
      scenes: doc.scenes,
      lines: doc.lines,
      packDraft: text,
    });
    return {
      episode: graph.episode,
      shorts: graph.shorts ?? [],
      roles: graph.roles,
      stills,
      characters: graph.characters ?? [],
      scenes: graph.scenes ?? [],
      lines: graph.lines ?? [],
      warnings: doc.warnings,
    };
  };

  const hasSceneScript =
    /0?7\.\s*SCRIPT\b/i.test(text) || /^(?:SCENE|SC|CẢNH|CANH)\s*0*\d+/im.test(text);
  if (hasSceneScript && looksLikeEpisodeScript(text)) {
    const episodeDoc = parseEpisodeScript(text);
    if (episodeDoc && (episodeDoc.scenes.length > 0 || episodeDoc.shots.length > 0)) {
      return finishEpisode(episodeDoc);
    }
  }

  const firstBar = text.search(/^---\s*(SHORT|SHOT|LONG|REF|STILL)\s*---/im);
  const headerText = firstBar >= 0 ? text.slice(0, firstBar) : text;
  const header = fieldLines(headerText);
  const blocks = splitBlocks(text);

  const shorts: FamixaShortClip[] = [];
  const shots: FamixaSeriesShot[] = [];
  const refs: { charCode: string; scene: string; shortId: string; note?: string }[] = [];
  for (const b of blocks) {
    const f = fieldLines(b.body);
    if (b.kind === 'SHORT') {
      const row = toShort(f, shorts.length);
      if (row) shorts.push(row);
    } else if (b.kind === 'REF') {
      const charCode = (f.CHAR ?? '').trim();
      const shortId = (f.SHORT ?? f.ID ?? '').trim();
      if (charCode || shortId || (f.SCENE ?? '').trim()) {
        refs.push({
          charCode,
          scene: (f.SCENE ?? '').trim(),
          shortId,
          note: (f.NOTE ?? '').trim() || undefined,
        });
      }
    } else {
      const row = toShot(f, shots.length);
      if (row) shots.push(row);
    }
  }

  if (shorts.length === 0 && shots.length === 0) {
    const master = parseMasterVideos(text, header);
    if (!(looksLikeEpisodeScript(text) && master.shots.length <= 1 && !looksLikeSingleShotMaster(text))) {
      shorts.push(...master.shorts);
      shots.push(...master.shots);
    }
  }

  if (shorts.length === 0 && shots.length === 0) {
    const episodeDoc = parseEpisodeScript(text);
    if (episodeDoc && episodeDoc.shots.length > 0) return finishEpisode(episodeDoc);
    return {
      shorts: [],
      roles: parseRoleLines(text),
      stills: [],
      ...emptyGraph,
      error:
        'Không đọc được pack. Dán kịch bản tập (FAMIXA / EPISODE / SCENE / thoại CHAR), hoặc --- SHORT --- / --- SHOT ---, hoặc VIDEO ID shot (…SC01-SH01).',
    };
  }

  const episode: FamixaSeriesEpisode = {
    seriesCode: (header.SERIES ?? header.PROJECT ?? '').trim(),
    seriesTitle: (header.SERIES_TITLE ?? header.SEASON ?? '').trim(),
    episode: (header.EP ?? header.EPISODE ?? '').trim(),
    title: (header.TITLE ?? header.VIDEO_TITLE ?? header.EPISODE ?? '').trim(),
    premise: (header.PREMISE ?? header.STYLE ?? '').trim(),
    moral: (header.MORAL ?? '').trim(),
    ctaRule: (header.CTA ?? header.FORMAT ?? '').trim(),
    shots: shots.slice(0, 24),
  };

  const sliced = shorts.slice(0, 12);
  const roles = parseRoleLines(headerText || text);
  const stills = stillsFromShortsAndRefs(sliced, refs, episode.shots, parseCharLocks(text));
  const graph = ensurePilotGraph({
    ...emptyPilot(),
    episode,
    shorts: sliced,
    roles,
    stills,
    packDraft: text,
  });
  return {
    episode: graph.episode,
    shorts: graph.shorts ?? sliced,
    roles: graph.roles,
    stills,
    characters: graph.characters ?? [],
    scenes: graph.scenes ?? [],
    lines: graph.lines ?? [],
  };
}

/** Drop “next shot / SC02” so the operator edits the current SH only. */
export function stripNextShotRule(text: string) {
  return text
    .replace(/\n13\.\s+PRODUCTION RULE[\s\S]*?(?=END OF PRODUCTION MASTER|$)/i, '')
    .replace(/\nNext production target:[^\n]*/gi, '')
    .trimEnd();
}

export function extractVideoMasterChunk(raw: string | undefined, videoId: string): string | undefined {
  const text = (raw ?? '').replace(/^\uFEFF/, '').trim();
  if (!text || !videoId.trim()) return undefined;
  const want = videoId.trim().toUpperCase();
  const re = /^(?:VIDEO[ _]?ID)\s*:\s*(.+)$/gim;
  const hits: { id: string; start: number }[] = [];
  let m: RegExpExecArray | null;
  while ((m = re.exec(text))) hits.push({ id: (m[1] ?? '').trim().toUpperCase(), start: m.index });
  if (hits.length === 0) {
    return text.toUpperCase().includes(want) ? stripNextShotRule(text) : undefined;
  }
  const i = hits.findIndex((h) => h.id === want || h.id.includes(want));
  if (i < 0) return undefined;
  const header = text.slice(0, hits[0].start);
  const body = text.slice(hits[i].start, hits[i + 1]?.start ?? text.length);
  return stripNextShotRule(`${header}${body}`.trim());
}

export function formatShotMasterPack(
  shot: FamixaSeriesShot,
  episode?: FamixaSeriesEpisode,
  stills: FamixaCharStill[] = [],
): string {
  const refs = stills.filter((s) => s.shortId === shot.id);
  const codes = shot.characters.length > 0 ? shot.characters : refs.map((s) => s.charCode).filter(Boolean);
  const charLines = codes
    .map((code) => {
      const row = refs.find((s) => s.charCode === code);
      return `${code}${row?.note ? ` — ${row.note}` : ''}`;
    })
    .join('\n');
  return stripNextShotRule(`FAMIXA — KIT MARKETING VIDEO PRODUCTION MASTER

PROJECT: ${episode?.seriesTitle || episode?.seriesCode || 'FAMIXA'}
EPISODE: ${episode?.episode || ''}
VIDEO ID: ${shot.id}
VIDEO TITLE: ${shot.story}
TARGET DURATION: ${shot.seconds} seconds
FORMAT: 16:9
STYLE: ${shot.visual || episode?.premise || ''}
LOCATION: ${shot.location}

03. CHARACTER LOCK
${charLines || '(CHAR từ pack)'}

06. VIDEO MOTION PROMPT
${shot.motionPrompt}

07. NEGATIVE MOTION PROMPT
${shot.negativePrompt ?? ''}

Đang sửa ${shot.id} — chưa sang shot sau.
`);
}

export function packForShotEdit(
  shot: FamixaSeriesShot,
  episode: FamixaSeriesEpisode | undefined,
  packDraft: string | undefined,
  livePaste: string | undefined,
  stills: FamixaCharStill[] = [],
) {
  return (
    extractVideoMasterChunk(packDraft, shot.id) ||
    extractVideoMasterChunk(livePaste, shot.id) ||
    formatShotMasterPack(shot, episode, stills)
  );
}

/** V02 QC, short enough for Runway — same blocking: Nam talks, Minh listens, Linh smiles. */
export const RUNWAY_V02_MOTION = `A warm Vietnamese family of three at dinner at night. Nam on the left talks naturally to Minh in the center and keeps his eyes on Minh the whole shot — never looks at the camera. One or two very small hand gestures only. Minh stays quiet, listens, looks at Nam, small realistic face movement. Linh on the right watches them with a gentle smile, does not speak, no talking gestures. Same faces, hair, clothes, seats, table, food, room and night light as the input image. Subtle live-action only: blink, breathe. Very slow push-in, eye-level, 35mm. No shake, no pan, no rotation, no zoom jump, no cut. 5 seconds.`;

export const RUNWAY_V02_NEGATIVE = `No looking at camera. No talking to camera. No Linh speaking. No large gestures. No exaggerated acting. No smoke or fog. No location change. No face morph. No extra people. No deformed hands. No text, logo, watermark. No camera shake, pan, rotation, or cut.`;

/** SH02 V02 — Minh asks once, Nam listens. Keep KF02. */
export const RUNWAY_SH02_V02_MOTION = `A warm Vietnamese family of three at dinner at night. Same faces, clothes, seats, table, food, room and night light as the input image. Minh in the center looks at Nam on the left for the entire 5 seconds. Minh speaks only once, asking Nam a short question. Minh's mouth moves only while asking, then stays still. Minh does not look at the camera or straight ahead. No repeated head turns. Nam stays silent the whole shot and only listens, eyes on Minh. One or two very small listening reactions. Nam never looks at the camera. Linh on the right stays silent, watches them with a gentle smile, no talking gestures. Subtle live-action: blink, breathe. Very slow push-in, eye-level, 35mm. No shake, no pan, no rotation, no cut. 5 seconds.`;

export const RUNWAY_SH02_V02_NEGATIVE = `No looking at camera. No talking to camera. No Nam speaking. No Linh speaking. No Minh looking straight ahead. No repeated head turns. No large gestures. No exaggerated acting. No face morph. No extra people. No deformed hands. No text, logo, watermark. No camera shake, pan, rotation, or cut.`;

const SAFE_I2V =
  'Cinematic live-action dinner scene. The photo is the first frame only. ' +
  'Start motion right away: people blink and breathe, hands serve rice, chopsticks, steam from bowls, ' +
  'small head turns, soft eye contact, gentle camera drift. Keep the same seats and faces. No captions.';

const PACK_RE = /VIDEO\s*ID|PRODUCTION MASTER|=======|CHAR-\d+\s*[—–-]/i;
const HARD_RE = /nude|sex|porn|kill|blood|abuse|suicide|weapon|gun|cãi|đánh|máu/i;
const VI_RE = /[àáạảãăắằặẳẵâấầậẩẫèéẹẻẽêếềệểễìíịỉĩòóọỏõôốồộổỗơớờợởỡùúụủũưứừựửữỳýỵỷỹđ]/i;
const WARN_RE = /11-year-old|year-old|child|minor|argue|crying|fight|shouting/i;

function compactAvoid(negative?: string) {
  const t = (negative ?? '').trim();
  if (!t) return '';
  const keep = t
    .split(/\n+/)
    .map((s) => s.replace(/^No\s+/i, '').trim())
    .filter((s) => s.length > 0 && !HARD_RE.test(s) && !WARN_RE.test(s) && !PACK_RE.test(s))
    .slice(0, 10);
  return keep.join(', ');
}

/** Prefer pasted Runway motion. Do not send the Story pack. */
export function turboI2vPrompt(motion?: string, negative?: string) {
  const m = (motion ?? '').trim();
  const usable = m.length > 20 && !PACK_RE.test(m) && !VI_RE.test(m);
  const body = usable ? m : SAFE_I2V;
  const avoid = compactAvoid(negative);
  const text = `${body}${avoid ? `\n\nAvoid: ${avoid}` : ''}`.replace(/\s+\n/g, '\n').trim();
  return text.length <= 980 ? text : text.slice(0, 980);
}

export type TurboPreflight = { ok: boolean; reasons: string[]; warnings: string[]; prompt: string };

/** Local gate only — Runway has no free moderation API; SAFETY still bills. */
export function preflightTurboSend(opts: { prompt: string; imageDataUrl?: string }): TurboPreflight {
  const reasons: string[] = [];
  const warnings: string[] = [];
  const prompt = (opts.prompt ?? '').trim();
  const image = (opts.imageDataUrl ?? '').trim();
  if (!image.startsWith('data:image/') && !/^https?:\/\//i.test(image)) {
    reasons.push('Chưa có ảnh cảnh (KF).');
  }
  if (!prompt) reasons.push('Chưa có prompt I2V.');
  if (PACK_RE.test(prompt)) reasons.push('Đừng dán giấy Story (VIDEO ID / MASTER) vào ô Motion.');
  if (HARD_RE.test(prompt)) reasons.push('Prompt còn từ Runway hay chặn (máu / bạo lực / nude).');
  if (VI_RE.test(prompt)) reasons.push('Prompt tiếng Việt — lần trước bị chặn.');
  if (prompt.length > 900) warnings.push('Prompt gần 1000 ký tự — Runway cắt / dễ chặn.');
  if (WARN_RE.test(prompt)) {
    warnings.push('Có chữ tuổi trẻ / argue / crying — moderation có thể trừ credit.');
  }
  return { ok: reasons.length === 0, reasons, warnings, prompt };
}

export type StudioPrecheckItem = { id: string; ok: boolean; label: string };

export type StudioI2vPrecheck = {
  ok: boolean;
  items: StudioPrecheckItem[];
  warnings: string[];
  prompt: string;
};

const WARDROBE_DELTA =
  /\b(đổi|thay|change|swap|new outfit|áo mới|quần mới).{0,24}(áo|quần|outfit|wardrobe|dress|shirt|clothes)\b/i;

/** Gate trên ô Tạo video — 0 cr. Credit chỉ sau confirm. */
export function studioI2vPrecheck(opts: {
  lock: SceneContinuityLock;
  action?: string;
  keyframeDataUrl?: string;
  status?: SeriesShotStatus;
  unlocked: boolean;
  sceneLocked?: boolean;
  scriptLocked?: boolean;
  shortsReady?: boolean;
  engine: 'turbo' | 'wan';
  hasEngineKey: boolean;
  state?: SeriesPilotState;
  shot?: FamixaSeriesShot;
  videoContext?: string;
}): StudioI2vPrecheck {
  const action = (opts.action ?? '').trim();
  const hasKf = Boolean((opts.keyframeDataUrl ?? '').trim());
  const kfApproved = hasKf && opts.status !== 'story_locked';
  const prompt =
    opts.state && opts.shot && opts.lock.locked && action
      ? compileI2vPrompt(opts.state, opts.shot, action, opts.videoContext)
      : opts.lock.locked && action
        ? inheritedTurboPrompt(opts.lock, action)
        : '';
  const items: StudioPrecheckItem[] = [
    { id: 'script', ok: Boolean(opts.scriptLocked), label: 'Kịch bản đã khóa' },
    {
      id: 'shorts',
      ok: Boolean(opts.shortsReady),
      label: opts.shortsReady ? 'Short đã xong / không có short' : 'Khóa hết short 9:16 trước khi dựng cảnh',
    },
    {
      id: 'scene',
      ok: !opts.sceneLocked,
      label: opts.sceneLocked ? 'Scene đã Final — mở khóa trên Timeline để làm lại' : 'Scene chưa Final',
    },
    { id: 'prev', ok: opts.unlocked, label: 'Được phép làm shot này' },
    { id: 'lock', ok: opts.lock.locked, label: 'Continuity đã khóa' },
    { id: 'action', ok: Boolean(action), label: 'Shot Action đã nhập' },
    { id: 'kf', ok: hasKf, label: 'Có ảnh keyframe cảnh' },
    { id: 'kfa', ok: kfApproved, label: 'Đã duyệt keyframe' },
    {
      id: 'key',
      ok: opts.hasEngineKey,
      label: opts.engine === 'wan' ? 'Đã có key Fal' : 'Đã có key Runway',
    },
  ];
  if (opts.state && opts.shot) {
    const ids = shotCharacterIds(opts.shot);
    items.push({
      id: 'chars',
      ok: ids.length > 0,
      label: ids.length ? `Shot có ${ids.length} CHAR trên graph` : 'Shot chưa gắn CHAR trên graph',
    });
    const scene = sceneNodeOf(opts.state, opts.shot);
    items.push({
      id: 'graph-scene',
      ok: Boolean(scene?.environment || opts.lock.environment),
      label: scene?.environment || opts.lock.environment ? 'Scene Memory có bối cảnh' : 'Scene Memory thiếu bối cảnh',
    });
    items.push({
      id: 'conflict',
      ok: !WARDROBE_DELTA.test(action),
      label: WARDROBE_DELTA.test(action)
        ? 'Action đổi trang phục — đụng Memory wardrobe'
        : 'Action không đụng wardrobe đã khóa',
    });
  }
  if (prompt && hasKf) {
    const gate = preflightTurboSend({ prompt, imageDataUrl: opts.keyframeDataUrl });
    items.push({
      id: 'prompt',
      ok: gate.ok,
      label: gate.ok ? 'Prompt I2V đạt (0 cr)' : gate.reasons[0] || 'Prompt I2V chưa đạt',
    });
    return { ok: items.every((i) => i.ok), items, warnings: gate.warnings, prompt };
  }
  items.push({ id: 'prompt', ok: false, label: 'Prompt I2V — cần Memory + Action + KF' });
  return { ok: false, items, warnings: [], prompt };
}
