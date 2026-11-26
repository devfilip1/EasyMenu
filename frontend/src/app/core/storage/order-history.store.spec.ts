import { TestBed } from '@angular/core/testing';
import { ORDER_HISTORY_KEY, OrderHistoryEntry, OrderHistoryStore } from './order-history.store';

function entry(n: number, slug = 'bar-do-ze'): OrderHistoryEntry {
  return {
    publicToken: `token-${String(n).padStart(2, '0')}`.padEnd(43, 'x'),
    slug,
    pickupCode: `K${String(n).padStart(3, '2')}`,
    createdAt: new Date(Date.UTC(2026, 10, 26, 12, n)).toISOString(),
  };
}

describe('OrderHistoryStore', () => {
  let store: OrderHistoryStore;

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({});
    store = TestBed.inject(OrderHistoryStore);
  });

  afterEach(() => localStorage.clear());

  it('remembers an order, most recent first', () => {
    store.remember(entry(1));
    store.remember(entry(2));

    expect(store.entries()).toEqual([entry(2), entry(1)]);
  });

  it('keeps only the 10 most recent orders', () => {
    for (let n = 1; n <= 12; n++) store.remember(entry(n));

    const tokens = store.entries().map((e) => e.publicToken);
    expect(tokens).toHaveLength(10);
    expect(tokens).toEqual(
      Array.from({ length: 10 }, (_, i) => entry(12 - i).publicToken),
    );
  });

  it('does not duplicate an order remembered twice', () => {
    store.remember(entry(1));
    store.remember(entry(1));

    expect(store.entries()).toEqual([entry(1)]);
  });

  it('persists in localStorage and survives a fresh store', () => {
    store.remember(entry(1));

    expect(localStorage.getItem(ORDER_HISTORY_KEY)).not.toBeNull();
    const fresh = TestBed.runInInjectionContext(() => new OrderHistoryStore());
    expect(fresh.entries()).toEqual([entry(1)]);
  });

  it('lastForSlug returns the most recent order of that establishment', () => {
    store.remember(entry(1, 'bar-do-ze'));
    store.remember(entry(2, 'outro-bar'));
    store.remember(entry(3, 'bar-do-ze'));
    store.remember(entry(4, 'outro-bar'));

    expect(store.lastForSlug('bar-do-ze')).toEqual(entry(3, 'bar-do-ze'));
    expect(store.lastForSlug('outro-bar')).toEqual(entry(4, 'outro-bar'));
    expect(store.lastForSlug('sem-pedidos')).toBeNull();
  });
});
