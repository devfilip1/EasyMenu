import { test as base, type Page } from '@playwright/test';
import type { Category, MenuItem } from '../../src/app/api/generated/models';
import {
  createCategory,
  createItem,
  loginAs,
  registerOwner,
  uniqueSlug,
  type OwnerSession,
} from './api';

/**
 * Default menu seeded on the API for each test (T069):
 * - Drinks: Caipirinha 25,00 → 19,90 (Bar) and Chopp 12,00 (Bar);
 * - Porções: Batata frita 30,00 (Cozinha).
 * The kitchen panel is operated by the Owner (FR-033a), so US3/US4 do not depend on US5.
 */
export interface Seeded {
  owner: OwnerSession;
  slug: string;
  /** Owner JWT (same as `owner.accessToken`). */
  token: string;
  categories: { drinks: Category; porcoes: Category };
  items: { caipirinha: MenuItem; chopp: MenuItem; batata: MenuItem };
}

interface Fixtures {
  seeded: Seeded;
  /** The default `page`, already signed in as the seeded Owner (no navigation yet). */
  ownerPage: Page;
}

export const test = base.extend<Fixtures>({
  seeded: async ({ request }, use) => {
    const owner = await registerOwner(request, {
      establishmentName: 'Bar do Zé E2E',
      slug: uniqueSlug('seed'),
    });
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
    });
    const batata = await createItem(request, owner, {
      categoryId: porcoes.id,
      name: 'Batata frita',
      priceCents: 3000,
      station: 'kitchen',
    });

    await use({
      owner,
      slug: owner.slug,
      token: owner.accessToken,
      categories: { drinks, porcoes },
      items: { caipirinha, chopp, batata },
    });
  },

  ownerPage: async ({ page, seeded }, use) => {
    await loginAs(page, seeded.owner);
    await use(page);
  },
});

export { expect } from '@playwright/test';
