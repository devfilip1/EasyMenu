import type { Locator, Page } from '@playwright/test';
import { placeOrder } from './support/api';
import { expect, test } from './support/fixtures';

/**
 * SC-008 (T091): the guest pages are fully usable on a 360 px wide phone: no horizontal scroll
 * and every visible button is a touch target at least 44 px tall.
 */
test.use({ viewport: { width: 360, height: 740 }, hasTouch: true });

const MIN_TARGET = 44;

async function expectNoHorizontalScroll(page: Page): Promise<void> {
  const { scrollWidth, clientWidth } = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
  }));
  expect(scrollWidth, `scrollWidth ${scrollWidth} > clientWidth ${clientWidth}`).toBeLessThanOrEqual(clientWidth);
}

async function expectTouchTargets(scope: Locator): Promise<void> {
  const buttons = scope.getByRole('button');
  const count = await buttons.count();
  const small: string[] = [];
  for (let i = 0; i < count; i++) {
    const button = buttons.nth(i);
    if (!(await button.isVisible())) continue;
    const box = await button.boundingBox();
    if (box && box.height < MIN_TARGET) {
      const name = (await button.getAttribute('aria-label')) ?? (await button.innerText()).trim();
      small.push(`"${name}" is ${box.height.toFixed(1)} px tall`);
    }
  }
  expect(small, `buttons shorter than ${MIN_TARGET} px`).toEqual([]);
}

test('public menu at 360 px', async ({ page, seeded }) => {
  await page.goto(`/m/${seeded.slug}`);
  await expect(page.getByTestId('menu-item-card').first()).toBeVisible();

  await expectNoHorizontalScroll(page);
  await expectTouchTargets(page.locator('body'));
});

test('cart at 360 px', async ({ page, seeded }) => {
  await page.goto(`/m/${seeded.slug}`);
  const card = (name: string) => page.getByTestId('menu-item-card').filter({ hasText: name });
  await card('Caipirinha').getByRole('button', { name: /adicionar/i }).click();
  await card('Batata frita').getByRole('button', { name: /adicionar/i }).click();
  await page.getByRole('button', { name: /ver carrinho/i }).click();
  const sheet = page.locator('app-cart-sheet');
  await expect(sheet.getByTestId('cart-line')).toHaveCount(2);

  await expectNoHorizontalScroll(page);
  await expectTouchTargets(sheet);
});

test('order page at 360 px', async ({ page, seeded, request }) => {
  const order = await placeOrder(request, seeded.slug, [
    { menuItemId: seeded.items.caipirinha.id, quantity: 2 },
    { menuItemId: seeded.items.batata.id, quantity: 1 },
  ]);

  await page.goto(`/pedido/${order.publicToken}`);
  await expect(page.getByTestId('pickup-code')).toHaveText(order.pickupCode);

  await expectNoHorizontalScroll(page);
  await expectTouchTargets(page.locator('body'));
});
