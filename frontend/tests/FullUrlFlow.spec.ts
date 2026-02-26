import { test, expect } from '@playwright/test';

test('Search and select repository', async ({ page }) => {
  await page.goto('/');

  // Mock search
  await page.route('**/api/Search*', async route => {
    await route.fulfill({
      json: { results: [{ fullName: 'router-for-me/CLIProxyAPI', description: 'CLI Proxy', stars: 100, language: 'TypeScript' }] }
    });
  });

  const searchInput = page.getByTestId('search-input');
  await searchInput.fill('CLIProxyAPI');
  await searchInput.press('Enter');

  // Should display search results
  await expect(page.getByText('router-for-me/CLIProxyAPI')).toBeVisible({ timeout: 10000 });
});