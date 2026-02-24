import { test, expect } from '@playwright/test';

test.describe('OpenWiki End-to-End Flow', () => {

  test('Mock offline ingestion and streaming fallback', async ({ page }) => {
    // Navigate to app
    await page.goto('/');

    const repoInput = page.locator('#repo-input');
    await repoInput.fill('microsoft/playwright');

    // intercept the fetch to simulate the backend
    await page.route('http://localhost:5000/api/ingest?owner=microsoft&repo=playwright', async route => {
      const json = { status: 'queued', owner: 'microsoft', repo: 'playwright' };
      await route.fulfill({ json });
    });

    const ingestButton = page.locator('#ingest-button');
    await ingestButton.click();

    const chatLog = page.locator('#chat-log');
    await expect(chatLog).toContainText('Ingested repository microsoft/playwright successfully', { timeout: 10000 });

    const deepButton = page.getByRole('button', { name: /Deep/i });
    await deepButton.click({ force: true });

    const chatInput = page.locator('#chat-input');
    await chatInput.fill('How does the networking work?');

    // Simulate clicking send and the UI reflecting it immediately
    const chatSend = page.locator('#chat-send');
    await chatSend.click({ force: true });

    await expect(chatLog).toContainText('You: How does the networking work?', { timeout: 5000 });
  });

});
