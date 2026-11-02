import type { ScriptPublishText } from '@/shared/api/content.api';

/**
 * Tự soạn nội dung đăng cho một video ngắn từ chính các câu trong clip. Không gọi AI, không tốn tiền.
 * Luật kênh: không kèo, không gợi ý mã, không quảng cáo sàn, số liệu có nguồn và ngày, có câu miễn trừ.
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

export function draftShortPublish(input: { lines: string[]; headline?: string | null; filmTitle: string; long?: ScriptPublishText | null }): ShortDraft {
  const body = input.lines.map(clean).filter((line) => line && !DISCLAIMER_LINE.test(line) && !SIGN_OFF.test(line));
  const headline = clean(input.headline || body[0] || input.filmTitle);
  const rest = body.filter((line) => clean(line) !== headline && !headline.startsWith(clean(line).slice(0, 30)));
  const brand = (input.long?.title || '').includes(' | ') ? (input.long!.title as string).split(' | ').pop()!.trim() : '';
  const name = filmName(input.filmTitle);
  const tags = Array.from(new Set(((input.long?.youtubeDescription || '') + ' ' + (input.long?.tiktokCaption || '')).match(/#[\p{L}\p{N}_]+/gu) ?? [])).slice(0, 5);
  const hashtags = tags.length >= 3 ? tags : FALLBACK_TAGS;
  const warnings: string[] = [];

  const all = input.lines.join(' ');
  if (PROMISE.test(all)) warnings.push('clip có lời hứa lợi nhuận, không đăng');
  const needsSource = NEEDS_SOURCE.test(all);
  const sourceLine = ((input.long?.youtubeDescription || '').split('\n').find((row) => /^nguồn\s*:/i.test(row.trim())) || '').trim();
  if (needsSource && !sourceLine) warnings.push('clip có số liệu nhưng chưa có dòng "Nguồn: …, ngày …" trong file đăng của kịch bản');
  const illustration = ILLUSTRATION.test(all) ? 'Các con số trong video là ví dụ minh hoạ.' : '';
  const question = 'Bạn thấy điều này có đúng với mình không?';

  const topic = headline.replace(/^(thứ (nhất|hai|ba|tư)|bước \d+)\s*[,:]\s*/i, '');
  const title = `${cutAt(endSentence(topic).replace(/[.]$/, ''), brand ? 90 - brand.length : 96)}${brand ? ` | ${brand}` : ''}`.slice(0, 100);

  let summary = endSentence(headline);
  for (const line of rest) {
    if ((summary + ' ' + line).length > 240) break;
    summary = `${summary} ${endSentence(line)}`;
  }
  const full = `Xem bản đầy đủ: video "${name}" trên ${brand ? `kênh ${brand}` : 'kênh'}.`;
  const youtube = [summary, [needsSource ? sourceLine : '', illustration].filter(Boolean).join(' '), full, PUBLISH_DISCLAIMER, hashtags.join(' ')]
    .filter(Boolean)
    .join('\n\n');

  let tiktok = endSentence(headline);
  if (rest[0] && (tiktok + ' ' + rest[0]).length <= 180) tiktok = `${tiktok} ${endSentence(rest[0])}`;
  tiktok = [tiktok, question, needsSource ? sourceLine : '', PUBLISH_DISCLAIMER, hashtags.join(' ')].filter(Boolean).join(' ');

  const first = cutAt(headline, 125);
  const bodyLines: string[] = [];
  let used = 0;
  for (const line of rest) {
    if (bodyLines.length >= 3 || used + line.length > 420) break;
    bodyLines.push(endSentence(line));
    used += line.length;
  }
  const facebook = [first, bodyLines.join('\n'), `💬 ${question}`, PUBLISH_DISCLAIMER, hashtags.join(' ')].filter(Boolean).join('\n\n');
  const fbComment = [needsSource ? sourceLine : '', illustration, `Bản đầy đủ: video "${name}" trên trang.`].filter(Boolean).join(' ');

  return { title, youtube, tiktok, facebook, fbComment, warnings };
}
