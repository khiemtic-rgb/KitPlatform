import fs from 'node:fs';
import path from 'node:path';
import { BRAND_TAGLINE, LOGO_PATH, OG_HEIGHT, OG_WIDTH } from './news-image-lib.mjs';

function escapeXml(s) {
  return String(s)
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

function buildFooterSvg(title) {
  const safeTitle = escapeXml(title.slice(0, 72));
  return Buffer.from(`<?xml version="1.0" encoding="UTF-8"?>
<svg xmlns="http://www.w3.org/2000/svg" width="${OG_WIDTH}" height="120" viewBox="0 0 ${OG_WIDTH} 120">
  <defs>
    <linearGradient id="bar" x1="0" y1="0" x2="0" y2="1">
      <stop offset="0%" stop-color="#061829" stop-opacity="0"/>
      <stop offset="35%" stop-color="#061829" stop-opacity="0.82"/>
      <stop offset="100%" stop-color="#061829" stop-opacity="0.95"/>
    </linearGradient>
  </defs>
  <rect width="${OG_WIDTH}" height="120" fill="url(#bar)"/>
  <text x="48" y="52" fill="#ffffff" font-family="Segoe UI, Arial, sans-serif" font-size="26" font-weight="700">${safeTitle}</text>
  <text x="48" y="92" fill="#cbd5e1" font-family="Segoe UI, Arial, sans-serif" font-size="20" font-weight="600">${escapeXml(BRAND_TAGLINE)}</text>
  <text x="${OG_WIDTH - 48}" y="92" fill="#94a3b8" font-family="Segoe UI, Arial, sans-serif" font-size="18" text-anchor="end">novixa.vn</text>
</svg>`);
}

/** Crop/resize ảnh nền + footer Novixa + logo góc phải. */
export async function applyBrandOverlay(imageBuffer, { title }) {
  const sharp = (await import('sharp')).default;
  const base = await sharp(imageBuffer)
    .resize(OG_WIDTH, OG_HEIGHT, { fit: 'cover', position: 'centre' })
    .png()
    .toBuffer();

  const composites = [{ input: buildFooterSvg(title), top: OG_HEIGHT - 120, left: 0 }];

  if (fs.existsSync(LOGO_PATH)) {
    const logo = await sharp(LOGO_PATH).resize(96, null, { fit: 'inside' }).png().toBuffer();
    composites.push({ input: logo, top: 28, left: OG_WIDTH - 120 });
  }

  return sharp(base).composite(composites).png({ quality: 92 }).toBuffer();
}

export async function writeOverlayImage(imageBuffer, outPath, meta) {
  fs.mkdirSync(path.dirname(outPath), { recursive: true });
  const png = await applyBrandOverlay(imageBuffer, meta);
  fs.writeFileSync(outPath, png);
  return outPath;
}
