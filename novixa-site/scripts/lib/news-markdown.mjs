import fs from 'node:fs';
import path from 'node:path';

export const PLACEHOLDER_VI = 'Nội dung đang được hoàn thiện';

export function isRealBody(body) {
  const trimmed = String(body ?? '').trim();
  if (!trimmed) return false;
  if (trimmed.includes(PLACEHOLDER_VI)) return false;
  return trimmed.length > 200;
}

export function yamlEscape(value) {
  return String(value).replace(/\\/g, '\\\\').replace(/"/g, '\\"');
}

function matchField(block, name) {
  const quoted = block.match(new RegExp(`^${name}:\\s*"(.*)"`, 'm'))?.[1];
  if (quoted !== undefined) return quoted;
  return block.match(new RegExp(`^${name}:\\s*(\\S+)`, 'm'))?.[1];
}

export function parseNewsMarkdown(raw) {
  const match = raw.match(/^---\r?\n([\s\S]*?)\r?\n---\r?\n([\s\S]*)$/);
  if (!match) return null;

  const block = match[1];
  const body = match[2] ?? '';
  const title = matchField(block, 'title');
  const description = matchField(block, 'description') ?? '';
  const pubDate = matchField(block, 'pubDate') ?? '';
  const lang = matchField(block, 'lang') ?? 'vi';
  const image = matchField(block, 'image');
  const category = matchField(block, 'category') ?? 'tin-tuc-novixa';

  if (!title) return null;
  return {
    frontmatter: {
      title,
      description,
      pubDate,
      lang,
      ...(image ? { image } : {}),
      category,
    },
    body,
  };
}

export function readNewsMarkdown(filePath) {
  if (!fs.existsSync(filePath)) return null;
  return parseNewsMarkdown(fs.readFileSync(filePath, 'utf8'));
}

export function writeNewsMarkdown(filePath, frontmatter, body) {
  fs.mkdirSync(path.dirname(filePath), { recursive: true });
  const lines = [
    '---',
    `title: "${yamlEscape(frontmatter.title)}"`,
    `description: "${yamlEscape(frontmatter.description)}"`,
    `pubDate: ${frontmatter.pubDate}`,
    `lang: ${frontmatter.lang ?? 'vi'}`,
    `category: ${frontmatter.category ?? 'tin-tuc-novixa'}`,
  ];
  if (frontmatter.image) {
    lines.push(`image: ${frontmatter.image}`);
  }
  lines.push('---', '', String(body).trim(), '');
  fs.writeFileSync(filePath, lines.join('\n'), 'utf8');
}

export function newsMarkdownPath(contentDir, slug) {
  return path.join(contentDir, `${slug}.md`);
}
