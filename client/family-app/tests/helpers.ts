import { expect, type Page } from '@playwright/test';

export const DEMO = {
  tenant: process.env.SMOKE_TENANT ?? 'DEMO_FAMILY',
  username: process.env.SMOKE_USER ?? 'admin',
  password: process.env.SMOKE_PASS ?? 'Admin@123',
};

/** Signs in through the real unlock form using the family-code path. */
export async function login(page: Page): Promise<void> {
  await page.goto('/unlock');

  const toLogin = page.getByRole('button', { name: /Đăng nhập/ }).first();
  if (await toLogin.count()) await toLogin.click();

  const other = page.getByRole('button', { name: /Cách khác/ });
  if (await other.count()) await other.click();

  await page.locator('input[autocomplete="organization"]').fill(DEMO.tenant);
  await page.locator('input[autocomplete="username"]').fill(DEMO.username);
  await page.locator('input[type="password"]').first().fill(DEMO.password);
  await page.getByRole('button', { name: /Vào nhà/ }).click();

  await page.waitForURL(/\/who$/, { timeout: 30_000 });
  await expect(page.locator('.home-v2-member').first()).toBeVisible();
}

/** Strips dropdown chevrons / whitespace so Who and Parent labels compare cleanly. */
export function normalizeFamilyLabel(raw: string): string {
  return raw
    .replace(/[▾▼›]/g, '')
    .replace(/\s+/g, ' ')
    .trim();
}

export async function readWhoFamilyName(page: Page): Promise<string> {
  const pill = page.locator('.home-v2-family-pill').first();
  await expect(pill).toBeVisible();
  return normalizeFamilyLabel(await pill.innerText());
}

export async function readParentFamilyName(page: Page): Promise<string> {
  const label = page.locator('.ph-b4-family').first();
  await expect(label).toBeVisible();
  return normalizeFamilyLabel(await label.innerText());
}

/**
 * Guards the failure mode where a sheet unmounts visually but leaves
 * `.sheet-backdrop` in the DOM: `body:has(.sheet-backdrop)` then pins
 * `overflow: hidden` and the whole screen stops scrolling.
 */
export async function expectNoScrollLock(page: Page, where: string): Promise<void> {
  const state = await page.evaluate(() => ({
    backdrops: document.querySelectorAll('.sheet-backdrop, .home-sheet-backdrop').length,
    bodyOverflowY: getComputedStyle(document.body).overflowY,
  }));
  expect(state.backdrops, `${where}: sheet backdrop left mounted`).toBe(0);
  expect(state.bodyOverflowY, `${where}: body scroll still locked`).not.toBe('hidden');
}

/** Fails if the page is taller than the viewport but refuses to scroll. */
export async function expectPageScrolls(page: Page, where: string): Promise<void> {
  const overflows = await page.evaluate(() => {
    const de = document.scrollingElement ?? document.documentElement;
    return de.scrollHeight > de.clientHeight + 8;
  });
  if (!overflows) return;

  await page.evaluate(() => {
    (document.scrollingElement ?? document.documentElement).scrollTop = 0;
  });
  await page.mouse.move(
    page.viewportSize()!.width / 2,
    Math.round(page.viewportSize()!.height / 2),
  );
  await page.mouse.wheel(0, 600);
  await page.waitForTimeout(400);

  const scrollTop = await page.evaluate(
    () => (document.scrollingElement ?? document.documentElement).scrollTop,
  );
  expect(scrollTop, `${where}: page does not scroll with the wheel`).toBeGreaterThan(0);
}

/**
 * Opens every "Xem tất cả" sheet on the current tab and checks that the sheet
 * actually lands inside the viewport, then closes it and re-checks scrolling.
 */
export async function exerciseSheets(page: Page, where: string): Promise<void> {
  const openers = page.getByRole('button', { name: /^Xem tất cả/ });
  const count = await openers.count();

  for (let i = 0; i < count; i += 1) {
    const opener = openers.nth(i);
    if (!(await opener.isVisible())) continue;

    await opener.click();
    await page.waitForTimeout(700);

    const sheet = await page.evaluate(() => {
      const bd = document.querySelector('.sheet-backdrop, .home-sheet-backdrop');
      if (!bd) return null;
      const r = bd.getBoundingClientRect();
      return {
        position: getComputedStyle(bd).position,
        insideViewport: r.top < innerHeight && r.bottom > 0 && r.left < innerWidth,
      };
    });

    if (sheet) {
      expect(sheet.position, `${where}: sheet #${i} is not a viewport overlay`).toBe('fixed');
      expect(sheet.insideViewport, `${where}: sheet #${i} opened off-screen`).toBe(true);

      const close = page.getByRole('button', { name: /^Đóng$/ }).first();
      if (await close.count()) await close.click();
      else await page.keyboard.press('Escape');
      await page.waitForTimeout(600);
    }

    await expectNoScrollLock(page, `${where} after sheet #${i}`);
    await expectPageScrolls(page, `${where} after sheet #${i}`);
  }
}
