import { PublicMenu } from '../app/api/generated/models/public-menu';
import { PublicMenuItem } from '../app/api/generated/models/public-menu-item';
import { PublicOrder } from '../app/api/generated/models/public-order';

export const SLUG = 'bar-do-ze';

export function publicItem(
  overrides: Partial<PublicMenuItem> & Pick<PublicMenuItem, 'id' | 'name'>,
): PublicMenuItem {
  const priceCents = overrides.priceCents ?? 1500;
  return {
    description: null,
    photoUrl: null,
    priceCents,
    promotionalPriceCents: null,
    effectivePriceCents: overrides.promotionalPriceCents ?? priceCents,
    isAvailable: true,
    ...overrides,
  };
}

/** Caipirinha 25,00 → 19,90 (promo, with photo). */
export const CAIPIRINHA = publicItem({
  id: 'item-caipirinha',
  name: 'Caipirinha',
  description: 'Limão, cachaça e açúcar',
  photoUrl: '/media/items/caipirinha.jpg',
  priceCents: 2500,
  promotionalPriceCents: 1990,
  effectivePriceCents: 1990,
});
/** Chopp 12,00, sold out. */
export const CHOPP = publicItem({ id: 'item-chopp', name: 'Chopp', priceCents: 1200, isAvailable: false });
/** Batata 30,00, no photo. */
export const BATATA = publicItem({ id: 'item-batata', name: 'Batata frita', priceCents: 3000 });

export function publicMenu(overrides: Partial<PublicMenu> = {}): PublicMenu {
  return {
    establishment: { name: 'Bar do Zé', slug: SLUG },
    categories: [
      { id: 'cat-drinks', name: 'Drinks', items: [CAIPIRINHA, CHOPP] },
      { id: 'cat-porcoes', name: 'Porções', items: [BATATA] },
    ],
    ...overrides,
  };
}

export const PUBLIC_TOKEN = 'tok_AbCdEfGhIjKlMnOpQrStUvWxYz0123456789-_abcd'.slice(0, 43);

export function publicOrder(overrides: Partial<PublicOrder> = {}): PublicOrder {
  return {
    publicToken: PUBLIC_TOKEN,
    pickupCode: 'K7QX',
    customerName: null,
    status: 'paid',
    totalCents: 6980,
    createdAt: '2026-11-26T12:00:00Z',
    establishment: { name: 'Bar do Zé', slug: SLUG },
    items: [
      { name: 'Caipirinha', unitPriceCents: 1990, quantity: 2, subtotalCents: 3980 },
      { name: 'Batata frita', unitPriceCents: 3000, quantity: 1, subtotalCents: 3000 },
    ],
    ...overrides,
  };
}
