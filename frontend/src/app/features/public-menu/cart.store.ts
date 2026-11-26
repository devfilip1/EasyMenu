import { Injectable, computed, signal } from '@angular/core';
import { PublicMenu } from '../../api/generated/models/public-menu';
import { PublicMenuItem } from '../../api/generated/models/public-menu-item';

export interface CartLine {
  menuItemId: string;
  name: string;
  /** Effective unit price (promotional when present), integer cents. */
  unitPriceCents: number;
  quantity: number;
  /** Set by `syncWithMenu`/`markUnavailable` when the item can no longer be bought. */
  unavailable: boolean;
}

/** localStorage key of the cart of one establishment. */
export function cartStorageKey(slug: string): string {
  return `easymenu.cart.${slug}`;
}

const MIN_QUANTITY = 1;
const MAX_QUANTITY = 99;

const clamp = (quantity: number) => Math.min(MAX_QUANTITY, Math.max(MIN_QUANTITY, quantity));

/** Shopping cart of the public menu, one per establishment (slug), persisted in localStorage. */
@Injectable({ providedIn: 'root' })
export class CartStore {
  private readonly currentSlug = signal<string | null>(null);
  private readonly currentLines = signal<CartLine[]>([]);

  readonly slug = this.currentSlug.asReadonly();
  readonly lines = this.currentLines.asReadonly();
  readonly total = computed(() =>
    this.lines().reduce((sum, line) => sum + line.unitPriceCents * line.quantity, 0),
  );
  readonly count = computed(() => this.lines().reduce((sum, line) => sum + line.quantity, 0));

  /** Switches to (and loads from localStorage) the cart of this establishment. */
  useSlug(slug: string): void {
    this.currentSlug.set(slug);
    this.currentLines.set(readLines(slug));
  }

  add(item: PublicMenuItem): void {
    if (!item.isAvailable) return;
    const existing = this.find(item.id);
    if (existing) {
      this.updateLine(item.id, (line) => ({ ...line, quantity: clamp(line.quantity + 1) }));
      return;
    }
    this.save([
      ...this.lines(),
      {
        menuItemId: item.id,
        name: item.name,
        unitPriceCents: item.effectivePriceCents,
        quantity: 1,
        unavailable: false,
      },
    ]);
  }

  increment(menuItemId: string): void {
    this.updateLine(menuItemId, (line) => ({ ...line, quantity: clamp(line.quantity + 1) }));
  }

  decrement(menuItemId: string): void {
    this.updateLine(menuItemId, (line) => ({ ...line, quantity: clamp(line.quantity - 1) }));
  }

  remove(menuItemId: string): void {
    this.save(this.lines().filter((line) => line.menuItemId !== menuItemId));
  }

  lineSubtotal(menuItemId: string): number {
    const line = this.find(menuItemId);
    return line ? line.unitPriceCents * line.quantity : 0;
  }

  clear(): void {
    this.save([]);
  }

  /** Refreshes names and prices from the current menu; missing or sold-out items are flagged. */
  syncWithMenu(menu: PublicMenu): void {
    const items = new Map(
      menu.categories.flatMap((category) => category.items).map((item) => [item.id, item]),
    );
    this.save(
      this.lines().map((line) => {
        const item = items.get(line.menuItemId);
        if (!item) return { ...line, unavailable: true };
        return {
          ...line,
          name: item.name,
          unitPriceCents: item.effectivePriceCents,
          unavailable: !item.isAvailable,
        };
      }),
    );
  }

  markUnavailable(menuItemIds: string[]): void {
    const ids = new Set(menuItemIds);
    this.save(
      this.lines().map((line) =>
        ids.has(line.menuItemId) ? { ...line, unavailable: true } : line,
      ),
    );
  }

  private find(menuItemId: string): CartLine | undefined {
    return this.lines().find((line) => line.menuItemId === menuItemId);
  }

  private updateLine(menuItemId: string, update: (line: CartLine) => CartLine): void {
    this.save(this.lines().map((line) => (line.menuItemId === menuItemId ? update(line) : line)));
  }

  private save(lines: CartLine[]): void {
    this.currentLines.set(lines);
    const slug = this.slug();
    if (slug) writeLines(slug, lines);
  }
}

function readLines(slug: string): CartLine[] {
  try {
    const value: unknown = JSON.parse(localStorage.getItem(cartStorageKey(slug)) ?? '[]');
    return Array.isArray(value) ? value.filter(isCartLine) : [];
  } catch {
    return [];
  }
}

function writeLines(slug: string, lines: CartLine[]): void {
  try {
    localStorage.setItem(cartStorageKey(slug), JSON.stringify(lines));
  } catch {
    // Storage unavailable: the cart lives only in memory.
  }
}

function isCartLine(value: unknown): value is CartLine {
  if (typeof value !== 'object' || value === null) return false;
  const line = value as Partial<CartLine>;
  return (
    typeof line.menuItemId === 'string' &&
    typeof line.name === 'string' &&
    Number.isInteger(line.unitPriceCents) &&
    Number.isInteger(line.quantity) &&
    typeof line.unavailable === 'boolean'
  );
}
