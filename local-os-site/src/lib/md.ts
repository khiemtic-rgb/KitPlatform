/** First readable paragraph — no invented dates or prices. */
export function articleLead(markdown: string, max = 220): string {
  const raw = (markdown ?? '').replace(/\r\n/g, '\n').trim();
  if (!raw) return '';
  const first = raw
    .split(/\n{2,}/)
    .map((b) => b.replace(/^#{1,6}\s+/, '').replace(/^[-*+]\s+/, '').replace(/\s+/g, ' ').trim())
    .find((b) => b.length > 0) ?? '';
  if (first.length <= max) return first;
  return `${first.slice(0, max).trim()}…`;
}

/** Escape + a few markdown bits. No raw HTML from the writer. */
export function renderArticleHtml(markdown: string): string {
  const raw = (markdown ?? '').replace(/\r\n/g, '\n').trim();
  if (!raw) return '';
  const blocks = raw.split(/\n{2,}/);
  return blocks.map((block) => {
    const line = block.trim();
    if (!line) return '';
    const h = line.match(/^(#{2,3})\s+(.+)$/);
    if (h) {
      const tag = h[1].length === 2 ? 'h2' : 'h3';
      return `<${tag}>${inline(h[2])}</${tag}>`;
    }
    const paras = line.split('\n').map((l) => inline(l.replace(/^#{1,6}\s+/, '').replace(/^[-*+]\s+/, '• ')));
    return `<p>${paras.join('<br/>')}</p>`;
  }).join('');
}

function rewriteTnlHost(url: string): string {
  return url.replace(/https?:\/\/(?:www\.)?thainguyen\.life/gi, 'https://thainguyenlife.vn');
}

function isTnlUrl(url: string): boolean {
  return /^https?:\/\/(?:www\.)?thainguyenlife\.vn(?:[/?#]|$)/i.test(url);
}

function tnlPath(url: string): string {
  try {
    const u = new URL(url);
    return `${u.pathname}${u.search}${u.hash}` || '/';
  } catch {
    return url;
  }
}

function inline(s: string): string {
  let t = escapeHtml(s);
  t = t.replace(/\[([^\]]+)\]\((https?:\/\/[^)]+)\)/g, (_, label: string, href: string) => {
    const canon = rewriteTnlHost(href);
    if (isTnlUrl(canon)) return `<a href="${tnlPath(canon)}">${label}</a>`;
    return `<a href="${canon}" rel="nofollow noopener" target="_blank">${label}</a>`;
  });
  t = t.replace(/\*\*(.+?)\*\*/g, '<strong>$1</strong>');
  t = t.replace(/`([^`]+)`/g, '<code>$1</code>');
  return t;
}

function escapeHtml(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}
