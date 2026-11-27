import type { Locator, Page } from '@playwright/test';
import { placeOrder } from './support/api';
import { expect, test } from './support/fixtures';

/**
 * US3 (T070, T156): the Owner runs the kitchen panel (/cozinha, FR-033a) while a guest buys in
 * another browser context. Runs against the real API (green once T153 and T155 are done).
 *
 * UI contract shared with kitchen.page.spec.ts:
 * - sections are accessible regions named "Bar" and "Cozinha" (<section aria-labelledby>);
 * - one card `[data-testid="kitchen-card"]` per order part, in the section of its station;
 * - buttons "Em preparo" / "Pronto" per part; once the whole order is ready, a "Código de
 *   retirada" field and "Confirmar retirada" (FR-027a: the server checks the code).
 */
const REALTIME = { timeout: 5_000 };

const section = (page: Page, name: 'Bar' | 'Cozinha') => page.getByRole('region', { name });
const cardIn = (page: Page, name: 'Bar' | 'Cozinha', code: string): Locator =>
  section(page, name).getByTestId('kitchen-card').filter({ hasText: code });
const orderCards = (page: Page, code: string) =>
  page.getByTestId('kitchen-card').filter({ hasText: code });

test('the kitchen follows a bar + kitchen order from payment to pickup', async ({
  ownerPage: kitchen,
  seeded,
  browser,
  baseURL,
}) => {
  // Context A: the Owner on the kitchen panel.
  await kitchen.goto('/cozinha');
  await expect(section(kitchen, 'Bar')).toBeVisible();
  await expect(section(kitchen, 'Cozinha')).toBeVisible();

  // Context B: an anonymous guest buys Caipirinha + Batata through the UI.
  const guestContext = await browser.newContext({ baseURL });
  const guest = await guestContext.newPage();
  await guest.goto(`/m/${seeded.slug}`);
  const card = (name: string) => guest.getByTestId('menu-item-card').filter({ hasText: name });
  await card('Caipirinha').getByRole('button', { name: /adicionar/i }).click();
  await card('Batata frita').getByRole('button', { name: /adicionar/i }).click();
  await guest.getByRole('button', { name: /ver carrinho/i }).click();
  await guest.locator('app-cart-sheet').getByRole('button', { name: /comprar/i }).click();
  await expect(guest).toHaveURL(/\/pedido\//);
  const code = (await guest.getByTestId('pickup-code').innerText()).trim();
  await guestContext.close();

  // Within 5 s the order shows up, split by station, without reloading.
  const bar = cardIn(kitchen, 'Bar', code);
  const cozinha = cardIn(kitchen, 'Cozinha', code);
  await expect(bar).toBeVisible(REALTIME);
  await expect(cozinha).toBeVisible(REALTIME);
  await expect(bar).toContainText('Caipirinha');
  await expect(bar).not.toContainText('Batata frita');
  await expect(cozinha).toContainText('Batata frita');
  await expect(cozinha).not.toContainText('Caipirinha');

  // Bar: Em preparo → Pronto. The kitchen part is still pending, so no pickup yet.
  await bar.getByRole('button', { name: /em preparo/i }).click();
  await expect(bar.getByRole('button', { name: /em preparo/i })).toHaveCount(0);
  await bar.getByRole('button', { name: /^pronto$/i }).click();
  await expect(bar.getByRole('button', { name: /^pronto$/i })).toHaveCount(0);
  await expect(orderCards(kitchen, code).getByRole('button', { name: /confirmar retirada/i })).toHaveCount(0);

  // Cozinha: Pronto → the whole order is ready and asks for the pickup code.
  await cozinha.getByRole('button', { name: /^pronto$/i }).click();
  const pickupCard = orderCards(kitchen, code).filter({ has: kitchen.getByLabel(/c[óo]digo de retirada/i) }).first();
  const codeField = pickupCard.getByLabel(/c[óo]digo de retirada/i);
  const confirm = pickupCard.getByRole('button', { name: /confirmar retirada/i });
  await expect(confirm).toBeVisible();
  await expect(kitchen.getByRole('button', { name: /^retirado$/i })).toHaveCount(0);

  // A wrong code is refused and the order stays.
  const wrong = code === 'ZZZZ' ? 'YYYY' : 'ZZZZ';
  await codeField.fill(wrong);
  await confirm.click();
  await expect(pickupCard).toContainText('Código de retirada incorreto');
  await expect(orderCards(kitchen, code)).toHaveCount(2);

  // An empty code is not sent.
  await codeField.fill('');
  await confirm.click();
  await expect(pickupCard).toContainText('Informe o código de retirada');
  await expect(orderCards(kitchen, code)).toHaveCount(2);

  // The right code, lowercase and padded, confirms (the server trims and uppercases it).
  await codeField.fill(` ${code.toLowerCase()} `);
  await confirm.click();
  await expect(orderCards(kitchen, code)).toHaveCount(0);
});

test('a bar-only order appears only in the Bar section', async ({ ownerPage: kitchen, seeded, request }) => {
  await kitchen.goto('/cozinha');
  await expect(section(kitchen, 'Bar')).toBeVisible();

  const order = await placeOrder(request, seeded.slug, [
    { menuItemId: seeded.items.chopp.id, quantity: 2 },
  ]);

  await expect(cardIn(kitchen, 'Bar', order.pickupCode)).toBeVisible(REALTIME);
  await expect(cardIn(kitchen, 'Bar', order.pickupCode)).toContainText(/Chopp\s*×\s*2/);
  await expect(cardIn(kitchen, 'Cozinha', order.pickupCode)).toHaveCount(0);
});
