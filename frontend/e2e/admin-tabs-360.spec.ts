import type { Locator, Page } from '@playwright/test';
import { expect, test } from './support/fixtures';

/**
 * FR-039 / SC-008 (T157): the /admin tabs fit the screen from 360 px up, without horizontal
 * scroll (neither on the page nor inside the nav) and with touch-sized targets.
 */
const MIN_TARGET = 44;

const TABS: { name: RegExp; href?: RegExp }[] = [
  { name: /^card[áa]pio$/i, href: /\/admin\/cardapio$/ },
  { name: /^equipe$/i, href: /\/admin\/equipe$/ },
  { name: /^pedidos$/i, href: /\/admin\/pedidos$/ },
  { name: /^estabelecimento$/i, href: /\/admin\/estabelecimento$/ },
  { name: /^painel da cozinha$/i, href: /\/cozinha$/ },
  { name: /^sair$/i }, // a button: not clicked here (it logs out)
];

const nav = (page: Page) => page.locator('nav[aria-label="Seções do painel"]');

/** The tab inside the nav, whether it is a link or a button. */
const tab = (page: Page, name: RegExp): Locator =>
  nav(page).getByRole('link', { name }).or(nav(page).getByRole('button', { name })).first();

async function expectNoPageHorizontalScroll(page: Page): Promise<void> {
  const { scrollWidth, clientWidth, scrollX } = await page.evaluate(() => ({
    scrollWidth: document.documentElement.scrollWidth,
    clientWidth: document.documentElement.clientWidth,
    scrollX: window.scrollX,
  }));
  expect(scrollWidth, `page scrollWidth ${scrollWidth} > clientWidth ${clientWidth}`).toBeLessThanOrEqual(clientWidth);
  expect(scrollX, 'page scrolled sideways').toBe(0);
}

for (const width of [360, 412, 1280]) {
  test.describe(`admin tabs at ${width} px`, () => {
    test.use({ viewport: { width, height: 740 } });

    test.beforeEach(async ({ ownerPage }) => {
      await ownerPage.goto('/admin/cardapio');
      await expect(nav(ownerPage)).toBeVisible();
    });

    test('the page and the nav fit without horizontal scroll', async ({ ownerPage: page }) => {
      await expectNoPageHorizontalScroll(page);

      const navBox = await nav(page).evaluate((el) => ({
        scrollWidth: el.scrollWidth,
        clientWidth: el.clientWidth,
        overflowX: getComputedStyle(el).overflowX,
      }));
      expect(navBox.scrollWidth, 'nav scrollWidth').toBeLessThanOrEqual(navBox.clientWidth);
      expect(['auto', 'scroll']).not.toContain(navBox.overflowX);
    });

    test('all six tabs are fully inside the viewport and at least 44 px tall', async ({ ownerPage: page }) => {
      const viewport = page.viewportSize()!;
      const problems: string[] = [];
      for (const { name } of TABS) {
        const box = await tab(page, name).boundingBox();
        if (!box) {
          problems.push(`${name} is not rendered`);
          continue;
        }
        if (box.x < 0 || box.y < 0 || box.x + box.width > viewport.width + 0.5 || box.y + box.height > viewport.height + 0.5) {
          problems.push(`${name} is outside the viewport (${JSON.stringify(box)})`);
        }
        if (box.height < MIN_TARGET) problems.push(`${name} is ${box.height.toFixed(1)} px tall`);
      }
      expect(problems).toEqual([]);
    });

    test('each link navigates when clicked, without sideways scrolling', async ({ ownerPage: page }) => {
      for (const { name, href } of TABS) {
        if (!href) continue;
        await page.goto('/admin/cardapio');
        await tab(page, name).click();
        await expect(page).toHaveURL(href);
        await expectNoPageHorizontalScroll(page);
      }
    });
  });
}
