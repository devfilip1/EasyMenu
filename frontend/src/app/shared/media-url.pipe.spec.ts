import { TestBed } from '@angular/core/testing';
import { environment } from '../../environments/environment';
import { MediaUrlPipe, PLACEHOLDER_ITEM_IMAGE } from './media-url.pipe';

describe('MediaUrlPipe', () => {
  let pipe: MediaUrlPipe;

  beforeEach(() => {
    pipe = TestBed.runInInjectionContext(() => new MediaUrlPipe());
  });

  it('prefixes a relative URL with the API base URL', () => {
    expect(pipe.transform('/media/items/abc.jpg')).toBe(`${environment.apiUrl}/media/items/abc.jpg`);
  });

  it('returns the placeholder image when there is no photo', () => {
    expect(PLACEHOLDER_ITEM_IMAGE).toBe('/images/placeholder-item.svg');
    expect(pipe.transform(undefined)).toBe('/images/placeholder-item.svg');
  });

  it('treats null (photoUrl is nullable in the contract) as no photo', () => {
    expect(pipe.transform(null)).toBe('/images/placeholder-item.svg');
  });
});
