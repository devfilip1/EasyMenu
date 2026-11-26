import { defineConfig, devices } from '@playwright/test';

/**
 * E2E runs against the real stack: the .NET API (port 5080, PostgreSQL from docker compose or the
 * CI service) and `ng serve`. Set E2E_BASE_URL (and E2E_API_URL) to target servers already running.
 */
const baseURL = process.env['E2E_BASE_URL'] ?? 'http://localhost:4200';
const apiURL = process.env['E2E_API_URL'] ?? 'http://localhost:5080';
const isCI = !!process.env['CI'];

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
    { name: 'mobile-chrome', use: { ...devices['Pixel 7'] } },
    { name: 'desktop-chrome', use: { ...devices['Desktop Chrome'] } },
  ],
  webServer: process.env['E2E_BASE_URL']
    ? undefined
    : [
        {
          name: 'api',
          command: 'dotnet run --project ../backend/src/EasyMenu.Api --launch-profile http',
          // /health only answers 200 once the database is reachable (503 otherwise).
          url: `${apiURL}/health`,
          reuseExistingServer: !isCI,
          timeout: 180_000,
        },
        {
          name: 'web',
          command: 'npm start',
          url: baseURL,
          reuseExistingServer: !isCI,
          timeout: 180_000,
        },
      ],
});
