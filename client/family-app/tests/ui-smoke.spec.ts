import { expect, test, type Page } from '@playwright/test';
import {
  exerciseSheets,
  expectNoScrollLock,
  expectPageScrolls,
  login,
  readParentFamilyName,
  readWhoFamilyName,
} from './helpers';

const KID_TABS = ['Trang chủ', 'Kế hoạch', 'Kho báu', 'Nhật ký'];
const PARENT_TABS = ['Trang chủ', 'Kế hoạch', 'Nhật ký'];

async function pickChild(page: Page): Promise<void> {
  await page.locator('.home-v2-member').first().click();
  await page.waitForURL(/\/today$/, { timeout: 30_000 });
  await expect(page.locator('.kid-v2')).toBeVisible({ timeout: 30_000 });
}

async function pickParent(page: Page): Promise<void> {
  await page.locator('.home-v2-member', { hasText: 'Phụ huynh' }).first().click();
  await page.waitForURL(/\/today$/, { timeout: 30_000 });
  await expect(page.locator('.ph-tabbar')).toBeVisible({ timeout: 30_000 });
}

test.describe('kid screen', () => {
  test('every tab scrolls and every sheet closes cleanly', async ({ page }) => {
    await login(page);
    await pickChild(page);

    for (const tab of KID_TABS) {
      await page.locator('.kv2-tabbar .kv2-tab', { hasText: tab }).first().click();
      await page.waitForTimeout(1200);

      await expectNoScrollLock(page, `kid/${tab}`);
      await expectPageScrolls(page, `kid/${tab}`);
      await exerciseSheets(page, `kid/${tab}`);
    }
  });

  test('home shows Nhà mình team strip with team percent', async ({ page }) => {
    await login(page);
    await pickChild(page);
    await page.locator('.kv2-tabbar').getByRole('button', { name: 'Trang chủ' }).click();
    await page.waitForTimeout(800);

    const strip = page.locator('.kv2-movie-strip');
    await expect(strip).toBeVisible();
    await expect(strip).toContainText(/Nhà mình/);
    await expect(strip.locator('strong')).toContainText(/%/);
  });

  test('garden preview shows the same plant state as the full garden', async ({ page }) => {
    await login(page);
    await pickChild(page);

    await page.locator('.kv2-tabbar').getByRole('button', { name: 'Trang chủ' }).click();
    await expect(page.locator('.kv2-garden-preview')).toBeVisible();

    const preview = await readPots(page, '.kv2-garden-preview-plants .kv2-pot');
    await page.locator('.kv2-garden-preview').click();
    await expect(page.locator('.kv2-garden-plot')).toBeVisible();
    const full = await readPots(page, '.kv2-garden-plot .kv2-pot');

    expect(preview.length, 'preview shows a different number of pots').toBe(full.length);

    preview.forEach((pot, i) => {
      expect(pot.mood, `pot ${i}: mood class differs from the full garden`).toBe(full[i].mood);
      // The mini pot only shrinks the drop shadow; the wilt/lock treatment
      // (grayscale, saturate, brightness) has to survive.
      expect(
        pot.plantEffects,
        `pot ${i}: plant effects dropped in the preview`,
      ).toEqual(full[i].plantEffects);
      expect(
        pot.avatarEffects,
        `pot ${i}: avatar effects dropped in the preview`,
      ).toEqual(full[i].avatarEffects);
    });
  });

  test('parent switch control is not a notifications bell; Soft-lock English is gone', async ({
    page,
  }) => {
    await login(page);
    await pickChild(page);

    const switchBtn = page.getByRole('button', { name: 'Đổi sang bố mẹ' });
    await expect(switchBtn).toBeVisible();
    const icon = (await switchBtn.innerText()).trim();
    expect(icon, 'switch-to-parent still uses the notifications bell').not.toContain('🔔');

    const body = await page.locator('.kid-v2').innerText();
    expect(body, 'Soft-lock English leftover on kid screen').not.toMatch(/Soft-lock/i);
  });
});

test.describe('parent screen', () => {
  test('family name on Who matches Parent Home (not rebuilt from child names)', async ({
    page,
  }) => {
    await login(page);

    const whoName = await readWhoFamilyName(page);
    expect(whoName.length, 'Who screen has no family name').toBeGreaterThan(0);

    await pickParent(page);
    await page.locator('.ph-tabbar').getByRole('button', { name: 'Trang chủ' }).click();
    await page.waitForTimeout(800);

    const parentName = await readParentFamilyName(page);
    expect(
      parentName,
      'Parent Home rebuilt the family label from child nicknames instead of session familyName',
    ).toBe(whoName);
  });

  test('home hero is house-first: team line + no lagging-child name on brief title', async ({
    page,
  }) => {
    await login(page);
    await pickParent(page);
    await page.locator('.ph-tabbar').getByRole('button', { name: 'Trang chủ' }).click();
    await page.waitForTimeout(800);

    const teamLine = page.locator('.ph-b4-team-line');
    await expect(teamLine).toBeVisible();
    await expect(teamLine).toContainText(/Cả nhà hôm nay/);

    const brief = page.locator('.ph-b4-brief');
    await expect(brief.locator('.ph-b4-brief-who')).toContainText('Cả nhà');

    const title = (await brief.locator('.ph-b4-brief-title').innerText()).trim();
    expect(title, 'brief title still names a child on the house hero').not.toMatch(
      /ưu tiên 1 việc của \S+ trước/,
    );
  });

  test('priority awaiting uses wait icon, not a done checkmark', async ({ page }) => {
    await login(page);
    await pickParent(page);
    await page.locator('.ph-tabbar').getByRole('button', { name: 'Trang chủ' }).click();
    await page.waitForTimeout(800);

    const awaiting = page.locator('.ph-b4-priority-item', {
      hasText: /Con báo đã xong|chạm để xác nhận/,
    });
    test.skip((await awaiting.count()) === 0, 'no awaiting items in this dataset');

    const icon = (await awaiting.first().locator('.ph-b4-priority-ico').innerText()).trim();
    expect(icon, 'awaiting priority still looks "done"').not.toBe('✓');
    expect(icon).toBe('⏳');
  });

  test('Movie Night treasure uses team progress and role-aware copy', async ({ page }) => {
    await login(page);
    await pickParent(page);
    await page.locator('.ph-tabbar').getByRole('button', { name: 'Trang chủ' }).click();
    await page.waitForTimeout(600);

    const khoBau = page.getByRole('button', { name: 'Kho báu' }).first();
    test.skip((await khoBau.count()) === 0, 'Kho báu chip missing on parent home');
    await khoBau.click();
    await page.waitForTimeout(1000);

    const card = page.locator('.ph-treasure-family').first();
    test.skip((await card.count()) === 0, 'no Movie Night treasure card');

    const shown = (await card.locator('.ph-treasure-family-bar em').innerText()).trim();
    expect(shown.endsWith('%'), `treasure bar label looks wrong: ${shown}`).toBe(true);

    const body = await card.innerText();
    expect(body, 'still says hardcoded "mẹ xác nhận"').not.toMatch(/— mẹ xác nhận/);
    // Prefer team/member framing when unlock exists, not day-task leftover.
    if (/Chỉ còn \d+/.test(body)) {
      expect(body, 'Movie Night still counts day tasks instead of team members').toMatch(
        /thành viên|việc|kế hoạch/,
      );
    }
  });

  test('every tab scrolls and every sheet closes cleanly', async ({ page }) => {
    await login(page);
    await pickParent(page);

    for (const tab of PARENT_TABS) {
      await page.locator('.ph-tabbar').getByRole('button', { name: tab }).click();
      await page.waitForTimeout(1200);

      await expectNoScrollLock(page, `parent/${tab}`);
      await expectPageScrolls(page, `parent/${tab}`);
      await exerciseSheets(page, `parent/${tab}`);
    }
  });

  test('"Xem tất cả đề xuất" stays on the inbox instead of jumping to tasks', async ({
    page,
  }) => {
    await login(page);
    await pickParent(page);

    const seeAll = page.getByRole('button', { name: /Xem tất cả đề xuất/ });
    test.skip((await seeAll.count()) === 0, 'no pending proposals in this dataset');

    await seeAll.first().click();
    await page.waitForTimeout(800);

    await expect(page.locator('.ph-inbox-all-sheet')).toBeVisible();
  });
});

/** Reads the mood classes and the non-shadow filter functions of each pot. */
async function readPots(page: Page, selector: string) {
  return page.evaluate((sel) => {
    const moodOf = (el: Element) =>
      ['is-locked', 'is-wilted', 'is-neutral'].filter((c) => el.classList.contains(c)).join(' ') ||
      'healthy';
    // drop-shadow is intentionally smaller on the mini pot, so compare only the
    // colour-grading functions that encode plant health.
    const effectsOf = (el: Element | null) => {
      if (!el) return [];
      const filter = getComputedStyle(el).filter;
      if (!filter || filter === 'none') return [];
      return (filter.match(/[a-z-]+\(/g) ?? [])
        .map((f) => f.slice(0, -1))
        .filter((f) => f !== 'drop-shadow')
        .sort();
    };
    return Array.from(document.querySelectorAll(sel)).map((pot) => ({
      mood: moodOf(pot),
      plantEffects: effectsOf(pot.querySelector('.kv2-pot-plant')),
      avatarEffects: effectsOf(pot.querySelector('.kv2-pot-avatar')),
    }));
  }, selector);
}
