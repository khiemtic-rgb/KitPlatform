/**
 * Trang bìa PDF — Hồ sơ đề nghị kết nối liên thông CSDL Dược Quốc gia.
 * Chỉ các hạng mục cần thiết để nộp Bộ Y tế / Cục QLD (không danh mục thừa).
 */
import { createRequire } from 'node:module';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const dossierRoot = path.resolve(__dirname, '..');
const assetsDir = path.join(dossierRoot, 'assets');
const outPdf = path.join(dossierRoot, 'word', 'NVX-CQD-00-Trang-bia.pdf');
const htmlOut = path.join(__dirname, 'cover.html');

const require = createRequire(import.meta.url);

function resolvePlaywright() {
  const candidates = [
    path.join(__dirname, 'node_modules', 'playwright'),
    path.join(dossierRoot, '..', '..', '04-gtm', 'white-papers', 'print', 'node_modules', 'playwright'),
  ];
  for (const c of candidates) {
    try {
      return require(path.join(c, 'index.js'));
    } catch {
      /* try next */
    }
  }
  try {
    return require('playwright');
  } catch {
    console.error('Install playwright: npm i playwright && npx playwright install chromium');
    process.exit(1);
  }
}

const { chromium } = resolvePlaywright();

const kitLogo = path.join(assetsDir, 'logo-kit.png');
const novixaLogo = path.join(assetsDir, 'logo-novixa.png');
if (!fs.existsSync(kitLogo)) throw new Error(`Missing ${kitLogo}`);
if (!fs.existsSync(novixaLogo)) throw new Error(`Missing ${novixaLogo}`);

const kitDataUri = `data:image/png;base64,${fs.readFileSync(kitLogo).toString('base64')}`;
const novixaDataUri = `data:image/png;base64,${fs.readFileSync(novixaLogo).toString('base64')}`;

function fontDataUri(fontPath) {
  return `data:font/ttf;base64,${fs.readFileSync(fontPath).toString('base64')}`;
}

const tahomaRegular = fontDataUri('C:/Windows/Fonts/tahoma.ttf');
const tahomaBold = fontDataUri('C:/Windows/Fonts/tahomabd.ttf');

const html = `<!DOCTYPE html>
<html lang="vi">
<head>
<meta charset="utf-8" />
<title>Hồ sơ đề nghị — Novixa</title>
<style>
  @font-face {
    font-family: "TahomaDoc";
    src: url("${tahomaRegular}") format("truetype");
    font-weight: 400 500;
    font-style: normal;
  }
  @font-face {
    font-family: "TahomaDoc";
    src: url("${tahomaBold}") format("truetype");
    font-weight: 600 800;
    font-style: normal;
  }
  @page { size: A4; margin: 0; }
  * { box-sizing: border-box; }
  html, body {
    margin: 0; padding: 0; width: 210mm; height: 297mm;
    font-family: "TahomaDoc", Tahoma, "Segoe UI", Helvetica, Arial, sans-serif;
    color: #0b3d2e;
    background: #fff;
    -webkit-print-color-adjust: exact;
    print-color-adjust: exact;
  }
  .page {
    position: relative;
    width: 210mm; height: 297mm;
    padding: 16mm 18mm 0;
    overflow: hidden;
    display: flex;
    flex-direction: column;
    align-items: center;
  }
  .kit-logo {
    width: 54mm;
    height: auto;
    object-fit: contain;
    margin-top: 2mm;
  }
  .kit-tagline {
    margin-top: 3mm;
    font-size: 12pt;
    letter-spacing: 0.06em;
    text-transform: uppercase;
    color: #1f4d3a;
    font-weight: 700;
  }
  .title-block {
    margin-top: 18mm;
    text-align: center;
    max-width: 168mm;
  }
  .title {
    margin: 0;
    font-size: 34pt;
    font-weight: 800;
    letter-spacing: 0.03em;
    color: #0a5c3a;
    line-height: 1.12;
  }
  .ornament {
    width: 48mm;
    height: 0;
    border-top: 1.5px solid #0a5c3a;
    margin: 7mm auto 0;
    position: relative;
  }
  .ornament::after {
    content: "";
    position: absolute;
    left: 50%;
    top: -3.5px;
    width: 6px;
    height: 6px;
    background: #0a5c3a;
    transform: translateX(-50%) rotate(45deg);
  }
  .subtitle {
    margin: 7mm auto 0;
    font-size: 15pt;
    line-height: 1.4;
    font-weight: 600;
    color: #145a3d;
    max-width: 168mm;
  }
  .product {
    margin-top: 14mm;
    display: flex;
    flex-direction: column;
    align-items: center;
    gap: 2mm;
  }
  .product img {
    width: 42mm;
    height: auto;
    object-fit: contain;
  }
  .proposer {
    margin-top: 14mm;
    text-align: center;
    width: 100%;
  }
  .proposer-label {
    display: flex;
    align-items: center;
    justify-content: center;
    gap: 10px;
    color: #0a5c3a;
    font-size: 13pt;
    font-weight: 700;
    letter-spacing: 0.06em;
    text-transform: uppercase;
  }
  .proposer-label::before,
  .proposer-label::after {
    content: "";
    width: 28mm;
    height: 1px;
    background: #0a5c3a;
  }
  .proposer-name {
    margin-top: 5mm;
    font-size: 16pt;
    font-weight: 800;
    color: #0a5c3a;
    letter-spacing: 0.01em;
    line-height: 1.3;
    max-width: 170mm;
    margin-left: auto;
    margin-right: auto;
  }
  .footer-wave {
    position: absolute;
    left: 0; right: 0; bottom: 0;
    height: 78mm;
    background:
      radial-gradient(120% 90% at 50% 120%, #0a4d32 0%, #0d6b45 42%, #12945c 70%, transparent 71%),
      linear-gradient(180deg, transparent 0%, #0b5a3a 58%, #08472d 100%);
  }
  .footer-wave::before {
    content: "";
    position: absolute;
    left: -10%;
    right: -10%;
    top: 0;
    height: 28mm;
    background:
      radial-gradient(70% 120% at 20% 100%, rgba(18,148,92,0.95) 0%, transparent 60%),
      radial-gradient(60% 100% at 55% 120%, rgba(10,92,58,0.95) 0%, transparent 62%),
      radial-gradient(70% 120% at 85% 100%, rgba(22,168,105,0.9) 0%, transparent 58%);
  }
  .footer-content {
    position: absolute;
    left: 0; right: 0; bottom: 0;
    height: 52mm;
    color: #fff;
    z-index: 2;
    display: flex;
    flex-direction: column;
    align-items: center;
    justify-content: flex-end;
    padding: 0 16mm 12mm;
  }
  .place-date {
    font-size: 13.5pt;
    font-weight: 600;
    letter-spacing: 0.02em;
    margin-bottom: 8mm;
  }
  .meta-box {
    align-self: flex-end;
    background: rgba(4, 48, 30, 0.88);
    border: 1px solid rgba(255,255,255,0.35);
    padding: 4mm 6mm;
    min-width: 56mm;
    text-align: center;
  }
  .meta-box .meta-title {
    font-size: 11pt;
    font-weight: 700;
    letter-spacing: 0.05em;
    text-transform: uppercase;
  }
  .meta-box .meta-line {
    margin: 2.5mm auto;
    width: 70%;
    height: 1px;
    background: rgba(255,255,255,0.45);
  }
  .meta-box .meta-ver {
    font-size: 11pt;
    font-weight: 600;
    letter-spacing: 0.03em;
  }
</style>
</head>
<body>
  <div class="page">
    <img class="kit-logo" src="${kitDataUri}" alt="KIT Technology" />
    <div class="kit-tagline">Giải pháp công nghệ — Giá trị thực tiễn</div>

    <div class="title-block">
      <h1 class="title">HỒ SƠ ĐỀ NGHỊ</h1>
      <div class="ornament"></div>
      <p class="subtitle">
        Hướng dẫn và hỗ trợ kết nối, liên thông phần mềm Novixa
        với Hệ thống Cơ sở dữ liệu Dược Quốc gia
      </p>
    </div>

    <div class="product">
      <img src="${novixaDataUri}" alt="Novixa" />
    </div>

    <div class="proposer">
      <div class="proposer-label">Đơn vị đề nghị</div>
      <div class="proposer-name">CÔNG TY TNHH TRUYỀN THÔNG VÀ CÔNG NGHỆ KIT</div>
    </div>

    <div class="footer-wave" aria-hidden="true"></div>
    <div class="footer-content">
      <div class="place-date">Thái Nguyên, tháng 08 năm 2026</div>
      <div class="meta-box">
        <div class="meta-title">Tài liệu gửi Bộ Y tế</div>
        <div class="meta-line"></div>
        <div class="meta-ver">Phiên bản 1.0</div>
      </div>
    </div>
  </div>
</body>
</html>`;

fs.mkdirSync(path.dirname(outPdf), { recursive: true });
fs.writeFileSync(htmlOut, html, 'utf8');

const browser = await chromium.launch();
const page = await browser.newPage();
await page.goto(pathToFileURL(htmlOut).href, { waitUntil: 'networkidle' });
await page.evaluate(async () => {
  if (document.fonts?.ready) await document.fonts.ready;
});
await page.pdf({
  path: outPdf,
  format: 'A4',
  printBackground: true,
  preferCSSPageSize: true,
  margin: { top: '0', right: '0', bottom: '0', left: '0' },
});
await browser.close();

console.log(`OK: ${outPdf}`);
