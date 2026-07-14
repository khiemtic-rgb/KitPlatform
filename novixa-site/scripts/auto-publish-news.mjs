/**
 * Tự động đăng tin Novixa — ChatGPT viết bài + OpenAI sinh ảnh (mô hình Kit Technology).
 */
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { loadDotEnv } from './load-env.mjs';
import { getPlanById, getPlanForDate } from './lib/news-content-plan.mjs';
import {
  isRealBody,
  newsMarkdownPath,
  readNewsMarkdown,
  writeNewsMarkdown,
} from './lib/news-markdown.mjs';
import {
  generateArticleContent,
  generateNewsHeroImage,
  hasOpenAiKey,
  sleep,
} from './lib/openai-news.mjs';
import { generateNewsImage } from './news-image-lib.mjs';

loadDotEnv();

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.join(__dirname, '..');
const CONTENT_DIR = path.join(ROOT, 'src/content/tin-tuc');

function publishDate() {
  if (process.env.PUBLISH_DATE?.trim()) return process.env.PUBLISH_DATE.trim();
  // Workflow chạy ~00:05 giờ VN (= 17:05 UTC ngày hôm trước) — phải dùng ngày VN.
  return new Date().toLocaleDateString('sv-SE', { timeZone: 'Asia/Ho_Chi_Minh' });
}

function parseArg(flag) {
  const entry = process.argv.find((arg) => arg.startsWith(`${flag}=`));
  return entry?.slice(flag.length + 1);
}

function targetArticleId() {
  return process.env.ARTICLE_ID?.trim() || parseArg('--id');
}

function forcePublish() {
  return process.env.FORCE_PUBLISH === '1' || process.argv.includes('--force') || Boolean(targetArticleId());
}

function resolvePlans() {
  const articleId = targetArticleId();
  if (articleId) {
    const plan = getPlanById(articleId);
    if (!plan) throw new Error(`Unknown ARTICLE_ID / --id: ${articleId}`);
    return [plan];
  }
  return getPlanForDate(publishDate());
}

async function ensureHeroImage(plan, title, description, { force = false } = {}) {
  const slug = plan.slug;
  const openAi = await generateNewsHeroImage({ slug, title, description, force });
  if (openAi.ok) return openAi;

  console.warn(`  ! OpenAI ảnh lỗi (${openAi.reason}) — fallback SVG`);
  await generateNewsImage({ slug, title, description });
  return { ok: true, path: path.join(ROOT, 'public/images/tin-tuc', `${slug}.png`), fallback: 'svg' };
}

async function publishPlanItem(plan) {
  const filePath = newsMarkdownPath(CONTENT_DIR, plan.slug);
  const existing = readNewsMarkdown(filePath);
  const today = publishDate();
  const shouldPublish = forcePublish() || plan.publishDate <= today;

  let frontmatter = existing?.frontmatter ?? {
    title: plan.title,
    description: `${plan.title} — Novixa.`,
    pubDate: plan.publishDate,
    lang: 'vi',
  };

  let body = existing?.body ?? '';

  if (!isRealBody(body) || forcePublish()) {
    if (!hasOpenAiKey()) {
      throw new Error('OPENAI_API_KEY is not set — cần để ChatGPT viết bài.');
    }
    console.log(`  → Generating article: ${plan.title}`);
    const generated = await generateArticleContent({
      title: plan.title,
      topic: plan.topic,
      categoryLabel: plan.categoryLabel,
      targetWords: plan.targetWords,
    });
    frontmatter = {
      ...frontmatter,
      title: plan.title,
      description: generated.description,
      pubDate: forcePublish() ? today : plan.publishDate,
      lang: 'vi',
    };
    body = generated.body;
    await sleep(800);
  }

  if (!shouldPublish) {
    console.log(`  · Draft future (${plan.publishDate}): ${plan.slug}`);
    return 'skipped';
  }

  if (isRealBody(body)) {
    console.log(`  → Hero image: ${plan.slug}`);
    await ensureHeroImage(plan, frontmatter.title, frontmatter.description, {
      force: forcePublish(),
    });
    await sleep(1200);
  }

  const changed =
    !existing ||
    existing.body.trim() !== body.trim() ||
    JSON.stringify(existing.frontmatter) !== JSON.stringify(frontmatter);

  if (!changed) {
    console.log(`  · Skipped: no changes (${plan.slug})`);
    return 'skipped';
  }

  writeNewsMarkdown(filePath, frontmatter, body);
  console.log(`  ✓ Saved: ${path.relative(ROOT, filePath)}`);
  return 'updated';
}

async function main() {
  const date = publishDate();
  const articleId = targetArticleId();
  const plans = resolvePlans();

  console.log('=== Novixa — Auto publish tin tức ===');
  console.log(`Publish date: ${date}`);
  if (articleId) {
    console.log(`Single article: ${articleId}`);
  } else {
    console.log(`Scheduled today: ${plans.length}`);
  }

  if (plans.length === 0) {
    const hint = articleId
      ? `Không tìm thấy bài "${articleId}" trong lịch (news-content-plan.mjs).`
      : `Không có bài lên lịch cho ngày ${date}. Chạy thử: ARTICLE_ID=nv-loyalty FORCE_PUBLISH=1`;
    console.log(hint);
    return;
  }

  if (!hasOpenAiKey()) {
    console.error('OPENAI_API_KEY chưa set — thêm vào GitHub Secrets hoặc .env');
    process.exit(1);
  }

  fs.mkdirSync(CONTENT_DIR, { recursive: true });

  let updates = 0;
  for (const plan of plans) {
    console.log(`\n[${plan.id}] ${plan.title} (${plan.publishDate})`);
    const result = await publishPlanItem(plan);
    if (result === 'updated') updates++;
  }

  console.log(`\nXong. Cập nhật ${updates} bài.`);
}

main().catch((err) => {
  console.error(err);
  process.exit(1);
});
