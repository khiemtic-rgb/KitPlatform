/**
 * Film-level script breakdown and step plan.
 * Uses the existing screenplay parser. Does not call Gemini, image, or video providers.
 * The Gemini script-draft endpoint writes a new pack from a short seed; it is not a breakdown service.
 */
import {
  emptyPilot,
  parseFamixaPack,
  type FamixaLine,
  type FamixaSceneNode,
  type FamixaSeriesShot,
  type SeriesPilotState,
  type SeriesShotRun,
  type ShotSplitReason,
} from './content-famixa-series';
import type { NovixaShortEdits, NovixaShortPlan } from './novixa-shorts';

export const FILM_SERIES_DESK_STEPS = ['script', 'scene', 'shot', 'voice', 'image', 'video', 'review', 'export'] as const;

const LOCAL_STEPS = new Set(['script', 'scene', 'shot']);

export type FilmExecutor = 'none' | 'novixa_local';

export const NOVIXA_LOCAL_RENDER_DETAIL =
  'Chưa dựng. Hình vẽ từ từng câu của kịch bản. Chưa gọi ElevenLabs.';

export type NovixaMotionScreen = { path: string; label: string };

export type NovixaMotionRun = {
  status: 'images_missing' | 'script_missing' | 'voice_not_run' | 'ready' | 'failed' | 'job_missing' | 'running';
  message: string;
  folder?: string | null;
  videoPath?: string | null;
  phase?: string | null;
  taskId?: string | null;
  /** Set only when this file was rendered from the current lines, voice, and stills. */
  inputKey?: string | null;
};

export type NovixaStill = {
  index: number;
  id: string;
  text: string;
  path: string;
  approved?: boolean;
  brandId?: string;
  brandStale?: boolean;
  signature?: string;
};

export type NovixaBrandProfile = {
  id: string;
  name: string;
  colors: string;
  style: string;
  imageNotes: string;
  products: string[];
  avoid: string[];
  /** Brand narration voice; the video desk uses it as default voice and settings. */
  voice?: { voiceId?: string | null; voiceName?: string | null; stability?: number | null; similarityBoost?: number | null; style?: number | null; speed?: number | null } | null;
};

/** Film brand only. A nav brand is not a source. */
export function novixaBrandProfile(brand: {
  id: string;
  name: string;
  knowledge?: {
    visualColors?: string | null;
    visualStyle?: string | null;
    imageNotes?: string | null;
    products?: string[] | null;
    avoidTerms?: string[] | null;
    voice?: NovixaBrandProfile['voice'];
  } | null;
} | null | undefined): NovixaBrandProfile | null {
  if (!brand?.id) return null;
  const knowledge = brand.knowledge;
  return {
    id: brand.id,
    name: brand.name || '',
    colors: knowledge?.visualColors || '',
    style: knowledge?.visualStyle || '',
    imageNotes: knowledge?.imageNotes || '',
    products: knowledge?.products || [],
    avoid: knowledge?.avoidTerms || [],
    voice: knowledge?.voice?.voiceId ? knowledge.voice : null,
  };
}

export function novixaStillBrandStale(still: NovixaStill, brandId: string) {
  return Boolean(still.brandId && brandId && still.brandId !== brandId);
}

export const NOVIXA_VOICE = {
  provider: 'elevenlabs',
  model: 'eleven_v3',
  voiceId: 'nbv4fVbfyLxvuHzyIeDo',
  label: 'Giọng đã kiểm thử',
} as const;

export type FilmTaskStatus = 'done' | 'blocked' | 'unavailable' | 'failed';

export type FilmProductionTask = {
  taskKey: string;
  shotId: string;
  step: string;
  status: FilmTaskStatus;
  detail?: string;
};

export type FilmExportSlot = { publishAt: string; target: string; status: string };

export type FilmExportRecord = {
  title: string;
  at: string;
  jobId?: string;
  topicId?: string;
  uploaded?: boolean;
  schedule?: FilmExportSlot[];
};

export type FilmOrchestration = {
  analysis: 'confirmed';
  scriptHash: string;
  /** Kept on the film graph. Server saves omit packDraft. */
  script?: string;
  screens?: NovixaMotionScreen[];
  stills?: NovixaStill[];
  /** Hướng khung của bộ `stills` đang hiển thị. Thiếu = dọc (bản cũ). */
  stillOrientation?: 'wide' | 'tall';
  /** Bộ ảnh + video của hướng còn lại, cất riêng để đổi hướng không ghi đè. */
  shelf?: { orientation: 'wide' | 'tall'; stills: NovixaStill[]; motion?: NovixaMotionRun };
  voiceChoice?: { model: string; voiceId: string; approved?: boolean; previewPath?: string | null };
  motion?: NovixaMotionRun;
  /** Video ngắn cắt theo cảnh, do người dựng chọn. */
  shortPlans?: NovixaShortPlan[];
  /** Chỉnh sửa của người dựng cho các video ngắn tự gợi ý (theo khóa bản cắt). */
  shortEdits?: NovixaShortEdits;
  /** Số video ngắn đã cắt ở lần xuất gần nhất. */
  shortsDone?: number;
  /** Video dài ghép từ bản dựng, bỏ các câu [ngắn]. `key` = kịch bản + bản dựng gốc; khác key là đã cũ. */
  longCut?: Partial<Record<'wide' | 'tall', { folder: string; seconds: number; key: string }>>;
  /** Thành phẩm đã đưa sang Video thành phẩm, theo khóa (wide, tall, short-1…). */
  /** Bước 5: mỗi thành phẩm thành một bài (topic) có video, rồi Agent xếp lịch. jobId = bản cũ (Video thành phẩm). */
  exports?: Record<string, FilmExportRecord>;
  /** File kịch bản trong thư viện mà phim được nạp từ đó. */
  scriptSource?: { id: string; code: string; title: string; approved: boolean };
  tasks: FilmProductionTask[];
};

const SCENE_HEADING = /^(cảnh|canh|sc)\s*\d+/i;
const DIRECTION_LABEL = /^(chủ đề|định dạng|phong cách|thời lượng|hình ảnh|lời dẫn|ghi chú|nhạc|âm thanh|chú thích|camera|bối cảnh|mục tiêu|tone|style)\b/i;
/** Nhãn đánh số hay nhấn ý ("Một:", "Thứ hai:", "Bước 3:", "Lưu ý:") là lời đọc, không phải tên người nói. */
export const SPOKEN_LABEL = /^(một|hai|ba|bốn|năm|sáu|bảy|tám|chín|mười|thứ\s+\S+|bước\s*\d+|\d+|lưu ý|ví dụ|nhớ|quy tắc(\s*\d+)?|điều\s+\S+|nguồn|kết luận|tóm lại|chú ý|cảnh báo|câu hỏi|hỏi|đáp)$/i;

/** A production note is not a sentence. Style, shot description, and scene titles stay off the voice. */
/** Dòng chỉ dùng cho video ngắn (hook, lời mời xem bản đầy đủ…): "[ngắn] Câu hook". Vẫn được đọc giọng, nhưng video dài bỏ đi. */
const SHORT_ONLY_MARK = /^(?:[-*]\s*)?\[\s*(?:chỉ\s*)?ngắn\s*\]\s*/iu;

export function isShortOnlyLine(raw: string) {
  return SHORT_ONLY_MARK.test(raw.trim());
}

export function spokenLine(raw: string) {
  const line = raw.trim().replace(SHORT_ONLY_MARK, '').trim();
  if (!line || /^#{1,6}\s/.test(line) || /^([-*_])\1{2,}$/.test(line.replace(/\s/g, '')) || SCENE_HEADING.test(line) || /^\[[^\]]*\]$/.test(line)) return '';
  const plain = line.replace(/\*\*/g, '').trim();
  if (!plain || SCENE_HEADING.test(plain) || /^([-*_])\1{2,}$/.test(plain)) return '';
  const colon = plain.indexOf(':');
  const label = colon > 0 ? plain.slice(0, colon).trim() : '';
  if (label && DIRECTION_LABEL.test(label)) return '';
  const speaker = colon > 0 && colon <= 40 && label.length > 0 && label.split(/\s+/).length <= 3 && !/[,.!?…]/.test(label) && !SPOKEN_LABEL.test(label);
  return speaker ? plain.slice(colon + 1).trim() : plain;
}

/** One picture unit per spoken sentence. A bracket-only direction is skipped. A colon inside a sentence stays. */
export function novixaUnits(script: string) {
  const units: { index: number; id: string; text: string }[] = [];
  for (const raw of script.split(/\r?\n/)) {
    const text = spokenLine(raw);
    if (!text) continue;
    units.push({ index: units.length, id: `U${String(units.length + 1).padStart(2, '0')}`, text });
  }
  return units;
}

export function mergeNovixaStills(previous: NovixaStill[], incoming: NovixaStill[], redrawIndex?: number, fresh?: boolean) {
  return incoming.map((item) => {
    const kept = previous.find((row) => row.text === item.text);
    const approved = fresh || item.index === redrawIndex ? false : Boolean(kept?.approved);
    return { ...item, approved };
  });
}

export function novixaInputKey(lines: string[], voiceId: string, stillPaths: string[]) {
  return `${voiceId}\n${lines.join('\n')}\n${stillPaths.join('\n')}`;
}

/** A previous video stays on screen, but it is current only when it used these lines, voice, and pictures. */
export function novixaVideoCurrent(state: SeriesPilotState) {
  const motion = state.filmOrchestration?.motion;
  const stills = state.filmOrchestration?.stills ?? [];
  if (motion?.status !== 'ready' || !motion.videoPath || !motion.inputKey || !stills.length) return false;
  const lines = stills.map((item) => item.text);
  const voiceId = state.filmOrchestration?.voiceChoice?.voiceId || '';
  return motion.inputKey === novixaInputKey(lines, voiceId, stills.map((item) => item.path));
}

export function novixaVideoGate(state: SeriesPilotState) {
  const stills = state.filmOrchestration?.stills ?? [];
  if (!stills.length) return 'Chưa có ảnh từ kịch bản.';
  if (stills.some((item) => !item.approved)) return 'Còn ảnh chưa duyệt.';
  const voice = state.filmOrchestration?.voiceChoice;
  if (!voice?.approved || !voice.voiceId) return 'Chưa xác nhận giọng.';
  return null;
}

/** A browser timeout is not a failed render. A finished video is kept over that timeout. */
/** Lý do lỗi để hiện cho người dùng: mã HTTP, lời nhắn của máy chủ, hoặc lỗi của trang. Không có thì để trống. */
export function describeClientError(error: unknown): string {
  const e = error as { isAxiosError?: boolean; code?: string; message?: string; response?: { status?: number; data?: unknown } } | null | undefined;
  if (e?.isAxiosError) {
    const data = e.response?.data as { message?: unknown; title?: unknown; detail?: unknown } | string | undefined;
    const server = typeof data === 'string'
      ? data.slice(0, 200)
      : [data?.message, data?.title, data?.detail].find((x): x is string => typeof x === 'string' && x.trim() !== '') ?? '';
    const parts = [e.response?.status ? `HTTP ${e.response.status}` : (e.code || 'không nhận được phản hồi'), server].filter(Boolean);
    return parts.join(' · ');
  }
  return error instanceof Error ? error.message : '';
}

export function novixaClientOutcome(code?: string, detail?: string) {
  if (code === 'ECONNABORTED' || code === 'ERR_CANCELED') {
    return {
      status: 'running' as const,
      message: 'Trang hết thời gian chờ. Tác vụ trên máy vẫn có thể đang chạy. Bấm Tiếp tục để kiểm tra, không tạo lượt mới.',
    };
  }
  return { status: 'failed' as const, message: `Không chạy được build_stock_motion. Chưa có video mới.${detail ? ` (${detail})` : ''}` };
}

export function adoptNovixaStatus(current: NovixaMotionRun | undefined, incoming: NovixaMotionRun): NovixaMotionRun {
  if (current?.status === 'ready' && current.videoPath && incoming.status !== 'ready') return current;
  if (incoming.status === 'ready' && incoming.videoPath) return incoming;
  if (current?.status === 'running' && incoming.status === 'failed') return current;
  return incoming;
}

/** Spoken lines from the film script. Scene headings and production notes are skipped. */
export function spokenLinesFromScript(script: string) {
  return novixaUnits(script).map((unit) => unit.text);
}

export function novixaMotionInputs(state: SeriesPilotState): {
  lines: string[];
  screens: NovixaMotionScreen[];
  block?: string;
} {
  const lines = spokenLinesFromScript(state.filmOrchestration?.script || '');
  const screens = (state.filmOrchestration?.screens ?? [])
    .map((screen) => ({ path: (screen.path || '').trim(), label: (screen.label || '').trim() }))
    .filter((screen) => screen.path && screen.label);
  if (!lines.length) {
    return { lines, screens, block: 'Phim chưa có lời để đọc.' };
  }
  return { lines, screens };
}

export type FilmShotDraft = {
  id: string;
  sceneId: string;
  sceneTitle: string;
  order: number;
  story: string;
  dialogue: string;
  visual: string;
  splitReason: ShotSplitReason;
};

export type FilmScriptProposal = {
  ok: boolean;
  parser: 'screenplay' | 'structure';
  warnings: string[];
  error?: string;
  script: string;
  scenes: { id: string; title: string }[];
  shots: FilmShotDraft[];
};

export type FilmProgress = {
  sceneCount: number;
  shotCount: number;
  pending: number;
  running: number;
  done: number;
  failed: number;
  imageDone: number;
  videoDone: number;
  analysis: 'idle' | 'confirmed';
  steps: { step: string; status: FilmTaskStatus | 'idle'; detail?: string }[];
  reviewExportReady: boolean;
};

type Beat = { kind: 'action' | 'dialogue'; text: string; speaker?: string };
type Draft = { n: number; title: string; beats: Beat[] };

export function filmScriptHash(script: string) {
  let hash = 5381;
  const text = script.trim();
  for (let i = 0; i < text.length; i += 1) hash = ((hash << 5) + hash + text.charCodeAt(i)) >>> 0;
  return hash.toString(16);
}

function episodeToken(raw?: string) {
  const n = (raw || 'EP01').toUpperCase().match(/(\d+)/)?.[1] || '01';
  return `EP${n.padStart(2, '0')}`;
}

function sceneIdOf(n: number) {
  return `SC${String(n).padStart(2, '0')}`;
}

function shotCodeOf(n: number) {
  return `SH${String(n).padStart(2, '0')}`;
}

function speakerOf(line: string) {
  const match = line.match(
    /^([A-ZÀ-ỴĐ][A-Za-zÀ-ỹĐđ]{0,24}(?:\s+[A-Za-zÀ-ỹĐđ][A-Za-zÀ-ỹĐđ]{0,24}){0,3})\s*[:：]\s*(\S.*)$/,
  );
  if (!match) return undefined;
  const name = (match[1] ?? '').trim();
  if (name.length < 2) return undefined;
  if (/VIDEO|EPISODE|TITLE|FORMAT|STYLE|NOTE|PROJECT|SEASON|STATUS|TONE|SCENE|SHOT|INT|EXT|CẢNH|CANH/i.test(name)) {
    return undefined;
  }
  return { name, rest: (match[2] ?? '').trim() };
}

function sceneOf(line: string) {
  const numbered = line.match(/^(?:SCENE|SC|CẢNH|CANH)\s*0*(\d+)\s*[.:—–-]?\s*(.*)$/i);
  if (numbered) return { n: Number(numbered[1]) || 1, title: (numbered[2] ?? '').trim() };
  const slug = line.match(/^(INT|EXT)\.?\s+(.+)$/i);
  if (slug) return { n: 0, title: line.trim() };
  return undefined;
}

function shotsFromDrafts(drafts: Draft[], episode: string): FilmShotDraft[] {
  const shots: FilmShotDraft[] = [];
  let shotN = 1;
  for (const draft of [...drafts].sort((a, b) => a.n - b.n)) {
    const groups: { actions: string[]; dialogue: string[]; reason: ShotSplitReason }[] = [];
    let current = { actions: [] as string[], dialogue: [] as string[], reason: 'DIALOGUE_PERFORMANCE' as ShotSplitReason };
    const flush = () => {
      if (!current.actions.length && !current.dialogue.length) return;
      groups.push(current);
      current = { actions: [], dialogue: [], reason: 'DIALOGUE_PERFORMANCE' };
    };
    for (const beat of draft.beats) {
      if (beat.kind === 'action') {
        if (current.actions.length || current.dialogue.length) flush();
        current = { actions: [beat.text], dialogue: [], reason: 'ACTION_CHANGE' };
        continue;
      }
      current.dialogue.push(`${beat.speaker}: ${beat.text}`);
    }
    flush();
    const sceneId = sceneIdOf(draft.n);
    groups.forEach((group, index) => {
      const story = [...group.actions, ...group.dialogue].join(' ').replace(/\s+/g, ' ').trim();
      if (story.length < 2) return;
      const shot = shotCodeOf(shotN);
      shots.push({
        id: `${episode}-${sceneId}-${shot}`,
        sceneId,
        sceneTitle: draft.title,
        order: shotN,
        story: story.slice(0, 400),
        dialogue: group.dialogue.join('\n'),
        visual: (group.actions.join(' ') || draft.title).slice(0, 240),
        splitReason: index === 0 && group.reason === 'DIALOGUE_PERFORMANCE' ? 'DIALOGUE_PERFORMANCE' : group.reason,
      });
      shotN += 1;
    });
  }
  return shots;
}

function analyzeStructure(script: string, episodeCode?: string): FilmScriptProposal {
  const episode = episodeToken(episodeCode);
  const lines = script.replace(/^\uFEFF/, '').split(/\r?\n/);
  const explicit = lines.some((line) => sceneOf(line.trim()));
  const drafts: Draft[] = [];
  let sceneN = 0;
  let current: Draft | undefined;
  let pendingBreak = false;
  const open = (title: string, n?: number) => {
    sceneN += 1;
    current = { n: n && n > 0 ? n : sceneN, title, beats: [] };
    drafts.push(current);
    return current;
  };
  for (const raw of lines) {
    const line = raw.trim();
    if (!line) {
      pendingBreak = true;
      continue;
    }
    const scene = explicit ? sceneOf(line) : sceneOf(line);
    if (scene && (explicit || scene.n > 0 || /^(INT|EXT)\b/i.test(line))) {
      open(scene.title, scene.n);
      pendingBreak = false;
      continue;
    }
    const speaker = speakerOf(line);
    if (!explicit && pendingBreak && current?.beats.some((beat) => beat.kind === 'dialogue')) {
      open(speaker ? '' : line.slice(0, 80));
      pendingBreak = false;
      if (!speaker) continue;
    }
    pendingBreak = false;
    if (!current) open('');
    if (speaker) current!.beats.push({ kind: 'dialogue', speaker: speaker.name, text: speaker.rest });
    else if (!/^(VIDEO|EPISODE|TITLE|FORMAT|STYLE|NOTE|PROJECT|SEASON)\b/i.test(line)) {
      current!.beats.push({ kind: 'action', text: line });
    }
  }
  const shots = shotsFromDrafts(drafts, episode);
  const scenes = drafts
    .filter((draft) => shots.some((shot) => shot.sceneId === sceneIdOf(draft.n)))
    .map((draft) => ({ id: sceneIdOf(draft.n), title: draft.title }));
  const heardSpeaker = drafts.some((draft) => draft.beats.some((beat) => beat.kind === 'dialogue'));
  const warnings: string[] = [];
  if (!explicit && !heardSpeaker) {
    warnings.push('Không thấy mốc cảnh hoặc lời thoại. Giữ một cảnh, một shot — sửa trong bản xem trước trước khi xác nhận.');
  } else if (!explicit && scenes.length < 2) {
    warnings.push('Không thấy tiêu đề cảnh. Các đoạn thoại cách nhau bởi dòng trống được tách thành cảnh.');
  }
  if (!shots.length) {
    return {
      ok: false,
      parser: 'structure',
      warnings,
      error: 'Không đọc được cảnh hoặc lời thoại.',
      script,
      scenes: [],
      shots: [],
    };
  }
  return { ok: true, parser: 'structure', warnings, script, scenes, shots };
}

function fromScreenplay(script: string): FilmScriptProposal | undefined {
  const parsed = parseFamixaPack(script);
  const parsedShots = parsed.episode?.shots ?? [];
  const weak = (parsed.warnings ?? []).some((warning) => /Không thấy SCENE/i.test(warning));
  if (!parsedShots.length || weak) return undefined;
  const scenes = (parsed.scenes ?? []).map((scene) => ({
    id: scene.id,
    title: scene.title || scene.environment || scene.id,
  }));
  const shots: FilmShotDraft[] = parsedShots.map((shot, index) => ({
    id: shot.id,
    sceneId: shot.sceneId || shot.scene,
    sceneTitle: scenes.find((scene) => scene.id === (shot.sceneId || shot.scene))?.title || shot.location || shot.scene,
    order: index + 1,
    story: shot.story,
    dialogue: shot.story,
    visual: shot.visual || shot.location || '',
    splitReason: shot.splitReason && shot.splitReason !== 'NONE' ? shot.splitReason : 'DIALOGUE_PERFORMANCE',
  }));
  if (!shots.length) return undefined;
  return {
    ok: true,
    parser: 'screenplay',
    warnings: parsed.warnings ?? [],
    script,
    scenes: scenes.length ? scenes : uniqueScenes(shots),
    shots,
  };
}

function uniqueScenes(shots: FilmShotDraft[]) {
  const seen = new Map<string, string>();
  for (const shot of shots) if (!seen.has(shot.sceneId)) seen.set(shot.sceneId, shot.sceneTitle);
  return [...seen.entries()].map(([id, title]) => ({ id, title }));
}

export function analyzeFilmScript(script: string, episodeCode?: string): FilmScriptProposal {
  const text = script.replace(/^\uFEFF/, '').trim();
  if (!text) {
    return {
      ok: false,
      parser: 'structure',
      warnings: [],
      error: 'Chưa có kịch bản.',
      script: '',
      scenes: [],
      shots: [],
    };
  }
  return fromScreenplay(text) ?? analyzeStructure(text, episodeCode);
}

export function shotHasProduction(run?: SeriesShotRun) {
  if (!run) return false;
  return Boolean(
    run.keyframeFileName ||
      (run.keyframeDataUrl || '').startsWith('data:image') ||
      run.previewUrl ||
      run.takeUrl ||
      run.lipsyncUrl ||
      run.localVideoPath ||
      run.turboTaskId ||
      (run.credits ?? 0) > 0 ||
      (run.runwayBilled ?? 0) > 0,
  );
}

function shotHasImage(run?: SeriesShotRun) {
  return Boolean(run?.keyframeFileName || (run?.keyframeDataUrl || '').startsWith('data:image'));
}

function shotHasVideo(run?: SeriesShotRun) {
  return Boolean(run?.previewUrl || run?.takeUrl || run?.lipsyncUrl || run?.localVideoPath);
}

export function applyConfirmedBreakdown(
  state: SeriesPilotState,
  proposal: FilmScriptProposal,
  opts: { confirmed: boolean; replaceProduced?: boolean; seriesCode?: string },
): { ok: true; state: SeriesPilotState } | { ok: false; reason: 'needs_confirm' | 'empty' | 'has_production'; shotIds: string[] } {
  if (!opts.confirmed) return { ok: false, reason: 'needs_confirm', shotIds: [] };
  if (!proposal.ok || proposal.shots.length === 0) return { ok: false, reason: 'empty', shotIds: [] };
  const nextIds = new Set(proposal.shots.map((shot) => shot.id));
  const produced = (state.episode?.shots ?? [])
    .map((shot) => shot.id)
    .filter((id) => !nextIds.has(id) && shotHasProduction(state.runs?.[id]));
  if (produced.length && !opts.replaceProduced) return { ok: false, reason: 'has_production', shotIds: produced };

  const scenes: FamixaSceneNode[] = proposal.scenes.map((scene) => ({
    id: scene.id,
    title: scene.title || undefined,
    environment: scene.title || undefined,
    content: proposal.shots
      .filter((shot) => shot.sceneId === scene.id)
      .map((shot) => shot.story)
      .join('\n'),
    characterIds: [],
    scriptBeats: proposal.shots
      .filter((shot) => shot.sceneId === scene.id)
      .map((shot) => ({ id: `${scene.id}-BEAT${String(shot.order).padStart(2, '0')}`, text: shot.story, shotIds: [shot.id] })),
  }));
  const shots: FamixaSeriesShot[] = proposal.shots.map((shot, index) => {
    const beatId = `${shot.sceneId}-BEAT${String(shot.order).padStart(2, '0')}`;
    return {
      id: shot.id,
      scene: shot.sceneId,
      shot: shot.id.split('-').at(-1) || shotCodeOf(index + 1),
      clock: '5s',
      seconds: 5,
      story: shot.story,
      visual: shot.visual,
      characters: [],
      characterIds: [],
      sceneId: shot.sceneId,
      location: shot.sceneTitle,
      motionPrompt: '',
      motionPromptVi: shot.story.slice(0, 220),
      status: 'story_locked',
      beatId,
      beatText: shot.story.slice(0, 280),
      previousShotId: proposal.shots[index - 1]?.id,
      splitReason: shot.splitReason,
      actionUnitIds: [`${beatId}-AU01`],
    };
  });
  const lines: FamixaLine[] = [];
  for (const shot of proposal.shots) {
    for (const row of shot.dialogue.split('\n')) {
      const speaker = speakerOf(row.trim());
      if (!speaker) continue;
      lines.push({
        id: `${shot.id}-L${lines.length + 1}`,
        characterId: speaker.name,
        text: speaker.rest,
        sceneId: shot.sceneId,
      });
    }
  }
  const hash = filmScriptHash(proposal.script);
  const previous = state.filmOrchestration;
  const samePlan =
    previous?.scriptHash === hash &&
    previous.tasks.length > 0 &&
    (state.episode?.shots ?? []).every((shot) => nextIds.has(shot.id)) &&
    proposal.shots.every((shot) => (state.episode?.shots ?? []).some((row) => row.id === shot.id));
  const episode = state.episode
    ? { ...state.episode, shots }
    : {
        seriesCode: opts.seriesCode || 'SERIES',
        seriesTitle: '',
        episode: episodeToken(undefined),
        title: proposal.scenes[0]?.title || '',
        premise: '',
        moral: '',
        ctaRule: '',
        shots,
      };
  return {
    ok: true,
    state: {
      ...state,
      packDraft: proposal.script,
      scriptLocked: true,
      storyReviewed: true,
      shotGraphLocked: true,
      parseWarnings: proposal.warnings,
      scenes,
      lines,
      episode,
      filmOrchestration: {
        analysis: 'confirmed',
        scriptHash: hash,
        script: proposal.script,
        screens: previous?.screens,
        motion: samePlan ? previous?.motion : undefined,
        tasks: samePlan ? previous!.tasks : [],
      },
    },
  };
}

function enabledSteps(steps: string[]) {
  const seen = new Set<string>();
  const list: string[] = [];
  for (const step of steps) {
    const key = step.trim();
    if (!key || seen.has(key)) continue;
    seen.add(key);
    list.push(key);
  }
  return list;
}

function dependenciesOf(step: string, enabled: string[]) {
  if (step === 'scene') return enabled.includes('script') ? ['script'] : [];
  if (step === 'shot') return enabled.includes('scene') ? ['scene'] : enabled.includes('script') ? ['script'] : [];
  if (step === 'voice' || step === 'image') return enabled.includes('shot') ? ['shot'] : [];
  if (step === 'video') {
    if (enabled.includes('image')) return ['image'];
    return enabled.includes('shot') ? ['shot'] : [];
  }
  if (step === 'review') return enabled.filter((item) => item !== 'review' && item !== 'export');
  if (step === 'export') {
    if (enabled.includes('review')) return ['review'];
    return enabled.filter((item) => item !== 'export');
  }
  return [];
}

export function planFilmTasks(
  state: SeriesPilotState,
  steps: string[],
  executor: FilmExecutor = 'none',
): FilmProductionTask[] {
  const enabled = enabledSteps(steps);
  const confirmed = state.filmOrchestration?.analysis === 'confirmed';
  const shots = state.episode?.shots ?? [];
  const tasks: FilmProductionTask[] = [];
  for (const shot of shots) {
    const mine = new Map<string, FilmProductionTask>();
    for (const step of enabled) {
      const needs = dependenciesOf(step, enabled);
      const missing = needs.find((need) => mine.get(need)?.status !== 'done');
      let status: FilmTaskStatus = 'unavailable';
      let detail = 'Chưa khả dụng';
      const motion = state.filmOrchestration?.motion;
      const stills = state.filmOrchestration?.stills ?? [];
      const imagesReady = stills.length > 0 && stills.every((item) => item.approved);
      const chosenVoice = state.filmOrchestration?.voiceChoice;
      const voiceReady = chosenVoice?.approved === true && Boolean(chosenVoice.voiceId);
      if (executor === 'novixa_local' && step === 'image' && imagesReady) {
        status = 'done';
        detail = `${stills.length} ảnh đã duyệt`;
      } else if (executor === 'novixa_local' && step === 'voice' && voiceReady) {
        status = 'done';
        detail = chosenVoice?.voiceId || NOVIXA_VOICE.voiceId;
      } else if (executor === 'novixa_local' && step === 'video' && novixaVideoCurrent(state)) {
        status = 'done';
        detail = motion?.videoPath || '';
      } else if (executor === 'novixa_local' && step === 'video' && stills.length > 0 && !imagesReady) {
        status = 'blocked';
        detail = 'Chưa duyệt ảnh.';
      } else if (executor === 'novixa_local' && step === 'video' && imagesReady && !voiceReady) {
        status = 'blocked';
        detail = 'Chưa xác nhận giọng.';
      } else if (executor === 'novixa_local' && step === 'video' && imagesReady && voiceReady && motion?.status === 'ready' && motion.videoPath && !novixaVideoCurrent(state)) {
        detail = 'Ảnh hoặc giọng đã đổi. Bấm Tạo video để dựng lại.';
      } else if (executor === 'novixa_local' && step === 'video' && motion?.status === 'running') {
        detail = motion.message || 'Đang chạy. Không tạo lượt mới.';
      } else if (executor === 'novixa_local' && !LOCAL_STEPS.has(step)) {
        detail = motion?.message || NOVIXA_LOCAL_RENDER_DETAIL;
      } else if (missing) {
        status = 'blocked';
        detail = missing === 'image' ? 'Chưa có hình — không chạy video.' : `Chưa xong bước ${missing}.`;
      } else if (LOCAL_STEPS.has(step)) {
        if (!confirmed) {
          status = 'blocked';
          detail = 'Chưa xác nhận phân cảnh.';
        } else {
          status = 'done';
          detail = 'Đã xác nhận trên phim.';
        }
      }
      const task = { taskKey: `${shot.id}:${step}`, shotId: shot.id, step, status, detail };
      mine.set(step, task);
      tasks.push(task);
    }
  }
  return tasks;
}

function sameTasks(left: FilmProductionTask[], right: FilmProductionTask[]) {
  if (left.length !== right.length) return false;
  return left.every((task, index) => task.taskKey === right[index]?.taskKey && task.status === right[index]?.status);
}

export function startFilmProduction(state: SeriesPilotState, steps: string[], executor: FilmExecutor = 'none') {
  if (state.filmOrchestration?.analysis !== 'confirmed') return { state, changed: false };
  const planned = planFilmTasks(state, steps, executor);
  const current = state.filmOrchestration.tasks;
  if (sameTasks(current, planned)) return { state, changed: false };
  if (current.length > 0) return continueFilmProduction(state, steps, executor);
  return {
    changed: true,
    state: {
      ...state,
      filmOrchestration: { ...state.filmOrchestration, tasks: planned },
    },
  };
}

export function continueFilmProduction(state: SeriesPilotState, steps: string[], executor: FilmExecutor = 'none') {
  if (state.filmOrchestration?.analysis !== 'confirmed') return { state, changed: false };
  const planned = planFilmTasks(state, steps, executor);
  const previous = new Map(state.filmOrchestration.tasks.map((task) => [task.taskKey, task]));
  const tasks = planned.map((task) => {
    const old = previous.get(task.taskKey);
    if (!old) return task;
    if (old.status === 'done' && !(executor === 'novixa_local' && old.step === 'video' && task.status !== 'done')) return old;
    if (old.status === 'failed') return { ...task, detail: task.detail };
    return { ...old, status: task.status, detail: task.detail };
  });
  const changed = !sameTasks(state.filmOrchestration.tasks, tasks);
  if (!changed) return { state, changed: false };
  return {
    changed: true,
    state: { ...state, filmOrchestration: { ...state.filmOrchestration, tasks } },
  };
}

export function applyNovixaMotion(state: SeriesPilotState, steps: string[], run: NovixaMotionRun): SeriesPilotState {
  if (state.filmOrchestration?.analysis !== 'confirmed') return state;
  const next = {
    ...state,
    filmOrchestration: { ...state.filmOrchestration, motion: run },
  };
  return {
    ...next,
    filmOrchestration: { ...next.filmOrchestration, tasks: planFilmTasks(next, steps, 'novixa_local') },
  };
}

export function failFilmTask(state: SeriesPilotState, taskKey: string, error: string): SeriesPilotState {
  const tasks = state.filmOrchestration?.tasks ?? [];
  if (!tasks.some((task) => task.taskKey === taskKey)) return state;
  return {
    ...state,
    filmOrchestration: {
      ...state.filmOrchestration!,
      tasks: tasks.map((task) => (task.taskKey === taskKey ? { ...task, status: 'failed', detail: error } : task)),
    },
  };
}

function rollup(tasks: FilmProductionTask[], step: string): { status: FilmTaskStatus | 'idle'; detail?: string } {
  const rows = tasks.filter((task) => task.step === step);
  if (!rows.length) return { status: 'idle' };
  if (rows.some((task) => task.status === 'failed')) return { status: 'failed', detail: rows.find((task) => task.detail)?.detail };
  if (rows.some((task) => task.status === 'blocked')) return { status: 'blocked', detail: rows.find((task) => task.status === 'blocked')?.detail };
  if (rows.some((task) => task.status === 'unavailable')) {
    return { status: 'unavailable', detail: 'Chưa khả dụng' };
  }
  if (rows.every((task) => task.status === 'done')) return { status: 'done' };
  return { status: 'idle' };
}

export function filmProductionSnapshot(state: SeriesPilotState, steps: string[]): FilmProgress {
  const shots = state.episode?.shots ?? [];
  const tasks = state.filmOrchestration?.tasks ?? [];
  const enabled = enabledSteps(steps);
  let done = 0;
  let failed = 0;
  let pending = 0;
  for (const shot of shots) {
    const mine = tasks.filter((task) => task.shotId === shot.id);
    if (mine.some((task) => task.status === 'failed')) failed += 1;
    else if (mine.length > 0 && mine.every((task) => task.status === 'done')) done += 1;
    else pending += 1;
  }
  const reviewRows = tasks.filter((task) => task.step === 'review' || task.step === 'export');
  return {
    sceneCount: new Set(shots.map((shot) => shot.sceneId || shot.scene)).size,
    shotCount: shots.length,
    pending,
    running: 0,
    done,
    failed,
    imageDone: shots.filter((shot) => shotHasImage(state.runs?.[shot.id])).length,
    videoDone: shots.filter((shot) => shotHasVideo(state.runs?.[shot.id])).length,
    analysis: state.filmOrchestration?.analysis === 'confirmed' ? 'confirmed' : 'idle',
    steps: enabled.map((step) => ({ step, ...rollup(tasks, step) })),
    reviewExportReady:
      enabled.includes('review') &&
      enabled.includes('export') &&
      reviewRows.length > 0 &&
      reviewRows.every((task) => task.status === 'done'),
  };
}

export function emptyFilmState(): SeriesPilotState {
  return emptyPilot();
}
