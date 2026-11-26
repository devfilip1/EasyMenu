import { placeOrder, uniqueEmail } from './support/api';
import { expect, test } from './support/fixtures';
import { advancePart, orderIdByPickupCode, pickUp } from './support/kitchen';

/**
 * US5 (T088): the Owner manages the kitchen staff (immediate cut on deactivation, FR-034) and
 * browses the orders. Runs against the real API (green once T080/T082/T084/T087 are done).
 *
 * UI contract: staff rows `[data-testid="staff-row"]` with "Ativo"/"Desativado" and a
 * "Desativar" button (window.confirm); orders rows `[data-testid="order-row"]` with a link to
 * /admin/pedidos/:orderId and a "Status" select.
 */
const KITCHEN_PASSWORD = 'Senha-cozinha-1';

test('the owner creates and deactivates a kitchen account, which loses access at once', async ({
  ownerPage: owner,
  browser,
  baseURL,
}) => {
  const email = uniqueEmail('cozinha');

  // Owner creates the kitchen account through the UI.
  await owner.goto('/admin/equipe');
  const form = owner.locator('form');
  await form.getByLabel(/^nome/i).fill('Cozinha E2E');
  await form.getByLabel(/e-?mail/i).fill(email);
  await form.getByLabel(/senha/i).fill(KITCHEN_PASSWORD);
  await form.getByRole('button', { name: /cadastrar/i }).click();
  const row = owner.getByTestId('staff-row').filter({ hasText: email });
  await expect(row).toContainText('Ativo');

  // Context B: the kitchen member logs in and lands on the kitchen panel.
  const kitchenContext = await browser.newContext({ baseURL });
  const kitchen = await kitchenContext.newPage();
  await kitchen.goto('/login');
  await kitchen.getByLabel(/e-?mail/i).fill(email);
  await kitchen.getByLabel(/senha/i).fill(KITCHEN_PASSWORD);
  await kitchen.getByRole('button', { name: /entrar/i }).click();
  await expect(kitchen).toHaveURL(/\/cozinha$/);

  // Kitchen cannot reach the admin area.
  await kitchen.goto('/admin');
  await expect(kitchen).not.toHaveURL(/\/admin/);

  // Owner deactivates the account.
  owner.once('dialog', (dialog) => dialog.accept());
  await row.getByRole('button', { name: /desativar/i }).click();
  await expect(row).toContainText('Desativado');

  // The next action in B is refused and sends it to /login; logging in again fails.
  await kitchen.goto('/cozinha');
  await expect(kitchen).toHaveURL(/\/login$/, { timeout: 10_000 });
  await kitchen.getByLabel(/e-?mail/i).fill(email);
  await kitchen.getByLabel(/senha/i).fill(KITCHEN_PASSWORD);
  await kitchen.getByRole('button', { name: /entrar/i }).click();
  await expect(kitchen.getByRole('alert')).toContainText('E-mail ou senha inválidos');
  await expect(kitchen).toHaveURL(/\/login$/);

  await kitchenContext.close();
});

test('the owner filters the orders by status and opens one', async ({ ownerPage: owner, seeded, request }) => {
  const { caipirinha, batata } = seeded.items;
  const pending = await placeOrder(request, seeded.slug, [{ menuItemId: batata.id, quantity: 1 }]);
  const done = await placeOrder(request, seeded.slug, [{ menuItemId: caipirinha.id, quantity: 2 }], 'Ana');
  const doneId = await orderIdByPickupCode(seeded.token, done.pickupCode);
  await advancePart(seeded.token, doneId, 'bar', 'ready');
  await pickUp(seeded.token, doneId);

  await owner.goto('/admin/pedidos');
  const rows = owner.getByTestId('order-row');
  await expect(rows.filter({ hasText: pending.pickupCode })).toHaveCount(1);
  await expect(rows.filter({ hasText: done.pickupCode })).toHaveCount(1);

  await owner.getByLabel(/status/i).selectOption({ label: 'Retirado' });
  await expect(rows.filter({ hasText: pending.pickupCode })).toHaveCount(0);
  const doneRow = rows.filter({ hasText: done.pickupCode });
  await expect(doneRow).toContainText('Ana');
  await expect(doneRow).toContainText('Retirado');

  await doneRow.getByRole('link').first().click();
  await expect(owner).toHaveURL(new RegExp(`/admin/pedidos/${doneId}$`));
  await expect(owner.getByTestId('order-line').filter({ hasText: 'Caipirinha' })).toContainText(/R\$\s39,80/);
});
