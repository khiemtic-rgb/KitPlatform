/**
 * Nội dung bài Fanpage lưu trong bản viết fb_page: thân bài, rồi (tuỳ chọn) bình luận đầu sau dòng đánh dấu.
 * Bình luận đầu dùng cho link và nguồn, vì link trong thân bài làm Facebook giảm hiển thị.
 */
const MARK = /\n-{3,}\s*\nBình luận đầu:\s*\n/i;

export function splitFacebookPost(body: string): { post: string; comment: string } {
  const text = (body ?? '').replace(/\r\n/g, '\n').trim();
  const m = MARK.exec(text);
  if (!m) return { post: text, comment: '' };
  return { post: text.slice(0, m.index).trim(), comment: text.slice(m.index + m[0].length).trim() };
}

export function joinFacebookPost(post: string, comment: string): string {
  const p = (post ?? '').trim();
  const c = (comment ?? '').trim();
  return c ? `${p}\n\n---\nBình luận đầu:\n${c}` : p;
}

/** Kiểm tra nhanh theo chuẩn bài Fanpage của kênh. Trả về danh sách việc cần sửa (rỗng = đạt). */
export function checkFacebookPost(post: string, disclaimer = 'không phải lời khuyên đầu tư'): string[] {
  const text = (post ?? '').trim();
  const issues: string[] = [];
  if (!text) return ['thiếu nội dung Facebook'];
  const first = text.split('\n')[0] ?? '';
  if (first.length > 125) issues.push('dòng đầu Facebook dài quá 125 ký tự (bị ẩn sau "Xem thêm")');
  if (/https?:\/\//i.test(text)) issues.push('nội dung Facebook có link (đưa link xuống bình luận đầu)');
  const tags = text.match(/#[\p{L}\p{N}_]+/gu) ?? [];
  if (tags.length > 5) issues.push(`Facebook có ${tags.length} hashtag (nên 3–5)`);
  if (disclaimer && !text.toLowerCase().includes(disclaimer)) issues.push('nội dung Facebook thiếu câu miễn trừ');
  return issues;
}
