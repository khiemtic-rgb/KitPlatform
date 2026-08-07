/**
 * Generate NOVIXA-WP-001 Executive Brief PDF (A4 print).
 * Uses Playwright from docs/novixa/07-customer/guides/print if available.
 */
import { createRequire } from 'node:module';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { pathToFileURL } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const require = createRequire(import.meta.url);

const playwrightRoots = [
  path.resolve(__dirname, '../../../07-customer/guides/print/node_modules/playwright'),
  path.resolve(__dirname, '../../../../../../docs/novixa/07-customer/guides/print/node_modules/playwright'),
];

let chromium;
for (const root of playwrightRoots) {
  try {
    ({ chromium } = require(root));
    break;
  } catch {
    // try next
  }
}

if (!chromium) {
  try {
    ({ chromium } = require('playwright'));
  } catch {
    console.error('Playwright not found. Install in print folder: npm i playwright && npx playwright install chromium');
    process.exit(1);
  }
}

const htmlPath = path.join(__dirname, 'NOVIXA-WP-001-executive-brief.html');
const pdfPath = path.join(__dirname, 'NOVIXA-WP-001-Tu-Quan-ly-den-Phat-trien.pdf');

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage();
await page.goto(pathToFileURL(htmlPath).href, { waitUntil: 'networkidle' });
await page.pdf({
  path: pdfPath,
  format: 'A4',
  printBackground: true,
  preferCSSPageSize: true,
  displayHeaderFooter: true,
  headerTemplate: `<div></div>`,
  footerTemplate: `
    <div style="width:100%;font-size:9pt;font-family:'Times New Roman',Times,serif;color:#5a6570;padding:0 18mm;display:flex;justify-content:space-between;box-sizing:border-box;">
      <span>Novixa · NOVIXA-WP-001-EB</span>
      <span><span class="pageNumber"></span> / <span class="totalPages"></span></span>
    </div>
  `,
  // Keep chrome margins small so CSS @page / cover can breathe; footer needs bottom space
  margin: {
    top: '0mm',
    bottom: '12mm',
    left: '0mm',
    right: '0mm',
  },
});
await browser.close();
console.log('PDF written:', pdfPath);
