/**
 * Gemini — viết bài tin tức + sinh ảnh hero (Google AI Studio API key).
 */
import fs from 'node:fs';
import path from 'node:path';
import { OUT_DIR } from '../news-image-lib.mjs';
import { writeOverlayImage } from '../image-brand-overlay.mjs';
import { buildNewsImagePrompt } from '../news-image-prompt.mjs';
import { loadManifest, saveManifest } from '../news-image-manifest.mjs';

const API_BASE = 'https://generativelanguage.googleapis.com/v1beta';

/** Text models tried in order if the preferred one fails. */
const TEXT_MODEL_FALLBACKS = [
  'gemini-2.0-flash',
  'gemini-2.5-flash-lite',
  'gemini-2.5-flash',
  'gemini-flash-latest',
];

/** Image models: Nano Banana / Gemini image → Imagen predict. */
const IMAGE_MODEL_FALLBACKS = [
  { model: 'gemini-2.0-flash-preview-image-generation', mode: 'generateContent' },
  { model: 'gemini-2.5-flash-image', mode: 'generateContent' },
  { model: 'imagen-4.0-generate-001', mode: 'predict' },
  { model: 'imagen-3.0-generate-002', mode: 'predict' },
];

const RATE_LIMIT_RETRIES = 4;
const RATE_LIMIT_BASE_MS = 20_000;

function getApiKey() {
  return (
    process.env.GEMINI_API_KEY?.trim() ||
    process.env.GOOGLE_API_KEY?.trim() ||
    process.env.GOOGLE_GENERATIVE_AI_API_KEY?.trim() ||
    undefined
  );
}

export function hasGeminiKey() {
  return Boolean(getApiKey());
}

/** @deprecated alias — pipeline đã chuyển sang Gemini */
export function hasOpenAiKey() {
  return hasGeminiKey();
}

export function sleep(ms) {
  return new Promise((resolve) => setTimeout(resolve, ms));
}

function isRateLimited(error) {
  return /failed \(429\)/.test(error?.message ?? '');
}

async function geminiRequestOnce(urlPath, body) {
  const apiKey = getApiKey();
  if (!apiKey) throw new Error('GEMINI_API_KEY is not set');

  const res = await fetch(`${API_BASE}${urlPath}`, {
    method: 'POST',
    headers: {
      'Content-Type': 'application/json',
      'x-goog-api-key': apiKey,
    },
    body: JSON.stringify(body),
  });

  if (!res.ok) {
    const detail = await res.text();
    throw new Error(`Gemini ${urlPath} failed (${res.status}): ${detail.slice(0, 500)}`);
  }

  return res.json();
}

/** Retry 429 (free-tier RPM) before giving up on a model. */
async function geminiRequest(urlPath, body) {
  let lastError;
  for (let attempt = 1; attempt <= RATE_LIMIT_RETRIES; attempt++) {
    try {
      return await geminiRequestOnce(urlPath, body);
    } catch (error) {
      lastError = error instanceof Error ? error : new Error(String(error));
      if (!isRateLimited(lastError) || attempt === RATE_LIMIT_RETRIES) throw lastError;
      const wait = RATE_LIMIT_BASE_MS * attempt;
      console.warn(`  · Rate limit 429 — chờ ${Math.round(wait / 1000)}s rồi thử lại (${attempt}/${RATE_LIMIT_RETRIES})`);
      await sleep(wait);
    }
  }
  throw lastError ?? new Error('Gemini request failed');
}

function extractText(data) {
  const parts = data?.candidates?.[0]?.content?.parts;
  if (!Array.isArray(parts)) return undefined;
  return parts
    .map((p) => p?.text)
    .filter(Boolean)
    .join('\n')
    .trim();
}

function extractInlineImage(data) {
  const parts = data?.candidates?.[0]?.content?.parts;
  if (!Array.isArray(parts)) return undefined;
  for (const part of parts) {
    const inline = part?.inlineData || part?.inline_data;
    if (inline?.data) {
      return Buffer.from(inline.data, 'base64');
    }
  }
  return undefined;
}

function extractPredictImage(data) {
  const pred = data?.predictions?.[0];
  const b64 = pred?.bytesBase64Encoded || pred?.image?.bytesBase64Encoded;
  if (b64) return Buffer.from(b64, 'base64');
  return undefined;
}

export async function generateArticleContent(input) {
  const preferred = process.env.GEMINI_MODEL?.trim();
  const models = preferred
    ? [preferred, ...TEXT_MODEL_FALLBACKS.filter((m) => m !== preferred)]
    : TEXT_MODEL_FALLBACKS;

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
    input.categoryLabel ? `Series / category: ${input.categoryLabel}` : '',
    `Target length: about ${input.targetWords || 1100} words`,
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

  let lastError;
  for (const model of models) {
    try {
      const data = await geminiRequest(`/models/${model}:generateContent`, {
        systemInstruction: { parts: [{ text: system }] },
        contents: [{ role: 'user', parts: [{ text: user }] }],
        generationConfig: {
          temperature: 0.7,
          responseMimeType: 'application/json',
        },
      });

      const raw = extractText(data);
      if (!raw) throw new Error('Gemini returned empty article content');

      const parsed = JSON.parse(raw);
      if (!parsed.body || !parsed.description) {
        throw new Error('Gemini article JSON missing body or description');
      }

      console.log(`  · Text model: ${model}`);
      return {
        description: String(parsed.description).trim(),
        body: String(parsed.body).trim(),
      };
    } catch (error) {
      lastError = error instanceof Error ? error : new Error(String(error));
      console.warn(`  ! Text model ${model} failed: ${lastError.message.split('\n')[0]}`);
    }
  }

  throw lastError ?? new Error('All Gemini text models failed');
}

export async function generateNewsHeroImage({ slug, title, description = '', force = false }) {
  if (!hasGeminiKey()) {
    return { ok: false, reason: 'missing_api_key' };
  }

  const outPath = path.join(OUT_DIR, `${slug}.png`);
  const manifest = loadManifest();
  if (!force && fs.existsSync(outPath)) {
    return { ok: true, skipped: true, path: outPath };
  }

  const preferred = process.env.GEMINI_IMAGE_MODEL?.trim();
  const prompt = buildNewsImagePrompt({ slug, title, description });
  const models = preferred
    ? [
        {
          model: preferred,
          mode: preferred.startsWith('imagen') ? 'predict' : 'generateContent',
        },
        ...IMAGE_MODEL_FALLBACKS.filter((m) => m.model !== preferred),
      ]
    : IMAGE_MODEL_FALLBACKS;

  let lastError;

  for (const item of models) {
    try {
      let raw;
      if (item.mode === 'predict') {
        const data = await geminiRequest(`/models/${item.model}:predict`, {
          instances: [{ prompt }],
          parameters: { sampleCount: 1, aspectRatio: '16:9' },
        });
        raw = extractPredictImage(data);
      } else {
        const data = await geminiRequest(`/models/${item.model}:generateContent`, {
          contents: [{ role: 'user', parts: [{ text: prompt }] }],
          generationConfig: {
            responseModalities: ['TEXT', 'IMAGE'],
          },
        });
        raw = extractInlineImage(data);
      }

      if (!raw) throw new Error('Gemini image generation returned no image data');

      await writeOverlayImage(raw, outPath, { title });
      manifest[slug] = {
        mode: 'gemini',
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
