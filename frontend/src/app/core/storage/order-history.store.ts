import { Injectable, signal } from '@angular/core';

export interface OrderHistoryEntry {
  publicToken: string;
  slug: string;
  pickupCode: string;
  createdAt: string;
}

/** localStorage key of the guest's recent orders. */
export const ORDER_HISTORY_KEY = 'easymenu.orders';

const MAX_ENTRIES = 10;

/** The guest's recent orders on this device (no account needed), persisted in localStorage. */
@Injectable({ providedIn: 'root' })
export class OrderHistoryStore {
  private readonly list = signal<OrderHistoryEntry[]>(readEntries());

  /** Most recent first, at most 10. */
  readonly entries = this.list.asReadonly();

  remember(entry: OrderHistoryEntry): void {
    const { publicToken, slug, pickupCode, createdAt } = entry;
    const others = this.list().filter((e) => e.publicToken !== publicToken);
    const next = [{ publicToken, slug, pickupCode, createdAt }, ...others].slice(0, MAX_ENTRIES);
    this.list.set(next);
    try {
      localStorage.setItem(ORDER_HISTORY_KEY, JSON.stringify(next));
    } catch {
      // Storage unavailable: the history lives only in memory.
    }
  }

  lastForSlug(slug: string): OrderHistoryEntry | null {
    return this.list().find((e) => e.slug === slug) ?? null;
  }
}

function readEntries(): OrderHistoryEntry[] {
  try {
    const value: unknown = JSON.parse(localStorage.getItem(ORDER_HISTORY_KEY) ?? '[]');
    return Array.isArray(value) ? value.filter(isEntry).slice(0, MAX_ENTRIES) : [];
  } catch {
    return [];
  }
}

function isEntry(value: unknown): value is OrderHistoryEntry {
  if (typeof value !== 'object' || value === null) return false;
  const e = value as Partial<OrderHistoryEntry>;
  return (
    typeof e.publicToken === 'string' &&
    typeof e.slug === 'string' &&
    typeof e.pickupCode === 'string' &&
    typeof e.createdAt === 'string'
  );
}
