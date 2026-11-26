import path from 'node:path';
import { expect, test, type Locator, type Page } from '@playwright/test';
import { DEFAULT_PASSWORD, uniqueEmail, uniqueSlug } from './support/api';

/**
 * US1 (T042): the owner signs up through the UI, builds the menu and finds everything again
 * after a reload and after logging out and in. Runs against the real API (green once T029/T040).
 *
 * UI contract shared with the component specs:
 * - categories: `app-categories-panel`, rows `[data-testid="category-row"]`, "Nova categoria" +
 *   "Adicionar", per-row "Subir"/"Descer";
 * - items: rows `[data-testid="menu-item-row"]` with "Esgotado" badge, "Disponível" toggle and
 *   the original price inside <s>/<del> when on promotion;
 * - form: `app-menu-item-form`, opened by the "Novo item" button of the menu page.
 */
const PHOTO = path.join(__dirname, 'fixtures', 'item.jpg');
const PROMO_RULE = 'O preço promocional deve ser maior que zero e menor que o preço';

const categoryRows = (page: Page) => page.getByTestId('category-row');
const itemRow = (page: Page, name: string) =>
  page.getByTestId('menu-item-row').filter({ hasText: name });

async function addCategory(page: Page, name: string): Promise<void> {
  const panel = page.locator('app-categories-panel');
  await panel.getByLabel(/nova categoria/i).fill(name);
  await panel.getByRole('button', { name: /adicionar/i }).click();
  await expect(categoryRows(page).filter({ hasText: name })).toHaveCount(1);
}

async function openItemForm(page: Page): Promise<Locator> {
  await page.getByRole('button', { name: /novo item/i }).first().click();
  const form = page.locator('app-menu-item-form');
  await expect(form).toBeVisible();
  return form;
}

async function fillItem(
  form: Locator,
  item: { name: string; price: string; promo?: string; station: 'Bar' | 'Cozinha'; category: string },
): Promise<void> {
  await form.getByRole('textbox', { name: /^nome/i }).fill(item.name);
  await form.getByRole('textbox', { name: /^pre[çc]o(?!\s*promocional)/i }).fill(item.price);
  await form.getByRole('textbox', { name: /promocional/i }).fill(item.promo ?? '');
  await form.getByRole('combobox', { name: /setor/i }).selectOption({ label: item.station });
  await form.getByRole('combobox', { name: /categoria/i }).selectOption({ label: item.category });
}

async function expectMenu(page: Page): Promise<void> {
  // Porções was moved above Drinks.
  await expect(categoryRows(page)).toHaveCount(2);
  await expect(categoryRows(page).nth(0)).toContainText('Porções');
  await expect(categoryRows(page).nth(1)).toContainText('Drinks');

  const caipirinha = itemRow(page, 'Caipirinha');
  await expect(caipirinha).toContainText('R$ 19,90');
  await expect(caipirinha.locator('s, del')).toHaveText(/R\$\s25,00/);
  await expect(caipirinha.locator('img')).toHaveAttribute('src', /\/media\//);
  await expect(caipirinha).not.toContainText('Esgotado');

  const chopp = itemRow(page, 'Chopp');
  await expect(chopp).toContainText('R$ 12,00');
  await expect(chopp).toContainText('Esgotado');
  await expect(chopp.getByRole('checkbox', { name: /dispon[ií]vel/i })).not.toBeChecked();
}

test('owner signs up, builds the menu and finds it again after reload and re-login', async ({
  page,
}) => {
  const email = uniqueEmail('us1');
  const slug = uniqueSlug('us1');

  // Sign up through the UI.
  await page.goto('/cadastro');
  await page.getByLabel(/nome do estabelecimento/i).fill('Bar do Zé E2E');
  await page.getByLabel(/e-?mail/i).fill(email);
  await page.getByLabel(/senha/i).fill(DEFAULT_PASSWORD);
  await page.getByLabel(/endere[çc]o/i).fill(slug);
  await page.getByRole('button', { name: /criar conta/i }).click();
  await expect(page).toHaveURL(/\/admin\/cardapio$/);

  // Categories: create Drinks and Porções, then move Porções up.
  await addCategory(page, 'Drinks');
  await addCategory(page, 'Porções');
  await categoryRows(page)
    .filter({ hasText: 'Porções' })
    .getByRole('button', { name: /subir/i })
    .click();
  await expect(categoryRows(page).nth(0)).toContainText('Porções');

  // Caipirinha 25,00 / 19,90, Bar, with photo.
  let form = await openItemForm(page);
  await fillItem(form, { name: 'Caipirinha', price: '25,00', promo: '19,90', station: 'Bar', category: 'Drinks' });
  await form.getByLabel(/^foto/i).setInputFiles(PHOTO);
  await form.getByRole('button', { name: /salvar/i }).click();
  await expect(itemRow(page, 'Caipirinha')).toBeVisible();
  await expect(itemRow(page, 'Caipirinha').locator('img')).toHaveAttribute('src', /\/media\//);

  // Promotion above the price is rejected with the rule explained.
  form = await openItemForm(page);
  await fillItem(form, { name: 'Chopp', price: '12,00', promo: '30,00', station: 'Bar', category: 'Drinks' });
  await form.getByRole('button', { name: /salvar/i }).click();
  await expect(form).toContainText(PROMO_RULE);
  await expect(itemRow(page, 'Chopp')).toHaveCount(0);

  // Without the promotion it saves.
  await form.getByRole('textbox', { name: /promocional/i }).fill('');
  await form.getByRole('button', { name: /salvar/i }).click();
  await expect(itemRow(page, 'Chopp')).toBeVisible();

  // Mark Chopp as sold out.
  await itemRow(page, 'Chopp').getByRole('checkbox', { name: /dispon[ií]vel/i }).click();
  await expect(itemRow(page, 'Chopp')).toContainText('Esgotado');

  // Everything survives a reload.
  await page.reload();
  await expectMenu(page);

  // Log out and back in.
  await page.getByRole('button', { name: /^sair$/i }).click();
  await expect(page).toHaveURL(/\/login$/);
  await page.getByLabel(/e-?mail/i).fill(email);
  await page.getByLabel(/senha/i).fill(DEFAULT_PASSWORD);
  await page.getByRole('button', { name: /entrar/i }).click();
  await expect(page).toHaveURL(/\/admin\/cardapio$/);
  await expectMenu(page);
});
