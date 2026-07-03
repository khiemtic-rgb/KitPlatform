/**
 * OpenAI — viết bài tin tức + sinh ảnh hero (giống Kit Technology auto-publish).
 */
import fs from 'node:fs';
import path from 'node:path';
import { OUT_DIR } from '../news-image-lib.mjs';
import { writeOverlayImage } from '../image-brand-overlay.mjs';
import { buildNewsImagePrompt } from '../news-image-prompt.mjs';
import { loadManifest, saveManifest } from '../news-image-manifest.mjs';

const API_BASE = 'https://api.openai.com/v1';

const IMAGE_MODEL_FALLBACKS = [
  { model: 'gpt-image-1', size: '1536x1024' },
  { model: 'dall-e-3', size: '1792x1024', quality: 'standard', response_format: 'url' },
  { model: 'dall-e-2', size: '1024x1024', response_format: 'url' },
];

function getApiKey() {
  return process.env.OPENAI_API_KEY?.trim() || undefined;
}

export function hasOpenAiKey() {
  return Boolean(getApiKey());
}

export function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

async function openaiRequest(endpoint, body) {
  const apiKey = getApiKey();
  if (!apiKey) throw new Error('OPENAI_API_KEY is not set');

  const res = await fetch(`${API_BASE}${endpoint}`, {
    method: 'POST',
    headers: {
      Authorization: `Bearer ${apiKey}`,
      'Content-Type': 'application/json',
    },
    body: JSON.stringify(body),
  });

  if (!res.ok) {
    const detail = await res.text();
    throw new Error(`OpenAI ${endpoint} failed (${res.status}): ${detail.slice(0, 500)}`);
  }

  return res.json();
}

export async function generateArticleContent(input) {
  const model = process.env.OPENAI_MODEL?.trim() || 'gpt-4o-mini';

  const system = [
    'You write SEO blog articles for Novixa (novixa.vn) — SaaS quản trị nhà thuốc tại Việt Nam.',
    'Novixa thuộc Kit Technology. Giọng văn: thực tế, gần gũi chủ nhà thuốc, không phóng đại.',
    'Nhắc nhẹ Novixa khi phù hợp; không so sánh giá công khai với đối thủ.',
    'Return valid JSON only.',
  ].join(' ');

  const user = [
    'Language: Vietnamese',
    `Title: ${input.title}`,
    input.topic ? `Topic angle: ${input.topic}` : '',
    `Target length: about ${input.targetWords} words`,
    '',
    'Return JSON with keys:',
    '- description: meta description, max 155 characters',
    '- body: markdown article only (no frontmatter), start with opening paragraph, use 3-5 ## sections, bullet lists where useful',
    '',
    'End with a short CTA linking to /vi/lien-he (markdown link).',
    'Do not invent statistics without qualifier (e.g. "theo kinh nghiệm vận hành").',
  ]
    .filter(Boolean)
    .join('\n');

  const data = await openaiRequest('/chat/completions', {
    model,
    temperature: 0.7,
    response_format: { type: 'json_object' },
    messages: [
      { role: 'system', content: system },
      { role: 'user', content: user },
    ],
  });

  const raw = data.choices?.[0]?.message?.content;
  if (!raw) throw new Error('OpenAI returned empty article content');

  const parsed = JSON.parse(raw);
  if (!parsed.body || !parsed.description) {
    throw new Error('OpenAI article JSON missing body or description');
  }

  return {
    description: String(parsed.description).trim(),
    body: String(parsed.body).trim(),
  };
}

async function downloadImageBuffer(source) {
  if (source.startsWith('data:image/')) {
    const raw = source.replace(/^data:image\/\w+;base64,/, '');
    return Buffer.from(raw, 'base64');
  }
  const res = await fetch(source);
  if (!res.ok) throw new Error(`Failed to download generated image (${res.status})`);
  return Buffer.from(await res.arrayBuffer());
}

export async function generateNewsHeroImage({ slug, title, description = '', force = false }) {
  if (!hasOpenAiKey()) {
    return { ok: false, reason: 'missing_api_key' };
  }

  const outPath = path.join(OUT_DIR, `${slug}.png`);
  const manifest = loadManifest();
  // Đã có ảnh trên đĩa → giữ nguyên (tránh gọi OpenAI lại mỗi lần chạy workflow).
  // Dùng force để bắt buộc tạo lại (bài mới auto-publish, hoặc --force-images).
  if (!force && fs.existsSync(outPath)) {
    return { ok: true, skipped: true, path: outPath };
  }

  const preferred = process.env.OPENAI_IMAGE_MODEL?.trim();
  const prompt = buildNewsImagePrompt({ slug, title, description });
  const models = preferred
    ? [{ model: preferred, size: preferred.startsWith('dall-e') ? '1792x1024' : '1536x1024' }]
    : IMAGE_MODEL_FALLBACKS;

  let lastError;

  for (const item of models) {
    try {
      const body = {
        model: item.model,
        prompt,
        n: 1,
        size: item.size,
      };
      if (item.quality) body.quality = item.quality;
      if (item.response_format) body.response_format = item.response_format;

      const data = await openaiRequest('/images/generations', body);
      const row = data.data?.[0];
      let raw;
      if (row?.b64_json) {
        raw = Buffer.from(row.b64_json, 'base64');
      } else if (row?.url) {
        raw = await downloadImageBuffer(row.url);
      } else {
        throw new Error('OpenAI image generation returned no image data');
      }

      await writeOverlayImage(raw, outPath, { title });
      manifest[slug] = {
        mode: 'openai',
        model: item.model,
        prompt,
        generatedAt: new Date().toISOString(),
      };
      saveManifest(manifest);
      console.log(`  · Image model: ${item.model}`);
      return { ok: true, path: outPath, model: item.model };
    } catch (error) {
      lastError = error instanceof Error ? error : new Error(String(error));
      console.warn(`  ! Image model ${item.model} failed: ${lastError.message.split('\n')[0]}`);
    }
  }

  return { ok: false, reason: lastError?.message ?? 'all_image_models_failed' };
}
