import path from 'node:path';
import { expect, test, type APIRequestContext, type Page } from '@playwright/test';
import type { PublicMenu } from '../src/app/api/generated/models';
import {
  createCategory,
  createItem,
  kitchenOrderByPickupCode,
  placeOrder,
  registerOwner,
  uniqueSlug,
  updatePartStatus,
  uploadItemPhoto,
  type OwnerSession,
} from './support/api';

/**
 * Security headers of the production image (research R15, T104) and same-origin deploy through
 * the nginx reverse proxy (research R18, T116). Runs only in the `prod-headers` project, against
 * the nginx container (E2E_PROD_URL), never against `ng serve`. Test data is seeded straight on
 * the API (E2E_API_URL); the browser and `request` (baseURL = E2E_PROD_URL) only use the proxy.
 */
const PHOTO = path.join(__dirname, 'fixtures', 'item.jpg');

const PAGES = [
  { name: 'root', path: '/' },
  { name: 'login', path: '/login' },
  // Deep link: only works if nginx falls back to index.html.
  { name: 'public menu (SPA fallback)', path: `/m/${uniqueSlug('csp')}` },
];

type Csp = Map<string, string[]>;

function parseCsp(header: string): Csp {
  const directives: Csp = new Map();
  for (const part of header.split(';')) {
    const [name, ...sources] = part.trim().split(/\s+/);
    if (name) directives.set(name.toLowerCase(), sources);
  }
  return directives;
}

interface CspReport {
  violations: string[];
  consoleErrors: string[];
}

/** Collects `securitypolicyviolation` events and CSP console errors while the page loads. */
async function recordCsp(page: Page): Promise<() => Promise<CspReport>> {
  const consoleErrors: string[] = [];
  page.on('console', (msg) => {
    const text = msg.text();
    if (/content security policy|content-security-policy|refused to/i.test(text)) {
      consoleErrors.push(text);
    }
  });
  await page.addInitScript(() => {
    const store: string[] = [];
    (window as unknown as { __cspViolations: string[] }).__cspViolations = store;
    document.addEventListener('securitypolicyviolation', (e) => {
      store.push(`${e.violatedDirective} blocked ${e.blockedURI || '(inline)'}`);
    });
  });
  return async () => ({
    violations: await page.evaluate(
      () => (window as unknown as { __cspViolations: string[] }).__cspViolations,
    ),
    consoleErrors,
  });
}

for (const { name, path } of PAGES) {
  test.describe(`${name} (${path.startsWith('/m/') ? '/m/{slug}' : path})`, () => {
    test('serves the SPA with a strict Content-Security-Policy', async ({ page }) => {
      const response = await page.goto(path);
      expect(response, 'navigation response').not.toBeNull();
      expect(response!.status()).toBe(200);
      expect(response!.headers()['content-type']).toContain('text/html');

      const header = response!.headers()['content-security-policy'];
      expect(header, 'Content-Security-Policy header').toBeTruthy();
      const csp = parseCsp(header!);

      expect(csp.get('default-src')).toContain("'self'");

      const scriptSrc = csp.get('script-src');
      expect(scriptSrc).toContain("'self'");
      expect(scriptSrc).not.toContain("'unsafe-inline'");

      expect(csp.get('object-src')).toEqual(["'none'"]);
      expect(csp.get('frame-ancestors')).toEqual(["'none'"]);
      // Same origin (R18): the API, hubs and media are proxied, so no external origin is allowed.
      expect([...(csp.get('connect-src') ?? [])].sort()).toEqual(["'self'"]);
      expect([...(csp.get('img-src') ?? [])].sort()).toEqual(["'self'", 'data:'].sort());
    });

    test('sends the hardening headers', async ({ page }) => {
      const response = await page.goto(path);
      const headers = response!.headers();

      expect(headers['x-content-type-options']).toBe('nosniff');
      expect(headers['referrer-policy']).toBe('strict-origin-when-cross-origin');
      expect(headers['x-frame-options']).toBe('DENY');
      const permissions = headers['permissions-policy'];
      expect(permissions, 'Permissions-Policy header').toBeTruthy();
      for (const feature of ['camera=()', 'microphone=()', 'geolocation=()']) {
        expect(permissions).toContain(feature);
      }
    });

    test('loads without any CSP violation', async ({ page }) => {
      const report = await recordCsp(page);

      await page.goto(path);
      await page.waitForLoadState('networkidle');
      // The app actually booted (scripts were allowed to run and rendered into the root).
      await expect(page.locator('app-root > *').first()).toBeAttached();

      const { violations, consoleErrors } = await report();
      expect(violations, 'securitypolicyviolation events').toEqual([]);
      expect(consoleErrors, 'CSP console errors').toEqual([]);
    });
  });
}

interface SeededShop {
  owner: OwnerSession;
  slug: string;
  caipirinhaId: string;
}

/** Owner + "Drinks" + Caipirinha with photo, straight on the API. */
async function seedShop(request: APIRequestContext): Promise<SeededShop> {
  const owner = await registerOwner(request, { establishmentName: 'Bar Proxy', slug: uniqueSlug('proxy') });
  const drinks = await createCategory(request, owner, 'Drinks');
  const caipirinha = await createItem(request, owner, {
    categoryId: drinks.id,
    name: 'Caipirinha',
    priceCents: 2500,
    station: 'bar',
  });
  await uploadItemPhoto(request, owner, caipirinha.id, PHOTO);
  return { owner, slug: owner.slug, caipirinhaId: caipirinha.id };
}

test.describe('same-origin API through the nginx proxy (R18)', () => {
  test('proxies /api/ to the API', async ({ request, baseURL }) => {
    const shop = await seedShop(request);

    // Relative URL: resolved against E2E_PROD_URL, i.e. through nginx.
    const response = await request.get(`/api/public/establishments/${shop.slug}/menu`);

    expect(new URL(response.url()).origin).toBe(new URL(baseURL!).origin);
    expect(response.status()).toBe(200);
    expect(response.headers()['content-type']).toContain('application/json');
    const menu = (await response.json()) as PublicMenu;
    expect(menu.establishment.slug).toBe(shop.slug);
  });

  test('/m/{slug} renders the menu with the photo served from /media/', async ({ page, request }) => {
    const shop = await seedShop(request);
    const report = await recordCsp(page);

    await page.goto(`/m/${shop.slug}`);

    await expect(page.getByRole('heading', { level: 1 })).toHaveText('Bar Proxy');
    const card = page.getByTestId('menu-item-card').filter({ hasText: 'Caipirinha' });
    const photo = card.locator('img');
    await expect(photo).toHaveAttribute('src', /^\/media\//);
    await expect
      .poll(() => photo.evaluate((img: HTMLImageElement) => img.complete && img.naturalWidth), {
        message: 'photo loaded through the proxy',
      })
      .toBeGreaterThan(0);

    const { violations, consoleErrors } = await report();
    expect(violations, 'securitypolicyviolation events').toEqual([]);
    expect(consoleErrors, 'CSP console errors').toEqual([]);
  });

  test('/pedido/{token} updates live through the /hubs WebSocket proxy', async ({ page, request }) => {
    const shop = await seedShop(request);
    const order = await placeOrder(request, shop.slug, [{ menuItemId: shop.caipirinhaId, quantity: 1 }]);
    const report = await recordCsp(page);

    await page.goto(`/pedido/${order.publicToken}`);
    await expect(page.getByTestId('order-status')).toContainText('Pago / aguardando preparo');

    const kitchenOrder = await kitchenOrderByPickupCode(request, shop.owner, order.pickupCode);
    await updatePartStatus(request, shop.owner, kitchenOrder.id, 'bar', 'in_preparation');

    // No reload: only the realtime hub (WebSocket via nginx) can bring this in.
    await expect(page.getByTestId('order-status')).toContainText('Em preparo', { timeout: 5_000 });

    const { violations, consoleErrors } = await report();
    expect(violations, 'securitypolicyviolation events').toEqual([]);
    expect(consoleErrors, 'CSP console errors').toEqual([]);
  });
});
