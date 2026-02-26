import { test, expect } from '@playwright/test';

test('has title and search bar', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByText('OpenWiki')).toBeVisible();
  await expect(page.getByText('Which repo would you like to understand?')).toBeVisible();
  await expect(page.getByTestId('search-input')).toBeVisible();
});

test('displays popular repositories', async ({ page }) => {
  await page.goto('/');
  await expect(page.getByText('Popular Repositories')).toBeVisible();
  await expect(page.getByText('microsoft/vscode')).toBeVisible();
});