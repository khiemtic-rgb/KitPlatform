/** FAMIXA Luật diễn xuất & cảm xúc V1 — performance direction only. Does not rewrite Script. */

export type ActingEmotion =
  | 'neutral'
  | 'uneasy'
  | 'tense'
  | 'annoyed'
  | 'burst'
  | 'silent'
  | 'hurt'
  | 'soft'
  | 'aftertaste';

export type ActingDirection = {
  emotion: ActingEmotion;
  intensity: 1 | 2 | 3 | 4 | 5;
  pace: 'slow' | 'natural' | 'fast';
  volume: 'low' | 'mid' | 'high';
  pauseSec: number;
  label: string;
};

/** Locked on a dialogue line. TTS / KF / I2V read this — do not re-infer after lock. */
export type LinePerformance = ActingDirection & {
  emphasis?: string;
  locked?: boolean;
};

export const ACTING_EMOTIONS: ActingEmotion[] = [
  'neutral',
  'uneasy',
  'tense',
  'annoyed',
  'burst',
  'silent',
  'hurt',
  'soft',
  'aftertaste',
];

export type LineActingInput = {
  text?: string;
  characterId?: string;
  name?: string;
  emotion?: string;
  action?: string;
  performance?: LinePerformance;
};

const MINH = /CHAR-001|minh/i;
const LINH = /CHAR-003|linh|mẹ/i;
const NAM = /CHAR-002|nam|bố/i;

export const ACTING_LAW_LOCK =
  'Đời thường, tiết chế. Cảm xúc có đường cong. Không la hét thay kịch bản. Không ôm/xin lỗi xóa tổn thương. Không nhìn camera.';

function spokenFromAction(action?: string) {
  const raw = (action ?? '').replace(/\s+/g, ' ').trim();
  const m = raw.match(/\b(minh|nam|linh|mẹ|bố)\s*[:—–-]\s*(.+)$/i);
  if (!m) return { who: '', text: raw };
  return { who: m[1] ?? '', text: (m[2] ?? '').trim() };
}

/** Drop stage cue after emdash — not spoken, not TTS. */
export function stripStageDirection(text: string) {
  let s = (text ?? '').replace(/\s+/g, ' ').trim();
  const cue = s.match(/\s*[—–]\s+(.+)$/);
  if (cue?.[1] && /^(?:KHOE|CHẠY|NHÌN|NGẬP|VỠ|BEAT|INSERT|CUT|\()/i.test(cue[1])) {
    s = s.slice(0, s.length - cue[0].length).trim();
  }
  s = s.replace(/\s*\(\d+\s*[–\-]\s*\d+\s*s?\)\s*$/i, '').trim();
  return s;
}

export function inferActingDirection(opts: {
  text?: string;
  characterId?: string;
  name?: string;
  emotion?: string;
  action?: string;
}): ActingDirection {
  const spoken = spokenFromAction(opts.action);
  const text = stripStageDirection((opts.text ?? '').replace(/\s+/g, ' ').trim() || spoken.text).trim();
  const blob = stripStageDirection(`${opts.emotion || ''} ${opts.action || ''} ${text}`).toLowerCase();
  const who = opts.characterId || opts.name || spoken.who || '';
  let emotion: ActingEmotion = 'neutral';
  let intensity: ActingDirection['intensity'] = 2;
  let pace: ActingDirection['pace'] = 'natural';
  let volume: ActingDirection['volume'] = 'mid';
  let pauseSec = 0.15;

  if (/im lặng|không trả lời|không có gì|con biết rồi/i.test(text)) {
    emotion = 'hurt';
    intensity = 2;
    pace = 'slow';
    volume = 'low';
    pauseSec = 0.45;
  } else if (/thì sao|tám thì có gì|nhìn bạn|cố thêm|chín rưỡi|bằng lòng/i.test(text) || /so sánh|phủ nhận|ép/i.test(blob)) {
    emotion = LINH.test(who) ? 'tense' : 'annoyed';
    intensity = 3;
    pace = 'fast';
    volume = 'mid';
    pauseSec = 0.12;
  } else if (/tám\?|tám\.?$/i.test(text) && LINH.test(who)) {
    emotion = 'uneasy';
    intensity = 2;
    pace = 'slow';
    volume = 'low';
    pauseSec = 0.4;
  } else if (/vỡ òa|chín điểm|gỡ được|háo hức/i.test(text) && MINH.test(who)) {
    emotion = 'burst';
    intensity = 4;
    pace = 'fast';
    volume = 'high';
    pauseSec = 0.12;
  } else if (/mẹ xem|được tám|từ năm lên tám|khoe/i.test(text) && MINH.test(who)) {
    emotion = 'uneasy';
    intensity = 2;
    pace = 'fast';
    volume = 'mid';
  } else if (/mẹ!|về khoe/i.test(text) && MINH.test(who)) {
    emotion = 'uneasy';
    intensity = 2;
    pace = 'fast';
    volume = 'mid';
  } else if (/thôi đi|anh biết/i.test(text) && NAM.test(who)) {
    emotion = 'tense';
    intensity = 2;
    pace = 'natural';
    volume = 'mid';
  } else if (/xin lỗi|mẹ chỉ muốn|bố chỉ muốn/i.test(text)) {
    emotion = 'soft';
    intensity = 2;
    pace = 'slow';
    volume = 'low';
    pauseSec = 0.35;
  } else if (/tổn thương|đau|nhỏ/i.test(opts.emotion || '')) {
    emotion = 'hurt';
    intensity = 2;
    pace = 'slow';
    volume = 'low';
  }

  if (MINH.test(who) && emotion === 'neutral' && text.length <= 12) {
    pace = 'fast';
  }

  return {
    emotion,
    intensity,
    pace,
    volume,
    pauseSec,
    label: labelOf(emotion, intensity),
  };
}

export function actingLabel(emotion: ActingEmotion, n: number) {
  return labelOf(emotion, n);
}

export function asActingDirection(p: LinePerformance): ActingDirection {
  return {
    emotion: p.emotion,
    intensity: p.intensity,
    pace: p.pace,
    volume: p.volume,
    pauseSec: p.pauseSec,
    label: p.label || labelOf(p.emotion, p.intensity),
  };
}

/** Locked field wins. Regex infer is draft only. */
export function resolveLinePerformance(opts: LineActingInput): ActingDirection {
  if (opts.performance?.locked) return asActingDirection(opts.performance);
  return inferActingDirection(opts);
}

export function lockLinePerformance(dir: ActingDirection, extra?: { emphasis?: string }): LinePerformance {
  return { ...dir, ...extra, locked: true };
}

export function actingOfLines(lines: LineActingInput[], action?: string): ActingDirection {
  if (!lines.length) return inferActingDirection({ action });
  return resolveLinePerformance({ ...lines[0], action: action || lines[0]?.action });
}

export function actingI2vBriefFromLines(lines: LineActingInput[], action?: string) {
  if (!lines.length) return actingI2vBrief(inferActingDirection({ action }));
  const dirs = lines.map((l) => resolveLinePerformance({ ...l, action: action || l.action }));
  if (dirs.length === 1) return actingI2vBrief(dirs[0]!);
  const beats = dirs.map((d, i) => `${i + 1} ${d.label}`).join(' then ');
  return `Acting beats: ${beats}. ${actingI2vBrief(dirs.at(-1)!)}`;
}

export function stillFaceFromPerformance(name: string, dir: ActingDirection) {
  const who = (name || 'Speaker').trim();
  if (dir.emotion === 'hurt' || dir.emotion === 'soft' || dir.emotion === 'silent') {
    return `${who}: held face, eyes down a little, no cheerful smile. Intensity ${dir.intensity}/5.`;
  }
  if (dir.emotion === 'annoyed' || dir.emotion === 'tense') {
    return `${who}: tight jaw, small mouth, not posing. Intensity ${dir.intensity}/5.`;
  }
  if (dir.emotion === 'uneasy') {
    return `${who}: small hopeful face that can shrink; not a big grin. Intensity ${dir.intensity}/5.`;
  }
  if (dir.emotion === 'burst') {
    return `${who}: brighter eyes, restrained lift — not a scream. Intensity ${dir.intensity}/5.`;
  }
  return `${who}: everyday restrained face. Intensity ${dir.intensity}/5.`;
}

function labelOf(emotion: ActingEmotion, n: number) {
  const vi: Record<ActingEmotion, string> = {
    neutral: 'bình thường',
    uneasy: 'mong manh',
    tense: 'căng',
    annoyed: 'bực',
    burst: 'bùng',
    silent: 'im',
    hurt: 'đau',
    soft: 'dịu',
    aftertaste: 'dư âm',
  };
  return `${vi[emotion]} ${n}/5`;
}

/** English I2V/still only. No Vietnamese. No invented hug/lesson. */
export function actingI2vClause(dir: ActingDirection) {
  const face =
    dir.emotion === 'hurt' || dir.emotion === 'soft'
      ? 'Small held face; eyes down a little; no tears unless the still already has them.'
      : dir.emotion === 'annoyed' || dir.emotion === 'tense'
        ? 'Tight jaw, short replies in the face; no big gestures.'
        : dir.emotion === 'uneasy'
          ? 'A flicker of hope then doubt on the face. No speech to camera.'
          : 'Everyday restrained acting.';
  return (
    `${face} Intensity ${dir.intensity}/5. No screaming. No looking at camera. ` +
    `No moral lecture. No hug or apology reset. No exaggerated acting.`
  );
}

/** Short English clause for the 900-char Runway prompt. */
export function actingI2vBrief(dir: ActingDirection) {
  const face =
    dir.emotion === 'hurt' || dir.emotion === 'soft'
      ? 'held face, eyes down'
      : dir.emotion === 'annoyed' || dir.emotion === 'tense'
        ? 'tight jaw, small motion'
        : dir.emotion === 'uneasy'
          ? 'hope then doubt'
          : 'restrained everyday acting';
  return `Acting ${dir.intensity}/5: ${face}. No scream, hug, lecture, or look at camera.`;
}

export function actingStillClause(dir: ActingDirection) {
  return actingI2vClause(dir);
}

/**
 * Text sent to ElevenLabs. Keep the Script line only.
 * English v3 tags (`[sadly]`) are spoken aloud when language_code=vi — do not prefix them.
 * Acting goes through voice_settings (stability / style / speed).
 */
export function actingTtsPerformText(spoken: string, _dir?: ActingDirection) {
  return spokenFromPerformText(spoken);
}

export function spokenFromPerformText(raw: string) {
  return stripStageDirection(raw.replace(/\[[^\]]+\]\s*/g, '').replace(/\s+/g, ' ').trim());
}

export function actingTtsVoiceSettings(
  dir: ActingDirection,
  base?: { stability?: number; similarity?: number; style?: number; speed?: number },
  opts?: { child?: boolean; northern?: boolean },
) {
  let speed = base?.speed ?? 1;
  let stability = base?.stability ?? 0.5;
  let style = base?.style ?? 0;
  let similarity = base?.similarity ?? 0.75;
  if (opts?.child) {
    speed = Math.min(1.12, Math.max(speed || 1, 1.04));
    stability = Math.min(stability, 0.44);
    style = Math.min(Math.max(style, 0.16), 0.22);
  }
  if (opts?.northern) {
    similarity = Math.max(similarity, 0.84);
    stability = Math.max(stability, opts.child ? 0.4 : 0.5);
    style = Math.min(style, 0.2);
  }
  if (dir.pace === 'slow') speed = Math.min(speed, opts?.child ? 1.0 : 0.88);
  if (dir.pace === 'fast') speed = Math.min(1.18, Math.max(speed, opts?.child ? 1.1 : 1.04));
  if (dir.volume === 'low') style = Math.min(style, opts?.child ? 0.22 : 0.12);
  if (dir.emotion === 'hurt' || dir.emotion === 'soft' || dir.emotion === 'silent') {
    if (opts?.child) {
      stability = Math.min(0.48, Math.max(stability, 0.36));
      speed = Math.min(speed, 1.0);
      style = Math.min(Math.max(style, 0.2), 0.3);
    } else {
      stability = Math.max(stability, 0.58);
      speed = Math.min(speed, 0.9);
      style = Math.min(style, 0.15);
    }
  }
  if (dir.emotion === 'annoyed' || dir.emotion === 'tense') {
    style = Math.max(style, opts?.child ? 0.28 : 0.18);
  }
  if (dir.emotion === 'burst' && dir.intensity >= 4) {
    style = Math.max(style, 0.32);
    speed = Math.min(1.18, speed + 0.08);
  }
  return {
    stability,
    similarityBoost: similarity,
    style,
    speed,
  };
}

/** Face/mood from Script notes. Does not invent plot. */
export function stillFaceFromScriptNote(name: string, note: string) {
  const blob = `${name} ${note}`.toLowerCase();
  const who = (name || 'Beat').trim();
  if (/điện thoại|chăm chăm|bận bịu|bận/.test(blob)) {
    return `${who}: eyes on phone, busy, not posing for a photo, not smiling.`;
  }
  if (/sắc lạnh|thực dụng|không vui|nghiêm nghị|nghiêm|không hài lòng|không hề nghĩ/.test(blob)) {
    return `${who}: stern unsatisfied face, tight mouth, no warm smile.`;
  }
  if (/kiệt sức|bất lực|chán nản|thương con|mệt|thành tích/.test(blob)) {
    return `${who}: weary, disappointed, slumped — not cheerful.`;
  }
  if (/háo hức|mong manh|khao khát/.test(blob)) {
    return `${who}: small hopeful face that can shrink; not a big grin.`;
  }
  if (/tối|căng|áp lực|thành tích/.test(blob)) {
    return `${who}: tense everyday face, dim room, no stock-photo smile.`;
  }
  return '';
}

export function stillAtmosphereFromAction(action: string, location?: string) {
  const blob = `${action} ${location || ''}`.toLowerCase();
  const bits: string[] = [];
  if (/điện thoại|chăm chăm|bận bịu/.test(blob)) bits.push('A parent is busy on a phone, not looking warmly at the child.');
  if (/tối|đêm|evening/.test(blob)) bits.push('Dim warm indoor evening. Not a bright sunlit catalog room.');
  if (/nghiêm nghị|nghiêm|không vui|không hài lòng|sắc/.test(blob)) bits.push('Faces are serious and unsatisfied.');
  if (/chán nản|thành tích/.test(blob)) bits.push('A parent looks quietly disappointed about grades — not a pep talk.');
  bits.push('Tense ordinary family atmosphere. No cheerful stock smiles. No hug or apology.');
  bits.push('No family portrait. No affectionate huddle. People keep distance.');
  return bits.join(' ');
}

/** Staging from who speaks — does not invent a hug or a new plot beat. */
export function compileShotBlocking(opts: {
  speakerNames: string[];
  peopleNames: string[];
  action?: string;
  namJustEntered?: boolean;
  namAlreadyIn?: boolean;
}) {
  const speakers = opts.speakerNames.map((s) => s.trim()).filter(Boolean);
  const people = opts.peopleNames.map((s) => s.trim()).filter(Boolean);
  const blob = `${opts.action || ''} ${speakers.join(' ')}`.toLowerCase();
  const bits: string[] = [];
  if (speakers.length) {
    bits.push(
      `SPEAKER LOCK: ${speakers.join(', ')} ${speakers.length > 1 ? 'are' : 'is'} speaking. Face visible, looking at the other person — never the lens. Do not hide a speaking parent off-frame.`,
    );
  }
  if (people.length >= 3) {
    bits.push('BLOCKING: three people in one tense room, standing apart. Not a family portrait. No huddle, no arms around shoulders, no cozy group photo.');
  } else if (people.length === 2) {
    bits.push(
      `BLOCKING: two people (${people.join(', ')}), physical distance, not leaning in affectionately. Primary face visible. Secondary may be a foreground shoulder — do not force an even two-shot. Do not crop the speaker to torso/hands.`,
    );
  }
  if (opts.namJustEntered || ((speakers.some((n) => /nam|bố|ba/i.test(n)) || /bước vào|về rồi|cửa/.test(blob)) && !opts.namAlreadyIn)) {
    bits.push(
      'Nam/father ENTERS now: stand opposite Linh, talking to her. Same dim room. Same one man — not a new extra. Not behind the table as a blur.',
    );
  } else if (opts.namAlreadyIn) {
    bits.push(
      'Nam/father STAYS: same face, same shirt, same place as the previous still. Keep all three people. Do not redesign him. Do not hide him behind Linh.',
    );
  }
  if (/điện thoại|chăm chăm|bận bịu/.test(blob)) {
    bits.push('A parent stays on the phone, not facing the child warmly.');
  }
  bits.push('No affectionate pose. No family portrait. Distance shows tension.');
  return bits.join(' ');
}

export function actingOfShot(lines: LineActingInput[], action?: string) {
  return actingOfLines(lines, action);
}
