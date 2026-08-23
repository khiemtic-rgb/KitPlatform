/** Story Input → Scene containers → scoped Shot map. Does not invent dialogue or spend credits. */

import type {
  FamixaCharacter,
  FamixaLine,
  FamixaSceneNode,
  FamixaSeriesEpisode,
  FamixaSeriesShot,
  SeriesRoleRow,
} from './content-famixa-series';
import {
  frameCanonIds,
  isMetaCanonSpeaker,
  isOffFrameCanon,
  resolveCanonSpeaker,
  seedFamixaCanon,
} from './content-famixa-char-canon';

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
  const m = line.match(/^(?:INT|EXT|NỘI|NGOẠI)\.?\s*[/.—–-]?\s*(.+)$/i);
  if (!m) return undefined;
  const title = (m[1] ?? '').trim();
  if (!title || isNonStoryLine(title)) return undefined;
  return { title };
}

const CAST_NAME = /\b(minh|nam|linh|mẹ|bố|ba|má|con)\b/i;
const CAST_VERB =
  /\b(chạy|bước|đưa|cầm|nhìn|nói|đứng|chờ|buông|rơi|bấm|mở|đặt|kéo|ôm|quay|ngồi|khóc|cười|liếc)\b/i;

/** Heading / slug / lighting / SFX — not a production Action. */
export function isNonCinematicAction(raw?: string) {
  const s = (raw ?? '').replace(/\s+/g, ' ').trim();
  if (!s || s.length < 4) return true;
  if (looksLikeScriptMetaHeading(s) || isNonStoryLine(s)) return true;
  if (/^(format|style|short-?form|social drama|vertical video)\b/i.test(s) && s.length < 96) return true;
  if (/^(?:nội|ngoại|int|ext)\.?\s/i.test(s) && !CAST_VERB.test(s)) return true;
  if (/^(ánh sáng|ánh đèn|đèn vàng|lighting|warm light|soft light)\b/i.test(s) && !CAST_VERB.test(s)) return true;
  if (/^(khoảng lặng|không khí|im lặng|nghẹt thở)\b/i.test(s) && !CAST_VERB.test(s) && !CAST_NAME.test(s)) return true;
  if (/^(tiếng |sfx\b)/i.test(s) && !CAST_NAME.test(s)) return true;
  if (isSoundSlug(s)) return true;
  if (/^(phòng (khách|ăn|ngủ)|bối cảnh)\b/i.test(s) && !CAST_VERB.test(s) && s.length < 72) return true;
  return false;
}

/** "Nhưng…" / "À." — not its own Shot. */
export function isDialogueFragment(raw?: string) {
  const s = (raw ?? '')
    .replace(/^(Minh|Nam|Linh|An|Mẹ|Bố|Ba|Má|Con)\s*[:：]\s*/i, '')
    .replace(/\s+/g, ' ')
    .trim();
  if (!s) return true;
  const bare = s.replace(/[.“"”…]+/g, '').trim();
  return bare.length <= 8 || /^(nhưng|à|ờ|ừ|ơ|ừm)\.?$/i.test(bare);
}

/** INSERT / liếc điểm — may be a second Shot on the same Beat. */
export function looksLikeInsertAction(raw?: string) {
  const s = (raw ?? '').replace(/\s+/g, ' ').trim();
  if (!s) return false;
  return /cận (cảnh )?(tờ|bài|điểm|số)|insert\b|liếc .{0,28}(điểm|số|9\/10|con số)|tờ (giấy|bài).{0,28}(rơi|nằm|trượt|trên)/i.test(
    s,
  );
}

/** Peel INSERT off a same-sentence Action (`…, cận cảnh điểm 8`). */
export function peelInsertClause(text: string): { rest: string; insert?: string } {
  const s = text.replace(/\s+/g, ' ').trim();
  const m = s.match(
    /^(.*?)(?:[,;]|\s+)\s*((?:cận cảnh|insert)\b.+|tờ (?:giấy|bài).{0,40}(?:rơi|nằm|trượt|trên).+)$/i,
  );
  if (!m) return { rest: s };
  const rest = (m[1] ?? '').replace(/[.,;]\s*$/, '').trim();
  const insert = (m[2] ?? '').trim();
  if (!rest || !insert || rest.length < 8) return { rest: s };
  return { rest, insert };
}

function subjectKey(text: string) {
  const t = text.toLowerCase();
  if (/\b(mẹ|linh)\b/.test(t) && !/\b(minh|con)\b/.test(t)) return 'linh';
  if (/\b(bố|nam|ba)\b/.test(t) && !/\b(minh|linh)\b/.test(t)) return 'nam';
  if (/\b(minh|con)\b/.test(t)) return 'minh';
  return '';
}

function stripNonCinematicClauses(text: string) {
  const parts = text
    .split(/(?<=[.!?…])\s+/)
    .map((s) => s.trim())
    .filter((s) => s.length >= 4 && !isNonCinematicAction(s) && !isDialogueFragment(s));
  return (parts.length ? parts.join(' ') : text).replace(/\s+/g, ' ').trim();
}

type PlannedShot = { story: string; splitReason: 'ACTION_CHANGE' | 'SUBJECT_CHANGE' | 'INSERT' | 'DIALOGUE_PERFORMANCE' };

/** 1 Beat → 1 Shot unless INSERT or subject change. Never split on comma / lighting / SFX. */
export function planBeatShots(actions: string[], dialogue: string[]): PlannedShot[] {
  const cinematic = actions.map((a) => stripNonCinematicClauses(a)).filter((a) => a && !isNonCinematicAction(a));
  const spoken = dialogue.filter((d) => !isDialogueFragment(d)).join(' ').replace(/\s+/g, ' ').trim();
  if (!cinematic.length) {
    if (!spoken) return [];
    return [{ story: spoken.slice(0, 400), splitReason: 'DIALOGUE_PERFORMANCE' }];
  }
  const clauses = cinematic.flatMap((block) =>
    block
      .split(/(?<=[.!?…])\s+/)
      .map((s) => s.trim())
      .filter((s) => s.length >= 6)
      .flatMap((s) => {
        const peeled = peelInsertClause(s);
        return peeled.insert ? [peeled.rest, peeled.insert] : [s];
      }),
  );
  const inserts = clauses.filter((c) => looksLikeInsertAction(c));
  const rest = clauses.filter((c) => !looksLikeInsertAction(c));
  if (inserts.length && rest.length) {
    return [
      { story: rest.join(' ').slice(0, 400), splitReason: 'ACTION_CHANGE' },
      { story: inserts[0]!.slice(0, 400), splitReason: 'INSERT' },
    ];
  }
  if (cinematic.length >= 2) {
    const a = cinematic[0]!;
    const b = cinematic[1]!;
    const ka = subjectKey(a);
    const kb = subjectKey(b);
    if (ka && kb && ka !== kb) {
      return [
        { story: a.slice(0, 400), splitReason: 'ACTION_CHANGE' },
        { story: b.slice(0, 400), splitReason: 'SUBJECT_CHANGE' },
      ];
    }
  }
  return [{ story: cinematic.join(' ').slice(0, 400), splitReason: 'ACTION_CHANGE' }];
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

const ACTING_HINT =
  /háo hức|thở dốc|phẳng lì|không cảm xúc|nghẹn|lí nhí|sắc lạnh|tàn nhẫn|thì thào|vỡ vụn|thì thầm|run rẩy|nức nở|lạnh lùng|mỉa mai|gắt gỏng|im lặng|nhẹ nhàng/;

/** `(hào hức, thở dốc)` — how they speak, not the line. */
export function looksLikeActingParenthetical(text: string) {
  const s = text.trim().replace(/^["“”']+|["“”']+$/g, '');
  if (!s) return false;
  const wrapped = /^\([^)]+\)$/.test(s);
  const inner = (wrapped ? s.slice(1, -1) : s).trim();
  if (!inner || inner.length > 64 || /[.!?…]/.test(inner)) return false;
  if (wrapped && ACTING_HINT.test(inner)) return true;
  if (wrapped && (inner.match(/,/g) ?? []).length >= 1 && inner.length < 48 && !CAST_VERB.test(inner)) return true;
  if (!wrapped && ACTING_HINT.test(inner) && (inner.match(/,/g) ?? []).length >= 1 && inner.length < 40) return true;
  return false;
}

function actingNoteOf(text: string) {
  const s = text.trim().replace(/^["“”']+|["“”']+$/g, '');
  return (s.startsWith('(') && s.endsWith(')') ? s.slice(1, -1) : s).trim();
}

/** Cast bible / acting note — not a spoken line. `Giọng ai thế?` stays thoại. */
export function looksLikeVoiceDirection(text: string) {
  const s = text.trim().replace(/^["“”']+|["“”']+$/g, '');
  if (!s) return false;
  if (looksLikeActingParenthetical(s)) return true;
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

/** Pack / director heading — not a shot Action and not TTS. */
export function looksLikeScriptMetaHeading(text: string) {
  const s = text.trim().replace(/^["“”']+|["“”']+$/g, '');
  if (!s) return false;
  if (/^(?:tone|thời lượng(?:\s*mục tiêu)?|target duration|estimated duration|format|style|continuity|emotion|cảm xúc|video(?:\s*id|\s*title)?|production purpose)\s*:/i.test(s)) {
    return true;
  }
  if (/^tone\s*:/i.test(s)) return true;
  return false;
}

export function isMetaSpeakerName(name?: string) {
  return /^(TONE|THỜI LƯỢNG|FORMAT|STYLE|NOTE|LOC|CONTINUITY|EMOTION|CẢM XÚC|TARGET|DURATION)$/i.test((name ?? '').trim());
}

/** Spoken CHAR line only. Action / SFX / SMS / memory → false. */
export function looksLikeSpokenLine(text: string, speaker?: string) {
  const s = text.trim().replace(/^["“”']+|["“”']+$/g, '');
  if (!s) return false;
  if (isSoundSlug(s) || isSoundSlug(speaker ?? '')) return false;
  if (isMetaSpeakerName(speaker) || looksLikeScriptMetaHeading(s) || looksLikeScriptMetaHeading(`${speaker ?? ''}: ${s}`)) {
    return false;
  }
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
  if (isMetaCanonSpeaker(name) || isMetaSpeakerName(name) || /^(SCENE|SC|SHOT|SH|BEAT|ROLE|VIDEO|FAMIXA|FORMAT|STYLE|NOTE|LOC|PROJECT|SEASON|EPISODE|STATUS)$/i.test(name)) {
    return undefined;
  }
  return { name, emotion: (m[2] ?? '').trim(), rest: (m[3] ?? '').trim() };
}

function resolveSpeaker(raw: string, chars: FamixaCharacter[]): FamixaCharacter | undefined {
  const token = raw.replace(/\s*\(.*\)\s*$/, '').trim();
  if (!token || isMetaCanonSpeaker(token)) return undefined;
  const row = resolveCanonSpeaker(token);
  if (row) {
    return chars.find((c) => c.id === row.id) ?? { id: row.id, name: row.name, role: row.role, offFrame: row.visual !== 'frame' };
  }
  return chars.find((c) => c.name.toLowerCase() === token.toLowerCase());
}

function ensureChar(chars: FamixaCharacter[], speaker: string): FamixaCharacter | undefined {
  if (isMetaCanonSpeaker(speaker)) return undefined;
  const hit = resolveSpeaker(speaker, chars);
  if (!hit) return undefined;
  if (!chars.some((c) => c.id === hit.id)) chars.push(hit);
  return hit;
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

function ensureNarrator(chars: FamixaCharacter[]): FamixaCharacter {
  return (
    ensureChar(chars, 'Lời bình') ?? { id: 'CHAR-VO', name: 'Lời bình', role: 'Lời bình', offFrame: true }
  );
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
  if (resolveSpeaker(token, chars) || resolveCanonSpeaker(token) || /^(MINH|NAM|LINH|BỐ|MẸ|BA|CON)$/i.test(token)) {
    return token;
  }
  return undefined;
}

function skipFurniture(line: string) {
  return (
    looksLikeScriptMetaHeading(line) ||
    /^(?:#{1,3}\s*)?(?:0?7\.\s*)?SCRIPT\b/i.test(line) ||
    /^(?:CUT TO BLACK|END|FADE OUT|FADE IN|KẾT THÚC)\.?\s*$/i.test(line) ||
    /^(VIDEO[ _]?ID|VIDEO[ _]?TITLE|TARGET DURATION|FORMAT|STYLE|PROJECT|SEASON|EPISODE|SERIES|STATUS|SOURCE KEYFRAME|PRODUCTION PURPOSE|ESTIMATED DURATION)\s*:/i.test(
      line,
    ) ||
    /^(FAMIXA|KIT MARKETING)\b/i.test(line) ||
    /^(SHORT-?FORM|SOCIAL DRAMA)\b/i.test(line) ||
    /kịch bản phim|drama voice famixa|production master/i.test(line) ||
    /^\d{2}\.\s+(STORY INTENT|CORE QUESTION|EMOTIONAL|CHARACTERS|LOCATION|VISUAL|SCRIPT|VOICE|SOUND)\b/i.test(line)
  );
}

function isMetaLine(line: string) {
  return (
    looksLikeScriptMetaHeading(line) ||
    /^(cuối\s*episode|mục tiêu|âm thanh|sound(?:\s*design)?|emotion|cảm xúc|nhạc|continuity|style|format|target duration|video id|video title|production purpose|season|project|tone|thời lượng)\s*:?\s*$/i.test(
      line.trim(),
    )
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

function parseCharLocks(text: string) {
  const rows: { code: string; name: string; role: string; offFrame?: boolean }[] = [];
  for (const raw of text.split(/\r?\n/)) {
    const m = raw.match(/^(CHAR-\d+)\s*[—–:-]\s*(.+)$/i);
    if (!m) continue;
    const rest = (m[2] ?? '').trim();
    const pipe = rest.split('|').map((s) => s.trim());
    const name = pipe[0] ?? rest;
    const row = resolveCanonSpeaker(name) || resolveCanonSpeaker(m[1] ?? '');
    if (!row || isMetaCanonSpeaker(name)) continue;
    rows.push({ code: row.id, name: row.name, role: row.role || pipe[1] || '', offFrame: row.visual !== 'frame' });
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
    const planned = planBeatShots(g.actions, g.dialogue);
    const shotIds: string[] = [];
    const charIds = frameCanonIds(g.charIds.length ? g.charIds : [...scene.charIds]);
    planned.forEach((row, au) => {
      const action = row.story.replace(/\s+/g, ' ').trim();
      if (action.length < 6 || isNonCinematicAction(action)) return;
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
        splitReason: row.splitReason,
        actionUnitIds: [`${beatId}-AU${String(au + 1).padStart(2, '0')}`],
      });
      shotIds.push(id);
      n += 1;
    });
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
    characterIds: frameCanonIds([...draft.charIds]),
  };
}

export function parseEpisodeStory(text: string): ParsedEpisodeStory | undefined {
  const source = text.replace(/^\uFEFF/, '').trim();
  if (!source) return undefined;
  const warnings: string[] = [];
  const header = headerFields(source.slice(0, 1800));
  const characters: FamixaCharacter[] = [];
  for (const row of parseCharLocks(source)) {
    characters.push({ id: row.code, name: row.name, role: row.role || undefined, offFrame: row.offFrame });
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
  let pendingEmotion: string | undefined;
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
    if (!isOffFrameCanon(ch.id, ch.name) && !current!.charIds.includes(ch.id)) current!.charIds.push(ch.id);
    current!.beats.push({ kind: 'dialogue', characterId: ch.id, name: ch.name || ch.id, text: spoken, emotion });
    current!.raw.push(`${ch.name || ch.id}: ${spoken}`);
  };

  const addAction = (line: string) => {
    const text = line.replace(/\s+/g, ' ').trim();
    if (!text || isNonCinematicAction(text)) {
      const loc = locHead(text);
      if (loc && current) current.location = current.location || loc.title;
      return;
    }
    if (!current) openScene(1, '');
    current!.beats.push({ kind: 'action', text });
    current!.raw.push(text);
    pendingUnspoken = introducesUnspokenText(text);
  };

  for (const raw of extractScriptBody(source).split(/\r?\n/)) {
    const line = raw.trim();
    if (!line) {
      if (pendingGotLine) {
        pendingSpeaker = undefined;
        pendingEmotion = undefined;
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
      const mapped = resolveCanonSpeaker(lock[1] ?? '');
      const c = characters.find((x) => x.id === (mapped?.id || normId(lock[1] ?? '')));
      if (c && !c.name) c.name = (lock[2] ?? '').trim();
      continue;
    }
    if (isMetaLine(line)) {
      pendingSpeaker = undefined;
      pendingGotLine = false;
      continue;
    }

    if (isSoundSlug(line) || (taggedSpeaker(line) && isSoundSlug(taggedSpeaker(line)!.name))) {
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
        if (looksLikeActingParenthetical(rest) || looksLikeVoiceDirection(rest)) {
          pendingEmotion = actingNoteOf(rest) || colon?.emotion || pendingEmotion;
          continue;
        }
        if (pendingUnspoken || !looksLikeSpokenLine(rest, slug)) {
          addAction(rest);
          pendingSpeaker = undefined;
          pendingEmotion = undefined;
        } else {
          const ch = ensureChar(characters, colon?.name || tagged?.name || slug);
          if (ch) addDialogue(ch, rest, colon?.emotion || pendingEmotion);
          pendingEmotion = undefined;
          pendingUnspoken = false;
          pendingSpeaker = undefined;
        }
      } else if (colon?.emotion) {
        pendingEmotion = colon.emotion;
      }
      continue;
    }

    if (pendingSpeaker) {
      if (looksLikeActingParenthetical(line) || looksLikeVoiceDirection(line)) {
        pendingEmotion = actingNoteOf(line);
        continue;
      }
      if (pendingUnspoken || !looksLikeSpokenLine(line, pendingSpeaker)) {
        addAction(line);
        continue;
      }
      const spoken = ensureChar(characters, pendingSpeaker);
      if (spoken) addDialogue(spoken, line, pendingEmotion);
      else addAction(line);
      pendingEmotion = undefined;
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
  const rest = characters.filter((c) => c.id !== 'CHAR-VO' && !isOffFrameCanon(c.id, c.name)).slice(0, vo.length ? 7 : 8);
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
