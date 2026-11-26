import { Pipe, PipeTransform } from '@angular/core';

/** Image shown when a menu item has no photo. */
export const PLACEHOLDER_ITEM_IMAGE = '/images/placeholder-item.svg';

// Stub (TDD red phase, T016). Implemented in T018.
@Pipe({ name: 'mediaUrl' })
export class MediaUrlPipe implements PipeTransform {
  transform(_url: string | null | undefined): string {
    throw new Error('Not implemented');
  }
}
