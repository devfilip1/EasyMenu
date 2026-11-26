import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import {
  createCategory,
  createItem,
  registerOwner,
  setItemAvailability,
  uniqueSlug,
  type OwnerSession,
} from './support/api';
import type { MenuItem } from '../src/app/api/generated/models';

/**
 * US2 (T057): an anonymous guest opens /m/{slug}, fills the cart and buys with simulated payment.
 * Runs against the real API (green once T049 and T055/T058 are done).
 *
 * UI contract shared with the component specs:
 * - menu cards `[data-testid="menu-item-card"]` with an "Adicionar" button (disabled when sold out);
 * - the "Ver carrinho" button opens `app-cart-sheet`: lines `[data-testid="cart-line"]`,
 *   total `[data-testid="cart-total"]`, optional "Seu nome" field and "Comprar";
 * - order page: `[data-testid="pickup-code"]`, `[data-testid="order-total"]`.
 */
const PICKUP_CODE = /^[2-9A-HJKMNP-Z]{4}$/;
const ORDER_URL = /\/pedido\/[A-Za-z0-9_-]{43}$/;

interface SeededMenu {
  owner: OwnerSession;
  caipirinha: MenuItem;
  chopp: MenuItem;
  batata: MenuItem;
}

/** Drinks: Caipirinha 25,00 → 19,90 and Chopp (sold out); Porções: Batata 30,00. */
async function seedMenu(request: APIRequestContext): Promise<SeededMenu> {
  const owner = await registerOwner(request, { establishmentName: 'Bar do Zé E2E', slug: uniqueSlug('us2') });
  const drinks = await createCategory(request, owner, 'Drinks');
  const porcoes = await createCategory(request, owner, 'Porções');
  const caipirinha = await createItem(request, owner, {
    categoryId: drinks.id,
    name: 'Caipirinha',
    priceCents: 2500,
    promotionalPriceCents: 1990,
    station: 'bar',
  });
  const chopp = await createItem(request, owner, {
    categoryId: drinks.id,
    name: 'Chopp',
    priceCents: 1200,
    station: 'bar',
    isAvailable: false,
  });
  const batata = await createItem(request, owner, {
    categoryId: porcoes.id,
    name: 'Batata frita',
    priceCents: 3000,
    station: 'kitchen',
  });
  return { owner, caipirinha, chopp, batata };
}

const card = (page: Page, name: string) => page.getByTestId('menu-item-card').filter({ hasText: name });
const addButton = (page: Page, name: string) => card(page, name).getByRole('button', { name: /adicionar/i });

async function openCart(page: Page) {
  await page.getByRole('button', { name: /ver carrinho/i }).click();
  const sheet = page.locator('app-cart-sheet');
  await expect(sheet).toBeVisible();
  return sheet;
}

test('guest buys from the public menu without a name and gets a pickup code', async ({
  page,
  request,
}) => {
  const { owner } = await seedMenu(request);

  await page.goto(`/m/${owner.slug}`);
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Bar do Zé E2E');

  // Sold-out item cannot be added.
  await expect(card(page, 'Chopp')).toContainText('Esgotado');
  await expect(addButton(page, 'Chopp')).toBeDisabled();

  // Caipirinha ×2 + Batata ×1 = 2 × 19,90 + 30,00 = R$ 69,80.
  await addButton(page, 'Caipirinha').click();
  await addButton(page, 'Caipirinha').click();
  await addButton(page, 'Batata frita').click();

  const sheet = await openCart(page);
  await expect(sheet.getByTestId('cart-line')).toHaveCount(2);
  await expect(sheet.getByTestId('cart-total')).toContainText(/R\$\s69,80/);

  // Buy without a name (simulated payment).
  await expect(sheet.getByLabel(/seu nome/i)).toHaveValue('');
  await sheet.getByRole('button', { name: /comprar/i }).click();

  await expect(page).toHaveURL(ORDER_URL);
  await expect(page.getByTestId('pickup-code')).toHaveText(PICKUP_CODE);
  await expect(page.getByTestId('order-total')).toContainText(/R\$\s69,80/);
  await expect(page.getByTestId('order-status')).toContainText('Pago / aguardando preparo');
});

test('an unknown establishment shows a not-found message', async ({ page }) => {
  await page.goto(`/m/${uniqueSlug('inexistente')}`);

  await expect(page.getByText('Estabelecimento não encontrado')).toBeVisible();
});

test('an item sold out while the cart is open makes the purchase fail with its name', async ({
  page,
  request,
}) => {
  const { owner, batata } = await seedMenu(request);

  await page.goto(`/m/${owner.slug}`);
  await addButton(page, 'Caipirinha').click();
  await addButton(page, 'Batata frita').click();
  const sheet = await openCart(page);

  // The kitchen runs out of fries after the guest built the cart.
  await setItemAvailability(request, owner, batata.id, false);

  await sheet.getByRole('button', { name: /comprar/i }).click();

  await expect(page.getByRole('alert')).toContainText('Itens indisponíveis: Batata frita');
  await expect(sheet.getByTestId('cart-line').filter({ hasText: 'Batata frita' })).toContainText(
    'Indisponível',
  );
  await expect(page).toHaveURL(new RegExp(`/m/${owner.slug}$`));
});
