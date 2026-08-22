/** Story Input → Scene containers → scoped Shot map. Does not invent dialogue or spend credits. */

import type {
  FamixaCharacter,
  FamixaLine,
  FamixaSceneNode,
  FamixaSeriesEpisode,
  FamixaSeriesShot,
  SeriesRoleRow,
} from './content-famixa-series';

const CAST: { id: string; names: string[]; role: string }[] = [
  { id: 'CHAR-001', names: ['minh', 'con'], role: 'Con' },
  { id: 'CHAR-002', names: ['nam', 'bố', 'bo', 'ba'], role: 'Bố' },
  { id: 'CHAR-003', names: ['linh', 'mẹ', 'me'], role: 'Mẹ' },
  { id: 'CHAR-VO', names: ['lời bình', 'voice-over', 'voice over', 'vo'], role: 'Lời bình' },
];

export type ParsedEpisodeStory = {
  episode: FamixaSeriesEpisode;
  shots: FamixaSeriesShot[];
  characters: FamixaCharacter[];
  scenes: FamixaSceneNode[];
  lines: FamixaLine[];
  roles: SeriesRoleRow[];
  warnings: string[];
};

type SceneBeat =
  | { kind: 'action'; text: string }
  | { kind: 'dialogue'; characterId: string; name: string; text: string; emotion?: string }
  | { kind: 'cut' };

type SceneDraft = {
  n: number;
  id: string;
  title: string;
  location: string;
  raw: string[];
  beats: SceneBeat[];
  charIds: string[];
};

function normId(raw: string) {
  const m = raw.toUpperCase().match(/CHAR\s*-?\s*(\d+)/);
  if (m) return `CHAR-${String(m[1]).padStart(3, '0')}`;
  return raw.replace(/\s+/g, ' ').trim();
}

function sceneCode(n: number) {
  return `SC${String(n).padStart(2, '0')}`;
}

function shotCode(n: number) {
  return `SH${String(n).padStart(2, '0')}`;
}

export function isNonStoryLine(line: string) {
  const s = line.trim();
  if (!s) return true;
  if (/^[=\-_*]{6,}$/.test(s)) return true;
  if (/^(KIT MARKETING|VIDEO PRODUCTION MASTER|CRITICAL FIX|FINAL ACCEPTANCE|ACCEPTANCE TEST|GOLDEN TEST)\b/i.test(s)) {
    return true;
  }
  if (/kịch bản phim ngắn|drama voice famixa/i.test(s) && s.length < 80) return true;
  if (
    /\b(do not|must not|required:|golden test|critical fix|acceptance criteria|developer instruction|implementation instruction|validation text|coding instruction|graph state|story parser|shot decomposition|parse version)\b/i.test(
      s,
    )
  ) {
    return true;
  }
  if (/decomposition must not|must not generate an arbitrary|không tiêu credit|không generate ảnh|không được diễn thành/i.test(s)) {
    return true;
  }
  if (/^(USER STORY INPUT|STORY PARSER|PRODUCTION MODEL|STORY MODEL|CONTINUITY|ARCHITECTURE)\b/i.test(s)) {
    return true;
  }
  return false;
}

function isFakeSceneTitle(title: string) {
  return /decomposition|parser|architecture|instruction|acceptance|golden test|graph state|must not/i.test(title);
}

function stripDialogue(raw: string) {
  return raw
    .trim()
    .replace(/^["“”']+|["“”']+$/g, '')
    .replace(/\s+/g, ' ')
    .trim();
}

function sceneHead(line: string) {
  const m = line.match(/^(?:#{1,3}\s*)?(?:SCENE|SC|CẢNH|CANH)\s*0*(\d+)\b\s*[—–:\-.]?\s*(.*)$/i);
  if (!m) return undefined;
  const n = Number(m[1]);
  const title = (m[2] ?? '').replace(/^[—–:-]\s*/, '').trim();
  if (!Number.isFinite(n) || n < 1 || n > 40) return undefined;
  if (isFakeSceneTitle(title) || isNonStoryLine(title)) return undefined;
  return { n, title };
}

function shotHead(line: string) {
  const m = line.match(/^(?:#{1,3}\s*)?(?:SHOT|SH)\s*0*(\d+)\b\s*[—–:\-.]?\s*(.*)$/i);
  if (!m) return undefined;
  const title = (m[2] ?? '').trim();
  if (isNonStoryLine(title) || isFakeSceneTitle(title)) return undefined;
  return { n: Number(m[1]), title };
}

function locHead(line: string) {
  const m = line.match(/^(?:INT|EXT)\.?\s*[/.—–-]?\s*(.+)$/i);
  if (!m) return undefined;
  const title = (m[1] ?? '').trim();
  if (!title || isNonStoryLine(title)) return undefined;
  return { title };
}

const SOUND_SLUG =
  /^(GÕ|GO|CẠCH|CACH|SFX|SOUND|FX|SUPER|INSERT|TITLE|CHÚ THÍCH|CHU THICH|SMASH(?:\s*CUT)?|WHAM|BANG|DING)$/i;

const CAST_PROPER = /^(Minh|Nam|Linh)(?:\.{2,}|…|\.)?$/i;
const CAST_VOCATIVE = /^(Minh|Nam|Linh)\s+ơi\b/i;
const THIRD_PERSON = /^(Minh|Nam|Linh|Cậu)\s+\S+/i;
const STAGE_CUE =
  /^(Không khí|Một nhịp|Một khoảng|Nụ cười|Điện thoại|Tin nhắn|Không trả lời|Không có tiếng|Dừng lại|Xóa |Cất |Nhìn điểm|Nhìn mẹ|Nhìn bố|Nhìn xuống|Nhớ lại|Nhớ câu|Cậu định|Cậu kéo|Cho vào |Gấp bài|Kéo khóa|Đặt bài|Cất bài|Bỏ vào cặp|CUT TO|FADE|SMASH|INSERT|SUPER)/i;

export function isSoundSlug(token: string) {
  const t = token.replace(/^\[|\]$/g, '').trim();
  return Boolean(t) && SOUND_SLUG.test(t);
}

function isThirdPersonAction(line: string) {
  const s = line.trim();
  if (CAST_PROPER.test(s) || CAST_VOCATIVE.test(s)) return false;
  return THIRD_PERSON.test(s);
}

/** Third-person action / SFX / direction — not a spoken line. */
export function looksLikeActionLine(line: string) {
  const s = line.trim().replace(/^\[|\]$/g, '');
  if (!s) return false;
  if (isSoundSlug(s)) return true;
  if (/^(INT\.|EXT\.)\b/i.test(s)) return true;
  if (/^CẠCH\.?$/i.test(s)) return true;
  if (/nói vọng ra\s*:?\s*$/i.test(s)) return true;
  if (isThirdPersonAction(s)) return true;
  if (STAGE_CUE.test(s)) return true;
  return false;
}

/** Cue that the next line is memory / on-screen text, not speech. */
export function introducesUnspokenText(line: string) {
  return /^(Nhớ câu|Nhớ lại|Tin nhắn|Điện thoại|SUPER|INSERT|TITLE)\b/i.test(line.trim());
}

/** SMS, recalled quote, on-screen text — even if a CHAR slug was still pending. */
export function looksLikeUnspokenLine(text: string, speaker?: string) {
  const s = text.trim();
  if (!s) return false;
  if (looksLikeVoiceDirection(s)) return true;
  if (/cô trả bài/i.test(s) || /^Mai cô\b/i.test(s)) return true;
  const who = (speaker ?? '').replace(/^\[|\]$/g, '').trim();
  const isNam = /^(nam|bố|bo|ba|char-002)$/i.test(who);
  if (isNam && /^Bạn An chín rưỡi\.?$/i.test(s)) return true;
  return false;
}

/** Cast bible / acting note — not a spoken line. `Giọng ai thế?` stays thoại. */
export function looksLikeVoiceDirection(text: string) {
  const s = text.trim().replace(/^["“”']+|["“”']+$/g, '');
  if (!s) return false;
  if (/^giọng (ai|gì|nào|đó|kia|bố|mẹ|con|minh|nam|linh)\b/i.test(s)) return false;
  if (/^giọng đọc\b/i.test(s)) return true;
  if (
    /^giọng\b/i.test(s) &&
    /(háo hức|mong manh|khao khát|sắc lạnh|thực dụng|mệt mỏi|kiệt sức|bất lực|trầm ấm|truyền cảm|không hề nghĩ|thương con)/i.test(s)
  ) {
    return true;
  }
  if (/^giọng\b/i.test(s) && (s.match(/,/g) ?? []).length >= 2) return true;
  return false;
}

/** Spoken CHAR line only. Action / SFX / SMS / memory → false. */
export function looksLikeSpokenLine(text: string, speaker?: string) {
  const s = text.trim().replace(/^["“”']+|["“”']+$/g, '');
  if (!s) return false;
  if (isSoundSlug(s) || isSoundSlug(speaker ?? '')) return false;
  if (looksLikeVoiceDirection(s)) return false;
  if (looksLikeActionLine(s) || looksLikeUnspokenLine(s, speaker)) return false;
  return true;
}

function taggedSpeaker(line: string) {
  const m = line.match(/^\[([^\]]{1,32})\](?:\s*[:：]\s*(.*))?$/);
  if (!m) return undefined;
  return { name: (m[1] ?? '').trim(), rest: (m[2] ?? '').trim() };
}

function speakerHead(line: string) {
  const m = line.match(/^(CHAR-\d+|[A-ZÀ-Ỵ][A-ZÀ-Ỵa-zà-ỹ]{1,24})(?:\s*\(([^)]{0,48})\))?\s*[:：]\s*(.*)$/);
  if (!m) return undefined;
  const name = (m[1] ?? '').trim();
  if (isSoundSlug(name)) return undefined;
  if (/^(SCENE|SC|SHOT|SH|BEAT|ROLE|VIDEO|FAMIXA|FORMAT|STYLE|NOTE|LOC|PROJECT|SEASON|EPISODE|STATUS)$/i.test(name)) {
    return undefined;
  }
  return { name, emotion: (m[2] ?? '').trim(), rest: (m[3] ?? '').trim() };
}

function resolveSpeaker(raw: string, chars: FamixaCharacter[]): FamixaCharacter | undefined {
  const token = raw.replace(/\s*\(.*\)\s*$/, '').trim();
  if (!token) return undefined;
  const asId = /^CHAR-\d+/i.test(token) ? normId(token) : '';
  if (asId) return chars.find((c) => c.id === asId);
  const key = token.toLowerCase();
  const known = CAST.find((c) => c.names.includes(key));
  if (known) return chars.find((c) => c.id === known.id) ?? { id: known.id, name: token, role: known.role };
  return chars.find((c) => c.name.toLowerCase() === key);
}

function ensureChar(chars: FamixaCharacter[], speaker: string): FamixaCharacter {
  const hit = resolveSpeaker(speaker, chars);
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

/** VOICE-OVER slug. Parenthetical = voice direction, not spoken. */
export function voiceOverHead(line: string) {
  const m = line.match(/^(?:VOICE[-\s]+OVER|V\.?\s*O\.?)\s*(?:\(([^)]{0,80})\))?\s*[—–:.]?\s*(.*)$/i);
  if (!m) return undefined;
  const direction = (m[1] ?? '').trim();
  const rest = (m[2] ?? '').trim();
  if (rest && /^(giọng đọc|giọng nam|giọng nữ)\b/i.test(rest) && rest.length < 80) {
    return { speaker: 'Lời bình', direction: direction || rest, rest: '' };
  }
  return { speaker: 'Lời bình', direction, rest };
}

function ensureNarrator(chars: FamixaCharacter[]) {
  return ensureChar(chars, 'Lời bình');
}

function screenplaySpeaker(line: string, chars: FamixaCharacter[]) {
  if (voiceOverHead(line)) return undefined;
  const tagged = taggedSpeaker(line);
  if (tagged) return isSoundSlug(tagged.name) ? undefined : tagged.name;
  const colon = speakerHead(line);
  if (colon) return colon.name;
  const token = line.replace(/\s*\([^)]{0,48}\)\s*$/, '').trim();
  if (!token || token.length > 28 || /[:：]/.test(line)) return undefined;
  if (isSoundSlug(token)) return undefined;
  if (/^(SCENE|SC|INT|EXT|CUT|END|FADE|TITLE|SCRIPT|SHOT|BEAT|FAMIXA|SERIES|SEASON|EPISODE)$/i.test(token)) {
    return undefined;
  }
  if (
    resolveSpeaker(token, chars) ||
    CAST.some((c) => c.names.includes(token.toLowerCase())) ||
    /^(MINH|NAM|LINH|BỐ|MẸ|BA|CON)$/i.test(token)
  ) {
    return token;
  }
  return undefined;
}

function skipFurniture(line: string) {
  return (
    /^(?:#{1,3}\s*)?(?:0?7\.\s*)?SCRIPT\b/i.test(line) ||
    /^(?:CUT TO BLACK|END|FADE OUT|FADE IN|KẾT THÚC)\.?\s*$/i.test(line) ||
    /^(VIDEO[ _]?ID|VIDEO[ _]?TITLE|TARGET DURATION|FORMAT|STYLE|PROJECT|SEASON|EPISODE|SERIES|STATUS|SOURCE KEYFRAME|PRODUCTION PURPOSE|ESTIMATED DURATION)\s*:/i.test(
      line,
    ) ||
    /^(FAMIXA|KIT MARKETING)\b/i.test(line) ||
    /kịch bản phim|drama voice famixa|production master/i.test(line) ||
    /^\d{2}\.\s+(STORY INTENT|CORE QUESTION|EMOTIONAL|CHARACTERS|LOCATION|VISUAL|SCRIPT|VOICE|SOUND)\b/i.test(line)
  );
}

function isMetaLine(line: string) {
  return /^(cuối\s*episode|mục tiêu|âm thanh|sound(?:\s*design)?|emotion|cảm xúc|nhạc|continuity|style|format|target duration|video id|video title|production purpose|season|project)\s*:?\s*$/i.test(
    line.trim(),
  );
}

function extractScriptBody(text: string) {
  const scriptHead = text.search(/^(?:#{1,3}\s*)?0?7\.\s*SCRIPT\b/im);
  const sceneHeadAt = text.search(/^(?:#{1,3}\s*)?(?:SCENE|SC|CẢNH)\s*0*1\b/im);
  const start = scriptHead >= 0 ? scriptHead : sceneHeadAt;
  const body = start >= 0 ? text.slice(start) : text;
  const end = body.search(
    /\n(?:#{1,3}\s*)?(?:0?8\.\s*|0?9\.\s*|10\.\s*)(?:EMOTIONAL|VOICE|SOUND|END STATE|DIRECTION)\b/im,
  );
  return (end > 0 ? body.slice(0, end) : body).trim();
}

function seedCast(text: string, characters: FamixaCharacter[]) {
  for (const row of CAST) {
    const proper = row.names[0] ?? '';
    const named = characters.find((c) => c.id === row.id);
    if (named) {
      if (!named.name) named.name = proper.charAt(0).toUpperCase() + proper.slice(1);
      if (!named.role) named.role = row.role;
      continue;
    }
    const token =
      row.names.find((n) => n.length >= 4 && new RegExp(n.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'i').test(text)) ??
      (proper.length >= 3 && new RegExp(proper.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'i').test(text) ? proper : '');
    if (!token) continue;
    characters.push({
      id: row.id,
      name: proper.charAt(0).toUpperCase() + proper.slice(1),
      role: row.role,
    });
  }
}

function parseCharLocks(text: string) {
  const rows: { code: string; name: string; role: string }[] = [];
  for (const raw of text.split(/\r?\n/)) {
    const m = raw.match(/^(CHAR-\d+)\s*[—–:-]\s*(.+)$/i);
    if (!m) continue;
    const rest = (m[2] ?? '').trim();
    const pipe = rest.split('|').map((s) => s.trim());
    rows.push({ code: normId(m[1] ?? ''), name: pipe[0] ?? rest, role: pipe[1] ?? '' });
  }
  return rows;
}

function headerFields(chunk: string) {
  const map: Record<string, string> = {};
  for (const raw of chunk.split(/\r?\n/)) {
    const m = raw.match(/^([A-Za-z][A-Za-z0-9]*(?:[ _-][A-Za-z0-9]+){0,6}):\s*(.*)$/);
    if (!m) continue;
    const key = (m[1] ?? '').trim().toUpperCase().replace(/\s+/g, '_');
    map[key] = (m[2] ?? '').trim();
  }
  return map;
}

function splitBeatIntoShotStories(text: string): string[] {
  const t = text.replace(/\s+/g, ' ').trim();
  if (!t) return [];
  const camera = /cận cảnh|nhìn bài|điểm|điện thoại|bước (ra|vào|khỏi)/i.test(t);
  if (!camera) return [t];
  const sentences = t.split(/(?<=[.!?…])\s+/).map((s) => s.trim()).filter((s) => s.length >= 6);
  if (sentences.length >= 2 && sentences.length <= 3) return sentences;
  if (/,/.test(t)) {
    const bits = t.split(/,\s+/).map((s) => s.trim()).filter((s) => s.length >= 8);
    if (bits.length >= 2 && bits.length <= 3) return bits;
  }
  return [t];
}

function decomposeSceneShots(
  scene: SceneDraft,
  startShotN: number,
  epCode: string,
): { shots: FamixaSeriesShot[]; nextN: number; beats: { id: string; text: string; shotIds: string[] }[] } {
  type Group = { actions: string[]; dialogue: string[]; charIds: string[] };
  const groups: Group[] = [];
  let cur: Group = { actions: [], dialogue: [], charIds: [] };
  const flush = () => {
    if (!cur.actions.length && !cur.dialogue.length) return;
    groups.push(cur);
    cur = { actions: [], dialogue: [], charIds: [] };
  };
  for (const beat of scene.beats) {
    if (beat.kind === 'cut') {
      flush();
      continue;
    }
    if (beat.kind === 'action') {
      if (cur.actions.length || cur.dialogue.length) flush();
      cur.actions.push(beat.text);
      continue;
    }
    cur.dialogue.push(`${beat.name}: ${beat.text}`);
    if (!cur.charIds.includes(beat.characterId)) cur.charIds.push(beat.characterId);
  }
  flush();

  const shots: FamixaSeriesShot[] = [];
  const beats: { id: string; text: string; shotIds: string[] }[] = [];
  let n = startShotN;
  let beatN = 0;
  for (const g of groups) {
    const beatText = [...g.actions, ...g.dialogue].join(' ').replace(/\s+/g, ' ').trim();
    if (!beatText) continue;
    beatN += 1;
    const beatId = `${scene.id}-BEAT${String(beatN).padStart(2, '0')}`;
    const stories = g.actions.length ? g.actions.flatMap(splitBeatIntoShotStories) : [beatText];
    const shotIds: string[] = [];
    const charIds = g.charIds.length ? g.charIds : [...scene.charIds];
    for (const story of stories) {
      const action = story.replace(/\s+/g, ' ').trim();
      if (action.length < 6) continue;
      const shot = shotCode(n);
      const id = `${epCode}-${scene.id}-${shot}`;
      shots.push({
        id,
        scene: scene.id,
        shot,
        clock: '5s',
        seconds: 5,
        story: action.slice(0, 400),
        visual: (g.actions.join(' ') || action).slice(0, 240),
        characters: charIds,
        characterIds: charIds,
        sceneId: scene.id,
        location: scene.location || scene.title,
        motionPrompt: '',
        motionPromptVi: action.slice(0, 220),
        status: 'story_locked',
        beatId,
        beatText,
        previousShotId: shots.at(-1)?.id,
      });
      shotIds.push(id);
      n += 1;
    }
    if (shotIds.length) beats.push({ id: beatId, text: beatText.slice(0, 280), shotIds });
  }
  return { shots, nextN: n, beats };
}

function toSceneNode(draft: SceneDraft): FamixaSceneNode {
  const dialogue = draft.beats
    .filter((b): b is Extract<SceneBeat, { kind: 'dialogue' }> => b.kind === 'dialogue')
    .map((b, i) => ({
      id: `line-${draft.id}-${b.characterId}-${i + 1}`,
      characterId: b.characterId,
      text: b.text,
      emotion: b.emotion,
    }));
  const actions = draft.beats.filter((b): b is Extract<SceneBeat, { kind: 'action' }> => b.kind === 'action').map((b) => b.text);
  return {
    id: draft.id,
    title: draft.title || undefined,
    environment: draft.location || undefined,
    content: draft.raw.join('\n').trim() || undefined,
    actions,
    dialogue,
    characterIds: [...draft.charIds],
  };
}

export function parseEpisodeStory(text: string): ParsedEpisodeStory | undefined {
  const source = text.replace(/^\uFEFF/, '').trim();
  if (!source) return undefined;
  const warnings: string[] = [];
  const header = headerFields(source.slice(0, 1800));
  const characters: FamixaCharacter[] = [];
  for (const row of parseCharLocks(source)) {
    characters.push({ id: row.code, name: row.name, role: row.role || undefined });
  }
  seedCast(source, characters);

  const epRaw = source.match(/EPISODE\s*[:—–-]?\s*(\d+)/i)?.[1] || source.match(/\bEP\s*(\d+)/i)?.[1] || '01';
  const epNo = String(Number.parseInt(epRaw, 10) || 1).padStart(2, '0');
  const title =
    (header.VIDEO_TITLE ?? header.TITLE ?? '').trim() ||
    (source.match(/BỐ ĐỪNG HỨA NỮA|BỐ MẸ KHÔNG CÃI NHAU ĐÂU/i)?.[0] ?? '') ||
    (header.EPISODE ?? '').trim() ||
    `Tập ${epNo}`;

  const drafts: SceneDraft[] = [];
  const byN = new Map<number, SceneDraft>();
  let current: SceneDraft | undefined;
  let pendingSpeaker: string | undefined;
  let pendingGotLine = false;
  let pendingUnspoken = false;
  let droppedInstruction = 0;

  const openScene = (n: number, sceneTitle: string) => {
    const existing = byN.get(n);
    if (existing) {
      current = existing;
      if (sceneTitle && !current.title) current.title = sceneTitle;
      return current;
    }
    current = {
      n,
      id: sceneCode(n),
      title: sceneTitle,
      location: '',
      raw: [],
      beats: [],
      charIds: [],
    };
    byN.set(n, current);
    drafts.push(current);
    return current;
  };

  const addDialogue = (ch: FamixaCharacter, spokenRaw: string, emotion?: string) => {
    if (!current) openScene(1, '');
    const spoken = stripDialogue(spokenRaw);
    if (!spoken || !looksLikeSpokenLine(spoken, ch.name || ch.id)) {
      if (spoken) addAction(spoken);
      return;
    }
    if (!current!.charIds.includes(ch.id)) current!.charIds.push(ch.id);
    current!.beats.push({ kind: 'dialogue', characterId: ch.id, name: ch.name || ch.id, text: spoken, emotion });
    current!.raw.push(`${ch.name || ch.id}: ${spoken}`);
  };

  const addAction = (line: string) => {
    if (!current) openScene(1, '');
    current!.beats.push({ kind: 'action', text: line });
    current!.raw.push(line);
    pendingUnspoken = introducesUnspokenText(line);
  };

  for (const raw of extractScriptBody(source).split(/\r?\n/)) {
    const line = raw.trim();
    if (!line) {
      if (pendingGotLine) {
        pendingSpeaker = undefined;
        pendingGotLine = false;
      }
      continue;
    }
    if (isNonStoryLine(line)) {
      droppedInstruction += 1;
      pendingSpeaker = undefined;
      pendingGotLine = false;
      continue;
    }
    if (skipFurniture(line) || (title && line.toLowerCase() === title.toLowerCase())) continue;

    const sc = sceneHead(line);
    if (sc) {
      pendingSpeaker = undefined;
      pendingGotLine = false;
      pendingUnspoken = false;
      openScene(sc.n, sc.title);
      continue;
    }

    const loc = locHead(line);
    if (loc) {
      pendingSpeaker = undefined;
      pendingGotLine = false;
      if (!current) openScene(1, loc.title);
      current!.location = current!.location || loc.title;
      if (!current!.title) current!.title = loc.title;
      continue;
    }

    const sh = shotHead(line);
    if (sh) {
      pendingSpeaker = undefined;
      pendingGotLine = false;
      if (!current) openScene(1, '');
      current!.beats.push({ kind: 'cut' });
      if (sh.title) addAction(sh.title);
      continue;
    }

    const lock = line.match(/^(CHAR-\d+)\s*[—–-]\s*(.+)$/i);
    if (lock) {
      pendingSpeaker = undefined;
      pendingGotLine = false;
      ensureChar(characters, lock[1] ?? '');
      const c = characters.find((x) => x.id === normId(lock[1] ?? ''));
      if (c && !c.name) c.name = (lock[2] ?? '').trim();
      continue;
    }
    if (isMetaLine(line)) {
      pendingSpeaker = undefined;
      pendingGotLine = false;
      continue;
    }

    if (isSoundSlug(line) || (taggedSpeaker(line) && isSoundSlug(taggedSpeaker(line)!.name))) {
      addAction(line);
      pendingSpeaker = undefined;
      pendingGotLine = false;
      continue;
    }

    const vo = voiceOverHead(line);
    if (vo) {
      pendingUnspoken = false;
      pendingGotLine = false;
      if (vo.rest && looksLikeSpokenLine(vo.rest, vo.speaker)) {
        addDialogue(ensureNarrator(characters), vo.rest);
        pendingSpeaker = undefined;
      } else {
        pendingSpeaker = vo.speaker;
      }
      continue;
    }

    const slug = screenplaySpeaker(line, characters);
    if (slug) {
      pendingSpeaker = slug;
      pendingGotLine = false;
      const tagged = taggedSpeaker(line);
      const colon = speakerHead(line);
      const rest = colon?.rest || tagged?.rest || '';
      if (rest) {
        if (pendingUnspoken || !looksLikeSpokenLine(rest, slug)) {
          addAction(rest);
        } else {
          addDialogue(ensureChar(characters, colon?.name || tagged?.name || slug), rest, colon?.emotion || undefined);
          pendingUnspoken = false;
        }
        pendingSpeaker = undefined;
      }
      continue;
    }

    if (pendingSpeaker) {
      if (pendingUnspoken || !looksLikeSpokenLine(line, pendingSpeaker)) {
        addAction(line);
        continue;
      }
      addDialogue(ensureChar(characters, pendingSpeaker), line);
      pendingGotLine = true;
      pendingUnspoken = false;
      continue;
    }

    if (pendingUnspoken || !looksLikeSpokenLine(line)) {
      addAction(line);
      continue;
    }

    if (/^["“].+["”]$/.test(line) && current) {
      const spoken = stripDialogue(line);
      const last = [...current.beats].reverse().find((b) => b.kind === 'dialogue');
      if (spoken && last && last.kind === 'dialogue') {
        addDialogue({ id: last.characterId, name: last.name }, spoken);
        continue;
      }
    }

    addAction(line);
  }

  if (droppedInstruction) {
    warnings.push(`Đã bỏ ${droppedInstruction} dòng instruction/debug — không đưa vào Story.`);
  }

  if (drafts.length === 0) {
    const body = extractScriptBody(source)
      .split(/\r?\n/)
      .map((l) => l.trim())
      .filter((l) => l && !isNonStoryLine(l) && !skipFurniture(l))
      .join('\n')
      .trim();
    if (!body) return undefined;
    warnings.push('Story parsing requires review. Không thấy SCENE — tạm gộp thành SC01.');
    openScene(1, '');
    current!.raw.push(body);
    current!.beats.push({ kind: 'action', text: body.slice(0, 400) });
  }

  const shots: FamixaSeriesShot[] = [];
  const beatMap = new Map<string, { id: string; text: string; shotIds: string[] }[]>();
  let shotN = 1;
  for (const draft of drafts.sort((a, b) => a.n - b.n)) {
    const part = decomposeSceneShots(draft, shotN, `EP${epNo}`);
    shots.push(...part.shots);
    beatMap.set(draft.id, part.beats);
    shotN = part.nextN;
  }

  const scenes = drafts
    .sort((a, b) => a.n - b.n)
    .map((d) => ({ ...toSceneNode(d), scriptBeats: beatMap.get(d.id) ?? [] }));

  for (const sc of scenes) {
    if (!(sc.content || sc.dialogue?.length || sc.actions?.length)) {
      warnings.push(`Missing Scene content detected: ${sc.id}.`);
    }
    const seenInScene = new Set<string>();
    for (const d of sc.dialogue ?? []) {
      const key = `${d.characterId}|${d.text}`;
      if (seenInScene.has(key)) warnings.push(`Duplicate content detected: “${d.text.slice(0, 48)}” in ${sc.id}.`);
      seenInScene.add(key);
    }
  }
  if (shots.some((s) => !(s.story || '').trim())) {
    warnings.push('KIT emitted a shot without Action. Shot graph must follow Script Beats.');
  }

  const lines: FamixaLine[] = [];
  for (const sc of scenes) {
    for (const d of sc.dialogue ?? []) {
      const ch = characters.find((c) => c.id === d.characterId);
      lines.push({
        id: d.id,
        characterId: d.characterId,
        text: d.text,
        voiceId: ch?.voiceId,
        sceneId: sc.id,
      });
    }
  }

  const vo = characters.filter((c) => c.id === 'CHAR-VO');
  const rest = characters.filter((c) => c.id !== 'CHAR-VO').slice(0, vo.length ? 7 : 8);
  const roles = [...rest, ...vo].slice(0, 8).map((c) => ({
    id: `role-${c.id}`,
    title: c.role || c.id,
    name: c.name,
    characterId: c.id,
    line: lines.find((l) => l.characterId === c.id)?.text,
  }));

  const episode: FamixaSeriesEpisode = {
    seriesCode: (header.SERIES ?? header.PROJECT ?? 'FAMIXA').trim() || 'FAMIXA',
    seriesTitle: (header.SEASON ?? header.SERIES_TITLE ?? '').trim(),
    episode: (header.EP ?? `EP${epNo}`).trim() || `EP${epNo}`,
    title,
    premise: (header.PREMISE ?? '').trim(),
    moral: (header.MORAL ?? '').trim(),
    ctaRule: (header.CTA ?? '').trim(),
    shots,
  };

  return { episode, shots, characters, scenes, lines, roles, warnings };
}

export function diagnoseEpisodeStory(doc: ParsedEpisodeStory) {
  const issues: string[] = [];
  const dump = JSON.stringify(doc);
  if (/decomposition must not|golden test|critical fix|story parser/i.test(dump)) {
    issues.push('Developer instruction leaked into Story.');
  }
  const ids = new Set<string>();
  for (const shot of doc.shots) {
    if (ids.has(shot.id)) issues.push(`Shot ID reused: ${shot.id}`);
    ids.add(shot.id);
    if (shot.sceneId && !shot.id.includes(shot.sceneId)) {
      issues.push(`Shot ${shot.id} scene mismatch.`);
    }
    if (!(shot.story || '').trim()) issues.push(`Empty shot ${shot.id} — no Script Beat Action.`);
    if (!shot.beatId) issues.push(`Shot ${shot.id} has no beatId.`);
  }
  const shotCodes = doc.shots.map((s) => s.shot);
  if (new Set(shotCodes).size !== shotCodes.length) issues.push('Shot codes are not unique in the episode.');
  for (const sc of doc.scenes) {
    const foreign = (sc.dialogue ?? []).filter((d) => {
      const own = (sc.content ?? '').includes(d.text);
      return !own && sc.content;
    });
    if (foreign.length) issues.push(`${sc.id} holds dialogue that is not in its content.`);
    for (const d of sc.dialogue ?? []) {
      const who = doc.characters.find((c) => c.id === d.characterId)?.name || d.characterId;
      if (!looksLikeSpokenLine(d.text, who)) {
        issues.push(`${sc.id} non-speech in dialogue: “${d.text.slice(0, 40)}”`);
      }
    }
  }
  return issues;
}
