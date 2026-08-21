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
  [tok('m(?=\\s+(?:có|còn|đang|cần))'), 'mình'],
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
  return lines.find((l) => test(l) && l.length <= 90) ?? lines.find(test) ?? '';
}

function isNoiseLine(line: string): boolean {
  const t = line.replace(/[\s.,;:!?-]+/g, '');
  if (t.length < 3) return true;
  return /^(ib|zalo|hot|gap|gapgap|ae|mn)$/i.test(t);
}

function stripChatOpener(line: string): string {
  return line
    .replace(/^(mình|em|tớ|tôi)\s+(có|còn|đang|cần)\s+/i, '')
    .replace(/^phòng giá rẻ cho thuê\.?\s*/i, '')
    .trim();
}

function explodeLines(cleaned: string): string[] {
  return cleaned
    .replace(/([.!?])\s+/g, '$1\n')
    .replace(/,(?=\s*(?:có |ở |địa chỉ|liên hệ|số điện thoại|camera|nóng lạnh|an ninh))/gi, '\n')
    .replace(/\s*[-–]\s*(?=(?:phường|xã|tp\.?|thành phố|thái nguyên|cầu vượt|quyết thắng))/gi, '\n')
    .split('\n')
    .map((l) => dropFiller(l.replace(/\s+/g, ' ').trim()))
    .map((l) => l.replace(/^[-–•·*]\s*/, ''))
    .map((l) => stripChatOpener(l))
    .filter((l) => l.length > 1 && !/^[-–•·]+$/.test(l) && !isNoiseLine(l));
}

function tidyLandmark(raw: string): string {
  return raw
    .replace(/(?<![A-Za-zÀ-ỹ])đh(?![A-Za-zÀ-ỹ])/gi, 'ĐH')
    .replace(/(?<![A-Za-zÀ-ỹ])cntt(?![A-Za-zÀ-ỹ])/gi, 'CNTT')
    .replace(/(?<![A-Za-zÀ-ỹ])tp\.?\s*thái nguyên(?![A-Za-zÀ-ỹ])/gi, 'TP. Thái Nguyên')
    .replace(/(?<![A-Za-zÀ-ỹ])quyết thắng(?![A-Za-zÀ-ỹ])/gi, 'Quyết Thắng')
    .replace(/(?<![A-Za-zÀ-ỹ])sơn tiến(?![A-Za-zÀ-ỹ])/gi, 'Sơn Tiến')
    .replace(/\s+/g, ' ')
    .trim();
}

function titleCasePlace(raw: string): string {
  const s = tidyLandmark(raw);
  return s
    .split(/(\s+)/)
    .map((w) => {
      if (/^\s+$/.test(w)) return w;
      if (/^(ĐH|CNTT|TP\.?)$/i.test(w)) return w.toUpperCase().replace(/^TP$/i, 'TP.');
      if (/^(và|ở|tại|gần|với)$/i.test(w)) return w.toLowerCase();
      return w.charAt(0).toUpperCase() + w.slice(1);
    })
    .join('');
}

export function extractShortPlace(text: string): string | undefined {
  const bits: string[] = [];
  const patterns = [
    /cầu vượt\s+[^,.\n()\-]{2,40}/i,
    /(?:phường|xã|p\.)\s+[^,.\n()]{2,28}/i,
    /(?:tp\.?|thành phố)\s*thái nguyên/i,
    /gần\s+(?:đh|đại học|cao đẳng|trường)[^,.\n()]{0,36}/i,
    /\d{1,4}[a-zA-Z]?\s+(?:đường|phố|ngõ|hẻm)[^,.\n]{2,40}/i,
    /(?:phường\s+)?quyết thắng/i,
    /phan đình phùng/i,
  ];
  for (const re of patterns) {
    const m = text.match(re);
    if (!m) continue;
    const s = titleCasePlace(m[0].replace(/\s+/g, ' '));
    if (s.length < 4 || s.length > 56) continue;
    if (bits.some((b) => b.toLowerCase().includes(s.toLowerCase()) || s.toLowerCase().includes(b.toLowerCase())))
      continue;
    bits.push(s);
  }
  if (!bits.length) return undefined;
  return bits.join(', ').slice(0, 80);
}

function shortTitle(kind: string, text: string, place?: string): string {
  const t = text.toLowerCase();
  if (kind === 'room') {
    const closed = /khép kín|khep kin/.test(t);
    const shared = /ở ghép|o ghep/.test(t);
    const near = text.match(/gần\s+([^,.\n()]{3,32})/i)?.[1];
    let title = shared ? 'Cho thuê chỗ ở ghép' : closed ? 'Cho thuê phòng khép kín' : 'Cho thuê phòng';
    if (near) title += ` gần ${tidyLandmark(near)}`;
    else if (place && place.length <= 36 && !/thái nguyên$/i.test(place)) title += ` tại ${place}`;
    return sentenceVi(title).slice(0, 80);
  }
  if (kind === 'event') {
    const named = text.match(/(?:sự kiện|workshop|đêm nhạc|ngày hội)\s+([^,.\n]{4,48})/i)?.[1];
    if (named) return sentenceVi(named).slice(0, 80);
    return 'Sự kiện tại Thái Nguyên';
  }
  const role = text.match(/(?:tuyển|tuyen)\s+([^,.\n]{4,40})/i)?.[1];
  if (role) return sentenceVi(`Tuyển ${role}`).replace(/\s*[|•·].*$/, '').slice(0, 80);
  const first = explodeLines(text).find((l) => l.length >= 8 && l.length <= 70 && !/^liên hệ/i.test(l));
  return sentenceVi(first ?? 'Tin tuyển dụng').replace(/\s*[|•·].*$/, '').slice(0, 80);
}

function roomFacts(text: string): string[] {
  const facts: string[] = [];
  const area = text.match(/(\d+(?:[.,]\d+)?)\s*m2\b/i);
  const closed = /khép kín|khep kin/i.test(text);
  if (area) facts.push(`Diện tích: ${area[1].replace('.', ',')}m²${closed ? ', khép kín.' : '.'}`);
  else if (closed) facts.push('Phòng khép kín.');
  const amen: string[] = [];
  if (/nóng lạnh|nong lanh/i.test(text)) amen.push('nóng lạnh');
  if (/camera/i.test(text)) amen.push('camera an ninh');
  if (/để xe|de xe/i.test(text)) amen.push('chỗ để xe máy');
  if (/an ninh tốt/i.test(text) && !amen.includes('camera an ninh')) amen.push('an ninh tốt');
  if (/cùng dãy với chủ|cung day voi chu/i.test(text)) amen.push('ở cùng dãy với chủ nhà');
  if (amen.length) facts.push(`Tiện ích: ${amen.join(', ')}.`);
  return facts;
}

export type ListingRewrite = {
  title: string;
  body: string;
  place?: string;
  phone?: string;
  salary?: string;
  contactName?: string;
  workingTime?: string;
  requirements?: string;
  organizationName?: string;
  employmentType?: string;
};

export function looksLikeRawDump(result: Pick<ListingRewrite, 'title' | 'body' | 'place'>, source: string): boolean {
  const title = (result.title ?? '').trim();
  const place = (result.place ?? '').trim();
  const body = (result.body ?? '').trim();
  if (title.length > 72) return true;
  if (/^(mình|em|tớ|tôi|mk|m)\s+(có|còn|đang|cần)\b/i.test(title)) return true;
  if (place.length > 72) return true;
  if (/phòng/i.test(place) && /triệu|cho thuê/i.test(place)) return true;
  const compact = source.replace(/\s+/g, ' ').trim();
  const titleHead = title.replace(/…$/, '').slice(0, 40).toLowerCase();
  if (compact.length > 60 && titleHead.length > 24 && compact.toLowerCase().startsWith(titleHead)) return true;
  const addr = body.match(/địa chỉ:\s*([^\n]+)/i)?.[1] ?? '';
  if (title.length > 20 && addr.toLowerCase().includes(title.slice(0, 24).toLowerCase())) return true;
  return false;
}

function extractContactName(text: string): string | undefined {
  const labeled = text.match(
    /(?:liên hệ|lh|zalo)\s*[:\-–]?\s*((?:anh|chị|cô|chú|em)\s+[A-Za-zÀ-ỹ][A-Za-zÀ-ỹ ]{0,28}?)(?=\s*(?:[-–:,]|sđt|sdt|0|\+84|$))/i,
  );
  const fromLabel = cleanPersonName(labeled?.[1]);
  if (fromLabel) return fromLabel;
  const full = text.match(
    /(?:liên hệ|lh)\s*[:\-–]\s*([A-Za-zÀ-ỹ][A-Za-zÀ-ỹ. ]{1,36}?)(?=\s*(?:[-–:]|sđt|sdt|0|\+84))/i,
  );
  const fromFull = cleanPersonName(full?.[1]);
  if (fromFull) return fromFull;
  const role = text.match(/\b((?:anh|chị|cô|chú)\s+[A-ZÀ-Ỹ][a-zà-ỹ]{1,20}(?:\s+[A-ZÀ-Ỹ][a-zà-ỹ]{1,20}){0,3})\b/);
  return cleanPersonName(role?.[1]);
}

function cleanPersonName(raw?: string): string | undefined {
  if (!raw) return undefined;
  const s = raw.replace(/\d/g, ' ').replace(/\s+/g, ' ').trim().replace(/^[:\-–.,;\s]+|[:\-–.,;\s]+$/g, '');
  if (s.length < 2 || s.length > 40) return undefined;
  if (/^(liên hệ|lh|zalo|sđt|sdt|tuyển|nhân viên|quán|nhà hàng)$/i.test(s)) return undefined;
  if (/thái nguyên|thu nhập|lương|địa điểm/i.test(s)) return undefined;
  return s;
}

function extractWorkingTime(text: string): string | undefined {
  const labeled = text.match(/(?:thời gian(?: làm việc)?|ca làm(?: việc)?|giờ làm)\s*[:\-–]\s*([^\n]{3,72})/i);
  const a = labeled?.[1]?.replace(/\s+/g, ' ').trim().replace(/[.,;:]+$/, '');
  if (a && a.length >= 3 && !/(triệu|\d+\s*k\b|000\s*đ)/i.test(a)) return a.slice(0, 72);
  const range = text.match(
    /\d{1,2}\s*h(?:\d{0,2})?\s*[-–]\s*\d{1,2}\s*h(?:\d{0,2})?(?:\s*(?:hoặc|\/)\s*\d{1,2}\s*h(?:\d{0,2})?\s*[-–]\s*\d{1,2}\s*h(?:\d{0,2})?)?(?:\s*,?\s*(?:T[2-7]|CN|thứ|cuối tuần)[^.\n]{0,28})?/i,
  );
  return range?.[0]?.replace(/\s+/g, ' ').trim().slice(0, 72);
}

function extractRequirements(text: string): string | undefined {
  const m = text.match(/(?:yêu cầu|yc)\s*[:\-–]\s*([^\n]{4,140})/i);
  if (m?.[1]) return m[1].replace(/\s+/g, ' ').trim().replace(/[.,;:]+$/, '').slice(0, 140);
  const age = text.match(/(?:từ\s*)?\d{2}\s*[-–]\s*\d{2}\s*tuổi|(?:từ\s+)?\d{2}\s*tuổi/i);
  return age?.[0]?.replace(/\s+/g, ' ').trim();
}

function extractOrganization(text: string): string | undefined {
  const m = text.match(
    /(?:quán|nhà hàng|công ty|cửa hàng|tiệm|khách sạn|cơ sở)\s+[A-Za-zÀ-ỹ0-9][A-Za-zÀ-ỹ0-9 &'’\-]{0,32}?(?=\s+(?:cần|tuyển|tuyen|nv|nhân viên|pt|ft|lh|liên hệ|,|$))/i,
  );
  const s = m?.[0]?.replace(/\s+/g, ' ').trim();
  if (!s || /tuyển|nhân viên|liên hệ|lương/i.test(s)) return undefined;
  return s.slice(0, 48);
}

function extractEmployment(text: string): string | undefined {
  const t = text.toLowerCase();
  if (/thực tập|intern/.test(t)) return 'internship';
  if (/bán thời gian|part[\s-]?time|partime|passtime/.test(t)) return 'part_time';
  if (/toàn thời gian|full[\s-]?time/.test(t)) return 'full_time';
  if (t.includes('cuối tuần')) return 'weekend';
  return undefined;
}

export function rewriteListingCopy(raw: string, kind = 'job'): ListingRewrite {
  const cleaned = normalizePay(dropFiller(expandPhrases(stripDecor(raw.replace(/\r/g, '')))));
  const phones = phonesIn(cleaned);
  const place = extractShortPlace(cleaned) ?? extractShortPlace(raw);
  const title = shortTitle(kind, cleaned, place);
  const contactName = extractContactName(cleaned) ?? extractContactName(raw);
  const workingTime = extractWorkingTime(cleaned) ?? extractWorkingTime(raw);
  const requirements = extractRequirements(cleaned) ?? extractRequirements(raw);
  const organizationName = extractOrganization(cleaned) ?? extractOrganization(raw);
  const employmentType = extractEmployment(cleaned);
  const payLine = takeLine(explodeLines(cleaned), (l) => /lương|thu nhập|triệu|k\/giờ|đ\/giờ/i.test(l) && l.length <= 90);
  const salary =
    kind === 'room'
      ? undefined
      : payLine
        ? payLine.replace(/^lương\s*:\s*/i, '').replace(/^thu nhập\s*:\s*/i, '').trim()
        : undefined;

  const blocks: string[] = [];
  if (kind === 'job') {
    if (organizationName && !title.toLowerCase().includes(organizationName.toLowerCase().slice(0, 12))) {
      blocks.push(`${sentenceVi(organizationName)} tuyển nhân viên.`);
    }
    if (salary) blocks.push(`Thu nhập: ${sentenceVi(salary)}`);
    if (workingTime) blocks.push(`Thời gian: ${sentenceVi(workingTime)}`);
    if (place) blocks.push(`Địa điểm: ${place}`);
    if (requirements) blocks.push(`Yêu cầu: ${sentenceVi(requirements.replace(/^yêu cầu\s*:\s*/i, ''))}`);
  } else if (kind === 'room') {
    blocks.push(...roomFacts(cleaned));
    if (place) blocks.push(`Địa chỉ: ${place}`);
  } else {
    if (workingTime) blocks.push(`Thời gian: ${sentenceVi(workingTime)}`);
    if (place) blocks.push(`Địa điểm: ${place}`);
  }
  const contact =
    contactName && phones[0] ? `${contactName} — ${phones[0]}` : contactName || phones[0];
  if (contact) blocks.push(`Liên hệ: ${contact}`);

  return {
    title,
    body: blocks.join('\n').trim().slice(0, 2000) || title,
    place,
    phone: phones[0],
    salary,
    contactName,
    workingTime,
    requirements,
    organizationName,
    employmentType,
  };
}
