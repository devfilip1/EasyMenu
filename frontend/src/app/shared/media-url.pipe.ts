import { Pipe, PipeTransform } from '@angular/core';
import { environment } from '../../environments/environment';

/** Image shown when a menu item has no photo. */
export const PLACEHOLDER_ITEM_IMAGE = '/images/placeholder-item.svg';

/** Resolves a media path returned by the API (e.g. `/media/items/x.jpg`) against the API base URL. */
@Pipe({ name: 'mediaUrl' })
export class MediaUrlPipe implements PipeTransform {
  transform(url: string | null | undefined): string {
    if (!url) return PLACEHOLDER_ITEM_IMAGE;
    if (/^https?:\/\//i.test(url)) return url;
    return `${environment.apiUrl}${url.startsWith('/') ? '' : '/'}${url}`;
  }
}
