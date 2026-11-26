import { Injectable, Signal, signal } from '@angular/core';

export interface OrderHistoryEntry {
  publicToken: string;
  slug: string;
  pickupCode: string;
  createdAt: string;
}

/** localStorage key of the guest's recent orders. */
export const ORDER_HISTORY_KEY = 'easymenu.orders';

// Stub (TDD red phase, T054). Implemented in T055.
@Injectable({ providedIn: 'root' })
export class OrderHistoryStore {
  /** Most recent first, at most 10. */
  readonly entries: Signal<OrderHistoryEntry[]> = signal([]);

  remember(_entry: OrderHistoryEntry): void {
    throw new Error('Not implemented');
  }

  lastForSlug(_slug: string): OrderHistoryEntry | null {
    throw new Error('Not implemented');
  }
}
