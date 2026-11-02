/**
 * Tách kịch bản dài thành các đoạn video ngắn, chọn hook và chấm điểm.
 * Điểm là heuristic từ chính văn bản, không phải dự đoán lượt xem.
 */
export type ShortScores = { hook: number; coherence: number; hot: number };

export type NovixaShortCut = {
  id: string;
  startIndex: number;
  endIndex: number;
  lines: string[];
  hookIndex: number;
  hookText: string;
  /** Hook nằm giữa đoạn: nên đưa lên đầu (cold open). */
  hookMoved: boolean;
  seconds: number;
  scores: ShortScores;
  notes: string[];
  riskFlags: string[];
  /** Kịch bản đọc cho video ngắn, hook ở đầu. */
  script: string;
};

const WORDS_PER_SECOND = 3.6;
const MIN_SECONDS = 25;
const TARGET_SECONDS = 42;
const MAX_SECONDS = 58;

const BOUNDARY = /^(thứ\s+(nhất|hai|ba|tư|năm)|bước\s+\d|còn\s+về|vậy|tóm lại|cuối cùng|tiếp theo|điều\s+thứ|lỗi\s+(thứ\s+)?\d|sai\s+lầm)/i;
// \b không nhận chữ có dấu ("và", "đó"), nên dùng (?!\p{L}) để biết từ đã hết.
const CONNECTIVE_START = /^(và|nhưng|vì vậy|do đó|cho nên|còn|nên|thế nên|tuy nhiên|bởi vì)(?!\p{L})/iu;
const ANAPHORA = /^(đó|đây|như vậy|khi đó|lúc đó|nó|họ|người đó|điều đó|cái đó)(?!\p{L})/iu;
/** Câu nhắc lại điều vừa nói ở câu trước ("Mình gọi đó là…", "như vậy"): đứng đầu video sẽ khó hiểu. */
const REFERS_BACK = /(^|\s)(đó|như vậy|như thế|kể trên|nói trên|vừa rồi)(?!\p{L})/iu;
/** Câu dẫn vào câu sau ("Bây giờ mình lấy một ví dụ…"): câu đứng sau nó không được kéo lên trước. */
const INTRO_LINE = /^(bây giờ|sau đây|đầu tiên|trước hết|mình lấy|mình sẽ|hãy hình dung|bạn hãy|để (dễ|bạn))|[:：]\s*$/iu;
const POWER = /(sai|đừng|mất|lỗ|cháy|bẫy|bí mật|sự thật|nguy hiểm|ít ai|không ai|thật ra|thực ra|hầu hết|90%|tuyệt đối|nhầm|cảnh báo|trước khi|chỉ cần|1 phút|30 giây)/i;
const CONCLUSION = /(tóm lại|vì vậy|do đó|nhớ|hãy|quy tắc|kết luận|bài học|cuối cùng|điều quan trọng)/i;
const PROMISE = /(chắc chắn (lãi|thắng|có lời)|đảm bảo (lợi nhuận|lãi)|không (bao giờ )?lỗ|x\d+ tài khoản|làm giàu nhanh|bao lãi|lãi \d+%\s*(mỗi|một)\s*(ngày|tuần))/i;
const NEEDS_DISCLAIMER = /(mua|bán|vào lệnh|entry|stoploss|stop loss|chốt lời|đòn bẩy|long|short)/i;

/** Có con số, kể cả viết bằng chữ ("hai mươi triệu", "ba ngăn"): kịch bản đọc thường viết số thành chữ. */
const NUMBER_WORD = /(^|[^\p{L}])(một|hai|ba|bốn|năm|sáu|bảy|tám|chín|mười|trăm|nghìn|ngàn|triệu|tỷ)(?!\p{L})/iu;
function hasNumber(text: string) {
  return /\d/.test(text) || NUMBER_WORD.test(text);
}

function wordCount(text: string) {
  return text.trim().split(/\s+/).filter(Boolean).length;
}

function secondsOf(lines: string[]) {
  return Math.round(lines.reduce((sum, line) => sum + wordCount(line), 0) / WORDS_PER_SECOND);
}

function clamp(n: number) {
  return Math.max(0, Math.min(100, Math.round(n)));
}

export function scoreHookLine(text: string, position: number): number {
  const words = wordCount(text);
  let score = 25;
  if (/\?\s*$/.test(text)) score += 14;
  if (hasNumber(text)) score += 10;
  if (POWER.test(text)) score += 14;
  if (/(^|[^\p{L}])(bạn|anh|chị|mình)(?!\p{L})/iu.test(text)) score += 6;
  if (words >= 6 && words <= 14) score += 12;
  else if (words > 22) score -= 18;
  else if (words < 4) score -= 10;
  if (CONNECTIVE_START.test(text.trim())) score -= 14;
  if (ANAPHORA.test(text.trim())) score -= 14; // nhắc lại câu trước, đứng đầu video sẽ khó hiểu
  if (position === 0) score += 4;
  return clamp(score);
}

function scoreCoherence(lines: string[], seconds: number, hookMoved: boolean, notes: string[]): number {
  let score = 60;
  const first = lines[0] || '';
  const last = lines[lines.length - 1] || '';
  if (CONNECTIVE_START.test(first.trim()) && !hookMoved) { score -= 15; notes.push('Mở đầu bằng từ nối, đoạn dễ bị hụt ý.'); }
  if (CONCLUSION.test(last) || /[.!]\s*$/.test(last)) score += 10; else { score -= 8; notes.push('Câu cuối chưa khép ý.'); }
  if (CONCLUSION.test(last)) score += 8;
  if (seconds < MIN_SECONDS) { score -= 14; notes.push(`Hơi ngắn (${seconds}s).`); }
  else if (seconds > MAX_SECONDS) { score -= 12; notes.push(`Hơi dài (${seconds}s).`); }
  else score += 10;
  if (lines.some(hasNumber)) score += 6; else notes.push('Chưa có con số hay ví dụ cụ thể.');
  const seen = new Set<string>();
  for (const line of lines) {
    const key = line.toLowerCase();
    if (seen.has(key)) { score -= 10; notes.push('Có câu lặp lại.'); break; }
    seen.add(key);
  }
  if (lines.length < 3) { score -= 10; notes.push('Quá ít câu để thành một ý trọn vẹn.'); }
  return clamp(score);
}

function riskOf(lines: string[]): string[] {
  const flags: string[] = [];
  const joined = lines.join(' ');
  if (PROMISE.test(joined)) flags.push('Có lời hứa lợi nhuận. Phải sửa trước khi đăng.');
  if (NEEDS_DISCLAIMER.test(joined) && !/(không phải lời khuyên|không khuyến nghị|rủi ro)/i.test(joined)) {
    flags.push('Có nói về vào/thoát lệnh nhưng thiếu câu cảnh báo rủi ro.');
  }
  return flags;
}

function groupLines(lines: string[]): [number, number][] {
  const groups: [number, number][] = [];
  let start = 0;
  let bucket: string[] = [];
  for (let i = 0; i < lines.length; i += 1) {
    bucket.push(lines[i]);
    const seconds = secondsOf(bucket);
    const next = lines[i + 1];
    const last = i === lines.length - 1;
    const boundary = next !== undefined && BOUNDARY.test(next.trim());
    const close = last
      || seconds >= MAX_SECONDS
      || (seconds >= MIN_SECONDS && boundary)
      || (seconds >= TARGET_SECONDS && /[.!?]\s*$/.test(lines[i]));
    if (close) {
      groups.push([start, i]);
      start = i + 1;
      bucket = [];
    }
  }
  // Đoạn cuối quá ngắn thì nhập vào đoạn trước.
  if (groups.length > 1) {
    const [s, e] = groups[groups.length - 1];
    if (secondsOf(lines.slice(s, e + 1)) < 15) {
      groups.pop();
      groups[groups.length - 1][1] = e;
    }
  }
  return groups;
}

function buildCut(clean: string[], start: number, end: number, n: number): NovixaShortCut {
  const segment = clean.slice(start, end + 1);
  let hookLocal = 0;
  let best = -1;
  segment.forEach((line, k) => {
    const s = scoreHookLine(line, k);
    if (s > best + 6 || (k === 0 && s > best)) { best = s; hookLocal = k; }
  });
  const hookMoved = hookLocal > 0;
  const ordered = hookMoved ? [segment[hookLocal], ...segment.filter((_, k) => k !== hookLocal)] : segment;
  const seconds = secondsOf(segment);
  const notes: string[] = [];
  const coherence = scoreCoherence(segment, seconds, hookMoved, notes);
  const riskFlags = riskOf(segment);
  const hot = clamp(best * 0.55 + coherence * 0.35 + (segment.some(hasNumber) ? 10 : 0) - riskFlags.length * 12);
  if (best < 50) notes.push('Hook yếu. Nên viết lại câu mở đầu thành câu hỏi hoặc con số gây tò mò.');
  if (hookMoved) notes.push(`Đã đưa câu ${hookLocal + 1} lên đầu làm hook.`);
  return {
    id: `S${String(n + 1).padStart(2, '0')}`,
    startIndex: start,
    endIndex: end,
    lines: segment,
    hookIndex: start + hookLocal,
    hookText: segment[hookLocal],
    hookMoved,
    seconds,
    scores: { hook: best, coherence, hot },
    notes,
    riskFlags,
    script: ordered.join('\n'),
  };
}

export function splitIntoShorts(lines: string[]): NovixaShortCut[] {
  const clean = lines.map((line) => line.trim()).filter(Boolean);
  if (!clean.length) return [];
  return groupLines(clean).map(([start, end], n) => buildCut(clean, start, end, n));
}

export function shortVerdict(cut: NovixaShortCut): { label: string; color: 'green' | 'gold' | 'red' } {
  if (cut.riskFlags.length) return { label: 'Cần sửa', color: 'red' };
  if (cut.scores.hot >= 70) return { label: 'Nên đăng', color: 'green' };
  if (cut.scores.hot >= 50) return { label: 'Tạm được', color: 'gold' };
  return { label: 'Yếu', color: 'red' };
}

/* ---------------- Video ngắn theo cảnh ---------------- */

/** Một khoảng câu đọc liền nhau, chỉ số từ 0. */
export type ShortPart = { from: number; to: number };

/** Video ngắn do người dựng chọn: các cảnh, câu thêm ("34, 36-37", đánh số từ 1) và có gắn câu miễn trừ hay không. */
export type NovixaShortPlan = { id: string; title: string; scenes: number[]; extra: string; disclaimer: boolean };

/** Một cảnh của kịch bản, ứng với khoảng câu đọc [from, to]. */
export type ScriptScene = { n: number; title: string; from: number; to: number };

export type ShortPlanResult = {
  parts: ShortPart[];
  lines: string[];
  seconds: number;
  riskFlags: string[];
  errors: string[];
  hasDisclaimer: boolean;
};

const SCENE_LINE = /^(?:#{1,6}\s*)?(?:\*\*)?\s*(?:cảnh|canh|sc)\s*(\d+)\s*\**\s*[.:\-–—)]?\s*(.*)$/i;
const DISCLAIMER = /không phải (là )?lời khuyên đầu tư/i;
const RISK_LINE = /rủi ro (mất|thua lỗ)/i;

/**
 * Cảnh của kịch bản theo dòng "Cảnh N. Tiêu đề" (có thể có ## hoặc **).
 * `spoken` là luật lời đọc dùng khi dựng (spokenLine), để chỉ số câu khớp với video.
 * Không có dòng "Cảnh N" thì trả về mảng rỗng.
 */
export function scriptScenes(rawLines: string[], spoken: (raw: string) => string): ScriptScene[] {
  const scenes: ScriptScene[] = [];
  let count = 0;
  let current: ScriptScene | null = null;
  const close = () => {
    if (current && current.to >= current.from) scenes.push(current);
  };
  for (const raw of rawLines) {
    const heading = raw.trim().match(SCENE_LINE);
    if (heading && !spoken(raw)) {
      close();
      const title = heading[2].replace(/\*\*/g, '').trim();
      current = { n: Number(heading[1]), title, from: count, to: count - 1 };
      continue;
    }
    if (!spoken(raw)) continue;
    if (!current) current = { n: 0, title: 'Trước cảnh 1', from: count, to: count - 1 };
    current.to = count;
    count += 1;
  }
  close();
  return scenes.some((scene) => scene.n > 0) ? scenes : [];
}

/** Câu miễn trừ cuối cùng của kịch bản ("…không phải lời khuyên đầu tư", kèm câu "…rủi ro mất vốn" liền sau nếu có). */
export function findDisclaimer(lines: string[]): ShortPart | null {
  for (let i = lines.length - 1; i >= 0; i -= 1) {
    if (!DISCLAIMER.test(lines[i])) continue;
    let from = i;
    let to = i;
    if (i + 1 < lines.length && RISK_LINE.test(lines[i + 1])) to = i + 1;
    else if (i > 0 && RISK_LINE.test(lines[i - 1])) from = i - 1;
    return { from, to };
  }
  return null;
}

/** "34, 36-37" (số câu từ 1) thành các khoảng từ 0. Số sai trả về lỗi, không đoán. */
export function parseExtraLines(text: string, total: number): { parts: ShortPart[]; errors: string[] } {
  const parts: ShortPart[] = [];
  const errors: string[] = [];
  for (const token of text.split(/[,;\s]+/).map((item) => item.trim()).filter(Boolean)) {
    const match = token.match(/^(\d+)(?:[-–](\d+))?$/);
    if (!match) {
      errors.push(`Không hiểu "${token}". Ghi số câu, ví dụ 34 hoặc 36-37.`);
      continue;
    }
    const a = Number(match[1]);
    const b = match[2] ? Number(match[2]) : a;
    if (a < 1 || b < a || b > total) {
      errors.push(`Câu ${token} nằm ngoài kịch bản (1–${total}).`);
      continue;
    }
    parts.push({ from: a - 1, to: b - 1 });
  }
  return { parts, errors };
}

function mergeParts(parts: ShortPart[]): ShortPart[] {
  const sorted = [...parts].sort((x, y) => x.from - y.from);
  const merged: ShortPart[] = [];
  for (const part of sorted) {
    const last = merged[merged.length - 1];
    if (last && part.from <= last.to + 1) last.to = Math.max(last.to, part.to);
    else merged.push({ ...part });
  }
  return merged;
}

/**
 * Các khoảng câu của một video ngắn, theo thứ tự trong kịch bản; câu miễn trừ luôn ở cuối.
 * Câu không liền nhau được ghép lại khi cắt (không thu lại giọng).
 */
export function resolveShortPlan(plan: NovixaShortPlan, scenes: ScriptScene[], lines: string[]): ShortPlanResult {
  const errors: string[] = [];
  const picked: ShortPart[] = [];
  for (const n of plan.scenes) {
    const scene = scenes.find((row) => row.n === n);
    if (scene) picked.push({ from: scene.from, to: scene.to });
    else errors.push(`Kịch bản không còn cảnh ${n}.`);
  }
  const extra = parseExtraLines(plan.extra || '', lines.length);
  errors.push(...extra.errors);
  picked.push(...extra.parts);
  const disclaimer = plan.disclaimer ? findDisclaimer(lines) : null;
  if (plan.disclaimer && !disclaimer) errors.push('Kịch bản chưa có câu miễn trừ ("…không phải lời khuyên đầu tư").');
  let body = mergeParts(picked);
  if (disclaimer) {
    // Bỏ câu miễn trừ khỏi phần thân (nếu cảnh chọn có chứa nó) rồi đặt ở cuối.
    body = body.flatMap((part) => {
      if (part.to < disclaimer.from || part.from > disclaimer.to) return [part];
      const left = part.from < disclaimer.from ? [{ from: part.from, to: disclaimer.from - 1 }] : [];
      const right = part.to > disclaimer.to ? [{ from: disclaimer.to + 1, to: part.to }] : [];
      return [...left, ...right];
    });
  }
  const parts = disclaimer ? [...body, disclaimer] : body;
  // Phần thân kết thúc ngay trước câu miễn trừ thì nối liền thành một đoạn.
  const joined: ShortPart[] = [];
  for (const part of parts) {
    const last = joined[joined.length - 1];
    if (last && part.from === last.to + 1) last.to = part.to;
    else joined.push({ ...part });
  }
  if (!body.length) errors.push('Chọn ít nhất một cảnh hoặc một câu.');
  const chosen = joined.flatMap((part) => lines.slice(part.from, part.to + 1));
  const hasDisclaimer = chosen.some((line) => DISCLAIMER.test(line));
  const riskFlags = riskOf(chosen);
  if (!hasDisclaimer) riskFlags.push('Chưa có câu miễn trừ trong video ngắn này.');
  return { parts: joined, lines: chosen, seconds: secondsOf(chosen), riskFlags, errors, hasDisclaimer };
}

export function newShortPlan(existing: NovixaShortPlan[], scene?: ScriptScene): NovixaShortPlan {
  let n = existing.length + 1;
  while (existing.some((row) => row.id === `P${String(n).padStart(2, '0')}`)) n += 1;
  return {
    id: `P${String(n).padStart(2, '0')}`,
    title: scene?.title || `Video ngắn ${n}`,
    scenes: scene ? [scene.n] : [],
    extra: '',
    disclaimer: true,
  };
}

/**
 * Các khoảng câu của bản cắt tự động khi mở bằng hook: câu hook đứng đầu, rồi tới phần còn lại theo thứ tự gốc.
 * Không thu giọng mới: chỉ ghép lại các đoạn của video dài đã dựng.
 */
export function hookFirstParts(cut: NovixaShortCut): ShortPart[] {
  const { startIndex: s, endIndex: e, hookIndex: h } = cut;
  if (!cut.hookMoved || h <= s || h > e) return [{ from: s, to: e }];
  const parts: ShortPart[] = [{ from: h, to: h }, { from: s, to: h - 1 }];
  if (h < e) parts.push({ from: h + 1, to: e });
  return parts;
}

/** Bản cắt tự động chỉ nên xuất khi không có lời hứa lợi nhuận/thiếu cảnh báo và điểm không "Yếu". */
export function shortExportable(cut: NovixaShortCut): boolean {
  return cut.riskFlags.length === 0 && cut.scores.hot >= 50;
}

/* ---------------- Sửa video ngắn tự gợi ý ---------------- */

/** Chỉnh sửa của người dựng cho một bản cắt tự gợi ý. Chỉ đổi cách chọn/ghép câu và chữ trên cảnh hook, không đổi giọng. */
export type NovixaShortEdit = {
  /** Câu làm hook (chỉ số câu từ 0). Thiếu: dùng câu hook gợi ý. */
  hookLine?: number;
  /** Chữ in trên cảnh hook (im lặng). Thiếu: dùng đúng câu hook. */
  cardText?: string;
  /** Các câu bỏ khỏi video ngắn. */
  drop: number[];
  /** Gắn câu miễn trừ ở cuối. */
  disclaimer: boolean;
  /** Người dựng đã duyệt, xuất dù điểm thấp. Cờ rủi ro vẫn chặn. */
  approved: boolean;
};

export type NovixaShortEdits = Record<string, NovixaShortEdit>;

/** Khóa của bản cắt: đổi kịch bản thì khóa đổi, chỉnh sửa cũ tự bị bỏ qua. */
export function cutKey(cut: NovixaShortCut): string {
  let h = 5381;
  for (const ch of cut.lines.join('\n')) h = ((h << 5) + h + ch.charCodeAt(0)) | 0;
  return `${cut.startIndex}-${cut.endIndex}:${(h >>> 0).toString(36)}`;
}

export const EMPTY_EDIT: NovixaShortEdit = { drop: [], disclaimer: false, approved: false };

export type ResolvedAutoCut = {
  /** Thứ tự câu của video ngắn (chỉ số từ 0), hook ở đầu khi bật. */
  order: number[];
  parts: ShortPart[];
  lines: string[];
  seconds: number;
  hookIndex: number;
  hookText: string;
  /** Chữ in trên cảnh hook. */
  cardText: string;
  scores: ShortScores;
  notes: string[];
  riskFlags: string[];
  exportable: boolean;
  verdict: { label: string; color: 'green' | 'gold' | 'red' };
};

/** Gộp các chỉ số liền nhau tăng dần thành khoảng, để ghép bằng đoạn cắt. */
function runsOf(order: number[]): ShortPart[] {
  const parts: ShortPart[] = [];
  for (const i of order) {
    const last = parts[parts.length - 1];
    if (last && i === last.to + 1) last.to = i;
    else parts.push({ from: i, to: i });
  }
  return parts;
}

export function resolveAutoCut(cut: NovixaShortCut, edit: NovixaShortEdit | undefined, allLines: string[], hookFirst: boolean): ResolvedAutoCut {
  const change = edit ?? EMPTY_EDIT;
  const disclaimer = change.disclaimer ? findDisclaimer(allLines) : null;
  const inDisclaimer = (i: number) => Boolean(disclaimer) && i >= disclaimer!.from && i <= disclaimer!.to;
  const body: number[] = [];
  for (let i = cut.startIndex; i <= cut.endIndex; i += 1) {
    if (!change.drop.includes(i) && !inDisclaimer(i)) body.push(i);
  }
  const wanted = change.hookLine ?? cut.hookIndex;
  const hookIndex = body.includes(wanted) ? wanted : (body[0] ?? cut.hookIndex);
  const front = hookFirst && body.length > 0 && hookIndex !== body[0];
  const ordered = front ? [hookIndex, ...body.filter((i) => i !== hookIndex)] : body;
  const order = disclaimer
    ? [...ordered, ...Array.from({ length: disclaimer.to - disclaimer.from + 1 }, (_, k) => disclaimer.from + k)]
    : ordered;
  const lines = order.map((i) => allLines[i]);
  const seconds = secondsOf(lines);
  const hookText = allLines[hookIndex] ?? '';
  const cardText = (change.cardText || '').trim().slice(0, 140) || hookText;
  const notes: string[] = [];
  const hook = scoreHookLine(cardText, 0);
  const coherence = scoreCoherence(lines, seconds, front, notes);
  const riskFlags = riskOf(lines);
  if (PROMISE.test(cardText)) riskFlags.push('Chữ trên cảnh hook có lời hứa lợi nhuận. Phải sửa trước khi đăng.');
  if (!body.length) riskFlags.push('Chưa còn câu nào trong video ngắn.');
  const hot = clamp(hook * 0.55 + coherence * 0.35 + (lines.some(hasNumber) ? 10 : 0) - riskFlags.length * 12);
  if (hook < 50) notes.push('Hook yếu. Thử chọn câu khác làm hook hoặc viết lại chữ trên cảnh hook thành câu hỏi hay con số gây tò mò.');
  const approved = change.approved && riskFlags.length === 0;
  const verdict = riskFlags.length
    ? { label: 'Cần sửa', color: 'red' as const }
    : hot >= 70 ? { label: 'Nên đăng', color: 'green' as const }
      : hot >= 50 ? { label: 'Tạm được', color: 'gold' as const }
        : approved ? { label: 'Đã duyệt', color: 'green' as const }
          : { label: 'Yếu', color: 'red' as const };
  return {
    order,
    parts: runsOf(order),
    lines,
    seconds,
    hookIndex,
    hookText,
    cardText,
    scores: { hook, coherence, hot },
    notes,
    riskFlags,
    exportable: riskFlags.length === 0 && body.length > 0 && (hot >= 50 || approved),
    verdict,
  };
}


/* ---------------- Phương án tối ưu tự động ---------------- */

export type OptimizedShort = { cut: NovixaShortCut; edit: NovixaShortEdit; result: ResolvedAutoCut };

export type ShortTarget = 'short' | 'balanced' | 'long';
/** Giới hạn tổng thời lượng lời đọc của một video ngắn: 60 giây trừ cảnh hook và cảnh kết. */
const MAX_TOTAL_SECONDS = 55;
export const SHORT_TARGET_SECONDS: Record<ShortTarget, number> = { short: 32, balanced: 42, long: 54 };

/**
 * Chia kịch bản thành các video ngắn đạt ngay: thử mọi khoảng câu liền nhau dài 25–58 giây, với mỗi khoảng
 * tự chọn câu hook tốt nhất, tự bỏ câu hứa lợi nhuận, tự gắn câu miễn trừ khi cần, rồi chọn cách chia cho
 * tổng điểm cao nhất (quy hoạch động). Chỉ giữ các video không còn cờ rủi ro và điểm từ 50.
 * Không đổi giọng: chỉ chọn, bỏ và sắp xếp lại các câu đã thu.
 */
/** Chữ ngắn cho cảnh hook: câu dài hơn 16 từ thì lấy phần đầu đến dấu phẩy/hai chấm gần nhất (6–14 từ) kèm "…". Không bịa thêm chữ. */
export function shortenForCard(text: string): string | undefined {
  if (wordCount(text) <= 16) return undefined;
  const pieces = text.split(/(?<=[,:;])\s+/);
  let acc = '';
  for (const piece of pieces) {
    const next = acc ? `${acc} ${piece}` : piece;
    if (wordCount(next) > 14) break;
    acc = next;
  }
  if (wordCount(acc) < 6) return undefined;
  return `${acc.replace(/[,:;]+$/, '')}…`;
}

export function optimizeShorts(lines: string[], target: ShortTarget = 'balanced', hookFirst = true, sceneStarts: number[] = []): OptimizedShort[] {
  const clean = lines.map((line) => line.trim()).filter(Boolean);
  const n = clean.length;
  if (!n) return [];
  const goal = SHORT_TARGET_SECONDS[target];
  const disclaimer = findDisclaimer(clean);
  const candidates: (OptimizedShort & { from: number; to: number; value: number })[] = [];
  for (let from = 0; from < n; from += 1) {
    for (let to = from + 2; to < n; to += 1) {
      const raw = secondsOf(clean.slice(from, to + 1));
      if (raw > MAX_SECONDS) break;
      if (raw < MIN_SECONDS) continue;
      const cut = buildCut(clean, from, to, 0);
      const drop = [] as number[];
      for (let i = from; i <= to; i += 1) if (PROMISE.test(clean[i])) drop.push(i);
      const needs = Boolean(disclaimer) && riskOf(cut.lines.filter((_, k) => !drop.includes(from + k))).length > 0;
      const bases: NovixaShortEdit[] = [];
      const leading = CONNECTIVE_START.test(clean[from].trim()) && !drop.includes(from) ? [...drop, from] : null;
      for (const dropSet of leading ? [drop, leading] : [drop]) {
        bases.push({ drop: dropSet, disclaimer: needs, approved: false });
      }
      let best: OptimizedShort | null = null;
      let bestScore = -Infinity;
      for (const base of bases) {
        for (let h = from; h <= to; h += 1) {
          if (base.drop.includes(h)) continue;
          const text = clean[h].trim();
          if (DISCLAIMER.test(text) || RISK_LINE.test(text)) continue; // câu miễn trừ không làm hook
          if (h > from && INTRO_LINE.test(clean[h - 1].trim()) && hookFirst) continue; // không kéo câu đứng sau câu dẫn lên trước câu dẫn
          if (hookFirst && h - from > 2) continue; // dời hook xa quá làm đứt mạch kể; chỉ cho dời tối đa hai câu
          if (h !== from && (ANAPHORA.test(text) || CONNECTIVE_START.test(text) || REFERS_BACK.test(text.split(/\s+/).slice(0, 6).join(' ')))) continue; // câu nhắc lại câu trước đứng đầu sẽ khó hiểu
          const short = shortenForCard(clean[h]);
          for (const cardText of short ? [undefined, short] : [undefined]) {
            const edit: NovixaShortEdit = { ...base, hookLine: h, ...(cardText ? { cardText } : {}) };
            const result = resolveAutoCut(cut, edit, clean, hookFirst);
            if (!result.exportable) continue;
            // Cả video ngắn (kể cả câu miễn trừ) cộng hai cảnh 2–2,6 giây phải vừa 60 giây.
            if (result.seconds > MAX_TOTAL_SECONDS) continue;
            // Dời hook lên đầu làm đảo mạch kể (ví dụ "Giả sử…" đứng trước câu dẫn "Bây giờ mình lấy ví dụ…"), nên bị trừ điểm.
            const moved = hookFirst && h !== body0(base, from, to);
            const adjusted = result.scores.hot - (moved ? 8 : 0);
            if (!best || adjusted > bestScore) { best = { cut, edit, result }; bestScore = adjusted; }
          }
        }
      }
      if (!best) continue;
      const seconds = best.result.seconds;
      const starts = new Set(sceneStarts);
      const edgeBonus = (starts.has(from) ? 6 : 0) + (starts.has(to + 1) || to === n - 1 ? 6 : 0);
      const value = bestScore - 45 - Math.abs(seconds - goal) * 0.3 + edgeBonus;
      candidates.push({ ...best, from, to, value });
    }
  }
  // dp[k]: tổng giá trị tốt nhất khi đã xử lý k câu đầu; bỏ một câu bị phạt nhẹ.
  const dp = new Array<number>(n + 1).fill(-Infinity);
  const pick = new Array<number>(n + 1).fill(-1);
  const byStart = new Map<number, (typeof candidates)[number][]>();
  for (const c of candidates) byStart.set(c.from, [...(byStart.get(c.from) ?? []), c]);
  dp[0] = 0;
  for (let k = 0; k < n; k += 1) {
    if (dp[k] === -Infinity) continue;
    if (dp[k] - 2 > dp[k + 1]) { dp[k + 1] = dp[k] - 2; pick[k + 1] = -1; }
    for (const c of byStart.get(k) ?? []) {
      if (c.value <= 0) continue;
      const next = c.to + 1;
      if (dp[k] + c.value > dp[next]) { dp[next] = dp[k] + c.value; pick[next] = candidates.indexOf(c); }
    }
  }
  const chosen: (typeof candidates)[number][] = [];
  let k = n;
  while (k > 0) {
    if (pick[k] >= 0) { const c = candidates[pick[k]]; chosen.unshift(c); k = c.from; } else k -= 1;
  }
  return chosen.map((c, index) => ({
    cut: { ...c.cut, id: `S${String(index + 1).padStart(2, '0')}` },
    edit: c.edit,
    result: c.result,
  }));
}

/** Chỉ số câu đầu tiên còn lại của đoạn sau khi bỏ câu (để biết hook có bị dời lên đầu không). */
function body0(edit: NovixaShortEdit, from: number, to: number): number {
  for (let i = from; i <= to; i += 1) if (!edit.drop.includes(i)) return i;
  return from;
}

/* ---------------- Kịch bản viết theo đoạn: mỗi cảnh là một video ngắn, câu "[ngắn]" chỉ dành cho video ngắn ---------------- */

/**
 * Chỉ số câu đọc (từ 0) của các dòng đánh dấu "[ngắn]". `isShortOnly` nhận dòng gốc, `spoken` là luật lời đọc
 * (spokenLine) để chỉ số khớp với video.
 */
export function shortOnlyLines(rawLines: string[], spoken: (raw: string) => string, isShortOnly: (raw: string) => boolean): number[] {
  const out: number[] = [];
  let count = 0;
  for (const raw of rawLines) {
    if (!spoken(raw)) continue;
    if (isShortOnly(raw)) out.push(count);
    count += 1;
  }
  return out;
}

/** Các khoảng câu của video dài: toàn bộ kịch bản trừ các câu chỉ-ngắn, theo thứ tự. */
export function longCutParts(total: number, shortOnly: number[]): ShortPart[] {
  const skip = new Set(shortOnly);
  const kept: number[] = [];
  for (let i = 0; i < total; i += 1) if (!skip.has(i)) kept.push(i);
  return runsOf(kept);
}

export type AuthoredScore = { seconds: number; scores: ShortScores; notes: string[]; riskFlags: string[] };

/** Chấm một video ngắn do người viết soạn sẵn: câu đầu là hook, giữ nguyên thứ tự (không dời hook). */
export function scoreAuthoredShort(lines: string[]): AuthoredScore {
  const clean = lines.map((line) => line.trim()).filter(Boolean);
  const notes: string[] = [];
  if (!clean.length) return { seconds: 0, scores: { hook: 0, coherence: 0, hot: 0 }, notes, riskFlags: ['Chưa có câu nào.'] };
  const seconds = secondsOf(clean);
  const hook = scoreHookLine(clean[0], 0);
  const coherence = scoreCoherence(clean, seconds, false, notes);
  const riskFlags = riskOf(clean);
  const hot = clamp(hook * 0.55 + coherence * 0.35 + (clean.some(hasNumber) ? 10 : 0) - riskFlags.length * 12);
  if (hook < 50) notes.push('Câu mở đầu chưa gây tò mò. Viết lại thành câu loại trừ, con số hoặc câu hỏi.');
  return { seconds, scores: { hook, coherence, hot }, notes, riskFlags };
}

/** Phần giọng đọc thêm vì có câu chỉ-ngắn, tính theo giây và theo phần trăm so với video dài. */
export function shortOnlyOverhead(lines: string[], shortOnly: number[]): { seconds: number; percent: number } {
  const skip = new Set(shortOnly);
  const extra = secondsOf(lines.filter((_, i) => skip.has(i)));
  const base = secondsOf(lines.filter((_, i) => !skip.has(i)));
  return { seconds: extra, percent: base > 0 ? Math.round((extra / base) * 100) : 0 };
}

/** Mỗi cảnh (có nội dung) thành một kế hoạch video ngắn, theo thứ tự đã viết. */
export function plansFromScenes(scenes: ScriptScene[]): NovixaShortPlan[] {
  const plans: NovixaShortPlan[] = [];
  for (const scene of scenes) {
    if (scene.n <= 0 || scene.to < scene.from) continue;
    plans.push(newShortPlan(plans, scene));
  }
  return plans;
}
