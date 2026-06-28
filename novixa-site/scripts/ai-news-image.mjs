/**
 * Sinh ảnh bài tin qua OpenAI Images (DALL-E 3) — mỗi bài một scene khác nhau.
 * Cần OPENAI_API_KEY trong .env hoặc biến môi trường.
 */
import fs from 'node:fs';
import path from 'node:path';
import { pickFrom, hashSlug } from './slug-hash.mjs';
import { pickTheme, OUT_DIR, ROOT } from './news-image-lib.mjs';
import { writeOverlayImage } from './image-brand-overlay.mjs';

export const MANIFEST_PATH = path.join(ROOT, 'import/news-images-manifest.json');

const SCENES = [
  'wide shot of a bright modern Vietnamese pharmacy interior, pharmacist arranging medicine boxes on shelves, natural daylight',
  'close-up of expiry date labels on pharmaceutical cartons, organized warehouse shelf, shallow depth of field',
  'pharmacist using tablet POS at checkout counter, customer queue, clean retail environment',
  'multi-branch pharmacy dashboard on laptop screen, stock charts and branch map in background blur',
  'inventory audit with clipboard and barcode scanner in pharmacy back room, realistic editorial photo',
  'FEFO concept: worker picking oldest batch first from refrigerated medicine cabinet, professional lighting',
  'CRM loyalty card handover at pharmacy counter, warm friendly service moment, photorealistic',
  'GPP compliance checklist on desk next to sealed medicine packages, clinical tidy workspace',
  'pharmacy owner reviewing revenue report in office, charts on monitor, confident business mood',
  'chain expansion: two pharmacy storefronts on a Vietnamese street, morning light, urban context',
  'digital transformation: replacing paper ledgers with cloud ERP on computer in small pharmacy office',
  'AI assistant concept: subtle holographic data overlay above pharmacy shelf, futuristic but realistic',
  'near-expiry alert sticky notes on shelf edge, pharmacist reviewing stock, urgent but calm atmosphere',
  'KPI dashboard projected on wall in pharmacy meeting room, team discussion, corporate editorial style',
  'cold chain storage with temperature monitor, vaccine and medicine boxes, professional healthcare logistics',
  'customer consultation at pharmacy counter with pharmacist pointing to medicine leaflet, trust and care',
  'night shift pharmacy with soft lighting, pharmacist closing daily sales report, quiet urban storefront',
  'delivery of new medicine cartons at pharmacy loading area, staff checking invoice, logistics scene',
  'pharmacy training session, staff learning POS system, collaborative workplace photography',
  'seasonal flu medicine display end-cap in pharmacy, promotional layout, retail merchandising photo',
  'pharmacist scanning GS1 barcode on medicine box, macro detail, tech-forward operations',
  'empty Excel spreadsheet replaced by mobile inventory app in pharmacist hands, change narrative',
  'family-owned pharmacy exterior with green cross sign, welcoming neighborhood context, Vietnam',
];

const MOODS = [
  'optimistic professional editorial photography',
  'clean corporate healthcare marketing photo',
  'warm documentary-style business photography',
  'crisp high-end stock photo aesthetic',
  'contemporary SaaS brand campaign photography',
];

const CAMERA = [
  '35mm lens, soft bokeh',
  '50mm lens, balanced composition',
  '24mm wide environmental shot',
  '85mm portrait-style framing on subject',
];

export function buildAiImagePrompt({ slug, title, description = '' }) {
  const theme = pickTheme(title, description);
  const scene = pickFrom(slug, SCENES);
  const mood = pickFrom(slug, MOODS);
  const camera = pickFrom(slug, CAMERA);
  const variant = hashSlug(slug) % 997;

  return [
    `Create a unique hero image for a Vietnamese pharmacy management software blog article.`,
    `Article topic: "${title}".`,
    `Visual theme hint: ${theme.label}.`,
    `Scene (variant ${variant}): ${scene}.`,
    `Style: ${mood}, ${camera}.`,
    `Color accents: pharmacy blue #0b4d8c and green #1fa85c subtly in environment.`,
    `Requirements: photorealistic, no text, no letters, no logos, no watermarks, no UI mockups with readable text.`,
    `Leave lower 20% relatively uncluttered for text overlay.`,
  ].join(' ');
}

export function loadManifest() {
  if (!fs.existsSync(MANIFEST_PATH)) return {};
  try {
    return JSON.parse(fs.readFileSync(MANIFEST_PATH, 'utf8'));
  } catch {
    return {};
  }
}

export function saveManifest(manifest) {
  fs.mkdirSync(path.dirname(MANIFEST_PATH), { recursive: true });
  fs.writeFileSync(MANIFEST_PATH, `${JSON.stringify(manifest, null, 2)}\n`, 'utf8');
}

export async function generateAiNewsImage({ slug, title, description = '', force = false }) {
  const apiKey = process.env.OPENAI_API_KEY?.trim();
  if (!apiKey) {
    return { ok: false, reason: 'missing_api_key' };
  }

  const outPath = path.join(OUT_DIR, `${slug}.png`);
  const manifest = loadManifest();
  if (!force && manifest[slug]?.mode === 'ai' && fs.existsSync(outPath)) {
    return { ok: true, skipped: true, path: outPath };
  }

  const prompt = buildAiImagePrompt({ slug, title, description });
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
    mode: 'ai',
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
      console.log(`  skip ${slug}.png (AI, đã có)`);
    } else if (result.ok) {
      ok++;
      console.log(`  AI ${slug}.png`);
      // Tránh rate limit OpenAI (~3 req/phút tier thấp)
      await new Promise((r) => setTimeout(r, 3500));
    } else {
      errors.push({ slug, reason: result.reason });
      console.error(`  lỗi ${slug}: ${result.reason}`);
    }
  }

  console.log(`AI ảnh tin tức: ${ok} mới, ${skipped} giữ nguyên, ${errors.length} lỗi / ${files.length} bài`);
  return { ok, skipped, errors, total: files.length };
}
