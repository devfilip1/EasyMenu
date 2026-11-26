import { Injectable, Signal, signal } from '@angular/core';
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

// Stub (TDD red phase, T050). Implemented in T051.
@Injectable({ providedIn: 'root' })
export class CartStore {
  readonly slug: Signal<string | null> = signal(null);
  readonly lines: Signal<CartLine[]> = signal([]);
  readonly total: Signal<number> = signal(0);
  readonly count: Signal<number> = signal(0);

  /** Switches to (and loads from localStorage) the cart of this establishment. */
  useSlug(_slug: string): void {
    throw new Error('Not implemented');
  }
  add(_item: PublicMenuItem): void {
    throw new Error('Not implemented');
  }
  increment(_menuItemId: string): void {
    throw new Error('Not implemented');
  }
  decrement(_menuItemId: string): void {
    throw new Error('Not implemented');
  }
  remove(_menuItemId: string): void {
    throw new Error('Not implemented');
  }
  lineSubtotal(_menuItemId: string): number {
    throw new Error('Not implemented');
  }
  clear(): void {
    throw new Error('Not implemented');
  }
  syncWithMenu(_menu: PublicMenu): void {
    throw new Error('Not implemented');
  }
  markUnavailable(_menuItemIds: string[]): void {
    throw new Error('Not implemented');
  }
}
