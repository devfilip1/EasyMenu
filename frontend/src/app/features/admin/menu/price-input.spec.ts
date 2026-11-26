import { formatCentsInput, parseBrlToCents } from './price-input';

describe('parseBrlToCents', () => {
  it.each([
    ['19,90', 1990],
    ['25', 2500],
    ['1.234,5', 123450],
    ['0,05', 5],
    ['  19,90  ', 1990],
    ['1.000.000,00', 100000000],
  ])('"%s" → %i', (text, cents) => {
    const result = parseBrlToCents(text);
    expect(result).toBe(cents);
    expect(Number.isInteger(result)).toBe(true);
  });

  it.each(['', '   ', 'abc', '19,9a', '-5', '12,345', '1,2,3', ','])('"%s" → null', (text) => {
    expect(parseBrlToCents(text)).toBeNull();
  });
});

describe('formatCentsInput', () => {
  it.each([
    [1990, '19,90'],
    [2500, '25,00'],
    [5, '0,05'],
    [123450, '1.234,50'],
  ])('%i → "%s"', (cents, text) => {
    expect(formatCentsInput(cents)).toBe(text);
  });

  it('round-trips with parseBrlToCents', () => {
    for (const cents of [1, 99, 1990, 123450]) {
      expect(parseBrlToCents(formatCentsInput(cents))).toBe(cents);
    }
  });
});
