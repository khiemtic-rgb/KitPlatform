import type { ScriptPublishText } from '@/shared/api/content.api';
import { scoreHookLine } from './novixa-shorts';

/**
 * Tự soạn nội dung đăng cho một video ngắn từ chính các câu trong clip. Không gọi AI, không tốn tiền.
 * Luật kênh: không kèo, không gợi ý mã, không quảng cáo sàn, số liệu có nguồn và ngày, có câu miễn trừ.
 * Câu miễn trừ đầu tư chỉ dành cho kênh tài chính (NGV); Novixa (nhà thuốc) không mang câu này.
 * Tiêu đề video ngắn là câu móc mạnh nhất trong clip, không phải câu đầu theo kịch bản, và không kèm đuôi tên kênh.
 * Câu chữ lấy nguyên từ lời đọc của clip (không viết thêm nhận định mới); phần nguồn lấy từ file đăng của kịch bản.
 */
export const PUBLISH_DISCLAIMER = 'Nội dung chỉ chia sẻ kiến thức, không phải lời khuyên đầu tư. Giao dịch có rủi ro mất vốn.';

export type ShortDraft = {
  title: string;
  youtube: string;
  tiktok: string;
  facebook: string;
  fbComment: string;
  /** Việc người duyệt cần xem (ví dụ có số liệu nhưng chưa có nguồn). */
  warnings: string[];
};

const FALLBACK_TAGS = ['#NguoiGiuVon', '#GiuVonTruoc', '#QuanTriRuiRo'];
const NOVIXA_TAGS = ['#Novixa', '#QuanLyNhaThuoc', '#NhaThuoc'];

/** Hashtag dự phòng theo thương hiệu, để phim Novixa không mang thẻ của kênh NGV. */
export function isPharmacyBrand(brandCode?: string): boolean {
  return /novixa|pharm|nha-?thuoc/i.test(brandCode || '');
}

function fallbackTags(brandCode?: string): string[] {
  return isPharmacyBrand(brandCode) ? NOVIXA_TAGS : FALLBACK_TAGS;
}

/** Câu miễn trừ theo thương hiệu: chỉ kênh tài chính cần câu "không phải lời khuyên đầu tư". */
export function publishDisclaimer(brandCode?: string): string {
  return isPharmacyBrand(brandCode) ? '' : PUBLISH_DISCLAIMER;
}

/** Câu mở bằng từ nối / dẫn dắt: đọc giữa bài thì ổn, làm tiêu đề thì người xem không hiểu đang nói gì. */
const WEAK_TITLE_START = /^(mình gọi (đó|điều đó) là|mình nghĩ (rằng)?|giả sử|cách đơn giản nhất là|đôi khi|khi |bây giờ|tiếp theo|thứ (nhất|hai|ba|tư)|ngăn thứ|và |nhưng |vì vậy|đó là|đây là|như vậy|ở đây)/i;
const HOOK_MIN = 50;
/** Số đọc bằng chữ (giọng đọc viết "hai mươi triệu" thay vì "20 triệu") vẫn là con số với người xem. */
const NUMBER_WORDS = /(mười|mươi|trăm|nghìn|ngàn|triệu|tỷ|phần trăm|một nửa|gấp (đôi|ba))/i;

/** Chọn câu móc làm tiêu đề: điểm hook cao nhất, trừ điểm câu mở bằng từ dẫn dắt. */
export function pickTitleLine(candidates: string[]): { text: string; score: number } {
  let best = { text: candidates[0] ?? '', score: -1 };
  candidates.forEach((line, index) => {
    const text = clean(line);
    if (!text) return;
    const score = scoreHookLine(text, index) + (/\d/.test(text) ? 0 : NUMBER_WORDS.test(text) ? 10 : 0) - (WEAK_TITLE_START.test(text) ? 20 : 0);
    if (score > best.score) best = { text, score };
  });
  return best;
}

/** Câu hỏi cuối bài: ưu tiên câu hỏi có sẵn trong clip, rồi câu hỏi của bản dài; không có thì bỏ trống (không chèn câu chung chung). */
function pickQuestion(lines: string[], long: ScriptPublishText | null | undefined, title: string): string {
  const own = lines.map(clean).find((line) => /\?$/.test(line) && line !== title);
  if (own) return own;
  const fromLong = ((long?.tiktokCaption || '') + ' ' + (long?.facebookCaption || ''))
    .split(/(?<=[.!?])\s+|\n+/)
    .map((part) => part.replace(/^💬\s*/u, '').trim())
    .find((part) => /\?$/.test(part) && part.length >= 12 && part !== title);
  return fromLong || '';
}
const DISCLAIMER_LINE = /không phải (là )?lời khuyên đầu tư|rủi ro (mất|thua lỗ)/i;
const SIGN_OFF = /^(mình là|giữ vốn trước|nội dung chỉ chia sẻ)/i;
const NEEDS_SOURCE = /(nghìn tỷ|dư nợ|số liệu|công bố|cơ quan quản lý|theo (báo cáo|thống kê)|cao nhất từ trước)/i;
const ILLUSTRATION = /(giả sử|ví dụ|minh họa)/i;
const PROMISE = /(chắc chắn (lãi|thắng|có lời)|đảm bảo (lợi nhuận|lãi)|bao lãi|làm giàu nhanh)/i;

function clean(text: string): string {
  return text.replace(/\s+/g, ' ').replace(/[…]+$/u, '').trim();
}

function cutAt(text: string, max: number): string {
  if (text.length <= max) return text;
  const slice = text.slice(0, max - 1);
  const space = slice.lastIndexOf(' ');
  return `${(space > max * 0.5 ? slice.slice(0, space) : slice).replace(/[,:;\s]+$/, '')}…`;
}

function endSentence(text: string): string {
  return /[.!?…]$/.test(text) ? text : `${text}.`;
}

/** Lấy phần tên phim: "A2 – Vốn sống và vốn giao dịch" thành "Vốn sống và vốn giao dịch". */
export function filmName(title: string): string {
  return title.replace(/^[A-Za-z]\d+\s*[–—-]\s*/, '').trim() || title;
}

export function draftShortPublish(input: {
  lines: string[];
  headline?: string | null;
  filmTitle: string;
  long?: ScriptPublishText | null;
  brandCode?: string;
  /** Soạn cho chính video dài: không có câu "Xem bản đầy đủ" và tiêu đề giữ tên phim. */
  whole?: boolean;
}): ShortDraft {
  const body = input.lines.map(clean).filter((line) => line && !DISCLAIMER_LINE.test(line) && !SIGN_OFF.test(line));
  const headline = clean(input.headline || body[0] || input.filmTitle);
  const rest = body.filter((line) => clean(line) !== headline && !headline.startsWith(clean(line).slice(0, 30)));
  const brand = (input.long?.title || '').includes(' | ') ? (input.long!.title as string).split(' | ').pop()!.trim() : '';
  const name = filmName(input.filmTitle);
  const tags = Array.from(new Set(((input.long?.youtubeDescription || '') + ' ' + (input.long?.tiktokCaption || '')).match(/#[\p{L}\p{N}_]+/gu) ?? [])).slice(0, 5);
  const hashtags = tags.length >= 3 ? tags : fallbackTags(input.brandCode);
  const warnings: string[] = [];

  const all = input.lines.join(' ');
  if (PROMISE.test(all)) warnings.push('clip có lời hứa lợi nhuận, không đăng');
  const needsSource = NEEDS_SOURCE.test(all);
  const sourceLine = ((input.long?.youtubeDescription || '').split('\n').find((row) => /^nguồn\s*:/i.test(row.trim())) || '').trim();
  if (needsSource && !sourceLine) warnings.push('clip có số liệu nhưng chưa có dòng "Nguồn: …, ngày …" trong file đăng của kịch bản');
  const illustration = ILLUSTRATION.test(all) ? 'Các con số trong video là ví dụ minh hoạ.' : '';
  const disclaimer = publishDisclaimer(input.brandCode);

  // Tiêu đề video ngắn: câu móc mạnh nhất trong clip (câu hỏi, con số, từ mạnh), không phải câu đầu theo thứ tự kịch bản.
  const hook = pickTitleLine([headline, ...rest]);
  if (!input.whole && hook.score < HOOK_MIN) warnings.push('tiêu đề chưa có câu móc (câu hỏi, con số, điều bất ngờ): viết lại trước khi duyệt');
  const topic = hook.text.replace(/^(thứ (nhất|hai|ba|tư)|bước \d+)\s*[,:]\s*/i, '');
  const wholeTitle = (input.long?.title || input.filmTitle).slice(0, 100);
  // Không gắn "| Tên kênh" cho video ngắn: trên điện thoại tiêu đề bị cắt ~40 ký tự, tên kênh đã hiện sẵn.
  const shortTitle = cutAt(endSentence(topic).replace(/[.]$/, ''), 70);
  const title = input.whole ? wholeTitle : shortTitle;
  const question = pickQuestion(input.lines, input.long, title);
  // Câu mở của mô tả/chú thích = câu móc; các câu còn lại theo thứ tự gốc.
  const lead = input.whole ? headline : hook.text;
  const others = [headline, ...rest].map(clean).filter((line) => line && line !== lead && line !== question);
  if (!question && !/\?$/.test(title)) warnings.push('chưa có câu hỏi cuối bài để mời bình luận: thêm tay nếu cần');

  let summary = endSentence(lead);
  for (const line of others) {
    if ((summary + ' ' + line).length > 240) break;
    summary = `${summary} ${endSentence(line)}`;
  }
  const full = `Xem bản đầy đủ: video "${name}" trên ${brand ? `kênh ${brand}` : 'kênh'}.`;
  const youtube = [summary, [needsSource ? sourceLine : '', illustration].filter(Boolean).join(' '), input.whole ? '' : full, disclaimer, hashtags.join(' ')]
    .filter(Boolean)
    .join('\n\n');

  let tiktok = endSentence(lead);
  if (others[0] && (tiktok + ' ' + others[0]).length <= 180) tiktok = `${tiktok} ${endSentence(others[0])}`;
  tiktok = [tiktok, question, needsSource ? sourceLine : '', disclaimer, hashtags.join(' ')].filter(Boolean).join(' ');

  const first = cutAt(lead, 125);
  const bodyLines: string[] = [];
  let used = 0;
  for (const line of others) {
    if (bodyLines.length >= 3 || used + line.length > 420) break;
    bodyLines.push(endSentence(line));
    used += line.length;
  }
  const facebook = [first, bodyLines.join('\n'), question ? `💬 ${question}` : '', disclaimer, hashtags.join(' ')].filter(Boolean).join('\n\n');
  const fbComment = [needsSource ? sourceLine : '', illustration, input.whole ? '' : `Bản đầy đủ: video "${name}" trên trang.`].filter(Boolean).join(' ');

  return { title, youtube, tiktok, facebook, fbComment, warnings };
}
