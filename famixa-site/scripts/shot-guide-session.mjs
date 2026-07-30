import { chromium } from 'playwright';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = path.dirname(fileURLToPath(import.meta.url));
const out = path.resolve(__dirname, '../public/images/guide');
const APP = process.env.FAMILY_APP_URL ?? 'http://127.0.0.1:5178';
const API = process.env.FAMILY_API_URL ?? 'http://127.0.0.1:5290';

fs.mkdirSync(out, { recursive: true });

async function apiLogin() {
  const res = await fetch(`${API}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({
      tenantCode: 'DEMO_FAMILY',
      username: 'admin',
      password: 'Admin@123',
    }),
  });
  if (!res.ok) {
    throw new Error(`API login failed (${res.status}): ${await res.text()}`);
  }
  const login = await res.json();
  const token = login.accessToken ?? login.AccessToken;
  if (!token) throw new Error('Login response missing accessToken');

  const famRes = await fetch(`${API}/api/family-os/families`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  if (!famRes.ok) {
    throw new Error(`Fetch families failed (${famRes.status}): ${await famRes.text()}`);
  }
  const families = await famRes.json();
  const family = families[0];
  if (!family?.id) throw new Error('No family found for DEMO_FAMILY');

  return {
    accessToken: token,
    refreshToken: login.refreshToken ?? login.RefreshToken ?? null,
    tenantCode: 'DEMO_FAMILY',
    familyId: String(family.id ?? family.Id),
    familyName: String(family.displayName ?? family.DisplayName ?? 'Gia đình mình'),
  };
}

async function seedSession(page, session) {
  await page.goto(`${APP}/unlock`, { waitUntil: 'domcontentloaded', timeout: 60000 });
  await page.evaluate((payload) => {
    localStorage.setItem(
      'familyos-mobile-session',
      JSON.stringify({
        state: {
          accessToken: payload.accessToken,
          refreshToken: payload.refreshToken,
          tenantCode: payload.tenantCode,
          familyId: payload.familyId,
          familyName: payload.familyName,
          member: null,
          parentPin: '1234',
        },
        version: 0,
      }),
    );
  }, session);
}

async function shot(page, filename) {
  await page.waitForTimeout(600);
  const target = path.join(out, filename);
  await page.screenshot({ path: target, fullPage: true });
  console.log('saved', filename);
}

const session = await apiLogin();

const browser = await chromium.launch({ headless: true });
const page = await browser.newPage({
  viewport: { width: 390, height: 844 },
  deviceScaleFactor: 2,
});

await seedSession(page, session);

await page.goto(`${APP}/who`, { waitUntil: 'networkidle', timeout: 60000 });
await page.getByText('Xin chào!', { exact: false }).first().waitFor({ timeout: 30000 });
await shot(page, '04-chon-thanh-vien.png');

await page.getByRole('button', { name: 'Mời tham gia' }).click();
await page.getByRole('heading', { name: 'Mời tham gia nhà' }).waitFor({ timeout: 15000 });
await shot(page, '05-moi-tham-gia-sheet.png');
await page.getByRole('button', { name: /Đóng|Close/i }).first().click().catch(() => page.keyboard.press('Escape'));
await page.waitForTimeout(400);

await page.goto(`${APP}/family-admin/members`, { waitUntil: 'networkidle', timeout: 60000 });
await page.locator('h1').filter({ hasText: 'Thành viên' }).waitFor({ timeout: 15000 });
await shot(page, '07-thanh-vien.png');

await page.goto(`${APP}/family-admin/invite`, { waitUntil: 'networkidle', timeout: 60000 });
await page.locator('h1').filter({ hasText: 'Mã nhà' }).waitFor({ timeout: 15000 });
await page.locator('.fa-invite-code').filter({ hasText: /[A-Z0-9-]{4,}/ }).waitFor({ timeout: 15000 });
await shot(page, '08-ma-nha.png');

await page.goto(`${APP}/who`, { waitUntil: 'networkidle', timeout: 60000 });
const parentCard = page.locator('.home-v2-member-list button').filter({ hasText: /Mẹ|Bố|Admin/i }).first();
await parentCard.waitFor({ timeout: 15000 });
await parentCard.click();
await page.waitForURL(/\/today/, { timeout: 30000 });
await page.waitForTimeout(1200);
await shot(page, '11-lich-hom-nay.png');

await page.goto(`${APP}/family-admin/routine`, { waitUntil: 'networkidle', timeout: 60000 });
await page.locator('h1').filter({ hasText: 'Routine' }).waitFor({ timeout: 15000 });
await shot(page, '09-routine.png');

await page.goto(`${APP}/family-admin/settings`, { waitUntil: 'networkidle', timeout: 60000 });
await page.locator('h1').filter({ hasText: 'Tài khoản & Cài đặt' }).waitFor({ timeout: 15000 });
await shot(page, '10-cai-dat.png');

await browser.close();
console.log('done:', fs.readdirSync(out).sort().join(', '));
