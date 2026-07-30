import { chromium } from 'playwright';
import path from 'node:path';
import fs from 'node:fs';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const out = path.resolve(__dirname, '../public/images/guide');
fs.mkdirSync(out, { recursive: true });

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({
  viewport: { width: 390, height: 844 },
  deviceScaleFactor: 2,
});

await page.goto('http://127.0.0.1:5178/unlock', {
  waitUntil: 'networkidle',
  timeout: 60000,
});
await page.waitForTimeout(800);
await page.screenshot({
  path: path.join(out, '01-tao-tai-khoan-dang-ky.png'),
  fullPage: true,
});

await page.getByRole('button', { name: /Nhập mã|mã mời/i }).first().click();
await page.waitForTimeout(400);
await page.screenshot({
  path: path.join(out, '03-ma-moi-collapse.png'),
  fullPage: true,
});

await page.getByRole('button', { name: /Đăng nhập/i }).first().click();
await page.waitForTimeout(500);
await page.screenshot({
  path: path.join(out, '02-dang-nhap.png'),
  fullPage: true,
});

await page.getByRole('button', { name: /Cách khác/i }).first().click();
await page.waitForTimeout(400);
await page.screenshot({
  path: path.join(out, '02b-dang-nhap-cach-khac.png'),
  fullPage: true,
});

await browser.close();
console.log('saved:', fs.readdirSync(out).join(', '));
