/**
 * Generate 2 PDFs for NOVIXA-WP-001 (Brief + Full).
 */
import { createRequire } from 'node:module';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const require = createRequire(import.meta.url);

let chromium;
try {
  ({ chromium } = require('playwright'));
} catch {
  console.error('Install playwright: npm i playwright && npx playwright install chromium');
  process.exit(1);
}

let marked;
try {
  ({ marked } = await import('marked'));
} catch {
  const { execSync } = await import('node:child_process');
  execSync('npm i marked --no-fund --no-audit', { cwd: __dirname, stdio: 'inherit' });
  ({ marked } = await import('marked'));
}

import { figureCss, injectFigures } from './wp001-figures.mjs';

const sharedCss = `
  @page { size: A4; margin: 14mm 16mm 12mm 16mm; }
  @page cover { margin: 0; }
  * { box-sizing: border-box; }
  html, body {
    margin: 0; padding: 0; color: #1a1f24;
    font-family: "Times New Roman", Times, "Noto Serif", serif;
    font-size: 13pt; line-height: 1.48;
    background: #fff;
    -webkit-print-color-adjust: exact; print-color-adjust: exact;
  }

  .cover {
    page: cover; page-break-after: always; min-height: 297mm;
    padding: 20mm 20mm 18mm; background: #0f2f38; color: #ffffff;
    display: flex; flex-direction: column; justify-content: space-between;
  }
  .cover-top {
    display: flex; align-items: flex-start; justify-content: space-between; gap: 18px;
  }
  .cover-brand { display: flex; align-items: flex-start; gap: 12px; max-width: 125mm; }
  .cover-brand img {
    width: 56px; height: 56px; object-fit: contain; flex-shrink: 0;
    background: #fff; border-radius: 6px; padding: 4px;
  }
  .cover-brand-text { display: flex; flex-direction: column; gap: 4px; padding-top: 2px; }
  .cover-brand-name {
    font-size: 18pt; font-weight: bold; letter-spacing: 0.06em; color: #ffffff;
  }
  .cover-tagline {
    font-size: 10.5pt; line-height: 1.35; color: #e8eef0; font-weight: normal;
    max-width: 95mm;
  }
  .cover-meta-code {
    font-size: 11pt; line-height: 1.45; letter-spacing: 0.04em;
    color: #f0f4f5; text-align: right; flex-shrink: 0;
  }
  .cover-mid { padding: 22mm 0 14mm; }
  .cover-eyebrow {
    font-size: 11.5pt; letter-spacing: 0.12em; text-transform: uppercase;
    color: #d7e2e5; margin-bottom: 12pt;
  }
  .cover h1 {
    font-size: 30pt; line-height: 1.18; font-weight: bold;
    margin: 0 0 14pt; max-width: 160mm; color: #ffffff;
  }
  .cover-subtitle {
    font-size: 15pt; line-height: 1.4; color: #f3f6f7;
    max-width: 155mm; margin: 0 0 18pt; font-style: italic;
  }
  .cover-lede {
    font-size: 14pt; max-width: 155mm; line-height: 1.5; color: #ffffff;
  }
  .cover-bottom {
    display: flex; justify-content: space-between; gap: 18px;
    font-size: 12pt; line-height: 1.45; color: #f2f6f7;
  }
  .cover-bottom strong { color: #ffffff; }

  .doc-header {
    display: flex; justify-content: space-between; align-items: baseline;
    margin-bottom: 12pt; font-size: 10pt; color: #44515a;
  }
  .doc-header strong { color: #0f2f38; }

  /* Chương / Phụ lục (h1) luôn sang trang mới — nhưng tránh để trang trước chỉ còn 1–2 dòng */
  main h1 {
    font-size: 17pt; color: #0f2f38; margin: 0 0 8pt;
    page-break-before: always; page-break-after: avoid;
    break-after: avoid-page;
  }
  main h1:first-of-type { page-break-before: always; }

  main h2.section-break {
    page-break-before: always;
  }

  h2 {
    font-size: 14.5pt; color: #0f2f38; margin: 12pt 0 6pt;
    page-break-after: avoid; break-after: avoid-page;
  }
  h3 {
    font-size: 13pt; margin: 10pt 0 4pt;
    page-break-after: avoid; break-after: avoid-page;
  }
  /* Kéo tiêu đề đi cùng đoạn phía sau — giảm trang chỉ còn 1 dòng tiêu đề / 1 dòng cuối */
  h2 + p, h2 + ul, h2 + ol, h2 + blockquote, h2 + table,
  h3 + p, h3 + ul, h3 + ol, h3 + blockquote, h3 + table {
    page-break-before: avoid; break-before: avoid-page;
  }
  p {
    margin: 0 0 6pt;
    orphans: 4; widows: 4;
  }
  ul, ol { margin: 0 0 8pt; padding-left: 18pt; orphans: 3; widows: 3; }
  li { margin: 0 0 2pt; orphans: 3; widows: 3; }
  blockquote {
    margin: 6pt 0 10pt; padding: 7pt 11pt; border-left: 3pt solid #0f2f38;
    background: #f3f7f8; font-style: italic;
    orphans: 3; widows: 3; page-break-inside: avoid;
  }
  blockquote p { margin: 0 0 4pt; orphans: 3; widows: 3; }
  blockquote p:last-child { margin: 0; }
  table {
    width: 100%; border-collapse: collapse; margin: 6pt 0 10pt;
    font-size: 11.5pt; page-break-inside: avoid;
  }
  th, td {
    border: 0.7pt solid #b7c3c9; padding: 5pt 7pt; vertical-align: top; text-align: left;
  }
  th { background: #0f2f38; color: #f4f1ea; font-weight: bold; }
  tr:nth-child(even) td { background: #f7fafb; }
  hr { display: none; }
  pre, code {
    font-family: "Times New Roman", Times, serif;
    font-size: 12pt; white-space: pre-wrap;
  }
  pre {
    background: #eef3f4; border: 0.7pt solid #c9d1d6;
    padding: 8pt; margin: 8pt 0 12pt;
  }
  a { color: inherit; text-decoration: none; }
  .callout {
    border-left: 3.5pt solid #c4a35a; background: #f7f4ec;
    padding: 8pt 11pt; margin: 0 0 14pt;
  }
  .footer-note {
    margin-top: 14pt; padding-top: 8pt;
    font-size: 10.5pt; color: #44515a;
    page-break-before: avoid;
  }
  ${figureCss}
`;

function coverHtml({ code, badge, title, subtitle, lede, extraRight }) {
  return `
  <section class="cover">
    <div class="cover-top">
      <div class="cover-brand">
        <img src="logo.png" alt="Novixa" />
        <div class="cover-brand-text">
          <div class="cover-brand-name">NOVIXA</div>
          <div class="cover-tagline">Healthcare Platform<br />Nền tảng AI dành cho nhà thuốc Việt Nam</div>
        </div>
      </div>
      <div class="cover-meta-code">${code}<br />${badge}</div>
    </div>
    <div class="cover-mid">
      <div class="cover-eyebrow">White Paper · Phân tích ngành</div>
      <h1>${title}</h1>
      <p class="cover-subtitle">${subtitle}</p>
      <p class="cover-lede">${lede}</p>
    </div>
    <div class="cover-bottom">
      <div>
        Dành cho chủ nhà thuốc · dược sĩ chủ · quản lý chuỗi nhỏ<br />
        <strong>Tài liệu để đối chiếu</strong> với thực tế nhà thuốc
      </div>
      <div style="text-align:right">
        ${extraRight}<br />
        Ngày xuất bản: 05/08/2026<br />
        novixa.vn
      </div>
    </div>
  </section>`;
}

function prepareFullMarkdown(md) {
  let text = md;
  text = text.replace(/\*\*Neo định vị:\*\*[^\n]+\n*/g, '');
  text = text.replace(/\*\*Brief đọc nhanh\*\*[^\n]+\n*/g, '');
  text = text.replace(/\*\*Outline biên tập\*\*[^\n]+\n*/g, '');
  text = text.replace(/Nếu anh\/chị chỉ có khoảng 10 phút[\s\S]*?12 chương dưới đây\.\n*/g, '');
  text = text.replace(/\n## Ghi chú nội bộ[\s\S]*$/m, '\n');
  text = text.replace(/\n## Changelog[\s\S]*$/m, '\n');
  text = text.replace(/^\| \*\*Brief đọc nhanh\*\* \|.*\|\n/gm, '');
  text = text.replace(/^\| \*\*Outline biên tập\*\* \|.*\|\n/gm, '');
  text = text.replace(/^# Từ Quản lý đến Phát triển\n\n\*\*[^*]+\*\*\n\n/, '');
  // Bỏ gạch ngang markdown ---
  text = text.replace(/^\s*---\s*$/gm, '');
  // Đánh dấu sang trang cho Lời nói đầu / Lời mời cuối
  text = text.replace(/^## Lời nói đầu\s*$/m, '## Lời nói đầu {.section-break}');
  text = text.replace(/^## Lời mời cuối\s*$/m, '## Lời mời cuối {.section-break}');
  // marked không parse {.class} mặc định — xử lý sau HTML
  text = text.replace(/## Lời nói đầu \{\.section-break\}/g, '## Lời nói đầu');
  text = text.replace(/## Lời mời cuối \{\.section-break\}/g, '## Lời mời cuối');
  return text.trim() + '\n';
}

function enhanceBodyHtml(html) {
  let out = injectFigures(html);
  // h2 đặc biệt sang trang mới
  out = out.replace(/<h2>Lời nói đầu<\/h2>/g, '<h2 class="section-break">Lời nói đầu</h2>');
  out = out.replace(/<h2>Lời mời cuối<\/h2>/g, '<h2 class="section-break">Lời mời cuối</h2>');
  return out;
}

function countPdfPages(buf) {
  const s = buf.toString('latin1');
  const re = /\/Type\s*\/Page(?!s)\b/g;
  let n = 0;
  while (re.exec(s)) n += 1;
  return n || 0;
}

async function htmlToPdf(htmlPath, pdfPath, footerLabel) {
  const browser = await chromium.launch({ headless: true });
  const page = await browser.newPage();
  await page.goto(pathToFileURL(htmlPath).href, { waitUntil: 'networkidle' });
  const pdfBuf = await page.pdf({
    path: pdfPath,
    format: 'A4',
    printBackground: true,
    preferCSSPageSize: true,
    displayHeaderFooter: true,
    headerTemplate: '<div></div>',
    footerTemplate: `
      <div style="width:100%;font-size:9pt;font-family:'Times New Roman',Times,serif;color:#5a6570;padding:0 18mm;display:flex;justify-content:space-between;box-sizing:border-box;">
        <span>${footerLabel}</span>
        <span><span class="pageNumber"></span> / <span class="totalPages"></span></span>
      </div>`,
    margin: { top: '0mm', bottom: '12mm', left: '0mm', right: '0mm' },
  });
  await browser.close();
  const pages = countPdfPages(Buffer.from(pdfBuf));
  console.log('PDF written:', pdfPath, `(${pages} trang)`);
  return pages;
}

// --- Brief HTML cover polish (tagline + readability) ---
function polishBriefHtml(html) {
  let out = html;
  // Insert tagline if missing
  if (!out.includes('Healthcare Platform')) {
    out = out.replace(
      /<div class="cover-brand-name">NOVIXA<\/div>/,
      `<div class="cover-brand-text">
          <div class="cover-brand-name">NOVIXA</div>
          <div class="cover-tagline">Healthcare Platform<br />Nền tảng AI dành cho nhà thuốc Việt Nam</div>
        </div>`,
    );
    // if structure was brand-name only inside cover-brand next to img
    out = out.replace(
      /(<div class="cover-brand">\s*<img[^>]*>)\s*<div class="cover-brand-text">/s,
      '$1\n        <div class="cover-brand-text">',
    );
  }
  // Cover readability CSS patches
  if (!out.includes('.cover-tagline')) {
    out = out.replace(
      '</style>',
      `
    .cover { color: #ffffff; }
    .cover-brand { align-items: flex-start; max-width: 125mm; }
    .cover-brand-text { display: flex; flex-direction: column; gap: 4px; padding-top: 2px; }
    .cover-brand-name { font-size: 18pt; color: #ffffff; }
    .cover-tagline { font-size: 10.5pt; line-height: 1.35; color: #e8eef0; max-width: 95mm; }
    .cover-meta-code { font-size: 11pt; color: #f0f4f5; }
    .cover-eyebrow { font-size: 11.5pt; color: #d7e2e5; }
    .cover h1, .cover-title { font-size: 30pt; color: #ffffff; }
    .cover-subtitle { font-size: 15pt; color: #f3f6f7; }
    .cover-lede { font-size: 14pt; color: #ffffff; line-height: 1.5; }
    .cover-bottom { font-size: 12pt; color: #f2f6f7; border-top: none; }
    .cover-rule { display: none; }
    h2 { border-bottom: none; padding-bottom: 0; }
    hr { display: none; }
</style>`,
    );
  }
  // Cover bottom copy
  out = out.replace(
    /Tài liệu giới thiệu tư duy sản phẩm — không phải bảng giá/,
    'Tài liệu để đối chiếu với thực tế nhà thuốc',
  );
  // Remove gold rule element if present
  out = out.replace(/<hr class="cover-rule"\s*\/?>/g, '');
  return out;
}

// ========== BRIEF ==========
const briefHtmlSrc = path.join(__dirname, 'NOVIXA-WP-001-executive-brief.html');
let briefHtml = fs.readFileSync(briefHtmlSrc, 'utf8');
briefHtml = polishBriefHtml(briefHtml);
const briefHtmlBuilt = path.join(__dirname, 'NOVIXA-WP-001-EB-Brief-built.html');
fs.writeFileSync(briefHtmlBuilt, briefHtml, 'utf8');
const briefPdf = path.join(__dirname, 'NOVIXA-WP-001-EB-Brief.pdf');
const briefPages = await htmlToPdf(
  briefHtmlBuilt,
  briefPdf,
  'Novixa · NOVIXA-WP-001-EB · Executive Brief',
);
fs.copyFileSync(briefPdf, path.join(__dirname, 'NOVIXA-WP-001-Tu-Quan-ly-den-Phat-trien.pdf'));

// ========== FULL ==========
const fullMdPath = path.join(__dirname, '..', 'NOVIXA-WP-001-tu-quan-ly-den-phat-trien-v1.md');
let md = prepareFullMarkdown(fs.readFileSync(fullMdPath, 'utf8'));
marked.setOptions({ gfm: true, breaks: false });
const bodyHtml = enhanceBodyHtml(marked.parse(md));

const fullHtmlPath = path.join(__dirname, 'NOVIXA-WP-001-full.html');
const fullPdf = path.join(__dirname, 'NOVIXA-WP-001-Full.pdf');

function buildFullHtml(pageNote) {
  return `<!DOCTYPE html>
<html lang="vi">
<head>
  <meta charset="utf-8" />
  <title>NOVIXA-WP-001 — Từ Quản lý đến Phát triển</title>
  <style>${sharedCss}</style>
</head>
<body>
${coverHtml({
  code: 'NOVIXA-WP-001',
  badge: 'White Paper · v1.6',
  title: 'Từ Quản lý đến Phát triển',
  subtitle: 'Vì sao nhà thuốc cần một nền tảng mới sau phần mềm bán hàng?',
  lede: 'Đây là <strong>tài liệu để đối chiếu</strong> với thực tế nhà thuốc mình — về khoảng trống giữa <strong>quản lý bán hàng</strong> và <strong>phát triển từ khách đã tin tưởng</strong>, kể cả qua <strong>Sổ sức khỏe số của gia đình</strong> sau khi khách ra khỏi cửa.',
  extraRight: pageNote,
})}
<main>
  <div class="doc-header">
    <div><strong>Novixa</strong> · NOVIXA-WP-001 · White Paper</div>
    <div>Từ Quản lý đến Phát triển</div>
  </div>
  ${bodyHtml}
  <div class="footer-note">
    <p><strong>Novixa</strong> · White Paper series · Dành cho chủ nhà thuốc.</p>
    <p>Tài liệu không thay thế tư vấn y khoa / pháp lý. Liên hệ / xem thử: <strong>novixa.vn</strong></p>
  </div>
</main>
</body>
</html>`;
}

// Pass 1: estimate pages with generic cover note
fs.writeFileSync(fullHtmlPath, buildFullHtml('13 chương + phụ lục A–J'), 'utf8');
let fullPages = await htmlToPdf(
  fullHtmlPath,
  fullPdf,
  'Novixa · NOVIXA-WP-001 · White Paper',
);

// Pass 2: stamp actual page count on cover
fs.writeFileSync(
  fullHtmlPath,
  buildFullHtml(`13 chương + phụ lục A–J<br /><strong>${fullPages} trang</strong>`),
  'utf8',
);
fullPages = await htmlToPdf(
  fullHtmlPath,
  fullPdf,
  'Novixa · NOVIXA-WP-001 · White Paper',
);

console.log('\nXong.');
console.log(`1) Brief: ${briefPdf} (${briefPages} trang)`);
console.log(`2) Full:  ${fullPdf} (${fullPages} trang)`);
