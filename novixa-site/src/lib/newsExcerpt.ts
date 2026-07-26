/** Tự cắt mô tả ngắn từ nội dung markdown khi bài không có description. */

const MAX_LENGTH = 160;

function stripMarkdown(body: string): string {
  return body
    .replace(/```[\s\S]*?```/g, ' ')
    .replace(/^#{1,6}\s+.*$/gm, ' ')
    .replace(/!\[[^\]]*\]\([^)]*\)/g, ' ')
    .replace(/\[([^\]]*)\]\([^)]*\)/g, '$1')
    .replace(/[*_~`>#-]/g, ' ')
    .replace(/\s+/g, ' ')
    .trim();
}

export function newsExcerpt(body: string | undefined, max = MAX_LENGTH): string {
  const text = stripMarkdown(body ?? '');
  if (!text) return '';
  if (text.length <= max) return text;
  const cut = text.slice(0, max);
  const lastSpace = cut.lastIndexOf(' ');
  return `${cut.slice(0, lastSpace > max * 0.6 ? lastSpace : max).trimEnd()}…`;
}

/** description trong frontmatter nếu có, ngược lại tự cắt từ body. */
export function postDescription(post: {
  data: { description?: string };
  body?: string;
}): string {
  const manual = post.data.description?.trim();
  return manual || newsExcerpt(post.body);
}
