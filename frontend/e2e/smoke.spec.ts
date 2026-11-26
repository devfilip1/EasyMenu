import { expect, test } from '@playwright/test';

test('app shell loads the login route', async ({ page }) => {
  await page.goto('/');
  await expect(page).toHaveURL(/\/login$/);
  await expect(page.getByRole('heading', { level: 1 })).toHaveText('Entrar');
});
