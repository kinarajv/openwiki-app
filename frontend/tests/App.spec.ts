import { test, expect } from '@playwright/test';

test('has title and search bar', async ({ page }) => {
  await page.goto('/');

  await expect(page.getByText('OpenWiki')).toBeVisible();
  await expect(page.getByText('Which repo would you like to understand?')).toBeVisible();

  const searchInput = page.getByPlaceholder('Enter GitHub URL or owner/repo');
  await expect(searchInput).toBeVisible();
});

test('can switch between fast and deep modes', async ({ page }) => {
  await page.goto('/');

  const fastButton = page.getByRole('button', { name: /Fast/i });
  const deepButton = page.getByRole('button', { name: /Deep/i });

  await expect(fastButton).toBeVisible();
  await expect(deepButton).toBeVisible();

  await page.route('http://localhost:5000/api/ingest?owner=microsoft&repo=playwright', async route => {
    const json = { status: 'queued', owner: 'microsoft', repo: 'playwright' };
    await route.fulfill({ json });
  });

  const repoInput = page.locator('#repo-input');
  await repoInput.fill('microsoft/playwright');

  const ingestButton = page.locator('#ingest-button');
  await ingestButton.click();
  
  await deepButton.click({ force: true });
  
  const chatInput = page.getByPlaceholder('Ask a question');
  await chatInput.fill('What is the architecture?');
  
  const sendButton = page.getByRole('button', { name: /Send/i });
  await sendButton.click({ force: true });

  const chatLog = page.locator('#chat-log');
  await expect(chatLog).toContainText('OpenWiki (deep mode):', { timeout: 10000 });
});
