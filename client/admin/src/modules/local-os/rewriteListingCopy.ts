/** Lọc tin dán từ nhóm: tắt, cảm thán, hô hào. Không bịa số liệu. */

function tok(inner: string, flags = 'gi'): RegExp {
  return new RegExp(`(?<![A-Za-zÀ-ỹ0-9])(?:${inner})(?![A-Za-zÀ-ỹ0-9])`, flags);
}

const PHRASES: [RegExp, string][] = [
  [tok('passtime|part[\\s-]?time|partime|pt'), 'bán thời gian'],
  [tok('full[\\s-]?time|fulltime|ft'), 'toàn thời gian'],
  [tok('work from home|wfh'), 'làm tại nhà'],
  [tok('remote'), 'làm từ xa'],
  [tok('nv|nhan vien'), 'nhân viên'],
  [tok('ns'), 'nhân sự'],
  [tok('ql|qly'), 'quản lý'],
  [tok('sv|sinh vien'), 'sinh viên'],
  [tok('hs'), 'học sinh'],
  [tok('quán\\s*cf|quan\\s*cf'), 'quán cà phê'],
  [tok('cf|cafe|coffee'), 'cà phê'],
  [tok('lcb'), 'lương cơ bản'],
  [tok('pc'), 'phụ cấp'],
  [tok('tca|tang ca'), 'tăng ca'],
  [tok('hđld|hdld|hđ|hdld'), 'hợp đồng'],
  [tok('bhxh'), 'BHXH'],
  [tok('bhyt'), 'BHYT'],
  [tok('bhtn'), 'BHTN'],
  [tok('sl'), 'số lượng'],
  [tok('sđt|sdt'), 'số điện thoại'],
  [tok('đt'), 'điện thoại'],
  [tok('add|đc chỉ|dc chi|dia chi'), 'địa chỉ'],
  [tok('sơ\\s*sở|so so|cơ sơ'), 'cơ sở'],
  [tok('cam ket'), 'cam kết'],
  [tok('thuong'), 'thưởng'],
  [tok('ko|khg|k0|kg|khong'), 'không'],
  [tok('dc|đc|dk'), 'được'],
  [tok('vs'), 'với'],
  [tok('j'), 'gì'],
  [tok('nx'), 'nữa'],
  [tok('mk'), 'mình'],
  [tok('nk'), 'nam/nữ'],
  [tok('cn'), 'chủ nhật'],
  [tok('t2'), 'T2'],
  [tok('t3'), 'T3'],
  [tok('t4'), 'T4'],
  [tok('t5'), 'T5'],
  [tok('t6'), 'T6'],
  [tok('t7'), 'T7'],
  [tok('lh'), 'liên hệ'],
  [tok('ib|inbox|nt'), 'nhắn tin'],
  [tok('zl|zalo'), 'Zalo'],
  [tok('hnay'), 'hôm nay'],
  [/liên hệ\s*:/gi, 'Liên hệ:'],
  [/thu nhập\s*:/gi, 'Thu nhập:'],
  [/lương\s*:/gi, 'Lương:'],
];

const DROP_PHRASES = [
  tok('ae ơi|a e ơi|anh em ơi|mn ơi|mọi người ơi|ban ơi|bạn ơi|oi ae|ơi ae'),
  tok('ae|a\\.e|anh em ơi'),
  tok('mn|mng|moi nguoi'),
  tok('inbox ngay|ib ngay|nhắn ngay|alo ngay|gọi ngay đi'),
  tok('siêu hot|sieu hot|hot nhất|hot nhat|very hot'),
  tok('tuyển gấp|tuyen gap|cần gấp lắm|can gap|gấp gấp|gap gap'),
  tok('ôi|ối|trời ơi|troi oi|wow+|wao+|omg'),
  tok('hehe+|hihi+|haha+|kkk+'),
  tok('nha+|nhé+|nhe+|nè+|ne+| hen+|á+|ý+'),
  tok('ạ+|a ơi|ơi ơi|ơi'),
  tok('luôn luôn|luôn đi|luôn nha'),
  tok('siêu|cực kỳ|cuc ky|quá trời|qua troi|quá đã|qua da'),
  tok('pls|plz|please'),
];

function expandPhrases(raw: string): string {
  let s = raw;
  for (const [re, to] of PHRASES) s = s.replace(re, to);
  return s;
}

function dropFiller(raw: string): string {
  let s = raw;
  for (const re of DROP_PHRASES) s = s.replace(re, ' ');
  return s
    .replace(/!{2,}/g, '.')
    .replace(/\?{2,}/g, '?')
    .replace(/[.]{3,}/g, '.')
    .replace(/[!]{1,}(?=\s|$)/g, '.')
    .replace(/\s+([,.;:!?])/g, '$1')
    .replace(/([,.;]){2,}/g, '$1')
    .replace(/\s{2,}/g, ' ')
    .trim();
}

function sentenceVi(raw: string): string {
  const t = raw.replace(/\s+/g, ' ').trim().replace(/^[,.;:\-–]+/, '').trim();
  if (!t) return t;
  const letters = t.replace(/[^A-Za-zÀ-ỹ]/g, '');
  const up = (letters.match(/[A-ZÀ-Ỹ]/g) ?? []).length;
  if (letters.length > 8 && up / letters.length > 0.55) {
    return t.charAt(0).toUpperCase() + t.slice(1).toLowerCase();
  }
  return t.charAt(0).toUpperCase() + t.slice(1);
}

function stripDecor(raw: string): string {
  return raw
    .replace(/[\u{1F300}-\u{1FAFF}\u{2600}-\u{27BF}]/gu, ' ')
    .replace(/[⭐✨🔥💥❗‼️♥️❤💕👉✅❌●■□▪▫★☆♥♡]/g, ' ')
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

function isNoiseLine(line: string): boolean {
  const t = line.replace(/[\s.,;:!?-]+/g, '');
  if (t.length < 3) return true;
  return /^(ib|zalo|hot|gap|gapgap|ae|mn)$/i.test(t);
}

export function rewriteListingCopy(raw: string, kind = 'job'): {
  title: string;
  body: string;
  place?: string;
  phone?: string;
  salary?: string;
} {
  const cleaned = normalizePay(dropFiller(expandPhrases(stripDecor(raw.replace(/\r/g, '')))));
  const lines = cleaned
    .split('\n')
    .map((l) => dropFiller(l.replace(/\s+/g, ' ').trim()))
    .map((l) => l.replace(/^[-–•·*]\s*/, ''))
    .filter((l) => l.length > 1 && !/^[-–•·]+$/.test(l) && !isNoiseLine(l));

  const first = lines.find((l) => l.length >= 8 && !/^liên hệ/i.test(l)) ?? lines[0] ?? 'Tin tuyển dụng';
  const title = sentenceVi(first).replace(/\s*[|•·].*$/, '').slice(0, 120);

  const pay = takeLine(lines, (l) => /lương|thu nhập|triệu|k\/giờ|đ\/giờ/i.test(l));
  const time = takeLine(lines, (l) => /giờ|ca |hành chính|t2|t7|toàn thời gian|bán thời gian/i.test(l) && !/lương|triệu/i.test(l));
  const place = takeLine(lines, (l) => /phường|xã|thái nguyên|địa điểm|địa chỉ|cơ sở|quán|sau |gần /i.test(l));
  const req = takeLine(lines, (l) => /nữ|nam|tuổi|yêu cầu|sinh viên|kinh nghiệm/i.test(l));
  const perk = takeLine(lines, (l) => /thưởng|bảo hiểm|bhxh|nghỉ|lễ|tết|phụ cấp/i.test(l));
  const phones = phonesIn(cleaned);

  const rest = lines
    .filter((l) => l !== first && l !== pay && l !== time && l !== place && l !== req && l !== perk)
    .filter((l) => !phones.some((p) => l.includes(p)))
    .filter((l) => !isNoiseLine(l))
    .slice(0, 8);

  const blocks: string[] = [];
  if (kind === 'job') {
    if (pay) blocks.push(`Thu nhập: ${sentenceVi(pay.replace(/^lương\s*:\s*/i, '').replace(/^thu nhập\s*:\s*/i, ''))}`);
    if (time) blocks.push(`Thời gian: ${sentenceVi(time)}`);
    if (place) blocks.push(`Địa điểm: ${sentenceVi(place.replace(/^địa điểm\s*:\s*/i, '').replace(/^địa chỉ\s*:\s*/i, ''))}`);
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
    place: place ? sentenceVi(place.replace(/^địa điểm\s*:\s*/i, '').replace(/^địa chỉ\s*:\s*/i, '')) : undefined,
    phone: phones[0],
    salary: kind === 'room' ? undefined : pay ? pay.replace(/^lương\s*:\s*/i, '').replace(/^thu nhập\s*:\s*/i, '').trim() : undefined,
  };
}
