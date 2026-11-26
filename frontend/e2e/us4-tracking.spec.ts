import { expect, test } from './support/fixtures';
import { advancePart, orderIdByPickupCode, pickUp } from './support/kitchen';

/**
 * US4 (T078): the guest follows the order live while the kitchen (Owner token, via API) advances
 * it. Runs against the real API (green once T072/T074/T076 are done).
 *
 * UI contract: `[data-testid="order-status"]` with the pt-BR label, the ready banner in
 * `role="alert"` with the pickup code, and the "Acompanhar meu pedido" link on /m/{slug}.
 */
const REALTIME = { timeout: 5_000 };
const READY_BANNER = 'Seu pedido está pronto — retire no balcão';

test('the order page follows the kitchen live and can be found again from the menu', async ({
  page,
  context,
  seeded,
}) => {
  // Buy Caipirinha (Bar) + Batata (Cozinha) as a guest.
  await page.goto(`/m/${seeded.slug}`);
  const card = (name: string) => page.getByTestId('menu-item-card').filter({ hasText: name });
  await card('Caipirinha').getByRole('button', { name: /adicionar/i }).click();
  await card('Batata frita').getByRole('button', { name: /adicionar/i }).click();
  await page.getByRole('button', { name: /ver carrinho/i }).click();
  await page.locator('app-cart-sheet').getByRole('button', { name: /comprar/i }).click();
  await expect(page).toHaveURL(/\/pedido\/[A-Za-z0-9_-]{43}$/);
  const orderUrl = new URL(page.url()).pathname;

  const status = page.getByTestId('order-status');
  await expect(status).toContainText('Pago / aguardando preparo');
  const code = (await page.getByTestId('pickup-code').innerText()).trim();
  const orderId = await orderIdByPickupCode(seeded.token, code);

  // The first part enters preparation: the page updates without a reload.
  await advancePart(seeded.token, orderId, 'bar', 'in_preparation');
  await expect(status).toContainText('Em preparo', REALTIME);

  // Every part ready: the highlighted banner with the pickup code.
  await advancePart(seeded.token, orderId, 'bar', 'ready');
  await advancePart(seeded.token, orderId, 'kitchen', 'ready');
  const banner = page.getByRole('alert').filter({ hasText: READY_BANNER });
  await expect(banner).toBeVisible(REALTIME);
  await expect(banner).toContainText(code);

  // Picked up.
  await pickUp(seeded.token, orderId);
  await expect(status).toContainText('Retirado', REALTIME);
  await expect(banner).toHaveCount(0);

  // Close the page; on the same device the menu links back to the order.
  await page.close();
  const again = await context.newPage();
  await again.goto(`/m/${seeded.slug}`);
  await again.getByRole('link', { name: /acompanhar meu pedido/i }).click();
  await expect(again).toHaveURL(new RegExp(`${orderUrl}$`));
  await expect(again.getByTestId('order-status')).toContainText('Retirado');
});
