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

export function inferActingDirection(opts: {
  text?: string;
  characterId?: string;
  name?: string;
  emotion?: string;
  action?: string;
}): ActingDirection {
  const spoken = spokenFromAction(opts.action);
  const text = ((opts.text ?? '').replace(/\s+/g, ' ').trim() || spoken.text).trim();
  const blob = `${opts.emotion || ''} ${opts.action || ''} ${text}`.toLowerCase();
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

function labelOf(emotion: ActingEmotion, n: number) {
  const vi: Record<ActingEmotion, string> = {
    neutral: 'bình thường',
    uneasy: 'khó chịu',
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
  return raw.replace(/\[[^\]]+\]\s*/g, '').replace(/\s+/g, ' ').trim();
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

export function actingOfShot(lines: { text?: string; characterId?: string; name?: string; emotion?: string }[], action?: string) {
  const first = lines[0];
  return inferActingDirection({
    text: first?.text,
    characterId: first?.characterId,
    name: first?.name,
    emotion: first?.emotion,
    action,
  });
}
