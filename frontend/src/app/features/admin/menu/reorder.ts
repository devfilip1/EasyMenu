/** Returns a copy of `list` with the element at `index` moved by `delta` positions (clamped). */
export function moved<T>(list: readonly T[], index: number, delta: -1 | 1): T[] {
  const target = index + delta;
  const copy = [...list];
  if (index < 0 || index >= copy.length || target < 0 || target >= copy.length) return copy;
  [copy[index], copy[target]] = [copy[target], copy[index]];
  return copy;
}

/** Sorts by the `position` the API returns. */
export function byPosition<T extends { position: number }>(list: readonly T[]): T[] {
  return [...list].sort((a, b) => a.position - b.position);
}
