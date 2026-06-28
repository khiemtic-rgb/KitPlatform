/**
 * Sinh ảnh bài tin qua Cloudflare Workers AI — Flux Schnell (free tier).
 * Cần CLOUDFLARE_ACCOUNT_ID + CLOUDFLARE_API_TOKEN (env hoặc import/cf-workers-ai.txt).
 */
import fs from 'node:fs';
import path from 'node:path';
import { hashSlug } from './slug-hash.mjs';
import { OUT_DIR, ROOT } from './news-image-lib.mjs';
import { writeOverlayImage } from './image-brand-overlay.mjs';
import { buildNewsImagePrompt } from './news-image-prompt.mjs';
import { loadManifest, saveManifest } from './news-image-manifest.mjs';
import { loadCfAiCredentials } from './cf-ai-config.mjs';

export const DEFAULT_CF_MODEL = '@cf/black-forest-labs/flux-1-schnell';

function cfRunUrl(accountId, model) {
  return `https://api.cloudflare.com/client/v4/accounts/${accountId}/ai/run/${model}`;
}

function extractImageBase64(data) {
  return data?.result?.image ?? data?.image ?? data?.result?.data?.[0]?.b64_json ?? null;
}

export async function generateCfNewsImage({ slug, title, description = '', force = false }) {
  const creds = loadCfAiCredentials();
  if (!creds) {
    return { ok: false, reason: 'missing_cf_credentials' };
  }

  const outPath = path.join(OUT_DIR, `${slug}.png`);
  const manifest = loadManifest();
  const model = process.env.CLOUDFLARE_AI_IMAGE_MODEL?.trim() || DEFAULT_CF_MODEL;

  if (!force && manifest[slug]?.mode === 'cf-flux' && fs.existsSync(outPath)) {
    return { ok: true, skipped: true, path: outPath };
  }

  const prompt = buildNewsImagePrompt({ slug, title, description });
  const steps = Math.min(8, Math.max(4, Number(process.env.CLOUDFLARE_AI_STEPS) || 4));
  const seed = hashSlug(slug) % 2_147_483_647;

  const res = await fetch(cfRunUrl(creds.accountId, model), {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${creds.apiToken}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify({ prompt, steps, seed }),
  });

  const contentType = res.headers.get('content-type') ?? '';
  let raw;

  if (contentType.includes('application/json')) {
    const data = await res.json();
    if (!res.ok || data.success === false) {
      const msg =
        data?.errors?.[0]?.message ?? data?.error ?? data?.messages?.[0] ?? res.statusText;
      return { ok: false, reason: String(msg) };
    }
    const b64 = extractImageBase64(data);
    if (!b64) return { ok: false, reason: 'empty_response' };
    raw = Buffer.from(b64, 'base64');
  } else {
    if (!res.ok) return { ok: false, reason: res.statusText };
    raw = Buffer.from(await res.arrayBuffer());
  }

  await writeOverlayImage(raw, outPath, { title });

  manifest[slug] = {
    mode: 'cf-flux',
    model,
    prompt,
    seed,
    generatedAt: new Date().toISOString(),
  };
  saveManifest(manifest);

  return { ok: true, path: outPath, prompt };
}

export async function generateAllCfNewsImages({ force = false } = {}) {
  const creds = loadCfAiCredentials();
  if (!creds) {
    console.log('Bỏ qua CF AI: chưa có CLOUDFLARE_ACCOUNT_ID/CLOUDFLARE_API_TOKEN');
    return { ok: 0, skipped: 0, errors: [], total: 0 };
  }

  const { parseArticleFrontmatter } = await import('./news-image-lib.mjs');
  const dir = path.join(ROOT, 'src/content/tin-tuc');
  const files = fs.readdirSync(dir).filter((f) => f.endsWith('.md'));
  let ok = 0;
  let skipped = 0;
  const errors = [];

  console.log(`Cloudflare Workers AI (${creds.source}): ${files.length} bài`);

  for (const file of files) {
    const slug = file.replace(/\.md$/, '');
    const raw = fs.readFileSync(path.join(dir, file), 'utf8');
    const meta = parseArticleFrontmatter(raw);
    if (!meta?.title) continue;

    const result = await generateCfNewsImage({
      slug,
      title: meta.title,
      description: meta.description ?? '',
      force,
    });

    if (result.ok && result.skipped) {
      skipped++;
      console.log(`  skip ${slug}.png (CF, đã có)`);
    } else if (result.ok) {
      ok++;
      console.log(`  CF ${slug}.png`);
      await new Promise((r) => setTimeout(r, 1200));
    } else {
      errors.push({ slug, reason: result.reason });
      console.error(`  lỗi ${slug}: ${result.reason}`);
    }
  }

  console.log(`CF ảnh tin tức: ${ok} mới, ${skipped} giữ nguyên, ${errors.length} lỗi / ${files.length} bài`);
  return { ok, skipped, errors, total: files.length };
}
