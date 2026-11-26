import { LOCALE_ID, Pipe, PipeTransform, inject } from '@angular/core';

/** Formats an integer amount in cents as Brazilian reais, e.g. 1990 → "R$ 19,90". */
@Pipe({ name: 'brl' })
export class BrlPipe implements PipeTransform {
  private readonly format = new Intl.NumberFormat(brazilianLocale(inject(LOCALE_ID)), {
    style: 'currency',
    currency: 'BRL',
  });

  transform(cents: number): string {
    return this.format.format(cents / 100);
  }
}

/** Reais are always written the Brazilian way, even if LOCALE_ID is not a Portuguese locale. */
function brazilianLocale(locale: string): string {
  return locale.toLowerCase().startsWith('pt') ? locale : 'pt-BR';
}
