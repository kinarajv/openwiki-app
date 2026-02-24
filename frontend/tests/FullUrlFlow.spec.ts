import { test, expect } from '@playwright/test';

test('Ingest repository using full GitHub URL', async ({ page }) => {
  await page.goto('/');

  // Enter the exact full URL provided by the user
  const repoInput = page.locator('#repo-input');
  await repoInput.fill('https://github.com/router-for-me/CLIProxyAPI');

  // Let's not mock the backend this time, let's actually hit the running .NET backend
  // to prove the fallback catch works and it doesn't 500 error.
  
  const ingestButton = page.locator('#ingest-button');
  await ingestButton.click();

  // Wait for the UI transition
  const chatLog = page.locator('#chat-log');
  
  // It should successfully parse the owner/repo from the URL and hit the fallback catch block
  await expect(chatLog).toContainText('Ingested repository router-for-me/CLIProxyAPI successfully', { timeout: 15000 });
  
  // The generated document pane should show the fallback message about the AI connection
  const docPane = page.locator('.prose');
  await expect(docPane).toContainText('DeepWiki Analysis', { timeout: 5000 });
  await expect(docPane).toContainText('router-for-me', { timeout: 5000 });
});
