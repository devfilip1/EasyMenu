import { defineConfig, devices, type PlaywrightTestConfig } from '@playwright/test';

/**
 * E2E runs against the real stack: the .NET API (port 5080, PostgreSQL from docker compose or the
 * CI service) and `ng serve`. Set E2E_BASE_URL (and E2E_API_URL) to target servers already running.
 *
 * The `prod-headers` project (security headers of the production image, T104/T105) only exists
 * when E2E_PROD_URL is set; an empty value means the default http://localhost:8080. In that mode
 * `ng serve` is not started: the production container is the web server.
 */
const baseURL = process.env['E2E_BASE_URL'] ?? 'http://localhost:4200';
const apiURL = process.env['E2E_API_URL'] ?? 'http://localhost:5080';
const prodURL = process.env['E2E_PROD_URL'];
const runProdHeaders = prodURL !== undefined;
const isCI = !!process.env['CI'];

const PROD_HEADERS_SPEC = /security-headers\.spec\.ts/;

const webServers: NonNullable<PlaywrightTestConfig['webServer']> = [
  {
    name: 'api',
    command: 'dotnet run --project ../backend/src/EasyMenu.Api --launch-profile http',
    // /health only answers 200 once the database is reachable (503 otherwise).
    url: `${apiURL}/health`,
    reuseExistingServer: !isCI,
    timeout: 180_000,
  },
];
if (!runProdHeaders) {
  webServers.push({
    name: 'web',
    command: 'npm start',
    url: baseURL,
    reuseExistingServer: !isCI,
    timeout: 180_000,
  });
}

export default defineConfig({
  testDir: './e2e',
  fullyParallel: true,
  forbidOnly: isCI,
  retries: isCI ? 2 : 0,
  reporter: isCI ? [['github'], ['html', { open: 'never' }]] : 'list',
  use: {
    baseURL,
    trace: 'on-first-retry',
  },
  projects: [
    {
      name: 'mobile-chrome',
      testIgnore: PROD_HEADERS_SPEC,
      use: { ...devices['Pixel 7'] },
    },
    {
      name: 'desktop-chrome',
      testIgnore: PROD_HEADERS_SPEC,
      use: { ...devices['Desktop Chrome'] },
    },
    ...(runProdHeaders
      ? [
          {
            name: 'prod-headers',
            testMatch: PROD_HEADERS_SPEC,
            use: { ...devices['Desktop Chrome'], baseURL: prodURL || 'http://localhost:8080' },
          },
        ]
      : []),
  ],
  webServer: process.env['E2E_BASE_URL'] ? undefined : webServers,
});
