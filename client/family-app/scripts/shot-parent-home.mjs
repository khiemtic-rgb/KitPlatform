import { chromium } from '@playwright/test';
import { mkdirSync } from 'node:fs';

const BASE = process.env.SMOKE_BASE ?? 'http://localhost:5178';
const TENANT = process.env.SMOKE_TENANT ?? 'DEMO_FAMILY';
const USER = process.env.SMOKE_USER ?? 'admin';
const PASS = process.env.SMOKE_PASS ?? 'Admin@123';
const OUT = process.env.SHOT_OUT ?? 'artifacts/parent-home-bcd.png';

mkdirSync('artifacts', { recursive: true });

const browser = await chromium.launch();
const page = await browser.newPage({ viewport: { width: 430, height: 932 } });

await page.goto(`${BASE}/unlock`);
const toLogin = page.getByRole('button', { name: /Đăng nhập/ }).first();
if (await toLogin.count()) await toLogin.click();
const other = page.getByRole('button', { name: /Cách khác/ });
if (await other.count()) await other.click();
await page.locator('input[autocomplete="organization"]').fill(TENANT);
await page.locator('input[autocomplete="username"]').fill(USER);
await page.locator('input[type="password"]').first().fill(PASS);
await page.getByRole('button', { name: /Vào nhà/ }).click();
await page.waitForURL(/\/who$/, { timeout: 30_000 });

await page.locator('.home-v2-member', { hasText: 'Phụ huynh' }).first().click();
await page.waitForSelector('.ph-b4-home', { timeout: 30_000 });
await page.waitForTimeout(2500);

await page.screenshot({ path: OUT, fullPage: true });
for (const [sel, name] of [
  ['.ph-sibling-nudge-cta', 'zoom-nudge'],
  ['.ph-coop-card', 'zoom-coop'],
  ['.ph-ritual-card', 'zoom-ritual'],
]) {
  const el = page.locator(sel).first();
  if (await el.count()) await el.screenshot({ path: `artifacts/${name}.png` });
}
const nudgeCta = page.locator('.ph-sibling-nudge-cta').first();
if (await nudgeCta.count()) {
  await nudgeCta.click();
  await page.waitForSelector('.ph-sibling-nudge-sheet', { timeout: 15_000 });
  await page.waitForTimeout(1200);
  await page.locator('.ph-sibling-nudge-sheet').screenshot({ path: 'artifacts/nudge-sheet.png' });
}
console.log(`saved ${OUT}`);
await browser.close();
