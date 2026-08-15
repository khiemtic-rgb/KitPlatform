/** Viết lại tin dán từ nhóm: tiếng Việt chuẩn, không bịa số liệu. */

const PHRASES: [RegExp, string][] = [
  [/\bpasstime\b/gi, 'bán thời gian'],
  [/\bpart[\s-]?time\b/gi, 'bán thời gian'],
  [/\bfull[\s-]?time\b/gi, 'toàn thời gian'],
  [/\bwork from home\b/gi, 'làm tại nhà'],
  [/\bremote\b/gi, 'làm từ xa'],
  [/\bnv\b/gi, 'nhân viên'],
  [/\bns\b/gi, 'nhân sự'],
  [/\btuyển dụng\b/gi, 'Tuyển dụng'],
  [/\bquán\s*cf\b/gi, 'quán cà phê'],
  [/\bcf\b/gi, 'cà phê'],
  [/\blcb\b/gi, 'lương cơ bản'],
  [/\bpc\b/gi, 'phụ cấp'],
  [/\btca\b/gi, 'tăng ca'],
  [/\bbhxh\b/gi, 'BHXH'],
  [/\bbhyt\b/gi, 'BHYT'],
  [/\bbhtn\b/gi, 'BHTN'],
  [/\bsl\b/gi, 'số lượng'],
  [/\bsơ\s*sở\b/gi, 'cơ sở'],
  [/\bso so\b/gi, 'cơ sở'],
  [/\bcơ sơ\b/gi, 'cơ sở'],
  [/\bcam ket\b/gi, 'cam kết'],
  [/\bthuong\b/gi, 'thưởng'],
  [/\bko\b/gi, 'không'],
  [/\bkhg\b/gi, 'không'],
  [/\bdc\b/gi, 'được'],
  [/\bđc\b/gi, 'được'],
  [/\bnk\b/gi, 'nam/nữ'],
  [/\bsv\b/gi, 'sinh viên'],
  [/\bcn\b/gi, 'chủ nhật'],
  [/\bt2\b/gi, 'T2'],
  [/\bt7\b/gi, 'T7'],
  [/\blh\s*:/gi, 'Liên hệ:'],
  [/\bliên hệ\s*:/gi, 'Liên hệ:'],
  [/\bthu nhập\s*:/gi, 'Thu nhập:'],
  [/\blương\s*:/gi, 'Lương:'],
];

function expandPhrases(raw: string): string {
  let s = raw;
  for (const [re, to] of PHRASES) s = s.replace(re, to);
  return s;
}

function sentenceVi(raw: string): string {
  const t = raw.replace(/\s+/g, ' ').trim();
  if (!t) return t;
  const letters = t.replace(/[^A-Za-zÀ-ỹ]/g, '');
  const up = (letters.match(/[A-ZÀ-Ỹ]/g) ?? []).length;
  if (letters.length > 8 && up / letters.length > 0.65) {
    return t.charAt(0).toUpperCase() + t.slice(1).toLowerCase();
  }
  return t.charAt(0).toUpperCase() + t.slice(1);
}

function stripDecor(raw: string): string {
  return raw
    .replace(/[\u{1F300}-\u{1FAFF}\u{2600}-\u{27BF}]/gu, ' ')
    .replace(/[⭐✨🔥💥❗‼️♥️❤💕👉✅❌●■□▪▫]/g, ' ')
    .replace(/[━─=_]{3,}/g, '\n')
    .replace(/[ \t]+/g, ' ')
    .replace(/\n{3,}/g, '\n\n')
    .trim();
}

function normalizePay(raw: string): string {
  return raw
    .replace(/(?<![A-Za-zÀ-ỹ])(\d+)tr(\d)\b/gi, '$1,$2 triệu')
    .replace(/(?<![A-Za-zÀ-ỹ])(\d+[.,]\d+)\s*tr(?:iệu)?\b/gi, '$1 triệu')
    .replace(/(?<![A-Za-zÀ-ỹ])(\d+)\s*tr(?:iệu)?\b/gi, '$1 triệu')
    .replace(/(\d+)\s*k\s*[-–~]\s*(\d+)\s*k(?:\s*\/?\s*(?:giờ|h))?/gi, '$1–$2k/giờ')
    .replace(/(\d+)\s*k\s*\/\s*(?:h|giờ)\b/gi, '$1k/giờ')
    .replace(/(\d{1,3}(?:[.\s]\d{3})+)\s*đ\s*[-–~]\s*(\d{1,3}(?:[.\s]\d{3})+)\s*đ\s*\/?\s*(?:giờ|h)/gi, '$1–$2đ/giờ');
}

function phonesIn(text: string): string[] {
  const found: string[] = [];
  const re = /(?:\+?84|0)(?:\s|\.|-)?[35789](?:\s|\.|-)?\d(?:\s|\.|-){0,2}\d{3}(?:\s|\.|-){0,2}\d{3,4}/g;
  for (const m of text.matchAll(re)) {
    let d = m[0].replace(/\D/g, '');
    if (d.startsWith('84') && d.length >= 11) d = `0${d.slice(2)}`;
    if (d.length >= 9 && d.length <= 12 && !found.includes(d)) found.push(d);
  }
  return found;
}

function takeLine(lines: string[], test: (s: string) => boolean): string {
  return lines.find(test) ?? '';
}

export function rewriteListingCopy(raw: string, kind = 'job'): {
  title: string;
  body: string;
  place?: string;
  phone?: string;
  salary?: string;
} {
  const cleaned = normalizePay(expandPhrases(stripDecor(raw.replace(/\r/g, ''))));
  const lines = cleaned
    .split('\n')
    .map((l) => l.replace(/\s+/g, ' ').trim())
    .filter((l) => l.length > 1 && !/^[-–•·]+$/.test(l));

  const first = lines.find((l) => l.length >= 8 && !/^liên hệ/i.test(l)) ?? lines[0] ?? 'Tin tuyển dụng';
  const title = sentenceVi(first).replace(/\s*[|•·].*$/, '').slice(0, 120);

  const pay = takeLine(lines, (l) => /lương|thu nhập|triệu|k\/giờ|đ\/giờ/i.test(l));
  const time = takeLine(lines, (l) => /giờ|ca |hành chính|t2|t7|toàn thời gian|bán thời gian/i.test(l) && !/lương|triệu/i.test(l));
  const place = takeLine(lines, (l) => /phường|xã|thái nguyên|địa điểm|cơ sở|quán|sau |gần /i.test(l));
  const req = takeLine(lines, (l) => /nữ|nam|tuổi|yêu cầu|sinh viên|kinh nghiệm/i.test(l));
  const perk = takeLine(lines, (l) => /thưởng|bảo hiểm|bhxh|nghỉ|lễ|tết|phụ cấp/i.test(l));
  const phones = phonesIn(cleaned);

  const rest = lines
    .filter((l) => l !== first && l !== pay && l !== time && l !== place && l !== req && l !== perk)
    .filter((l) => !phones.some((p) => l.includes(p)))
    .slice(0, 8);

  const blocks: string[] = [];
  if (kind === 'job') {
    if (pay) blocks.push(`Thu nhập: ${sentenceVi(pay.replace(/^lương\s*:\s*/i, '').replace(/^thu nhập\s*:\s*/i, ''))}`);
    if (time) blocks.push(`Thời gian: ${sentenceVi(time)}`);
    if (place) blocks.push(`Địa điểm: ${sentenceVi(place.replace(/^địa điểm\s*:\s*/i, ''))}`);
    if (req) blocks.push(`Yêu cầu: ${sentenceVi(req.replace(/^yêu cầu\s*:\s*/i, ''))}`);
    if (perk) blocks.push(`Quyền lợi: ${sentenceVi(perk)}`);
  } else if (kind === 'room') {
    if (place) blocks.push(`Địa chỉ: ${sentenceVi(place)}`);
    if (req || perk) blocks.push(`Mô tả: ${sentenceVi(req || perk)}`);
  } else {
    if (time) blocks.push(`Thời gian: ${sentenceVi(time)}`);
    if (place) blocks.push(`Địa điểm: ${sentenceVi(place)}`);
  }
  for (const r of rest) {
    const s = sentenceVi(r);
    if (s.length >= 8 && !blocks.some((b) => b.includes(s.slice(0, 24)))) blocks.push(s);
  }
  if (phones.length) blocks.push(`Liên hệ: ${phones.join(', ')}`);

  const body = [title, '', ...blocks].join('\n').trim();
  return {
    title,
    body: body.slice(0, 2000),
    place: place ? sentenceVi(place.replace(/^địa điểm\s*:\s*/i, '')) : undefined,
    phone: phones[0],
    salary: pay ? pay.replace(/^lương\s*:\s*/i, '').replace(/^thu nhập\s*:\s*/i, '').trim() : undefined,
  };
}
