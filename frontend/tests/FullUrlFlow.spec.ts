import { test, expect } from '@playwright/test';

test('Search and select repository', async ({ page }) => {
  await page.goto('/');

  // Mock search using the actual path that hooks/useApi.ts calls (`/api/search?q=...`)
  await page.route('**/api/search*', async route => {
    await route.fulfill({
      json: { results: [{ fullName: 'router-for-me/CLIProxyAPI', description: 'CLI Proxy', stars: 100, language: 'TypeScript' }] }
    });
  });

  const searchInput = page.getByTestId('search-input');
  await searchInput.fill('CLIProxyAPI');
  await searchInput.press('Enter');

  // Wait for search result to show up in the popular/searched repos list
  const repoCards = page.getByTestId('repo-card');
  await expect(repoCards.filter({ hasText: 'router-for-me/CLIProxyAPI' })).toBeVisible({ timeout: 10000 });
});