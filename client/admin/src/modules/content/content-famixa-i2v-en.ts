/** Runway I2V motion must be English. Story Action stays Vietnamese on the card. */

const PACK_RE = /VIDEO\s*ID|PRODUCTION MASTER|=======|CHAR-\d+\s*[—–-]/i;
export const I2V_VI_RE = /[àáạảãăắằặẳẵâấầậẩẫèéẹẻẽêếềệểễìíịỉĩòóọỏõôốồộổỗơớờợởỡùúụủũưứừựửữỳýỵỷỹđ]/i;

const RULES: { re: RegExp; en: string }[] = [
  { re: /cổng trường|cổng/, en: 'outside a school gate in afternoon light' },
  { re: /trong bếp|bếp|nhà bếp/, en: 'in a home kitchen' },
  { re: /buổi tối|tối/, en: 'in warm evening light' },
  { re: /chiều/, en: 'in late-afternoon light' },
  { re: /lớp|trường/, en: 'near the school' },
  { re: /bài kiểm tra|tờ giấy|nhìn bài|điểm|tám\?/, en: 'holding a graded test paper' },
  { re: /nhận lấy|cầm|đưa/, en: 'receives a paper with both hands' },
  { re: /về rồi|về nhà|anh về/, en: 'Nam arrives home in the evening' },
  { re: /cơm|nấu/, en: 'preparing a family meal' },
  { re: /điện thoại|tin nhắn/, en: 'glancing at a phone' },
  { re: /chạy/, en: 'a short run then a stop' },
  { re: /bước ra|bước vào|đi /, en: 'a few walking steps' },
  { re: /cười/, en: 'a small natural smile' },
  { re: /khựng|đứng lại/, en: 'Minh freezes for a beat' },
  { re: /nhìn mẹ|nhìn/, en: 'looks toward family, not the camera' },
  { re: /gọi mẹ|mẹ!/, en: 'the boy calls to his mother' },
  { re: /nói|hỏi/, en: 'a small face reaction, no freeze' },
];

/** English-only motion line for Runway. Does not invent a new story beat. */
export function englishI2vMotion(action: string, _seconds?: number) {
  const raw = (action ?? '').replace(/\s+/g, ' ').trim();
  if (raw.length > 20 && !PACK_RE.test(raw) && !I2V_VI_RE.test(raw)) {
    return raw.length <= 900 ? raw : raw.slice(0, 900);
  }
  const t = raw.toLowerCase();
  const hits = RULES.filter((r) => r.re.test(t)).map((r) => r.en);
  const unique = [...new Set(hits)].slice(0, 4);
  const beat = unique.length
    ? `Play this beat: ${unique.join('; ')}.`
    : 'Play the action shown in the still.';
  const who = [
    /\bminh\b/i.test(raw) ? 'Minh' : '',
    /\bnam\b/i.test(raw) ? 'Nam' : '',
    /\blinh\b/i.test(raw) ? 'Linh' : '',
  ].filter(Boolean);
  const subject = who[0] || 'The subject';
  const text = `${subject}: ${beat} Blink and breathe. Camera remains steady.`;
  return text.length <= 900 ? text : text.slice(0, 900);
}

export function i2vPromptIsEnglish(prompt: string) {
  return !I2V_VI_RE.test(prompt) && !PACK_RE.test(prompt);
}
