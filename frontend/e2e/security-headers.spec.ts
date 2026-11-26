import { expect, test, type Page } from '@playwright/test';
import { API_URL, uniqueSlug } from './support/api';

/**
 * Security headers of the production image (research R15, T104). Runs only in the `prod-headers`
 * project, against the nginx container (E2E_PROD_URL), never against `ng serve`.
 */
const API_ORIGIN = new URL(API_URL).origin;

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
      expect(csp.get('connect-src')).toContain(API_ORIGIN);
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
