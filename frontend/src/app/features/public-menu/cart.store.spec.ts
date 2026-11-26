import { TestBed } from '@angular/core/testing';
import { BATATA, CAIPIRINHA, CHOPP, SLUG, publicMenu } from '../../../testing/public-menu';
import { CartStore, cartStorageKey } from './cart.store';

describe('CartStore', () => {
  let cart: CartStore;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
    cart = TestBed.inject(CartStore);
    cart.useSlug(SLUG);
  });

  afterEach(() => localStorage.clear());

  const quantity = (id: string) => cart.lines().find((l) => l.menuItemId === id)?.quantity;

  describe('add', () => {
    it('adds a line with quantity 1 using the effective price and name', () => {
      cart.add(CAIPIRINHA);

      expect(cart.lines()).toEqual([
        {
          menuItemId: CAIPIRINHA.id,
          name: 'Caipirinha',
          unitPriceCents: 1990,
          quantity: 1,
          unavailable: false,
        },
      ]);
    });

    it('increments the quantity when the item is added again', () => {
      cart.add(CAIPIRINHA);
      cart.add(CAIPIRINHA);

      expect(cart.lines()).toHaveLength(1);
      expect(quantity(CAIPIRINHA.id)).toBe(2);
    });

    it('does not add a sold-out item', () => {
      cart.add(CHOPP);

      expect(cart.lines()).toEqual([]);
    });
  });

  describe('increment / decrement', () => {
    it('caps the quantity at 99', () => {
      cart.add(BATATA);
      for (let i = 0; i < 120; i++) cart.increment(BATATA.id);

      expect(quantity(BATATA.id)).toBe(99);
    });

    it('add also respects the 99 cap', () => {
      for (let i = 0; i < 105; i++) cart.add(BATATA);

      expect(quantity(BATATA.id)).toBe(99);
    });

    it('decrements but keeps at least 1', () => {
      cart.add(BATATA);
      cart.increment(BATATA.id);
      cart.decrement(BATATA.id);
      expect(quantity(BATATA.id)).toBe(1);

      cart.decrement(BATATA.id);
      expect(quantity(BATATA.id)).toBe(1);
      expect(cart.lines()).toHaveLength(1);
    });
  });

  it('remove drops the line', () => {
    cart.add(CAIPIRINHA);
    cart.add(BATATA);

    cart.remove(CAIPIRINHA.id);

    expect(cart.lines().map((l) => l.menuItemId)).toEqual([BATATA.id]);
  });

  it('computes line subtotals and the total in integer cents', () => {
    cart.add(CAIPIRINHA);
    cart.add(CAIPIRINHA);
    cart.add(BATATA);

    expect(cart.lineSubtotal(CAIPIRINHA.id)).toBe(3980);
    expect(cart.lineSubtotal(BATATA.id)).toBe(3000);
    expect(cart.total()).toBe(6980);
    expect(cart.count()).toBe(3);
  });

  describe('persistence', () => {
    it('persists the cart per slug in localStorage', () => {
      cart.add(CAIPIRINHA);

      const stored = JSON.parse(localStorage.getItem(cartStorageKey(SLUG)) ?? 'null');
      expect(stored).toEqual(
        expect.arrayContaining([expect.objectContaining({ menuItemId: CAIPIRINHA.id, quantity: 1 })]),
      );
    });

    it('restores the cart of a slug in a fresh store', () => {
      cart.add(CAIPIRINHA);
      cart.add(CAIPIRINHA);

      const fresh = TestBed.runInInjectionContext(() => new CartStore());
      fresh.useSlug(SLUG);

      expect(fresh.lines()).toEqual(cart.lines());
      expect(fresh.total()).toBe(3980);
    });

    it('keeps the carts of different establishments isolated', () => {
      cart.add(CAIPIRINHA);

      cart.useSlug('outro-bar');
      expect(cart.lines()).toEqual([]);
      cart.add(BATATA);

      cart.useSlug(SLUG);
      expect(cart.lines().map((l) => l.menuItemId)).toEqual([CAIPIRINHA.id]);
      cart.useSlug('outro-bar');
      expect(cart.lines().map((l) => l.menuItemId)).toEqual([BATATA.id]);
    });
  });

  it('clear empties the cart and its storage', () => {
    cart.add(CAIPIRINHA);

    cart.clear();

    expect(cart.lines()).toEqual([]);
    expect(cart.total()).toBe(0);
    const stored = JSON.parse(localStorage.getItem(cartStorageKey(SLUG)) ?? '[]');
    expect(stored).toEqual([]);
  });

  describe('syncWithMenu', () => {
    it('updates names and prices from the current menu', () => {
      cart.add(CAIPIRINHA);

      cart.syncWithMenu(
        publicMenu({
          categories: [
            {
              id: 'cat-drinks',
              name: 'Drinks',
              items: [
                {
                  ...CAIPIRINHA,
                  name: 'Caipirinha de limão',
                  promotionalPriceCents: null,
                  effectivePriceCents: 2500,
                },
              ],
            },
          ],
        }),
      );

      expect(cart.lines()[0]).toMatchObject({
        name: 'Caipirinha de limão',
        unitPriceCents: 2500,
        unavailable: false,
      });
      expect(cart.total()).toBe(2500);
    });

    it('marks lines whose item became sold out or disappeared', () => {
      cart.add(CAIPIRINHA);
      cart.add(BATATA);

      cart.syncWithMenu(
        publicMenu({
          categories: [{ id: 'cat-drinks', name: 'Drinks', items: [{ ...CAIPIRINHA, isAvailable: false }] }],
        }),
      );

      const byId = Object.fromEntries(cart.lines().map((l) => [l.menuItemId, l.unavailable]));
      expect(byId).toEqual({ [CAIPIRINHA.id]: true, [BATATA.id]: true });
    });
  });

  it('markUnavailable flags the given lines only', () => {
    cart.add(CAIPIRINHA);
    cart.add(BATATA);

    cart.markUnavailable([BATATA.id]);

    const byId = Object.fromEntries(cart.lines().map((l) => [l.menuItemId, l.unavailable]));
    expect(byId).toEqual({ [CAIPIRINHA.id]: false, [BATATA.id]: true });
  });

  describe('restoring a tampered localStorage (T132)', () => {
    const line = (menuItemId: string, quantity: unknown, unitPriceCents = 1990) => ({
      menuItemId,
      name: `Item ${menuItemId}`,
      unitPriceCents,
      quantity,
      unavailable: false,
    });

    /** Writes raw storage for SLUG and restores it in a fresh store. */
    function restore(raw: string): CartStore {
      localStorage.setItem(cartStorageKey(SLUG), raw);
      const fresh = TestBed.runInInjectionContext(() => new CartStore());
      fresh.useSlug(SLUG);
      return fresh;
    }

    const quantities = (store: CartStore) =>
      Object.fromEntries(store.lines().map((l) => [l.menuItemId, l.quantity]));

    it.each([
      [0, 1],
      [-3, 1],
      [150, 99],
      [99, 99],
      [1, 1],
    ])('quantity %s is clamped to %s', (stored, expected) => {
      const store = restore(JSON.stringify([line('a', stored)]));

      expect(quantities(store)).toEqual({ a: expected });
    });

    it.each([['abc'], [null], [1.5], [undefined], [[2]], ['3']])(
      'a non-integer quantity (%j) drops the line',
      (stored) => {
        const store = restore(JSON.stringify([line('bad', stored), line('ok', 2)]));

        expect(quantities(store)).toEqual({ ok: 2 });
      },
    );

    it.each([['{oops'], ['{"lines": 1}'], ['null'], ['42']])(
      'unusable storage (%s) gives an empty cart without throwing',
      (raw) => {
        let store: CartStore | undefined;
        expect(() => (store = restore(raw))).not.toThrow();

        expect(store!.lines()).toEqual([]);
        expect(store!.total()).toBe(0);
      },
    );

    it('recomputes the total from the corrected quantities', () => {
      const store = restore(
        JSON.stringify([line('a', 0, 1990), line('b', 150, 3000), line('c', 'abc', 1000)]),
      );

      expect(quantities(store)).toEqual({ a: 1, b: 99 });
      expect(store.total()).toBe(1990 + 99 * 3000);
      expect(store.count()).toBe(100);
    });
  });
});
