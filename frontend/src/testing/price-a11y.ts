import { normalize, textOf } from './dom';

/**
 * T130: a promotion must be understandable without seeing the strike-through. The visually
 * hidden labels (global `.sr-only` class) come right before each value, outside the <s>, so the
 * accessible text reads "Preço original R$ 25,00 Preço promocional R$ 19,90".
 */
export const ORIGINAL_LABEL = 'Preço original';
export const PROMO_LABEL = 'Preço promocional';

export function srOnlyTexts(root: Element): string[] {
  return Array.from(root.querySelectorAll('.sr-only')).map((el) => textOf(el));
}

export function expectPromotionLabels(root: Element, original: string, promotional: string): void {
  const labels = srOnlyTexts(root);
  expect(labels).toContain(ORIGINAL_LABEL);
  expect(labels).toContain(PROMO_LABEL);

  const text = normalize(root.textContent);
  expect(text).toContain(`${ORIGINAL_LABEL} ${original}`);
  expect(text).toContain(`${PROMO_LABEL} ${promotional}`);

  // The label sits next to the struck value, not inside it.
  const struck = root.querySelector('s, del');
  expect(normalize(struck?.textContent)).toBe(original);
}

export function expectNoPromotionLabels(root: Element): void {
  const text = normalize(root.textContent);
  expect(text).not.toContain(ORIGINAL_LABEL);
  expect(text).not.toContain(PROMO_LABEL);
}
