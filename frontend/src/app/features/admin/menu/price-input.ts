// Stubs (TDD red phase, T037). Implemented in T040.

/** Parses a pt-BR price typed by the owner ("19,90", "1.234,5", "25") into integer cents. */
export function parseBrlToCents(_text: string): number | null {
  throw new Error('Not implemented');
}

/** Formats integer cents for a price input ("19,90", without the currency symbol). */
export function formatCentsInput(_cents: number): string {
  throw new Error('Not implemented');
}
