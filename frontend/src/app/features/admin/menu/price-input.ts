/**
 * Accepted price formats: digits with optional thousand dots in groups of three, and at most two
 * decimals after a comma ("25", "19,90", "1.234,5", "1.000.000,00").
 */
const PRICE = /^(\d{1,3}(?:\.\d{3})+|\d+)(?:,(\d{1,2}))?$/;

/** Parses a pt-BR price typed by the owner ("19,90", "1.234,5", "25") into integer cents. */
export function parseBrlToCents(text: string): number | null {
  const match = PRICE.exec(text.trim());
  if (!match) return null;
  const reais = Number(match[1].replace(/\./g, ''));
  const cents = Number((match[2] ?? '').padEnd(2, '0'));
  const total = reais * 100 + cents;
  return Number.isSafeInteger(total) ? total : null;
}

/** Formats integer cents for a price input ("19,90", without the currency symbol). */
export function formatCentsInput(cents: number): string {
  const reais = Math.floor(cents / 100)
    .toString()
    .replace(/\B(?=(\d{3})+(?!\d))/g, '.');
  const fraction = (cents % 100).toString().padStart(2, '0');
  return `${reais},${fraction}`;
}
