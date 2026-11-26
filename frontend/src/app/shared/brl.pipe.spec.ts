import { TestBed } from '@angular/core/testing';
import { BrlPipe } from './brl.pipe';

/** Intl uses a non-breaking space after "R$"; normalize it for readable expectations. */
const normalize = (value: string) => value.replace(/\s/g, ' ');

describe('BrlPipe', () => {
  let pipe: BrlPipe;

  beforeEach(() => {
    pipe = TestBed.runInInjectionContext(() => new BrlPipe());
  });

  it.each([
    [1990, 'R$ 19,90'],
    [0, 'R$ 0,00'],
    [1000000, 'R$ 10.000,00'],
    [5, 'R$ 0,05'],
  ])('formats %i cents as %s', (cents, expected) => {
    expect(normalize(pipe.transform(cents))).toBe(expected);
  });
});
