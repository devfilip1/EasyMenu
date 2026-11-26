/** Slug pattern of the contract (RegisterOwnerRequest.slug). */
export const SLUG_PATTERN = /^[a-z0-9](?:[a-z0-9-]{1,48})[a-z0-9]$/;

const MAX_SLUG_LENGTH = 50;

/** Suggests a slug from a name: lowercase, no accents, other characters become single hyphens. */
export function slugify(name: string): string {
  return name
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '')
    .slice(0, MAX_SLUG_LENGTH)
    .replace(/-+$/, '');
}
