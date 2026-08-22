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

function inline(s: string): string {
  let t = escapeHtml(s);
  t = t.replace(/\[([^\]]+)\]\((https?:\/\/[^)]+)\)/g, '<a href="$2" rel="nofollow noopener" target="_blank">$1</a>');
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
