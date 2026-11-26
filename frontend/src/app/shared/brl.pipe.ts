import { Pipe, PipeTransform } from '@angular/core';

// Stub (TDD red phase, T016). Implemented in T018.
@Pipe({ name: 'brl' })
export class BrlPipe implements PipeTransform {
  transform(_cents: number): string {
    throw new Error('Not implemented');
  }
}
