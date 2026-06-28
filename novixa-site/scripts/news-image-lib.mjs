/**
 * Tạo ảnh OG 1200×630 cho bài tin — layout khác nhau theo slug, tagline Novixa cố định.
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { hashSlug, hueShift, pickFrom, pickInt } from './slug-hash.mjs';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
export const ROOT = path.join(__dirname, '..');
export const OUT_DIR = path.join(ROOT, 'public/images/tin-tuc');
export const LOGO_PATH = path.join(ROOT, 'public/images/logo.png');

export const BRAND_TAGLINE = 'Novixa — Nền tảng quản trị nhà thuốc thế hệ mới';
export const OG_WIDTH = 1200;
export const OG_HEIGHT = 630;

const THEMES = [
  {
    match: /fefo|hết hạn|cận date|lô|tồn kho|kiểm kê|thất thoát/i,
    label: 'Tồn kho & FEFO',
    accent: '#1fa85c',
    bg: ['#0b4d8c', '#0d5a9e'],
  },
  {
    match: /pos|bán hàng|quầy|quét mã|hóa đơn|ca bán/i,
    label: 'POS nhà thuốc',
    accent: '#38bdf8',
    bg: ['#0b4d8c', '#1e40af'],
  },
  {
    match: /kpi|doanh thu|báo cáo|real-time|lợi nhuận/i,
    label: 'Báo cáo & KPI',
    accent: '#fbbf24',
    bg: ['#0b4d8c', '#334155'],
  },
  {
    match: /gpp|tuân thủ|quy định/i,
    label: 'Tuân thủ GPP',
    accent: '#34d399',
    bg: ['#065f46', '#0b4d8c'],
  },
  {
    match: /crm|khách hàng|chăm sóc/i,
    label: 'CRM khách hàng',
    accent: '#a78bfa',
    bg: ['#4c1d95', '#0b4d8c'],
  },
  {
    match: /ai|thông minh|phân tích/i,
    label: 'Giải pháp thông minh',
    accent: '#22d3ee',
    bg: ['#0e7490', '#0b4d8c'],
  },
  {
    match: /chuỗi|chi nhánh|multi|đa/i,
    label: 'Quản lý chuỗi',
    accent: '#fb923c',
    bg: ['#9a3412', '#0b4d8c'],
  },
  {
    match: /excel|chuyển đổi số|phần mềm|erp/i,
    label: 'Chuyển đổi số',
    accent: '#4ade80',
    bg: ['#14532d', '#0b4d8c'],
  },
];

const DEFAULT_THEME = {
  label: 'Quản lý nhà thuốc',
  accent: '#1fa85c',
  bg: ['#0b4d8c', '#1a6bb5'],
};

const LAYOUTS = ['classic', 'diagonal', 'split', 'grid', 'waves', 'corner', 'frame', 'orbit'];

export function pickTheme(title, description = '') {
  const text = `${title} ${description}`;
  return THEMES.find((t) => t.match.test(text)) ?? DEFAULT_THEME;
}

export function escapeXml(s) {
  return String(s)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

export function wrapLines(text, maxChars = 28, maxLines = 4) {
  const words = String(text).trim().split(/\s+/);
  const lines = [];
  let line = '';
  for (const word of words) {
    const next = line ? `${line} ${word}` : word;
    if (next.length > maxChars && line) {
      lines.push(line);
      line = word;
    } else {
      line = next;
    }
    if (lines.length >= maxLines) break;
  }
  if (line && lines.length < maxLines) lines.push(line);
  if (words.join(' ').length > lines.join(' ').length && lines.length === maxLines) {
    const last = lines[maxLines - 1];
    lines[maxLines - 1] = last.length > 25 ? `${last.slice(0, 24)}…` : `${last}…`;
  }
  return lines;
}

function themedColors(theme, slug) {
  const shift = pickInt(slug, -18, 18);
  return {
    c1: hueShift(theme.bg[0], shift),
    c2: hueShift(theme.bg[1], -shift),
    accent: hueShift(theme.accent, shift * 2),
  };
}

function titleBlock(lines, startY, fontSize, x = 80) {
  return lines
    .map(
      (ln, i) =>
        `<text x="${x}" y="${startY + i * (fontSize + 14)}" fill="#ffffff" font-family="Segoe UI, Arial, sans-serif" font-size="${fontSize}" font-weight="700">${escapeXml(ln)}</text>`,
    )
    .join('\n');
}

function footerBar() {
  return `
  <rect x="0" y="${OG_HEIGHT - 88}" width="${OG_WIDTH}" height="88" fill="#061829" opacity="0.55"/>
  <text x="80" y="${OG_HEIGHT - 34}" fill="#e2e8f0" font-family="Segoe UI, Arial, sans-serif" font-size="24" font-weight="600">${escapeXml(BRAND_TAGLINE)}</text>
  <text x="${OG_WIDTH - 80}" y="${OG_HEIGHT - 34}" fill="#94a3b8" font-family="Segoe UI, Arial, sans-serif" font-size="20" text-anchor="end">novixa.vn</text>`;
}

function brandHeader(theme, slug) {
  const { accent } = themedColors(theme, slug);
  return `
  <rect x="80" y="72" width="200" height="44" rx="10" fill="${accent}" opacity="0.92"/>
  <text x="96" y="102" fill="#ffffff" font-family="Segoe UI, Arial, sans-serif" font-size="22" font-weight="800">NOVIXA</text>
  <text x="300" y="102" fill="${accent}" font-family="Segoe UI, Arial, sans-serif" font-size="18" font-weight="600">${escapeXml(theme.label)}</text>`;
}

function buildLayoutClassic({ title, theme, slug }) {
  const lines = wrapLines(title, 30, 3);
  const { c1, c2, accent } = themedColors(theme, slug);
  const r1 = pickInt(slug, 80, 220);
  const r2 = pickInt(slug, 90, 200);
  return `
  <defs>
    <linearGradient id="bg" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="${c1}"/><stop offset="100%" stop-color="${c2}"/>
    </linearGradient>
  </defs>
  <rect width="${OG_WIDTH}" height="${OG_HEIGHT}" fill="url(#bg)"/>
  <circle cx="1050" cy="120" r="${r1}" fill="${accent}" opacity="0.14"/>
  <circle cx="150" cy="520" r="${r2}" fill="#ffffff" opacity="0.06"/>
  ${brandHeader(theme, slug)}
  ${titleBlock(lines, 248, 44)}
  ${footerBar()}`;
}

function buildLayoutDiagonal({ title, theme, slug }) {
  const lines = wrapLines(title, 32, 3);
  const { c1, c2, accent } = themedColors(theme, slug);
  return `
  <defs>
    <linearGradient id="bg" x1="0%" y1="100%" x2="100%" y2="0%">
      <stop offset="0%" stop-color="${c1}"/><stop offset="100%" stop-color="${c2}"/>
    </linearGradient>
  </defs>
  <rect width="${OG_WIDTH}" height="${OG_HEIGHT}" fill="url(#bg)"/>
  <polygon points="720,0 1200,0 1200,420 520,630 720,630" fill="${accent}" opacity="0.16"/>
  <polygon points="0,180 420,0 720,0 0,420" fill="#ffffff" opacity="0.05"/>
  ${brandHeader(theme, slug)}
  ${titleBlock(lines, 260, 42, 96)}
  ${footerBar()}`;
}

function buildLayoutSplit({ title, theme, slug }) {
  const lines = wrapLines(title, 24, 4);
  const { c1, c2, accent } = themedColors(theme, slug);
  const split = pickInt(slug, 520, 640);
  return `
  <rect width="${split}" height="${OG_HEIGHT}" fill="${c1}"/>
  <rect x="${split}" width="${OG_WIDTH - split}" height="${OG_HEIGHT}" fill="${c2}"/>
  <rect x="${split - 4}" width="8" height="${OG_HEIGHT}" fill="${accent}" opacity="0.85"/>
  ${Array.from({ length: 6 }, (_, i) => {
    const y = 140 + i * 70;
    return `<rect x="${split + 40}" y="${y}" width="${OG_WIDTH - split - 80}" height="12" rx="6" fill="#ffffff" opacity="${0.06 + (i % 3) * 0.03}"/>`;
  }).join('')}
  ${brandHeader(theme, slug)}
  ${titleBlock(lines, 230, 38, 72)}
  ${footerBar()}`;
}

function buildLayoutGrid({ title, theme, slug }) {
  const lines = wrapLines(title, 30, 3);
  const { c1, c2, accent } = themedColors(theme, slug);
  const cells = pickInt(slug, 8, 14);
  let grid = '';
  for (let i = 0; i < cells; i++) {
    const col = i % 6;
    const row = Math.floor(i / 6);
    const x = 860 + col * 52;
    const y = 40 + row * 52;
    grid += `<rect x="${x}" y="${y}" width="44" height="44" rx="8" fill="${accent}" opacity="${0.08 + (hashSlug(slug + i) % 12) / 100}"/>`;
  }
  return `
  <defs>
    <linearGradient id="bg" x1="0%" y1="0%" x2="0%" y2="100%">
      <stop offset="0%" stop-color="${c1}"/><stop offset="100%" stop-color="${c2}"/>
    </linearGradient>
  </defs>
  <rect width="${OG_WIDTH}" height="${OG_HEIGHT}" fill="url(#bg)"/>
  ${grid}
  ${brandHeader(theme, slug)}
  ${titleBlock(lines, 250, 43)}
  ${footerBar()}`;
}

function buildLayoutWaves({ title, theme, slug }) {
  const lines = wrapLines(title, 30, 3);
  const { c1, c2, accent } = themedColors(theme, slug);
  return `
  <defs>
    <linearGradient id="bg" x1="0%" y1="0%" x2="100%" y2="100%">
      <stop offset="0%" stop-color="${c1}"/><stop offset="100%" stop-color="${c2}"/>
    </linearGradient>
  </defs>
  <rect width="${OG_WIDTH}" height="${OG_HEIGHT}" fill="url(#bg)"/>
  <path d="M0,420 Q300,340 600,420 T1200,380 L1200,630 L0,630 Z" fill="${accent}" opacity="0.18"/>
  <path d="M0,480 Q400,400 800,470 T1200,440 L1200,630 L0,630 Z" fill="#ffffff" opacity="0.06"/>
  ${brandHeader(theme, slug)}
  ${titleBlock(lines, 240, 42)}
  ${footerBar()}`;
}

function buildLayoutCorner({ title, theme, slug }) {
  const lines = wrapLines(title, 28, 3);
  const { c1, c2, accent } = themedColors(theme, slug);
  return `
  <defs>
    <linearGradient id="bg" x1="100%" y1="0%" x2="0%" y2="100%">
      <stop offset="0%" stop-color="${c1}"/><stop offset="100%" stop-color="${c2}"/>
    </linearGradient>
  </defs>
  <rect width="${OG_WIDTH}" height="${OG_HEIGHT}" fill="url(#bg)"/>
  <rect x="820" y="-40" width="420" height="420" rx="48" fill="${accent}" opacity="0.22" transform="rotate(${pickInt(slug, 8, 24)} 1030 170)"/>
  <rect x="-60" y="360" width="280" height="280" rx="40" fill="#ffffff" opacity="0.05"/>
  ${brandHeader(theme, slug)}
  ${titleBlock(lines, 255, 41)}
  ${footerBar()}`;
}

function buildLayoutFrame({ title, theme, slug }) {
  const lines = wrapLines(title, 26, 4);
  const { c1, c2, accent } = themedColors(theme, slug);
  return `
  <defs>
    <linearGradient id="bg" x1="0%" y1="0%" x2="100%" y2="0%">
      <stop offset="0%" stop-color="${c1}"/><stop offset="100%" stop-color="${c2}"/>
    </linearGradient>
  </defs>
  <rect width="${OG_WIDTH}" height="${OG_HEIGHT}" fill="url(#bg)"/>
  <rect x="56" y="56" width="${OG_WIDTH - 112}" height="${OG_HEIGHT - 144}" rx="24" fill="none" stroke="${accent}" stroke-width="4" opacity="0.55"/>
  <rect x="72" y="72" width="${OG_WIDTH - 144}" height="${OG_HEIGHT - 176}" rx="18" fill="#ffffff" opacity="0.04"/>
  ${brandHeader(theme, slug)}
  ${titleBlock(lines, 220, 38, 110)}
  ${footerBar()}`;
}

function buildLayoutOrbit({ title, theme, slug }) {
  const lines = wrapLines(title, 30, 3);
  const { c1, c2, accent } = themedColors(theme, slug);
  const dots = Array.from({ length: 8 }, (_, i) => {
    const angle = (i / 8) * Math.PI * 2 + hashSlug(slug) / 100;
    const cx = 980 + Math.cos(angle) * pickInt(slug, 90, 130);
    const cy = 280 + Math.sin(angle) * pickInt(slug, 70, 110);
    const r = 10 + (hashSlug(`${slug}-${i}`) % 18);
    return `<circle cx="${cx.toFixed(1)}" cy="${cy.toFixed(1)}" r="${r}" fill="${i % 2 ? accent : '#ffffff'}" opacity="${0.12 + (i % 4) * 0.05}"/>`;
  }).join('');
  return `
  <defs>
    <linearGradient id="bg" x1="0%" y1="50%" x2="100%" y2="50%">
      <stop offset="0%" stop-color="${c1}"/><stop offset="100%" stop-color="${c2}"/>
    </linearGradient>
  </defs>
  <rect width="${OG_WIDTH}" height="${OG_HEIGHT}" fill="url(#bg)"/>
  ${dots}
  ${brandHeader(theme, slug)}
  ${titleBlock(lines, 248, 43)}
  ${footerBar()}`;
}

const LAYOUT_BUILDERS = {
  classic: buildLayoutClassic,
  diagonal: buildLayoutDiagonal,
  split: buildLayoutSplit,
  grid: buildLayoutGrid,
  waves: buildLayoutWaves,
  corner: buildLayoutCorner,
  frame: buildLayoutFrame,
  orbit: buildLayoutOrbit,
};

export function buildNewsImageSvg({ title, theme, slug }) {
  const layout = pickFrom(slug, LAYOUTS);
  const body = LAYOUT_BUILDERS[layout]({ title, theme, slug });
  return `<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" width="${OG_WIDTH}" height="${OG_HEIGHT}" viewBox="0 0 ${OG_WIDTH} ${OG_HEIGHT}">
${body}
</svg>`;
}

export function parseArticleFrontmatter(raw) {
  const match = raw.match(/^---\r?\n([\s\S]*?)\r?\n---/);
  if (!match) return null;
  const meta = {};
  for (const line of match[1].split(/\r?\n/)) {
    const trimmed = line.trim();
    if (!trimmed) continue;
    const m = trimmed.match(/^(\w+):\s*(.+)$/);
    if (!m) continue;
    let val = m[2].trim();
    if ((val.startsWith('"') && val.endsWith('"')) || (val.startsWith("'") && val.endsWith("'"))) {
      val = val.slice(1, -1);
    }
    meta[m[1]] = val;
  }
  return meta;
}

export function newsImagePublicPath(slug) {
  return `/images/tin-tuc/${slug}.png`;
}

async function svgToPng(svg, outPath) {
  let sharp;
  try {
    sharp = (await import('sharp')).default;
  } catch {
    const svgPath = outPath.replace(/\.png$/, '.svg');
    fs.writeFileSync(svgPath, svg, 'utf8');
    console.warn(`sharp chưa cài — lưu SVG: ${path.basename(svgPath)}`);
    return svgPath;
  }

  const png = await sharp(Buffer.from(svg)).png({ quality: 90 }).toBuffer();

  if (fs.existsSync(LOGO_PATH)) {
    try {
      const logo = await sharp(LOGO_PATH).resize(120, null).png().toBuffer();
      const composed = await sharp(png)
        .composite([{ input: logo, top: 64, left: OG_WIDTH - 160 }])
        .png({ quality: 90 })
        .toBuffer();
      fs.writeFileSync(outPath, composed);
      return outPath;
    } catch {
      fs.writeFileSync(outPath, png);
      return outPath;
    }
  }

  fs.writeFileSync(outPath, png);
  return outPath;
}

export async function generateNewsImage({ slug, title, description = '' }) {
  if (!slug || !title) return null;

  fs.mkdirSync(OUT_DIR, { recursive: true });
  const theme = pickTheme(title, description);
  const svg = buildNewsImageSvg({ title, theme, slug });
  const outPath = path.join(OUT_DIR, `${slug}.png`);
  return svgToPng(svg, outPath);
}

export async function generateFromMarkdownFile(mdPath) {
  const slug = path.basename(mdPath, '.md');
  const raw = fs.readFileSync(mdPath, 'utf8');
  const meta = parseArticleFrontmatter(raw);
  if (!meta?.title) return null;
  return generateNewsImage({
    slug,
    title: meta.title,
    description: meta.description ?? '',
  });
}

export async function generateAllNewsImages({ forceSvg = false } = {}) {
  const force = process.argv.includes('--force');
  const dir = path.join(ROOT, 'src/content/tin-tuc');
  const files = fs.readdirSync(dir).filter((f) => f.endsWith('.md'));

  if (!forceSvg) {
    const { hasCfAiCredentials } = await import('./cf-ai-config.mjs');
    if (hasCfAiCredentials()) {
      const { generateAllCfNewsImages } = await import('./cf-ai-news-image.mjs');
      const cfResult = await generateAllCfNewsImages({ force });
      if (cfResult.errors.length === 0) return cfResult.ok + cfResult.skipped;
      console.warn('Một số bài CF AI lỗi — thử OpenAI hoặc SVG cho bài thiếu.');
    } else if (process.env.OPENAI_API_KEY?.trim()) {
      const { generateAllAiNewsImages } = await import('./ai-news-image.mjs');
      const aiResult = await generateAllAiNewsImages({ force });
      if (aiResult.errors.length === 0) return aiResult.ok + aiResult.skipped;
      console.warn('Một số bài OpenAI lỗi — fallback SVG cho bài thiếu ảnh.');
    }
  }

  let count = 0;
  for (const file of files) {
    const slug = file.replace(/\.md$/, '');
    const outPath = path.join(OUT_DIR, `${slug}.png`);
    if (!forceSvg && fs.existsSync(outPath) && !force) {
      count++;
      continue;
    }
    const result = await generateFromMarkdownFile(path.join(dir, file));
    if (result) {
      count++;
      console.log(`  🖼 ${slug}.png`);
    }
  }
  console.log(`Ảnh tin tức: ${count}/${files.length} bài`);
  return count;
}
