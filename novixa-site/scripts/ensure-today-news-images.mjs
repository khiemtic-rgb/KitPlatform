/**
 * Chỉ tạo ảnh cho bài có pubDate = hôm nay (giờ VN) và chưa có .png.
 * Dùng trong workflow thay cho generate:news-images (không quét cả 23 bài).
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadDotEnv } from './load-env.mjs';
import { parseNewsMarkdown } from './lib/news-markdown.mjs';
import { generateNewsHeroImage } from './lib/gemini-news.mjs';
import { generateNewsImage, OUT_DIR } from './news-image-lib.mjs';

loadDotEnv();

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.join(__dirname, '..');
const CONTENT_DIR = path.join(ROOT, 'src/content/tin-tuc');

function todayVn() {
  return new Intl.DateTimeFormat('en-CA', {
    timeZone: 'Asia/Ho_Chi_Minh',
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date());
}

function targetDate() {
  return process.env.PUBLISH_DATE?.trim() || todayVn();
}

async function ensureImage({ slug, title, description }) {
  const outPath = path.join(OUT_DIR, `${slug}.png`);
  if (fs.existsSync(outPath)) {
    console.log(`  · Đã có ảnh: ${slug}.png`);
    return 'skipped';
  }

  console.log(`  → Tạo ảnh: ${slug}`);
  const ai = await generateNewsHeroImage({ slug, title, description, force: true });
  if (ai.ok) return 'created';

  console.warn(`  ! Gemini lỗi (${ai.reason}) — fallback SVG`);
  await generateNewsImage({ slug, title, description });
  return 'svg';
}

async function main() {
  const date = targetDate();
  console.log(`=== Ảnh tin đăng hôm nay (${date}) ===`);

  if (!fs.existsSync(CONTENT_DIR)) {
    console.log('Chưa có thư mục tin tức.');
    return;
  }

  const files = fs.readdirSync(CONTENT_DIR).filter((f) => f.endsWith('.md'));
  let created = 0;
  let skipped = 0;

  for (const file of files) {
    const raw = fs.readFileSync(path.join(CONTENT_DIR, file), 'utf8');
    const parsed = parseNewsMarkdown(raw);
    if (!parsed || parsed.frontmatter.pubDate !== date) continue;

    const slug = file.replace(/\.md$/, '');
    const { title, description = '' } = parsed.frontmatter;
    if (!title) continue;

    const result = await ensureImage({ slug, title, description });
    if (result === 'skipped') skipped++;
    else created++;
  }

  console.log(`\nXong: ${created} ảnh mới/tạo lại, ${skipped} đã có sẵn.`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
