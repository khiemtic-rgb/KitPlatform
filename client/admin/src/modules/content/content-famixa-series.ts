/** Famixa Series pilot — nhận khối dán. KIT không điền sẵn chuyện. */

import { parseEpisodeStory } from './content-famixa-story-parse';
import { ensureStoryMemory, inheritStoryMemory, needsInheritanceReview, type FamixaStoryMemory } from './content-famixa-story-memory';
import { deriveVoiceScript, mergeVoiceGenerated, voiceProductionReady, type FamixaVoicePreview } from './content-famixa-voice-script';
import { canonPixelsOf, loadCanonPixels, rememberCanonFromChars, rememberCanonPixels } from './content-famixa-canon-store';
import { kfPixelsOf, loadKfPixels, rememberKfFromRuns, rememberKfPixels, saveKfPixels } from './content-famixa-kf-store';
import { famixaCanonSeedFor } from './content-famixa-canon-seed';
import { displayUrlForData, stripJsonDataUrls } from './content-famixa-blob-url';
import { englishI2vMotion, I2V_VI_RE } from './content-famixa-i2v-en';
import { ACTING_LAW_LOCK, actingI2vBrief, actingI2vBriefFromLines, inferActingDirection } from './content-famixa-acting-law';
import { mergeKeepDialoguePerformance } from './content-famixa-performance';
import { looksLikeVoiceDirection } from './content-famixa-story-parse';
import { setFamixaMediaScope } from './content-famixa-media-scope';
import { mergeKeepFinalSource } from './content-famixa-final-source';
import { compileVisualPrompt, type VisualSpec } from './content-famixa-visual-spec';
import {
  FAMIXA_CANON_VERSION,
  frameCanonIds,
  isMetaCanonSpeaker,
  persistFamixaCanonLaw,
  resolveCanonSpeaker,
  seedFamixaCanon,
} from './content-famixa-char-canon';

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
  nextShotId?: string;
  inheritFromShotId?: string;
  /** Script beat this shot was decomposed from. No beat → not production. */
  beatId?: string;
  beatText?: string;
  /** Explicit Voice Script line ids. [] = NONE. Missing = not locked yet. */
  dialogueSegmentIds?: string[];
  /** Auto Short nối thoại >10s — cùng Action/KF với shot gốc. Không bịa beat. */
  voiceChainFrom?: string;
  /** Edit length from voice + pause. I2V stays 5 or 10; assemble trims. */
  editSeconds?: number;
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
  /** Locked Voice Bible — do not change Voice ID between episodes. */
  voiceBible?: import('./content-famixa-performance').VoiceBible;
  line?: string;
  performance?: string;
  canonFileName?: string;
  canonLocalPath?: string;
  /** Session only — slimPilot strips this. */
  canonImageDataUrl?: string;
  /** An / VO / extra — named in script, not a body in the still. */
  offFrame?: boolean;
};

export type FamixaSceneDialogue = {
  id: string;
  characterId: string;
  text: string;
  emotion?: string;
  performance?: import('./content-famixa-acting-law').LinePerformance;
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
  /** Script beats that produced shots. Shot graph follows these, not a target count. */
  scriptBeats?: { id: string; text: string; shotIds: string[] }[];
};

export type FamixaLine = {
  id: string;
  characterId: string;
  text: string;
  voiceId?: string;
  sceneId?: string;
  performance?: import('./content-famixa-acting-law').LinePerformance;
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
  performance: ACTING_LAW_LOCK,
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
    characters: [],
    characterIds: [],
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

export function studioShotCode(shot?: FamixaSeriesShot, allShots?: FamixaSeriesShot[]) {
  if (!shot) return 'SH';
  if (allShots?.length) {
    const i = allShots.findIndex((s) => s.id === shot.id);
    if (i >= 0) return `SH01-${String(i + 1).padStart(2, '0')}`;
  }
  const frame = (codeMatch(shot.shot, 'SH') || codeMatch(shot.id, 'SH') || 'SH01')
    .replace(/\D/g, '')
    .padStart(2, '0');
  return `SH01-${frame}`;
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
  for (const raw of [action, story]) {
    const t = (raw ?? '').replace(/\s+/g, ' ').trim();
    if (!t || looksLikePackHeading(t)) continue;
    const line = t.split(/(?<=[.!?])\s|\n/)[0] ?? t;
    return line.length > 64 ? `${line.slice(0, 64)}…` : line;
  }
  return 'Chưa có mô tả shot';
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
    return {
      tone: 'locked' as StudioShotTone,
      label: 'Đã khóa',
      hint: r.lipsynced ? 'KF đã duyệt · Video đã khóa · KHỚP MÔI' : 'KF đã duyệt · Video đã khóa',
    };
  }
  if (r.status === 'rejected' || r.turboError) {
    return { tone: 'error' as StudioShotTone, label: 'Lỗi', hint: (r.turboError || 'Cần làm lại').slice(0, 48) };
  }
  if (r.status === 'turbo_testing') {
    return { tone: 'on' as StudioShotTone, label: 'Đang tạo video', hint: 'Đợi take' };
  }
  if (r.status === 'reviewed' || hasTake) {
    return {
      tone: 'warn' as StudioShotTone,
      label: r.lipsynced ? 'KHỚP MÔI' : 'Cần kiểm tra',
      hint: r.lipsynced ? 'Take đã khớp môi · chưa khóa' : 'Có take · chưa khóa',
    };
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
  const src = lock.sourceShotId || lock.id;
  const motion = englishI2vMotion(action, seconds);
  const acting = actingI2vBrief(inferActingDirection({ action }));
  return (
    `Same scene as ${src}. Same wardrobe, seats and lighting as the still. ` +
    `${motion} ${acting}`
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
  /** Cumulative Runway cr on Start (fail still bills). */
  runwayBilled?: number;
  model?: string;
  notes?: string;
  review?: Partial<Record<SeriesReviewAxis, boolean>>;
  previewUrl?: string;
  /** Mute Runway take. Fal FINAL lives in lipsyncUrl — do not overwrite this. */
  takeUrl?: string;
  /** Previous take URLs — latest generate overwrites previewUrl. */
  takeHistory?: { url: string; taskId?: string }[];
  localVideoPath?: string;
  turboTaskId?: string;
  turboStatus?: string;
  turboError?: string;
  /** Derived I2V pipe — HTTP 200 ≠ VIDEO READY. */
  videoPipe?: import('./content-famixa-runway-pipe').VideoPipeStatus;
  videoVerified?: boolean;
  videoBytes?: number;
  videoMime?: string;
  runwayAttempts?: import('./content-famixa-runway-pipe').RunwayAttempt[];
  kfCheck?: import('./content-famixa-runway-pipe').KfInputCheck;
  sentKfCheck?: import('./content-famixa-runway-pipe').KfInputCheck;
  runwayDiagnostics?: import('./content-famixa-runway-pipe').RunwayDiagRow[];
  /** Operator duyệt KF mới sau INTERNAL — cho đúng 1 job, không spam cùng ảnh. */
  kfRetryOk?: boolean;
  /** Fal sync-lipsync take — mouth follows TTS. Assemble keeps this audio. */
  lipsynced?: boolean;
  /** Fal output URL. Kept even if previewUrl is later replaced. */
  lipsyncUrl?: string;
  /** Timeline SoT — FAL keeps Fal audio; RUNWAY_TTS is preview-only. */
  finalSource?: import('./content-famixa-final-source').FinalSource;
  /** Operator locked Start/End after KF approve — do not re-derive. */
  stateLocked?: boolean;
  shotQa?: {
    action?: boolean;
    continuity?: boolean;
    motion?: boolean;
    dialogue?: boolean;
    lipsync?: boolean;
    /** Voice vs Face — same Performance Plan. Fail → fix KF, not Fal. */
    voiceFace?: boolean;
  };
  lipsyncTaskId?: string;
  lipsyncStatus?: string;
  lipsyncError?: string;
  /** Scene start frame (KF01) — not a CHAR face crop. */
  keyframeDataUrl?: string;
  keyframeFileName?: string;
  keyframePath?: string;
  /** Shot LOCK mà KF này copy từ đó (cùng khung cảnh). Pixel copy only — not “drew from previous ref”. */
  keyframeInheritedFrom?: string;
  /** Operator duyệt KF. AI still starts as draft. */
  kfApproved?: boolean;
  /** Operator ép KF mới — bỏ plan REUSE. */
  kfForceNew?: boolean;
  /** Continuity instruction after KIT rewrite — not the raw user complaint. */
  kfTechNote?: string;
  visualSpec?: import('./content-famixa-visual-spec').VisualSpec;
  visualQa?: import('./content-famixa-visual-spec').VisualQa;
  /** Shot dư — không sản xuất. */
  prodSkip?: boolean;
  /** What we send to Runway — not the Story pack. */
  runwayMotion?: string;
  runwayNegative?: string;
  /** Only the delta for this shot — inherits scene continuity. */
  shotAction?: string;
  continuity?: Partial<Record<ContinuityGateId, boolean>>;
  startState?: import('./content-famixa-continuity-chain').ShotBeatState;
  endState?: import('./content-famixa-continuity-chain').ShotBeatState;
  transitionType?: import('./content-famixa-continuity-chain').TransitionType;
};

export type SeriesPilotState = {
  /** pack_content.series_build.id — namespaces local KF/TTS. */
  buildId?: string;
  roles: SeriesRoleRow[];
  runs: Record<string, SeriesShotRun>;
  episode?: FamixaSeriesEpisode;
  shorts?: FamixaShortClip[];
  stills?: FamixaCharStill[];
  sceneLocked?: boolean;
  /** FINAL + KF + I2V share this frame. Choose before first still. */
  outputAspect?: '16:9' | '9:16';
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
  /** Operator approved beat → shot split. Empty SH pruned. */
  shotGraphLocked?: boolean;
  /** Long-form Series/Season/Episode/CHAR/relationship memory — not visual continuity. */
  storyMemory?: FamixaStoryMemory;
  /** Full Voice duyệt xong mới I2V / short. */
  voiceLocked?: boolean;
  voicePreview?: FamixaVoicePreview;
  /** TTS production assets — duration survives F5 via this graph + IndexedDB. */
  voiceAssets?: Record<string, FamixaVoiceAsset>;
  /** Per-scene continuity lock. Shot inherits; Action is the only delta. */
  sceneMasters?: Record<string, import('./content-famixa-scene-first').SceneMaster>;
  /** Famixa Character Canon version applied on load / parse. */
  canonVersion?: number;
  /** Preview range signed off — required for full-EP Final, not for a test cut. */
  previewApproved?: boolean;
  /** Per-scene preview sign-off. */
  sceneApproved?: Record<string, boolean>;
};

export type FamixaVoiceAsset = {
  lineId: string;
  shotId?: string;
  characterId?: string;
  duration: number;
  status: 'ready';
};

function sceneCodeOf(scene?: string) {
  return scene?.match(/SC\s*\d+/i)?.[0]?.replace(/\s+/g, '').toUpperCase() ?? '';
}

export function appendSceneShot(state: SeriesPilotState, scene?: string) {
  const ep = state.episode ?? emptyEpisode();
  const sc = sceneCodeOf(scene || ep.shots.at(-1)?.scene) || 'SC01';
  const lastInScene = ep.shots.filter((s) => sceneCodeOf(s.scene) === sc).at(-1);
  return insertSceneShot(state, { scene: sc, afterId: lastInScene?.id });
}

/** Test helper only. Production UI must not pad empty SH to hit a count. */
export function padSceneShots(state: SeriesPilotState, scene?: string, count = 6) {
  const n = Math.max(1, Math.min(12, Math.floor(count)));
  const sc = sceneCodeOf(scene || state.episode?.shots.at(-1)?.scene) || 'SC01';
  let cur = state;
  const added: FamixaSeriesShot[] = [];
  const inScene = () =>
    (cur.episode?.shots ?? []).filter(
      (s) => sceneCodeOf(s.scene) === sc || sceneCodeOf(s.sceneId) === sc,
    );
  while (inScene().length < n) {
    const next = appendSceneShot(cur, sc);
    cur = next.state;
    added.push(next.shot);
  }
  return { state: cur, added, scene: sc };
}

function nextSceneShotIndex(shots: FamixaSeriesShot[], scene: string, mode: 'first-free' | 'after-max') {
  const used = shots
    .filter((s) => sceneCodeOf(s.scene) === scene || sceneCodeOf(s.sceneId) === scene)
    .map((s) => Number((codeMatch(s.shot, 'SH') || codeMatch(s.id, 'SH') || 'SH0').replace(/\D/g, '') || 0));
  if (mode === 'after-max') return Math.max(0, ...used) + 1;
  let n = 1;
  const set = new Set(used);
  while (set.has(n)) n += 1;
  return n;
}

export function insertSceneShot(
  state: SeriesPilotState,
  opts?: { beforeId?: string; afterId?: string; scene?: string },
) {
  const ep = state.episode ?? emptyEpisode();
  const all = ep.shots;
  const neighbor = all.find((s) => s.id === (opts?.beforeId || opts?.afterId));
  const sc =
    sceneCodeOf(opts?.scene || neighbor?.scene || neighbor?.sceneId || all[0]?.scene) || 'SC01';
  const shot = newSceneShot(
    sc,
    nextSceneShotIndex(all, sc, opts?.beforeId ? 'first-free' : 'after-max'),
  );
  if (neighbor) {
    const ids = shotCharacterIds(neighbor);
    shot.characterIds = ids;
    shot.characters = ids;
    shot.sceneId = neighbor.sceneId || sc;
    shot.location = neighbor.location;
    shot.seconds = neighbor.seconds || 5;
  }
  let shots = all;
  if (opts?.beforeId) {
    const i = all.findIndex((s) => s.id === opts.beforeId);
    shots = i < 0 ? [shot, ...all] : [...all.slice(0, i), shot, ...all.slice(i)];
  } else if (opts?.afterId) {
    const i = all.findIndex((s) => s.id === opts.afterId);
    shots = i < 0 ? [...all, shot] : [...all.slice(0, i + 1), shot, ...all.slice(i + 1)];
  } else {
    shots = [...all, shot];
  }
  return {
    state: ensurePilotGraph({ ...state, episode: { ...ep, shots } }),
    shot,
  };
}

/** First pack shot with story — chèn SH01/SH02 trước mốc này, không đảo thứ tự clip mới. */
export function sceneInsertAnchor(shots: FamixaSeriesShot[]) {
  return shots.find((s) => (s.story || '').trim()) ?? shots[0];
}

export function removeSceneShots(state: SeriesPilotState, ids: string[]) {
  const drop = new Set(ids.filter(Boolean));
  if (!drop.size) return state;
  const ep = state.episode ?? emptyEpisode();
  return {
    ...state,
    episode: { ...ep, shots: ep.shots.filter((s) => !drop.has(s.id)) },
  };
}

function nextShortId(shorts: FamixaShortClip[]) {
  const ids = new Set(shorts.map((s) => s.id));
  let n = 1;
  while (ids.has(`S${String(n).padStart(2, '0')}`)) n += 1;
  return `S${String(n).padStart(2, '0')}`;
}

/** Insert a blank short. beforeId = chèn trước clip đang mở (S04→S08 vẫn giữ KF). */
export function insertShortClip(
  state: SeriesPilotState,
  opts?: { beforeId?: string; afterId?: string },
) {
  const shorts = [...(state.shorts ?? [])];
  const neighbor =
    shorts.find((s) => s.id === (opts?.beforeId || opts?.afterId)) ?? shorts[0];
  const row: FamixaShortClip = {
    id: nextShortId(shorts),
    hook: '',
    visual: '',
    seconds: neighbor?.seconds ?? 7,
    motionPrompt: '',
    motionPromptVi: '',
    scene: neighbor?.scene,
    sceneId: neighbor?.sceneId,
    characters: neighbor?.characters ?? [],
    characterIds: neighbor?.characterIds ?? [],
  };
  let next = shorts;
  if (opts?.beforeId) {
    const i = shorts.findIndex((s) => s.id === opts.beforeId);
    next = i < 0 ? [row, ...shorts] : [...shorts.slice(0, i), row, ...shorts.slice(i)];
  } else if (opts?.afterId) {
    const i = shorts.findIndex((s) => s.id === opts.afterId);
    next = i < 0 ? [...shorts, row] : [...shorts.slice(0, i + 1), row, ...shorts.slice(i + 1)];
  } else {
    next = [...shorts, row];
  }
  return { state: { ...state, shorts: next }, short: row };
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

export type OutputAspect = '16:9' | '9:16';

export function outputAspectOf(state: Pick<SeriesPilotState, 'outputAspect'>): OutputAspect {
  return state.outputAspect === '9:16' ? '9:16' : '16:9';
}

/** Official KF / I2V / FINAL pixels. Gemini must not invent 1344×768 or 1024×1024. */
export function outputFrameOf(aspect?: string) {
  return aspect === '9:16' || aspect === '720:1280' ? { width: 720, height: 1280 } : { width: 1280, height: 720 };
}

export function sceneHasKeyframe(state: SeriesPilotState) {
  return Object.values(state.runs ?? {}).some((r) => Boolean((r.keyframeDataUrl ?? '').trim()));
}

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
    shotGraphLocked: false,
    storyMemory: undefined,
    voiceLocked: false,
    voicePreview: undefined,
  };
}

export function loadSeriesPilot(): SeriesPilotState {
  try {
    let raw = localStorage.getItem(STORAGE_KEY);
    if (!raw) return emptyPilot();
    if (raw.includes('data:image')) {
      raw = stripJsonDataUrls(raw);
      try {
        localStorage.setItem(STORAGE_KEY, raw);
      } catch {
        /* quota */
      }
    }
    const v = JSON.parse(raw) as SeriesPilotState;
    const loaded: SeriesPilotState = {
      buildId: typeof v.buildId === 'string' ? v.buildId : undefined,
      roles: Array.isArray(v.roles) ? v.roles : [],
      runs: v.runs ?? {},
      episode: v.episode,
      shorts: Array.isArray(v.shorts) ? v.shorts : [],
      stills: Array.isArray(v.stills) ? v.stills : [],
      sceneLocked: Boolean(v.sceneLocked),
      outputAspect: v.outputAspect === '9:16' || v.outputAspect === '16:9' ? v.outputAspect : undefined,
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
      shotGraphLocked: v.shotGraphLocked === true ? true : v.shotGraphLocked === false ? false : undefined,
      storyMemory: v.storyMemory && typeof v.storyMemory === 'object' ? v.storyMemory : undefined,
      voiceLocked: Boolean(v.voiceLocked),
      voicePreview: v.voicePreview && typeof v.voicePreview === 'object' ? v.voicePreview : undefined,
      voiceAssets: v.voiceAssets && typeof v.voiceAssets === 'object' ? v.voiceAssets : undefined,
      sceneMasters: v.sceneMasters && typeof v.sceneMasters === 'object' ? v.sceneMasters : undefined,
      canonVersion: typeof v.canonVersion === 'number' ? v.canonVersion : undefined,
      previewApproved: Boolean(v.previewApproved),
    };
    const migrated = slimPilotForStorage(ensurePilotGraph(loaded));
    const hadDeadLipsync = Object.values(loaded.runs ?? {}).some((r) =>
      /404|405|504|403|exhausted|request failed/i.test(r.lipsyncError || ''),
    );
    if ((loaded.schemaVersion ?? 0) < PILOT_SCHEMA || hadDeadLipsync) {
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
    runs[id] = { ...dropDeadLipsync(run), keyframeDataUrl: undefined };
  }
  return {
    ...state,
    stills: (state.stills ?? []).map((s) => ({ ...s, imageDataUrl: undefined })),
    characters: (state.characters ?? []).map((c) => ({ ...c, canonImageDataUrl: undefined })),
    runs,
  };
}

/** Server graph — parsed story only. packDraft stays on the machine. */
export function slimPilotForServer(state: SeriesPilotState): SeriesPilotState {
  const slim = slimPilotForStorage(state);
  return { ...slim, packDraft: undefined };
}

let lastPilotJson = '';

export function saveSeriesPilot(state: SeriesPilotState, preJson?: string) {
  rememberCanonFromChars(state.characters ?? [], state.stills);
  rememberKfFromRuns(state.runs);
  for (const [id, run] of Object.entries(state.runs)) {
    if (run.keyframeDataUrl?.startsWith('data:image')) {
      void saveKfPixels(id, run.keyframeDataUrl, run.keyframeFileName);
    }
  }
  persistFamixaCanonLaw();
  const slim = slimPilotForStorage(state);
  const json = preJson ?? JSON.stringify(slim);
  if (json === lastPilotJson) return;
  lastPilotJson = json;
  try {
    localStorage.setItem(STORAGE_KEY, json);
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
  const mapped = resolveCanonSpeaker(row.name || '') || resolveCanonSpeaker(row.id);
  const id = mapped?.id || normCharId(row.id);
  if (!id || isMetaCanonSpeaker(row.name) || isMetaCanonSpeaker(id)) return;
  const prev = map.get(id);
  map.set(id, {
    id,
    name: mapped?.name || (row.name || prev?.name || '').trim(),
    role: mapped?.role || row.role || prev?.role,
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
    offFrame: mapped ? mapped.visual !== 'frame' : row.offFrame ?? prev?.offFrame,
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
    performance: scene?.performance || base?.performance || ACTING_LAW_LOCK,
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
  if (state.packDraft && charMap.size === 0) {
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
  const nextRoleId = (name?: string) => resolveCanonSpeaker(name || '')?.id;
  const roles = (state.roles ?? []).map((role) => {
    const byId = role.characterId ? charMap.get(normCharId(role.characterId)) : undefined;
    const named = byId
      ?? [...charMap.values()].find((c) => c.name && role.name && c.name.toLowerCase() === role.name.toLowerCase());
    const id = named?.id || (role.name.trim() ? nextRoleId(role.name) : undefined);
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
  return applyCanonToStills(applyFamixaCanonToPilot(next));
}

/** Remap roster + strip off-frame bodies. Runs on every load / parse so the next pack stays correct. */
export function applyFamixaCanonToPilot(state: SeriesPilotState): SeriesPilotState {
  persistFamixaCanonLaw();
  const oldById = new Map((state.characters ?? []).map((c) => [c.id, c]));
  const characters = seedFamixaCanon(state.characters ?? []).map((s) => {
    const old = oldById.get(s.id);
    return {
      ...old,
      ...s,
      name: s.name,
      role: s.role || old?.role,
      offFrame: s.offFrame,
    } as FamixaCharacter;
  });
  const strip = (ids?: string[]) => frameCanonIds(ids ?? []);
  const shots = (state.episode?.shots ?? []).map((s) => {
    const ids = strip(shotCharacterIds(s));
    return { ...s, characterIds: ids, characters: ids.length ? ids : s.characters };
  });
  const shorts = (state.shorts ?? []).map((s) => {
    const ids = strip(shotCharacterIds({ characters: s.characters, characterIds: s.characterIds }));
    return { ...s, characterIds: ids, characters: ids.length ? ids : s.characters };
  });
  const scenes = (state.scenes ?? []).map((sc) => ({
    ...sc,
    characterIds: strip(sc.characterIds),
  }));
  const roles = (state.roles ?? []).flatMap((r) => {
    if (isMetaCanonSpeaker(r.name) || isMetaCanonSpeaker(r.title) || isMetaCanonSpeaker(r.characterId)) return [];
    const row = resolveCanonSpeaker(r.characterId || '') || resolveCanonSpeaker(r.name) || resolveCanonSpeaker(r.title || '');
    if (!row || row.visual === 'mention') return [];
    return [{ ...r, characterId: row.id, name: r.name || row.name, title: r.title || row.role }];
  });
  return {
    ...state,
    characters,
    scenes,
    shorts,
    roles,
    episode: state.episode ? { ...state.episode, shots } : state.episode,
    canonVersion: FAMIXA_CANON_VERSION,
  };
}

function sceneIdOfShot(shot: { scene?: string; id?: string }) {
  return codeMatch(shot.scene, 'SC') || codeMatch(shot.id, 'SC') || '';
}

function sceneCodeKey(raw?: string) {
  return (raw ?? '').replace(/\s+/g, '').toUpperCase().match(/SC\d+/)?.[0] ?? '';
}

export function sceneNodeOf(state: SeriesPilotState, shot?: FamixaSeriesShot) {
  const id = shot?.sceneId || (shot ? sceneIdOfShot(shot) : state.scenes?.[0]?.id);
  const code = sceneCodeKey(id);
  return (state.scenes ?? []).find((s) => s.id === id || sceneCodeKey(s.id) === code);
}

function sceneMasterRecord(state: SeriesPilotState, shot?: FamixaSeriesShot) {
  const node = sceneNodeOf(state, shot);
  const sc = sceneCodeKey(shot?.sceneId || shot?.scene || node?.id);
  if (!sc) return undefined;
  if (state.sceneMasters?.[sc]) return state.sceneMasters[sc];
  return Object.values(state.sceneMasters ?? {}).find((m) => sceneCodeKey(m.sceneId) === sc);
}

export function lockFromGraph(state: SeriesPilotState, shot?: FamixaSeriesShot): SceneContinuityLock {
  const node = sceneNodeOf(state, shot);
  const base = continuityFromGraph(node, state.characters ?? [], state.episode, state.continuity);
  const master = sceneMasterRecord(state, shot);
  const place = base.environment || master?.location || master?.environment || shot?.location || '';
  return {
    ...base,
    environment: place || (master?.locked || base.locked ? 'same dim indoor room as the locked keyframe' : ''),
    wardrobe: base.wardrobe || master?.wardrobe || '',
    camera: base.camera || master?.camera || '',
    locked: Boolean(base.locked || master?.locked),
  };
}

/** Action / thoại for I2V. Skips pack headings. Does not invent story. */
export function i2vActionOf(state: SeriesPilotState, shot: FamixaSeriesShot, run?: SeriesShotRun) {
  const resolved = run ?? shotRunOf(state, shot);
  const beat = effectiveShotAction(shot, resolved);
  if (beat && !looksLikePackHeading(beat)) return beat;
  const ids = shot.dialogueSegmentIds ?? [];
  const dlg = (state.scenes ?? [])
    .flatMap((sc) => sc.dialogue ?? [])
    .filter((d) => ids.includes(d.id) && (d.text ?? '').trim());
  if (dlg.length) {
    return dlg
      .map((d) => {
        const name = (state.characters ?? []).find((c) => c.id === d.characterId)?.name || d.characterId;
        return `${name}: ${d.text}`.trim();
      })
      .join('. ');
  }
  const raw = (resolved.shotAction || shot.story || shot.motionPromptVi || '').replace(/\s+/g, ' ').trim();
  return looksLikePackHeading(raw) ? '' : raw;
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
  const names = ids
    .map((id) => (state.characters ?? []).find((row) => row.id === id)?.name || '')
    .filter(Boolean)
    .join(', ');
  const motion = englishI2vMotion(act, shot.seconds);
  const idsDlg = shot.dialogueSegmentIds ?? [];
  const dlg = (state.scenes ?? [])
    .flatMap((sc) => sc.dialogue ?? [])
    .filter((d) => idsDlg.includes(d.id));
  const acting = actingI2vBriefFromLines(dlg, act);
  const ctx = clipMem(videoContext, 280);
  const ctxEn = ctx && !I2V_VI_RE.test(ctx) ? ctx : '';
  const run = shotRunOf(state, shot);
  const start = run.startState;
  const end = run.endState;
  const startBit = start
    ? `START: ${[start.location, start.pose, start.prop, start.lighting, start.camera].filter(Boolean).join('; ')}. `
    : '';
  const endBit = end ? `END: ${[end.pose, end.prop, end.facing].filter(Boolean).join('; ')}. ` : '';
  const startEn = startBit && I2V_VI_RE.test(startBit) ? `${englishI2vMotion(startBit, 5).slice(0, 180)} ` : startBit;
  const endEn = endBit && I2V_VI_RE.test(endBit) ? `${englishI2vMotion(endBit, 5).slice(0, 140)} ` : endBit;
  const text =
    `The still is the first frame. Continue the same scene as ${inherit}. ` +
    (names ? `Cast: ${names}. ` : cast ? `Cast on graph. ` : '') +
    startEn +
    `Keep wardrobe, place, lighting and camera. ` +
    (ctxEn ? `${ctxEn} ` : '') +
    motion +
    ' ' +
    acting +
    ' ' +
    endEn +
    (shot.dialogueSegmentIds?.length
      ? ' Mute take: blink and breathe only. Do not animate speech — lipsync is a later step. '
      : '') +
    'Do not reset the scene.';
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
  performance?: import('./content-famixa-acting-law').LinePerformance;
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
  const id = normCharId(characterId);
  const ch = (state.characters ?? []).find((c) => c.id === id);
  const seed = famixaCanonSeedFor(ch ?? { id });
  const userFile = Boolean(ch?.canonFileName && seed && ch.canonFileName !== seed.fileName);
  if (seed && !userFile) return seed.publicPath;
  const pixels = canonImageOf(state, characterId);
  return displayUrlForData(`canon:${id}`, pixels) || seed?.publicPath;
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
  for (const c of state.characters ?? []) {
    if (c.canonImageDataUrl?.startsWith('data:image')) {
      rememberCanonPixels(c.id, c.canonImageDataUrl, c.canonFileName);
      continue;
    }
    const seed = famixaCanonSeedFor(c);
    const userFile = Boolean(c.canonFileName && seed && c.canonFileName !== seed.fileName);
    if (seed && !userFile) continue;
    await loadCanonPixels(c.id);
  }
  return slimPilotForStorage(state);
}

/** Restore scene/short KF from IndexedDB after slim graph load. */
export async function hydratePilotKeyframes(state: SeriesPilotState): Promise<SeriesPilotState> {
  setFamixaMediaScope(state.buildId);
  const ids = [
    ...episodeShots(state).map((s) => s.id),
    ...(state.shorts ?? []).map((s) => s.id),
    ...Object.keys(state.runs ?? {}),
  ];
  const uniq = [...new Set(ids.filter(Boolean))];
  rememberKfFromRuns(state.runs ?? {});
  for (const id of uniq) {
    const run = state.runs[id];
    if (run?.keyframeDataUrl?.startsWith('data:image')) continue;
    if (kfPixelsOf(id)) continue;
    await loadKfPixels(id);
  }
  return state;
}

/** Gemini paints quoted lines as captions. Keep dialogue off the still prompt. */
export const STILL_NO_TEXT =
  'HARD BAN: zero readable letters or numbers anywhere — no subtitles, captions, logos, watermarks, UI, name tags, posters, exam papers with writing, phone screens with text, reference boards, or typography. Blank walls and blank paper. If a reference has letters, ignore them; do not copy typography.';

export function stripStillLettering(raw?: string) {
  let t = (raw ?? '').trim();
  if (!t) return '';
  t = t.replace(/Spoken this shot[^.]{0,500}\./gi, '');
  t = t.replace(/(?:^|[·.|])\s*(Minh|Nam|Linh|An|Mẹ|Bố|Ba|Má)[:：]\s*["“]?[^"”\n·]{2,200}/gi, '');
  t = t.replace(/[“"][^”"]{4,160}[”"]/g, '');
  return t.replace(/\s+/g, ' ').trim();
}

export function seriesSceneStillPrompt(opts: {
  aspect: '9:16' | '16:9';
  visual: string;
  action?: string;
  location?: string;
  refs: SeriesCanonRef[];
  continuityNote?: string;
  peopleCount?: number;
  peopleNames?: string;
  atmosphere?: string;
  lightingLock?: string;
  speakers?: string;
  visualSpec?: VisualSpec;
}) {
  const people = opts.refs.filter((r) => r.role !== 'scene' && !/loi binh|narrator|voice.?over/i.test(`${r.name} ${r.role}`));
  const who = opts.peopleNames?.trim() || people.map((r) => `${r.name}${r.role ? ` (${r.role})` : ''}`).join(', ');
  const count = opts.peopleCount && opts.peopleCount > 0 ? opts.peopleCount : people.length;
  const frame =
    opts.aspect === '9:16'
      ? 'vertical 9:16 phone frame (720×1280). Compose as a vertical two-shot — not a cropped 16:9 table.'
      : 'widescreen 16:9 cinematic frame (1280×720)';
  const action = stripStillLettering(looksLikePackHeading(opts.action) ? '' : opts.action);
  const visual = stripStillLettering(looksLikePackHeading(opts.visual) ? '' : opts.visual);
  const speakers = stripStillLettering(opts.speakers);
  const note = stripStillLettering(opts.continuityNote);
  return [
    STILL_NO_TEXT,
    `Format: ${frame}. One photoreal live-action film still of the Action — not a title card.`,
    'Canon attachments are FACE identity only for anyone newly entering. NEVER reproduce a character bible, master reference, turnaround, expression grid, contact sheet, or typography. Do not change clothes already shown in PREV-SHOT.',
    count
      ? `CAST COUNT LOCK: exactly ${count} people in the frame${who ? `: ${who}` : ''}. Do not add another person. Do not add father/Nam unless listed. No extras.`
      : who
        ? `People in the scene (faces from refs): ${who}.`
        : '',
    opts.visualSpec?.primary
      ? `FACE LOCK: ${opts.visualSpec.primary.name} full face — eyes, nose, mouth, hairline. ${
          opts.visualSpec.secondary.length
            ? `Secondary (${opts.visualSpec.secondary.map((p) => `${p.name} ${p.face}`).join(', ')}) may be partial unless they speak.`
            : ''
        } Forbidden: cropped primary face, back-turned primary, unreadable eyes.`
      : count >= 2 || who
        ? `FACE LOCK: every named person (${who || 'cast'}) must show a complete face — eyes, nose, mouth, hairline. Forbidden: faceless torso, cropped at chest, hands-only parent, mother with no head.`
        : '',
    opts.aspect === '9:16' && count >= 2
      ? 'VERTICAL BLOCKING: both heads in the upper two-thirds. Over-shoulder or stacked. Do not place a parent only on the far right of a landscape table — they will be cut out of 9:16.'
      : '',
    opts.location ? `Location: ${opts.location.slice(0, 180)}.` : '',
    visual ? `Scene lock (do not redesign): ${visual.slice(0, 360)}` : '',
    action ? `Only change the Action (must be visible): ${action.slice(0, 400)}` : '',
    stripStillLettering(opts.atmosphere)
      ? `SCRIPT MOOD (must show on faces): ${stripStillLettering(opts.atmosphere).slice(0, 420)}`
      : '',
    opts.lightingLock
      ? `LIGHTING LOCK: ${opts.lightingLock.slice(0, 160)}. Same dim evening as the locked first frame. If a later still went bright, restore dim. Forbidden: daylight, bright cheerful living room, studio softbox.`
      : '',
    speakers
      ? `SPEAKER LOCK: ${speakers.slice(0, 120)}. The speaker must be on camera. Do not hide a speaking parent off-frame. Do not write their line on the image.`
      : '',
    'WARDROBE LOCK: copy exact clothes from PREV-SHOT. The boy in the room is Minh — same shirt as frames 1–4. Do not dress him from the Canon sheet. Do not draw classmate An. If Nam is already in PREV-SHOT, copy that exact man (face, hair, shirt).',
    opts.refs.some((r) => r.role === 'scene')
      ? 'Continue the exact same room from the attached PREVIOUS keyframe (not a redesigned set). Same clothes, faces already in scene, place, dim lighting, props. Do NOT copy a smile, huddle, or brightened room. Change only this Action. Not a family portrait.'
      : 'Vietnamese family, dim warm indoor evening after dinner, photoreal, not anime, not a smiling catalog still, not a family portrait.',
    note
      ? `Continuity lock (operator, already rewritten): ${note.slice(0, 280)} Do not change the story action or add people.`
      : '',
    opts.visualSpec ? compileVisualPrompt(opts.visualSpec) : '',
    STILL_NO_TEXT,
    actingI2vBrief(inferActingDirection({ action, text: action })),
  ]
    .filter(Boolean)
    .join('\n');
}

export function characterOfRole(state: SeriesPilotState, role: SeriesRoleRow) {
  const id = role.characterId ? normCharId(role.characterId) : '';
  if (!id) return undefined;
  return (state.characters ?? []).find((c) => c.id === id);
}

export function isVoiceOnlyRole(
  role: Pick<SeriesRoleRow, 'title' | 'name' | 'characterId'>,
  character?: { id?: string; name?: string },
) {
  const id = normCharId(role.characterId || character?.id || '');
  const hay = foldVoiceText([role.characterId, role.title, role.name, character?.id, character?.name].filter(Boolean).join(' '));
  return id === 'CHAR-VO' || /loi binh|voice.?over|\bnarrator\b/.test(hay);
}

export function roleCanonReady(state: SeriesPilotState, role: SeriesRoleRow) {
  if (isVoiceOnlyRole(role, characterOfRole(state, role))) return true;
  return characterCanonReady(characterOfRole(state, role));
}

export function roleVoiceReady(state: SeriesPilotState, role: SeriesRoleRow) {
  const ch = characterOfRole(state, role);
  return Boolean((ch?.voiceId || role.voiceId || '').trim());
}

export type FamixaVoiceLaneKey = 'boy' | 'girl' | 'father' | 'mother' | 'man' | 'woman' | 'narrator' | 'any';

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

export function isChildVoiceLane(lane: FamixaVoiceLane) {
  return lane.key === 'boy' || lane.key === 'girl';
}

export function isKidLibraryVoice(v: FamixaVoicePick) {
  const a = normVoiceAge(v.age);
  if (a === 'middle_aged' || a === 'old') return false;
  if (a === 'young') return true;
  return /boy|girl|child|kid|teen|be trai|be gai|cau be|co be|tre em|hoc sinh/.test(voiceHay(v));
}

export function voiceLaneForRole(
  role: Pick<SeriesRoleRow, 'title' | 'name' | 'characterId'>,
  character?: { id?: string; name?: string },
): FamixaVoiceLane {
  const id = normCharId(role.characterId || character?.id || '');
  const hay = foldVoiceText([role.characterId, role.title, role.name, character?.id, character?.name].filter(Boolean).join(' '));
  if (id === 'CHAR-VO' || /loi binh|voice.?over|\bnarrator\b/.test(hay)) {
    return { key: 'narrator', label: 'lời bình miền Bắc', gender: 'male', ages: ['middle_aged'] };
  }
  if (
    id === 'CHAR-001' ||
    id === 'CHAR-004' ||
    /\bminh\b/.test(hay) ||
    (/\ban\b/.test(hay) && !/\b(nam|linh)\b/.test(hay)) ||
    /be trai|cau be|con trai|ban an/.test(hay)
  ) {
    return { key: 'boy', label: 'bé trai 11 tuổi miền Bắc', gender: 'male', ages: ['young'] };
  }
  if (/be gai|con gai|co be|nu nhi/.test(hay)) {
    return { key: 'girl', label: 'bé gái 11 tuổi miền Bắc', gender: 'female', ages: ['young'] };
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

export function isSouthernOrCentralVoice(v: FamixaVoicePick) {
  return /southern|\bsouth\b|south viet|saigon|sai.?gon|ho chi minh|\bhcm\b|mien nam|nam ky|giong nam|mekong|can tho|cantho|vung tau|bien hoa|dong nai|central|mien trung|hue|da nang|nha trang/.test(
    voiceHay(v),
  );
}

export function isNorthernVoice(v: FamixaVoicePick) {
  return /northern|north viet|hanoi|ha noi|ha-noi|ha noi|mien bac|giong bac|bac ky|hai phong|nam dinh|thai nguyen/.test(
    voiceHay(v),
  );
}

/** Cast / TTS: An·Minh must be tagged Northern. Unknown accent = often Southern. */
export function voiceSoundsNorthern(v?: FamixaVoicePick) {
  if (!v) return false;
  if (isSouthernOrCentralVoice(v)) return false;
  return isNorthernVoice(v);
}

export function voicesForLane(voices: FamixaVoicePick[], lane: FamixaVoiceLane, selectedId?: string) {
  const northern = voices.filter((v) => !v.cloned && v.vietnamese !== false && voiceSoundsNorthern(v));
  const pool =
    northern.length > 0
      ? northern
      : voices.filter((v) => !v.cloned && v.vietnamese !== false && !isSouthernOrCentralVoice(v));
  let base = pool;
  if (lane.gender) {
    const gendered = base.filter((v) => {
      const g = normVoiceGender(v.gender);
      return !g || g === lane.gender;
    });
    if (gendered.length >= 2) base = gendered;
  }
  if (lane.ages?.length && lane.key !== 'narrator') {
    const aged = base.filter((v) => {
      const a = normVoiceAge(v.age);
      if (isChildVoiceLane(lane)) return isKidLibraryVoice(v) || (!a && !/dad|mom|father|mother|man|woman|adult|uncle|aunt/.test(voiceHay(v)));
      return !a || lane.ages!.includes(a);
    });
    if (aged.length >= 1) base = aged;
  }
  if (lane.key === 'narrator') {
    const adults = base.filter((v) => {
      const a = normVoiceAge(v.age);
      return a === 'middle_aged' || a === 'old' || /dad|father|man|adult|narrat|uncle|\bong\b/.test(voiceHay(v));
    });
    const males = (adults.length ? adults : base).filter((v) => {
      const g = normVoiceGender(v.gender);
      return !g || g === 'male';
    });
    base = males.length ? males : adults.length ? adults : base;
  } else if (isChildVoiceLane(lane)) {
    const kids = base.filter(isKidLibraryVoice);
    if (kids.length >= 1) base = kids;
    else {
      const notAdult = base.filter((v) => {
        const a = normVoiceAge(v.age);
        return a !== 'middle_aged' && a !== 'old';
      });
      if (notAdult.length) base = notAdult;
    }
  }
  if (!base.length && pool.length) base = pool;
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

/** Voice-over is a speaker, not a face. Keep CHAR-VO on Cast so Full Voice can assign a Northern adult. */
export function syncVoiceOnlyRoles(state: SeriesPilotState): SeriesPilotState {
  const spokenIds = new Set<string>();
  for (const sc of state.scenes ?? []) {
    for (const d of sc.dialogue ?? []) spokenIds.add(normCharId(d.characterId));
  }
  for (const l of state.lines ?? []) spokenIds.add(normCharId(l.characterId));
  const chars = state.characters ?? [];
  const voChar =
    chars.find((c) => c.id === 'CHAR-VO') ||
    chars.find((c) => /loi binh|voice.?over|\bnarrator\b/.test(foldVoiceText(`${c.id} ${c.name} ${c.role}`)));
  if (!voChar || !spokenIds.has(normCharId(voChar.id))) return state;
  const hasRole = state.roles.some((r) => isVoiceOnlyRole(r, characterOfRole(state, r)) || r.characterId === voChar.id);
  const roles = hasRole
    ? state.roles
    : [
        ...state.roles,
        {
          id: `role-${voChar.id}`,
          title: voChar.role || 'Lời bình',
          name: voChar.name || 'Lời bình',
          characterId: voChar.id,
          line: (state.lines ?? []).find((l) => l.characterId === voChar.id)?.text,
        },
      ];
  const next = { ...state, roles };
  const voiceReady = roles.every((r) => !isVoiceOnlyRole(r, characterOfRole(next, r)) || roleVoiceReady(next, r));
  if (next.castLocked && !voiceReady) return { ...next, castLocked: false };
  return hasRole && next.castLocked === state.castLocked ? state : next;
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
  rememberCanonFromChars(local.characters ?? [], local.stills);
  rememberCanonFromChars(remote.characters ?? [], remote.stills);
  rememberKfFromRuns(local.runs ?? {});
  rememberKfFromRuns(remote.runs ?? {});
  const graph = ensurePilotGraph({ ...remote, schemaVersion: PILOT_SCHEMA });
  const localChars = local.characters ?? [];
  const characters = (graph.characters ?? []).map((c) => {
    const old = localChars.find((x) => x.id === c.id);
    rememberCanonPixels(c.id, old?.canonImageDataUrl || c.canonImageDataUrl, old?.canonFileName || c.canonFileName);
    return {
      ...c,
      voiceBible: c.voiceBible || old?.voiceBible,
      canonFileName: c.canonFileName || old?.canonFileName,
      canonLocalPath: c.canonLocalPath || old?.canonLocalPath,
      canonImageDataUrl: undefined,
    };
  });
  const runs: SeriesPilotState['runs'] = {};
  const ids = new Set([...Object.keys(graph.runs ?? {}), ...Object.keys(local.runs ?? {})]);
  for (const id of ids) {
    const rem = graph.runs?.[id];
    const old = local.runs?.[id];
    rememberKfPixels(id, old?.keyframeDataUrl || rem?.keyframeDataUrl, old?.keyframeFileName || rem?.keyframeFileName);
    const base = {
      ...(rem ?? old ?? { status: 'story_locked' as const }),
      keyframeDataUrl: undefined,
      keyframeFileName: old?.keyframeFileName || rem?.keyframeFileName,
      keyframePath: old?.keyframePath || rem?.keyframePath,
    };
    const keep = mergeKeepFinalSource(base, old);
    runs[id] = {
      ...base,
      ...keep,
      takeUrl: keep.takeUrl || base.takeUrl || old?.takeUrl,
      lipsyncTaskId: base.lipsyncTaskId || (keep.lipsynced ? old?.lipsyncTaskId : undefined),
      lipsyncStatus: base.lipsyncStatus || (keep.lipsynced ? old?.lipsyncStatus || 'SUCCEEDED' : undefined),
      stateLocked: Boolean(base.stateLocked || old?.stateLocked),
      startState: base.stateLocked ? base.startState : base.startState ?? old?.startState,
      endState: base.stateLocked ? base.endState : base.endState ?? old?.endState,
      shotQa: base.shotQa ?? old?.shotQa,
      visualSpec: base.visualSpec ?? old?.visualSpec,
      visualQa: base.visualQa ?? old?.visualQa,
    };
  }
  const stills = (graph.stills ?? []).map((s) => {
    const old = (local.stills ?? []).find((x) => x.id === s.id);
    if (s.charCode) rememberCanonPixels(s.charCode, old?.imageDataUrl || s.imageDataUrl, old?.fileName || s.fileName);
    return { ...s, imageDataUrl: undefined, fileName: s.fileName || old?.fileName };
  });
  const sameEp = episodeCodeOf(graph.episode?.episode || graph.episode?.title) ===
    episodeCodeOf(local.episode?.episode || local.episode?.title);
  const voicePreview = sameEp
    ? pickRicherVoicePreview(graph.voicePreview, local.voicePreview)
    : graph.voicePreview;
  return {
    ...graph,
    buildId: graph.buildId || local.buildId,
    characters,
    runs,
    stills,
    voiceLocked: sameEp ? Boolean(graph.voiceLocked || local.voiceLocked) : Boolean(graph.voiceLocked),
    voicePreview,
    shorts: sameEp ? mergeClipLists(graph.shorts ?? [], local.shorts ?? []) : graph.shorts,
    scenes: sameEp
      ? (graph.scenes ?? []).map((sc) => {
          const old = (local.scenes ?? []).find((x) => x.id === sc.id);
          return { ...sc, dialogue: mergeKeepDialoguePerformance(sc.dialogue, old?.dialogue) };
        })
      : graph.scenes,
    sceneMasters: sameEp ? { ...local.sceneMasters, ...graph.sceneMasters } : graph.sceneMasters,
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

export function mergeClipLists<T extends { id: string }>(primary: T[], extra: T[]) {
  const seen = new Set(primary.map((s) => s.id));
  return [...primary, ...extra.filter((s) => !seen.has(s.id))];
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
      voiceBible: old?.voiceBible || c.voiceBible,
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
  const keepIds = new Set([
    ...(parsed.episode?.shots ?? []).map((s) => s.id),
    ...(parsed.shorts ?? []).map((s) => s.id),
    ...((switchedEpisode ? [] : prev.shorts) ?? []).map((s) => s.id),
  ]);
  const runs = switchedEpisode
    ? {}
    : Object.fromEntries(Object.entries(prev.runs).filter(([id]) => keepIds.has(id)));
  const shorts = switchedEpisode
    ? parsed.shorts
    : mergeClipLists(parsed.shorts, prev.shorts ?? []);
  const next = {
    ...prev,
    episode: parsed.episode,
    scenes: parsed.scenes,
    lines: parsed.lines,
    characters,
    roles,
    shorts,
    stills: parsed.stills.length ? mergeStills(switchedEpisode ? [] : prev.stills ?? [], parsed.stills) : parsed.stills,
    packDraft: packText,
    scriptLocked: false,
    sceneLocked: false,
    storyReviewed: false,
    shotGraphLocked: false,
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
    canonVersion: FAMIXA_CANON_VERSION,
  };
  return ensurePilotGraph(syncVoiceOnlyRoles({
    ...next,
    castLocked: Boolean(prev.castLocked && next.roles.every((r) => roleCanonReady(next, r) && roleVoiceReady(next, r))),
  }));
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

export function withKfPixels(id: string, run?: SeriesShotRun): SeriesShotRun {
  const base = run ?? { status: 'story_locked' as const };
  if (base.keyframeDataUrl?.startsWith('data:image')) return base;
  const pixels = kfPixelsOf(id);
  return pixels ? { ...base, keyframeDataUrl: pixels } : base;
}

/** Stale Fal 404/405/403 stay in local graph after key change — drop so Khớp môi POSTs new. */
export function dropDeadLipsync(run: SeriesShotRun): SeriesShotRun {
  if (run.lipsynced) return run;
  const err = `${run.lipsyncError || ''} ${run.lipsyncStatus || ''}`;
  if (!/404|405|504|403|exhausted|locked|downstream|request failed/i.test(err)) return run;
  return { ...run, lipsyncError: undefined, lipsyncStatus: undefined, lipsyncTaskId: undefined };
}

export function shotRunOf(state: SeriesPilotState, shot: FamixaSeriesShot): SeriesShotRun {
  return dropDeadLipsync(withKfPixels(shot.id, state.runs[shot.id] ?? { status: shot.status }));
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
  if (!shotHasValidAction(shot, shotRunOf(state, shot))) return state;
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

export function looksLikePackHeading(raw?: string) {
  const s = (raw ?? '').replace(/\s+/g, ' ').trim();
  if (s.length < 4) return false;
  if (/kịch bản phim|tên kịch bản|drama voice|master reference|character (canon|sheet)|turnaround|expression sheet|production master|video title|estimated duration|target duration/i.test(s)) {
    return true;
  }
  if (/^(?:#{1,3}\s*)?(?:famixa|kit marketing)\b/i.test(s)) return true;
  if (/^kịch bản\b.{0,56}$/i.test(s)) return true;
  if (looksLikeVoiceDirection(s)) return true;
  if (/^bối cảnh\s*:/i.test(s)) return true;
  if (/^(#{1,3}\s*)?(phân cảnh|nhân vật)\b/i.test(s)) return true;
  if (/phân cảnh chi tiết/i.test(s) && s.length < 64) return true;
  if (/^giọng\b/i.test(s) && s.length < 96) return true;
  if (/^(?:tone|thời lượng(?:\s*mục tiêu)?)\s*:/i.test(s)) return true;
  return false;
}

/** Prefer a real beat over a pack title stuck in shotAction. Does not invent story. */
export function effectiveShotAction(shot: FamixaSeriesShot, run?: SeriesShotRun) {
  const seen = new Set<string>();
  for (const raw of [run?.shotAction, shot.story, shot.motionPromptVi, shot.visual, shot.beatText]) {
    const t = (raw ?? '').replace(/\s+/g, ' ').trim();
    if (t.length < 6 || seen.has(t) || looksLikePackHeading(t)) continue;
    seen.add(t);
    return t;
  }
  return '';
}

/** Silent OK. Empty / placeholder Action = HOLD — not reuse, not generate. */
export function shotHasValidAction(shot: FamixaSeriesShot, run?: SeriesShotRun) {
  const raw = effectiveShotAction(shot, run) || ((run?.shotAction ?? '').trim() || shotActionFromPack(shot)).replace(/\s+/g, ' ').trim();
  if (looksLikePackHeading(raw) || raw.length < 6) {
    const beat = (shot.beatText ?? '').replace(/\s+/g, ' ').trim();
    if (beat.length >= 6 && !looksLikePackHeading(beat)) return true;
    if (/^(?:tone|thời lượng)\s*:/i.test(raw)) return false;
    return (shot.dialogueSegmentIds?.length ?? 0) > 0;
  }
  if (/^[-—–._/\\\s]+$/u.test(raw)) return false;
  if (/^(n\/a|none|null|không|chưa có( mô tả)?( shot)?)$/i.test(raw)) return false;
  return /[\p{L}\p{N}]/u.test(raw);
}

/** Drop SH with no Action. Graph may list them; production graph after duyệt must not. */
export function pruneEmptyShots(state: SeriesPilotState): SeriesPilotState {
  const ep = state.episode;
  if (!ep?.shots.length) return { ...state, shotGraphLocked: true };
  const keep = ep.shots.filter((s) => shotHasValidAction(s, shotRunOf(state, s)));
  const keepIds = new Set(keep.map((s) => s.id));
  const scenes = (state.scenes ?? []).map((sc) => ({
    ...sc,
    scriptBeats: (sc.scriptBeats ?? [])
      .map((b) => ({ ...b, shotIds: b.shotIds.filter((id) => keepIds.has(id)) }))
      .filter((b) => b.shotIds.length || (b.text || '').trim()),
  }));
  return {
    ...state,
    episode: { ...ep, shots: keep },
    scenes,
    shotGraphLocked: true,
    runs: Object.fromEntries(Object.entries(state.runs).filter(([id]) => keepIds.has(id) || id.startsWith('S'))),
  };
}

export function groupShotsByBeat(shots: FamixaSeriesShot[]) {
  const out: { key: string; beatId?: string; label: string; shots: FamixaSeriesShot[] }[] = [];
  const ix = new Map<string, number>();
  for (const s of shots) {
    const empty = !shotHasValidAction(s);
    const key = s.beatId || (empty ? `hold:${s.sceneId || s.scene || 'SC'}` : `lone:${s.id}`);
    let i = ix.get(key);
    if (i === undefined) {
      i = out.length;
      ix.set(key, i);
      out.push({
        key,
        beatId: s.beatId,
        label: s.beatText || (empty ? 'Không có Script Beat — không production' : shotActionFromPack(s)),
        shots: [s],
      });
    } else {
      out[i]!.shots.push(s);
    }
  }
  return out;
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
  if (!shotHasValidAction(shot, run)) return state;
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
  const of = (run: SeriesShotRun) => run.runwayBilled ?? run.runwaySpent ?? 0;
  const shot = episodeShots(state).reduce((n, s) => n + of(shotRunOf(state, s)), 0);
  const short = (state.shorts ?? []).reduce((n, s) => n + of(shortRunOf(state, s.id)), 0);
  return shot + short;
}

export function shortRunOf(state: SeriesPilotState, id: string): SeriesShotRun {
  return dropDeadLipsync(withKfPixels(id, state.runs[id] ?? { status: 'keyframe_ready' }));
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

export function visualRolesOf(state: SeriesPilotState) {
  return state.roles.filter((r) => !isVoiceOnlyRole(r, characterOfRole(state, r)));
}

/** Voice lock means the script was accepted — do not bounce back to step 1. */
export function ensureScriptFollowsVoice(state: SeriesPilotState): SeriesPilotState {
  if (state.scriptLocked || !state.voiceLocked) return state;
  return { ...state, scriptLocked: true };
}

export function canLockScript(state: SeriesPilotState) {
  const n =
    (state.shorts?.length ?? 0) +
    episodeShots(state).filter((s) => shotHasValidAction(s, shotRunOf(state, s))).length;
  if (n === 0) return false;
  if ((state.scenes?.length ?? 0) > 0 && !state.storyReviewed) return false;
  if (state.roles.length > 0 && !rolesReady(state.roles)) return false;
  if (state.roles.length > 0 && !state.roles.every((r) => roleCanonReady(state, r))) return false;
  const visual = visualRolesOf(state);
  const visualReady =
    visual.length === 0 || visual.every((r) => roleCanonReady(state, r) && roleVoiceReady(state, r));
  if (state.roles.length > 0 && !state.castLocked && !visualReady) return false;
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
  if ((state.episode?.shots.length ?? 0) > 0 && state.shotGraphLocked === false) return false;
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
  if (state.shotGraphLocked === false) {
    return 'Duyệt cách chia shot (Script Beat → SH) rồi mới dựng cảnh. KIT không tạo SH rỗng.';
  }
  return undefined;
}

/** After Voice LOCK, open Shorts review (studio). 9:16 is Advanced, not the main path. */
export function studioFallbackPane(state: SeriesPilotState): 'script' | 'shorts' | 'studio' {
  if (!canOpenStudio(state) || !voiceProductionReady(state)) return 'script';
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
    const row = resolveCanonSpeaker(name) || resolveCanonSpeaker(code);
    if (!row) continue;
    rows.push({ code: row.id, name: row.name, role: row.role || role });
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
  if (!token || isMetaCanonSpeaker(token)) return undefined;
  const row = resolveCanonSpeaker(token);
  if (row) return chars.find((c) => c.id === row.id) ?? { id: row.id, name: row.name, role: row.role, offFrame: row.visual !== 'frame' };
  return chars.find((c) => c.name.toLowerCase() === token.toLowerCase());
}

export function ensureEpisodeChar(chars: FamixaCharacter[], speaker: string): FamixaCharacter | undefined {
  const hit = resolveEpisodeSpeaker(speaker, chars);
  if (!hit) return undefined;
  if (!chars.some((c) => c.id === hit.id)) chars.push(hit);
  return hit;
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
  if (resolveEpisodeSpeaker(token, chars) || resolveCanonSpeaker(token) || /^(MINH|NAM|LINH|BỐ|MẸ|BA|CON)$/i.test(token)) {
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
  const seeded = seedFamixaCanon(characters);
  characters.length = 0;
  characters.push(...(seeded as FamixaCharacter[]));
  if (/\bbạn\s+an\b|\bCHAR-004\b|(?:^|\n)\s*An\s*[:：]/im.test(text)) {
    const an = resolveCanonSpeaker('An');
    if (an && !characters.some((c) => c.id === an.id)) {
      characters.push({ id: an.id, name: an.name, role: an.role, offFrame: true });
    }
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

export const RUNWAY_V02_NEGATIVE = `No looking at camera. No talking to camera. No Linh speaking. No large gestures. No exaggerated acting. No moral lecture. No sudden hug or apology reset. No scream without cause. No smoke or fog. No location change. No face morph. No extra people. No deformed hands. No text, logo, watermark. No camera shake, pan, rotation, or cut.`;

/** SH02 V02 — Minh asks once, Nam listens. Keep KF02. */
export const RUNWAY_SH02_V02_MOTION = `A warm Vietnamese family of three at dinner at night. Same faces, clothes, seats, table, food, room and night light as the input image. Minh in the center looks at Nam on the left for the entire 5 seconds. Minh speaks only once, asking Nam a short question. Minh's mouth moves only while asking, then stays still. Minh does not look at the camera or straight ahead. No repeated head turns. Nam stays silent the whole shot and only listens, eyes on Minh. One or two very small listening reactions. Nam never looks at the camera. Linh on the right stays silent, watches them with a gentle smile, no talking gestures. Subtle live-action: blink, breathe. Very slow push-in, eye-level, 35mm. No shake, no pan, no rotation, no cut. 5 seconds.`;

export const RUNWAY_SH02_V02_NEGATIVE = `No looking at camera. No talking to camera. No Nam speaking. No Linh speaking. No Minh looking straight ahead. No repeated head turns. No large gestures. No exaggerated acting. No moral lecture. No sudden hug or apology reset. No scream without cause. No face morph. No extra people. No deformed hands. No text, logo, watermark. No camera shake, pan, rotation, or cut.`;

const SAFE_I2V =
  'Cinematic live-action dinner scene. The photo is the first frame only. ' +
  'Start motion right away: people blink and breathe, hands serve rice, chopsticks, steam from bowls, ' +
  'small head turns, soft eye contact, gentle camera drift. Keep the same seats and faces. No captions.';

const PACK_RE = /VIDEO\s*ID|PRODUCTION MASTER|=======|CHAR-\d+\s*[—–-]/i;
const HARD_RE = /nude|sex|porn|kill|blood|abuse|suicide|weapon|gun/i;
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
  const usable = m.length > 20 && !PACK_RE.test(m) && !I2V_VI_RE.test(m);
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
  let prompt = (opts.prompt ?? '').trim();
  const image = (opts.imageDataUrl ?? '').trim();
  if (!image.startsWith('data:image/') && !/^https?:\/\//i.test(image)) {
    reasons.push('Chưa có ảnh cảnh (KF).');
  }
  if (/^https?:\/\/(localhost|127\.0\.0\.1|\[::1\])/i.test(image)) {
    reasons.push('KF là URL máy local — Runway không tải được. Gửi data-URI.');
  }
  if (I2V_VI_RE.test(prompt) || PACK_RE.test(prompt)) {
    prompt = englishI2vMotion(prompt);
  }
  if (!prompt) reasons.push('Chưa có prompt I2V.');
  if (PACK_RE.test(prompt)) reasons.push('Đừng dán giấy Story (VIDEO ID / MASTER) vào ô Motion.');
  if (HARD_RE.test(prompt)) reasons.push('Prompt còn từ Runway hay chặn (máu / bạo lực / nude).');
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
  const action = (
    opts.state && opts.shot ? i2vActionOf(opts.state, opts.shot) : ''
  ).trim() || (opts.action ?? '').trim();
  const hasKf = Boolean((opts.keyframeDataUrl ?? '').trim());
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
      label: opts.shortsReady ? 'Voice + Shot Plan đã khóa' : 'Khóa kịch bản / Full Voice / cách chia Shot trước',
    },
    {
      id: 'scene',
      ok: !opts.sceneLocked,
      label: opts.sceneLocked ? 'Scene đã Final — mở khóa trên Timeline để làm lại' : 'Scene chưa Final',
    },
    { id: 'prev', ok: opts.unlocked, label: 'Shot liền trước đã khóa (I2V / LOCK)' },
    {
      id: 'lock',
      ok: opts.lock.locked,
      label: opts.lock.locked ? 'Scene Master / Continuity đã khóa' : 'Chưa khóa Scene Master / Continuity',
    },
    { id: 'action', ok: Boolean(action), label: action ? 'Shot Action / thoại đủ để I2V' : 'Thiếu Action / thoại để I2V' },
    { id: 'kf', ok: hasKf, label: 'Có ảnh keyframe cảnh' },
    { id: 'kfa', ok: Boolean(hasKf && opts.status !== 'story_locked'), label: 'Đã duyệt keyframe' },
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
    const place = scene?.environment || opts.lock.environment || opts.shot.location || '';
    const placeOk = Boolean(place) || (opts.lock.locked && hasKf);
    items.push({
      id: 'graph-scene',
      ok: placeOk,
      label: place
        ? `Bối cảnh: ${place.slice(0, 48)}`
        : placeOk
          ? 'Bối cảnh theo Scene Master / KF đã khóa'
          : 'Chưa khóa Scene Master — thiếu bối cảnh',
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
    return { ok: items.every((i) => i.ok), items, warnings: gate.warnings, prompt: gate.prompt };
  }
  items.push({ id: 'prompt', ok: false, label: 'Prompt I2V — cần Memory + Action + KF' });
  return { ok: false, items, warnings: [], prompt };
}
