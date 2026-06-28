/**
 * Sinh ảnh bài tin qua OpenAI Images — mỗi bài một scene khác nhau.
 * Cần OPENAI_API_KEY trong .env hoặc biến môi trường.
 */
import fs from 'node:fs';
import path from 'node:path';
import { OUT_DIR, ROOT } from './news-image-lib.mjs';
import { writeOverlayImage } from './image-brand-overlay.mjs';
import { buildNewsImagePrompt } from './news-image-prompt.mjs';
import { loadManifest, saveManifest } from './news-image-manifest.mjs';

export async function generateAiNewsImage({ slug, title, description = '', force = false }) {
  const apiKey = process.env.OPENAI_API_KEY?.trim();
  if (!apiKey) {
    return { ok: false, reason: 'missing_api_key' };
  }

  const outPath = path.join(OUT_DIR, `${slug}.png`);
  const manifest = loadManifest();
  if (!force && manifest[slug]?.mode === 'openai' && fs.existsSync(outPath)) {
    return { ok: true, skipped: true, path: outPath };
  }

  const prompt = buildNewsImagePrompt({ slug, title, description });
  const model = process.env.OPENAI_IMAGE_MODEL?.trim() || 'gpt-image-1';
  const size = model.startsWith('dall-e') ? '1792x1024' : '1536x1024';

  const payload = {
    model,
    prompt,
    n: 1,
    size,
  };
  if (model.startsWith('dall-e-3')) {
    payload.quality = process.env.OPENAI_IMAGE_QUALITY?.trim() || 'standard';
  }

  const res = await fetch('https://api.openai.com/v1/images/generations', {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${apiKey}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(payload),
  });

  const data = await res.json();
  if (!res.ok) {
    const msg = data?.error?.message ?? res.statusText;
    return { ok: false, reason: msg };
  }

  const imageUrl = data?.data?.[0]?.url;
  const b64 = data?.data?.[0]?.b64_json;
  let raw;
  if (b64) {
    raw = Buffer.from(b64, 'base64');
  } else if (imageUrl) {
    const imgRes = await fetch(imageUrl);
    if (!imgRes.ok) return { ok: false, reason: `download_failed: ${imgRes.status}` };
    raw = Buffer.from(await imgRes.arrayBuffer());
  } else {
    return { ok: false, reason: 'empty_response' };
  }

  await writeOverlayImage(raw, outPath, { title });

  manifest[slug] = {
    mode: 'openai',
    model,
    prompt,
    generatedAt: new Date().toISOString(),
  };
  saveManifest(manifest);

  return { ok: true, path: outPath, prompt };
}

export async function generateAllAiNewsImages({ force = false } = {}) {
  const dir = path.join(ROOT, 'src/content/tin-tuc');
  const { parseArticleFrontmatter } = await import('./news-image-lib.mjs');
  const files = fs.readdirSync(dir).filter((f) => f.endsWith('.md'));
  let ok = 0;
  let skipped = 0;
  const errors = [];

  for (const file of files) {
    const slug = file.replace(/\.md$/, '');
    const raw = fs.readFileSync(path.join(dir, file), 'utf8');
    const meta = parseArticleFrontmatter(raw);
    if (!meta?.title) continue;

    const result = await generateAiNewsImage({
      slug,
      title: meta.title,
      description: meta.description ?? '',
      force,
    });

    if (result.ok && result.skipped) {
      skipped++;
      console.log(`  skip ${slug}.png (OpenAI, đã có)`);
    } else if (result.ok) {
      ok++;
      console.log(`  OpenAI ${slug}.png`);
      await new Promise((r) => setTimeout(r, 3500));
    } else {
      errors.push({ slug, reason: result.reason });
      console.error(`  lỗi ${slug}: ${result.reason}`);
    }
  }

  console.log(`OpenAI ảnh tin tức: ${ok} mới, ${skipped} giữ nguyên, ${errors.length} lỗi / ${files.length} bài`);
  return { ok, skipped, errors, total: files.length };
}
